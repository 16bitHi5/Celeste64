/* CelesteXR: minimal OpenXR session and stereo swapchain bridge for SDL GPU. */
#include <SDL3/SDL.h>
#include "openxr/openxr.h"
#include "vr_panel_pose.h"
#include <SDL3/SDL_openxr.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

#ifdef _WIN32
#define API __declspec(dllexport)
#else
#define API __attribute__((visibility("default")))
#endif

typedef struct { float px,py,pz,qx,qy,qz,qw,left,right,down,up; int32_t width,height; void *texture; } CelesteEye;
typedef struct { XrPosef aim, surface; int32_t valid, trigger; } CelestePointer;
typedef struct { XrPosef left, right; int32_t leftValid, rightValid; float leftTrigger, rightTrigger; } CelesteHands;
SDL_COMPILE_TIME_ASSERT(CelesteHandsSize, sizeof(CelesteHands) == 72);
typedef struct { XrSwapchain handle; SDL_GPUTexture **images; uint32_t image; int acquired; XrExtent2Di size; } EyeSwap;
typedef struct {
    SDL_GPUDevice *device; XrInstance instance; XrSystemId system; XrSession session; XrSpace space;
    PFN_xrGetInstanceProcAddr get;
    PFN_xrDestroySession destroySession; PFN_xrDestroySpace destroySpace;
    PFN_xrEnumerateViewConfigurationViews enumerateViews; PFN_xrCreateReferenceSpace createSpace;
    PFN_xrPollEvent poll; PFN_xrBeginSession beginSession; PFN_xrEndSession endSession;
    PFN_xrWaitFrame waitFrame; PFN_xrBeginFrame beginFrame; PFN_xrEndFrame endFrame;
    PFN_xrLocateViews locateViews; PFN_xrAcquireSwapchainImage acquire;
    PFN_xrWaitSwapchainImage waitImage; PFN_xrReleaseSwapchainImage release;
    PFN_xrStringToPath stringToPath; PFN_xrCreateActionSet createActionSet; PFN_xrDestroyActionSet destroyActionSet;
    PFN_xrCreateAction createAction; PFN_xrSuggestInteractionProfileBindings suggestBindings;
    PFN_xrAttachSessionActionSets attachActions; PFN_xrSyncActions syncActions;
    PFN_xrGetActionStateBoolean getBoolean; PFN_xrGetActionStateFloat getFloat; PFN_xrGetActionStateVector2f getVector;
    PFN_xrGetActionStatePose getPose; PFN_xrCreateActionSpace createActionSpace; PFN_xrLocateSpace locateSpace;
    XrActionSet actionSet; XrAction move, camera, jump, dash, trigger, leftTrigger, grip, pause, select, aim, leftHand, rightHand;
    XrSpace aimSpace, leftHandSpace, rightHandSpace; CelestePointer pointer; CelesteHands hands;
    SDL_JoystickID joystickId; SDL_Joystick *joystick; int inputEnabled, focused, menuActive, gripDash, firstPerson;
    EyeSwap eye[3]; XrView views[2]; XrTime displayTime; int running, frameBegun, shouldRender;
    char error[256]; int loggedFrame, flatLast; XrPosef flatPose;
    Uint64 rateStart; unsigned int submittedFrames; XrDuration displayPeriod;
} Bridge;

static void fail(Bridge *b, const char *name, XrResult result) { SDL_snprintf(b->error, sizeof(b->error), "%s failed (%d)", name, (int)result); }
#define LOAD(field, name) do { if (XR_FAILED(b->get(b->instance, #name, (PFN_xrVoidFunction*)&b->field))) { fail(b,#name,-1); return 0; } } while(0)
#define CHECK(call) do { XrResult r_ = (call); if (XR_FAILED(r_)) { fail(b,#call,r_); return 0; } } while(0)

static int make_action(Bridge *b, XrAction *action, XrActionType type, const char *name, const char *label) {
    XrActionCreateInfo info={XR_TYPE_ACTION_CREATE_INFO}; info.actionType=type;
    SDL_strlcpy(info.actionName,name,sizeof(info.actionName)); SDL_strlcpy(info.localizedActionName,label,sizeof(info.localizedActionName));
    CHECK(b->createAction(b->actionSet,&info,action)); return 1;
}

static int init_input(Bridge *b) {
    LOAD(stringToPath,xrStringToPath); LOAD(createActionSet,xrCreateActionSet); LOAD(destroyActionSet,xrDestroyActionSet);
    LOAD(createAction,xrCreateAction); LOAD(suggestBindings,xrSuggestInteractionProfileBindings);
    LOAD(attachActions,xrAttachSessionActionSets); LOAD(syncActions,xrSyncActions);
    LOAD(getBoolean,xrGetActionStateBoolean); LOAD(getFloat,xrGetActionStateFloat); LOAD(getVector,xrGetActionStateVector2f);
    LOAD(getPose,xrGetActionStatePose); LOAD(createActionSpace,xrCreateActionSpace); LOAD(locateSpace,xrLocateSpace);
    XrActionSetCreateInfo set={XR_TYPE_ACTION_SET_CREATE_INFO};
    SDL_strlcpy(set.actionSetName,"gameplay",sizeof(set.actionSetName)); SDL_strlcpy(set.localizedActionSetName,"Celeste Controls",sizeof(set.localizedActionSetName));
    CHECK(b->createActionSet(b->instance,&set,&b->actionSet));
    if (!make_action(b,&b->move,XR_ACTION_TYPE_VECTOR2F_INPUT,"move","Move") ||
        !make_action(b,&b->camera,XR_ACTION_TYPE_VECTOR2F_INPUT,"camera","Camera") ||
        !make_action(b,&b->jump,XR_ACTION_TYPE_BOOLEAN_INPUT,"jump","Jump and Confirm") ||
        !make_action(b,&b->dash,XR_ACTION_TYPE_BOOLEAN_INPUT,"dash","Dash and Back") ||
        !make_action(b,&b->trigger,XR_ACTION_TYPE_FLOAT_INPUT,"trigger","Climb and Select") ||
        !make_action(b,&b->leftTrigger,XR_ACTION_TYPE_FLOAT_INPUT,"left_trigger","Left Hand Climb") ||
        !make_action(b,&b->grip,XR_ACTION_TYPE_FLOAT_INPUT,"grip","Optional Grip Dash") ||
        !make_action(b,&b->pause,XR_ACTION_TYPE_BOOLEAN_INPUT,"pause","Pause") ||
        !make_action(b,&b->select,XR_ACTION_TYPE_BOOLEAN_INPUT,"select","Menu Select") ||
        !make_action(b,&b->aim,XR_ACTION_TYPE_POSE_INPUT,"pointer","Menu Pointer") ||
        !make_action(b,&b->leftHand,XR_ACTION_TYPE_POSE_INPUT,"left_hand","Left Hand") ||
        !make_action(b,&b->rightHand,XR_ACTION_TYPE_POSE_INPUT,"right_hand","Right Hand")) return 0;
    const char *paths[]={"/user/hand/left/input/thumbstick", "/user/hand/right/input/thumbstick",
        "/user/hand/right/input/a/click", "/user/hand/right/input/b/click",
        "/user/hand/right/input/trigger/value", "/user/hand/left/input/trigger/value", "/user/hand/right/input/squeeze/value", "/user/hand/left/input/squeeze/value",
        "/user/hand/left/input/x/click", "/user/hand/left/input/y/click",
        "/user/hand/left/input/menu/click", "/user/hand/right/input/thumbstick/click", "/user/hand/right/input/aim/pose",
        "/user/hand/left/input/grip/pose", "/user/hand/right/input/grip/pose"};
    XrAction actions[]={b->move,b->camera,b->jump,b->dash,b->trigger,b->leftTrigger,b->grip,b->grip,b->pause,b->select,b->pause,b->pause,b->aim,b->leftHand,b->rightHand};
    XrActionSuggestedBinding bindings[15];
    for(int i=0;i<15;i++){bindings[i].action=actions[i]; CHECK(b->stringToPath(b->instance,paths[i],&bindings[i].binding));}
    XrPath profile; CHECK(b->stringToPath(b->instance,"/interaction_profiles/oculus/touch_controller",&profile));
    XrInteractionProfileSuggestedBinding suggested={XR_TYPE_INTERACTION_PROFILE_SUGGESTED_BINDING};
    suggested.interactionProfile=profile; suggested.countSuggestedBindings=15; suggested.suggestedBindings=bindings;
    CHECK(b->suggestBindings(b->instance,&suggested));
    XrSessionActionSetsAttachInfo attach={XR_TYPE_SESSION_ACTION_SETS_ATTACH_INFO}; attach.countActionSets=1; attach.actionSets=&b->actionSet;
    CHECK(b->attachActions(b->session,&attach));
    XrActionSpaceCreateInfo aimInfo={XR_TYPE_ACTION_SPACE_CREATE_INFO}; aimInfo.action=b->aim; aimInfo.poseInActionSpace.orientation.w=1;
    CHECK(b->createActionSpace(b->session,&aimInfo,&b->aimSpace));
    aimInfo.action=b->leftHand; CHECK(b->createActionSpace(b->session,&aimInfo,&b->leftHandSpace));
    aimInfo.action=b->rightHand; CHECK(b->createActionSpace(b->session,&aimInfo,&b->rightHandSpace));
    SDL_VirtualJoystickDesc desc; SDL_zero(desc); SDL_INIT_INTERFACE(&desc);
    desc.type=SDL_JOYSTICK_TYPE_GAMEPAD; desc.naxes=4; desc.nbuttons=SDL_GAMEPAD_BUTTON_COUNT;
    desc.axis_mask=(1u<<SDL_GAMEPAD_AXIS_LEFTX)|(1u<<SDL_GAMEPAD_AXIS_LEFTY)|(1u<<SDL_GAMEPAD_AXIS_RIGHTX)|(1u<<SDL_GAMEPAD_AXIS_RIGHTY);
    desc.button_mask=(1u<<SDL_GAMEPAD_BUTTON_COUNT)-1; desc.name="Celeste Quest Touch";
    b->joystickId=SDL_AttachVirtualJoystick(&desc);
    if(!b->joystickId || !(b->joystick=SDL_OpenJoystick(b->joystickId))) {SDL_snprintf(b->error,sizeof(b->error),"Quest input device failed: %s",SDL_GetError()); return 0;}
    b->inputEnabled=1; SDL_Log("Quest Touch controls initialized"); return 1;
}

static void set_input(Bridge *b, int enabled) {
    if(!b->joystick) return;
    XrActionStateVector2f move={XR_TYPE_ACTION_STATE_VECTOR2F}, camera={XR_TYPE_ACTION_STATE_VECTOR2F};
    XrActionStateBoolean jump={XR_TYPE_ACTION_STATE_BOOLEAN}, dash={XR_TYPE_ACTION_STATE_BOOLEAN}, pause={XR_TYPE_ACTION_STATE_BOOLEAN}, select={XR_TYPE_ACTION_STATE_BOOLEAN};
    XrActionStateFloat trigger={XR_TYPE_ACTION_STATE_FLOAT}, leftTrigger={XR_TYPE_ACTION_STATE_FLOAT}, grip={XR_TYPE_ACTION_STATE_FLOAT};
    b->pointer.trigger=0; b->pointer.valid=0;
    b->hands.leftTrigger=0; b->hands.rightTrigger=0;
    if(enabled && b->focused) {
        XrActiveActionSet active={b->actionSet,XR_NULL_PATH}; XrActionsSyncInfo sync={XR_TYPE_ACTIONS_SYNC_INFO}; sync.countActiveActionSets=1; sync.activeActionSets=&active;
        if(XR_SUCCEEDED(b->syncActions(b->session,&sync))) {
            XrActionStateGetInfo get={XR_TYPE_ACTION_STATE_GET_INFO};
            get.action=b->move; b->getVector(b->session,&get,&move); get.action=b->camera; b->getVector(b->session,&get,&camera);
            get.action=b->jump; b->getBoolean(b->session,&get,&jump); get.action=b->dash; b->getBoolean(b->session,&get,&dash);
            get.action=b->pause; b->getBoolean(b->session,&get,&pause); get.action=b->select; b->getBoolean(b->session,&get,&select);
            get.action=b->trigger; b->getFloat(b->session,&get,&trigger); get.action=b->leftTrigger; b->getFloat(b->session,&get,&leftTrigger); get.action=b->grip; b->getFloat(b->session,&get,&grip);
            b->pointer.trigger=trigger.isActive && trigger.currentState>0.5f;
            if (b->firstPerson && !b->menuActive) {
                b->hands.leftTrigger=leftTrigger.isActive ? leftTrigger.currentState : 0;
                b->hands.rightTrigger=trigger.isActive ? trigger.currentState : 0;
            }
        }
    }
    SDL_SetJoystickVirtualAxis(b->joystick,0,(Sint16)(SDL_clamp(move.currentState.x,-1.0f,1.0f)*32767));
    SDL_SetJoystickVirtualAxis(b->joystick,1,(Sint16)(-SDL_clamp(move.currentState.y,-1.0f,1.0f)*32767));
    SDL_SetJoystickVirtualAxis(b->joystick,2,(Sint16)(SDL_clamp(camera.currentState.x,-1.0f,1.0f)*32767));
    SDL_SetJoystickVirtualAxis(b->joystick,3,(Sint16)(-SDL_clamp(camera.currentState.y,-1.0f,1.0f)*32767));
    SDL_SetJoystickVirtualButton(b->joystick,SDL_GAMEPAD_BUTTON_SOUTH,jump.currentState!=0 || (b->menuActive && select.currentState));
    SDL_SetJoystickVirtualButton(b->joystick,SDL_GAMEPAD_BUTTON_EAST,dash.currentState!=0 || (!b->menuActive && b->gripDash && grip.currentState>0.4f));
    SDL_SetJoystickVirtualButton(b->joystick,SDL_GAMEPAD_BUTTON_LEFT_SHOULDER,!b->menuActive && (trigger.currentState>0.4f || (b->firstPerson && leftTrigger.currentState>0.4f)));
    SDL_SetJoystickVirtualButton(b->joystick,SDL_GAMEPAD_BUTTON_START,pause.currentState!=0);
}

API void CelesteXR_SetInputEnabled(Bridge *b, int enabled) { if(b) {b->inputEnabled=enabled!=0; if(!enabled) set_input(b,0);} }
API void CelesteXR_SetInputOptions(Bridge *b, int menuActive, int gripDash, int firstPerson) { if(b) {b->menuActive=menuActive!=0; b->gripDash=gripDash!=0; b->firstPerson=firstPerson!=0;} }
API void CelesteXR_GetPointer(Bridge *b, CelestePointer *pointer) { if(b && pointer) *pointer=b->pointer; }
API void CelesteXR_GetHands(Bridge *b, CelesteHands *hands) { if(b && hands) *hands=b->hands; }

API const char *CelesteXR_Error(Bridge *b) { return b ? b->error : "No XR bridge"; }
API Bridge *CelesteXR_New(void *device, void *instance, uint64_t system) {
    Bridge *b = (Bridge*)calloc(1, sizeof(Bridge));
    if (!b) return NULL;
    b->device=(SDL_GPUDevice*)device; b->instance=(XrInstance)instance; b->system=(XrSystemId)system;
    b->get=(PFN_xrGetInstanceProcAddr)SDL_OpenXR_GetXrGetInstanceProcAddr();
    if (!b->get) SDL_snprintf(b->error,sizeof(b->error),"OpenXR loader unavailable: %s",SDL_GetError());
    return b;
}
API int CelesteXR_Init(Bridge *b) {
    uint32_t count=0; XrViewConfigurationView cfg[2]={{XR_TYPE_VIEW_CONFIGURATION_VIEW},{XR_TYPE_VIEW_CONFIGURATION_VIEW}};
    if (!b || !b->get) return 0;
    LOAD(destroySession,xrDestroySession); LOAD(destroySpace,xrDestroySpace);
    LOAD(enumerateViews,xrEnumerateViewConfigurationViews); LOAD(createSpace,xrCreateReferenceSpace);
    LOAD(poll,xrPollEvent); LOAD(beginSession,xrBeginSession); LOAD(endSession,xrEndSession);
    LOAD(waitFrame,xrWaitFrame); LOAD(beginFrame,xrBeginFrame); LOAD(endFrame,xrEndFrame);
    LOAD(locateViews,xrLocateViews); LOAD(acquire,xrAcquireSwapchainImage);
    LOAD(waitImage,xrWaitSwapchainImage); LOAD(release,xrReleaseSwapchainImage);
    XrSessionCreateInfo si={XR_TYPE_SESSION_CREATE_INFO};
    CHECK(SDL_CreateGPUXRSession(b->device,&si,&b->session));
    if(!init_input(b)) { SDL_Log("Quest Touch controls unavailable: %s",b->error); }
    XrReferenceSpaceCreateInfo space={XR_TYPE_REFERENCE_SPACE_CREATE_INFO};
    space.referenceSpaceType=XR_REFERENCE_SPACE_TYPE_LOCAL; space.poseInReferenceSpace.orientation.w=1;
    CHECK(b->createSpace(b->session,&space,&b->space));
    CHECK(b->enumerateViews(b->instance,b->system,XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO,0,&count,NULL));
    if (count != 2) { SDL_snprintf(b->error,sizeof(b->error),"Expected two views, got %u",count); return 0; }
    CHECK(b->enumerateViews(b->instance,b->system,XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO,2,&count,cfg));
    int n=0; SDL_GPUTextureFormat *formats=SDL_GetGPUXRSwapchainFormats(b->device,b->session,&n);
    if (!formats || n<1) { SDL_snprintf(b->error,sizeof(b->error),"No compatible XR swapchain formats"); SDL_free(formats); return 0; }
    SDL_GPUTextureFormat format=formats[0];
    for(int i=0;i<n;i++) if(formats[i]==SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM_SRGB || formats[i]==SDL_GPU_TEXTUREFORMAT_B8G8R8A8_UNORM_SRGB) {format=formats[i];break;}
    SDL_free(formats);
    for(int i=0;i<3;i++) {
        XrSwapchainCreateInfo sc={XR_TYPE_SWAPCHAIN_CREATE_INFO};
        sc.usageFlags=XR_SWAPCHAIN_USAGE_COLOR_ATTACHMENT_BIT | XR_SWAPCHAIN_USAGE_SAMPLED_BIT;
        sc.width=i<2 ? cfg[i].recommendedImageRectWidth : 1280; sc.height=i<2 ? cfg[i].recommendedImageRectHeight : 720;
        sc.sampleCount=1; sc.faceCount=1; sc.arraySize=1; sc.mipCount=1;
        CHECK(SDL_CreateGPUXRSwapchain(b->device,b->session,&sc,format,&b->eye[i].handle,&b->eye[i].images));
        b->eye[i].size.width=(int32_t)sc.width; b->eye[i].size.height=(int32_t)sc.height;
        if(i<2) b->views[i].type=XR_TYPE_VIEW;
    }
    return 1;
}

/* Returns 1 when a frame is ready, 0 while idle, -1 on an XR error.
   If render is zero the caller must still call EndFrame. */
API int CelesteXR_BeginFrame(Bridge *b, CelesteEye *out, int *render) {
    if (!b || !b->session || b->frameBegun) return -1;
    XrEventDataBuffer e={XR_TYPE_EVENT_DATA_BUFFER}; XrResult r;
    while ((r=b->poll(b->instance,&e)) == XR_SUCCESS) {
        if(e.type==XR_TYPE_EVENT_DATA_SESSION_STATE_CHANGED) {
            XrSessionState s=((XrEventDataSessionStateChanged*)&e)->state;
            b->focused=(s==XR_SESSION_STATE_FOCUSED);
            SDL_Log("CelesteXR session state: %d", (int)s);
            if(s==XR_SESSION_STATE_READY && !b->running) {
                XrSessionBeginInfo info={XR_TYPE_SESSION_BEGIN_INFO}; info.primaryViewConfigurationType=XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO;
                r=b->beginSession(b->session,&info); if(XR_FAILED(r)){fail(b,"xrBeginSession",r);return -1;} b->running=1;
            } else if(s==XR_SESSION_STATE_STOPPING && b->running) { b->endSession(b->session); b->running=0; }
            else if(s==XR_SESSION_STATE_EXITING || s==XR_SESSION_STATE_LOSS_PENDING) { fail(b,"OpenXR session exited",-1);return -1; }
        } else if(e.type==XR_TYPE_EVENT_DATA_INSTANCE_LOSS_PENDING) {fail(b,"OpenXR instance lost",-1);return -1;}
        memset(&e,0,sizeof(e)); e.type=XR_TYPE_EVENT_DATA_BUFFER;
    }
    if(XR_FAILED(r)) {fail(b,"xrPollEvent",r);return -1;}
    if(!b->running) return 0;
    if(b->actionSet && b->joystick) set_input(b,b->inputEnabled);
    XrFrameWaitInfo wi={XR_TYPE_FRAME_WAIT_INFO}; XrFrameState state={XR_TYPE_FRAME_STATE};
    r=b->waitFrame(b->session,&wi,&state); if(XR_FAILED(r)){fail(b,"xrWaitFrame",r);return -1;}
    XrFrameBeginInfo bi={XR_TYPE_FRAME_BEGIN_INFO};
    r=b->beginFrame(b->session,&bi); if(XR_FAILED(r)){fail(b,"xrBeginFrame",r);return -1;}
    b->frameBegun=1; b->displayTime=state.predictedDisplayTime; b->displayPeriod=state.predictedDisplayPeriod; b->shouldRender=0; *render=0;
    b->hands.leftValid=b->hands.rightValid=0;
    if(b->firstPerson && b->inputEnabled && b->focused) {
        XrAction actions[2]={b->leftHand,b->rightHand}; XrSpace spaces[2]={b->leftHandSpace,b->rightHandSpace};
        for(int i=0;i<2;i++) {
            if(!spaces[i]) continue;
            XrActionStateGetInfo gi={XR_TYPE_ACTION_STATE_GET_INFO}; gi.action=actions[i];
            XrActionStatePose ps={XR_TYPE_ACTION_STATE_POSE}; XrSpaceLocation loc={XR_TYPE_SPACE_LOCATION};
            if(XR_SUCCEEDED(b->getPose(b->session,&gi,&ps)) && ps.isActive &&
               XR_SUCCEEDED(b->locateSpace(spaces[i],b->space,b->displayTime,&loc)) &&
               (loc.locationFlags & XR_SPACE_LOCATION_POSITION_VALID_BIT) && (loc.locationFlags & XR_SPACE_LOCATION_ORIENTATION_VALID_BIT)) {
                if(i==0) {b->hands.left=loc.pose;b->hands.leftValid=1;} else {b->hands.right=loc.pose;b->hands.rightValid=1;}
            }
        }
    }
    if(b->menuActive && b->inputEnabled && b->focused && b->aimSpace && b->flatLast) {
        XrActionStateGetInfo gi={XR_TYPE_ACTION_STATE_GET_INFO}; gi.action=b->aim;
        XrActionStatePose ps={XR_TYPE_ACTION_STATE_POSE};
        XrSpaceLocation loc={XR_TYPE_SPACE_LOCATION};
        if(XR_SUCCEEDED(b->getPose(b->session,&gi,&ps)) && ps.isActive &&
           XR_SUCCEEDED(b->locateSpace(b->aimSpace,b->space,b->displayTime,&loc)) &&
           (loc.locationFlags & XR_SPACE_LOCATION_POSITION_VALID_BIT) && (loc.locationFlags & XR_SPACE_LOCATION_ORIENTATION_VALID_BIT)) {
            b->pointer.aim=loc.pose; b->pointer.surface=b->flatPose; b->pointer.valid=1;
        }
    }
    if(!state.shouldRender) return 1;
    XrViewLocateInfo li={XR_TYPE_VIEW_LOCATE_INFO}; li.viewConfigurationType=XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO; li.displayTime=b->displayTime; li.space=b->space;
    XrViewState vs={XR_TYPE_VIEW_STATE}; uint32_t n=0;
    r=b->locateViews(b->session,&li,&vs,2,&n,b->views);
    if(XR_FAILED(r) || n!=2 || !(vs.viewStateFlags & XR_VIEW_STATE_POSITION_VALID_BIT) || !(vs.viewStateFlags & XR_VIEW_STATE_ORIENTATION_VALID_BIT)) return 1;
    for(int i=0;i<3;i++) {
        XrSwapchainImageAcquireInfo ai={XR_TYPE_SWAPCHAIN_IMAGE_ACQUIRE_INFO};
        r=b->acquire(b->eye[i].handle,&ai,&b->eye[i].image); if(XR_FAILED(r)){fail(b,"xrAcquireSwapchainImage",r);return 1;}
        b->eye[i].acquired=1;
        XrSwapchainImageWaitInfo swi={XR_TYPE_SWAPCHAIN_IMAGE_WAIT_INFO}; swi.timeout=XR_INFINITE_DURATION;
        r=b->waitImage(b->eye[i].handle,&swi); if(XR_FAILED(r)){fail(b,"xrWaitSwapchainImage",r);return 1;}
        if(i<2) {
            XrPosef p=b->views[i].pose; XrFovf f=b->views[i].fov;
            out[i]=(CelesteEye){p.position.x,p.position.y,p.position.z,p.orientation.x,p.orientation.y,p.orientation.z,p.orientation.w,f.angleLeft,f.angleRight,f.angleDown,f.angleUp,b->eye[i].size.width,b->eye[i].size.height,b->eye[i].images[b->eye[i].image]};
        } else {
            memset(&out[i],0,sizeof(out[i]));
            out[i].width=b->eye[i].size.width; out[i].height=b->eye[i].size.height; out[i].texture=b->eye[i].images[b->eye[i].image];
        }
    }
    if (!b->loggedFrame) { SDL_Log("CelesteXR is submitting stereo frames"); b->loggedFrame=1; }
    b->shouldRender=1; *render=1; return 1;
}
/* mode: 0 projection, 1 menu only, 2 gameplay with HUD, 3 paused gameplay */
API int CelesteXR_EndFrame(Bridge *b, int mode, int imagesReady) {
    if(!b || !b->frameBegun) return 0;
    /* The managed renderer normally waits for its upload and render fences.
       Retain the full idle wait as a fallback for errors or interrupted frames. */
    if (!imagesReady) SDL_WaitForGPUIdle(b->device);
    for(int i=0;i<3;i++) if(b->eye[i].acquired) { XrSwapchainImageReleaseInfo ri={XR_TYPE_SWAPCHAIN_IMAGE_RELEASE_INFO}; b->release(b->eye[i].handle,&ri); b->eye[i].acquired=0; }
    XrCompositionLayerProjectionView pv[2]={{XR_TYPE_COMPOSITION_LAYER_PROJECTION_VIEW},{XR_TYPE_COMPOSITION_LAYER_PROJECTION_VIEW}};
    for(int i=0;i<2;i++){pv[i].pose=b->views[i].pose;pv[i].fov=b->views[i].fov;pv[i].subImage.swapchain=b->eye[i].handle;pv[i].subImage.imageRect.extent=b->eye[i].size;}
    XrCompositionLayerProjection layer={XR_TYPE_COMPOSITION_LAYER_PROJECTION}; layer.space=b->space; layer.viewCount=2; layer.views=pv;
    XrCompositionLayerQuad quad={XR_TYPE_COMPOSITION_LAYER_QUAD};
    if (mode && b->shouldRender && mode != b->flatLast) {
        b->flatPose=CelesteXR_LevelPanelPose(b->views[0].pose,b->views[1].pose);
    }
    if (b->shouldRender) b->flatLast=mode;
    quad.space=b->space; quad.eyeVisibility=XR_EYE_VISIBILITY_BOTH;
    quad.subImage.swapchain=b->eye[2].handle; quad.subImage.imageRect.extent=b->eye[2].size;
    quad.pose=b->flatPose; quad.size.width=1.6f; quad.size.height=0.9f;
    if(mode>=2) quad.layerFlags=XR_COMPOSITION_LAYER_BLEND_TEXTURE_SOURCE_ALPHA_BIT;
    const XrCompositionLayerBaseHeader *layers[2]={(const XrCompositionLayerBaseHeader*)&layer, (const XrCompositionLayerBaseHeader*)&quad};
    if(mode==1) layers[0]=(const XrCompositionLayerBaseHeader*)&quad;
    XrFrameEndInfo ei={XR_TYPE_FRAME_END_INFO}; ei.displayTime=b->displayTime;ei.environmentBlendMode=XR_ENVIRONMENT_BLEND_MODE_OPAQUE;ei.layerCount=b->shouldRender?(mode>=2?2:1):0;ei.layers=b->shouldRender?layers:NULL;
    XrResult r=b->endFrame(b->session,&ei);b->frameBegun=0;
    if(XR_FAILED(r)){fail(b,"xrEndFrame",r);return 0;}
    if (b->shouldRender) {
        Uint64 now = SDL_GetTicksNS();
        if (!b->rateStart) b->rateStart = now;
        else b->submittedFrames++;
        if (now - b->rateStart >= 5000000000ULL) {
            double seconds = (double)(now - b->rateStart) / 1000000000.0;
            double predictedHz = b->displayPeriod > 0 ? 1000000000.0 / (double)b->displayPeriod : 0;
            SDL_Log("CelesteXR frame rate: %.1f submitted fps, runtime predicted cadence: %.1f Hz (%.1f s)",
                b->submittedFrames / seconds, predictedHz, seconds);
            b->rateStart = now;
            b->submittedFrames = 0;
        }
    } else {
        b->rateStart = 0;
        b->submittedFrames = 0;
    }
    return 1;
}
API void CelesteXR_Destroy(Bridge *b) {
    if(!b) return;
    if(b->frameBegun) { b->shouldRender=0; CelesteXR_EndFrame(b,0,0); }
    if(b->device) SDL_WaitForGPUIdle(b->device);
    for(int i=0;i<3;i++) if(b->eye[i].handle) SDL_DestroyGPUXRSwapchain(b->device,b->eye[i].handle,b->eye[i].images);
    if(b->space && b->destroySpace) b->destroySpace(b->space);
    if(b->aimSpace && b->destroySpace) b->destroySpace(b->aimSpace);
    if(b->leftHandSpace && b->destroySpace) b->destroySpace(b->leftHandSpace);
    if(b->rightHandSpace && b->destroySpace) b->destroySpace(b->rightHandSpace);
    if(b->session && b->destroySession) b->destroySession(b->session);
    if(b->actionSet && b->destroyActionSet) b->destroyActionSet(b->actionSet);
    if(b->joystick) SDL_CloseJoystick(b->joystick);
    if(b->joystickId) SDL_DetachVirtualJoystick(b->joystickId);
    free(b);
}
