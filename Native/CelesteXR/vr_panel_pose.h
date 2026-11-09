#pragma once
#include <SDL3/SDL.h>
#include "openxr/openxr.h"

/* Place a level panel two metres along the headset's horizontal heading. */
static XrPosef CelesteXR_LevelPanelPose(XrPosef left, XrPosef right)
{
    XrQuaternionf q=left.orientation;
    float fx=-2.0f*(q.x*q.z+q.w*q.y);
    float fz=-(1.0f-2.0f*(q.x*q.x+q.y*q.y));
    float length=SDL_sqrtf(fx*fx+fz*fz);
    if(length < 0.0001f) {
        /* Looking straight up/down: derive heading from the projected right axis. */
        float rx=1.0f-2.0f*(q.y*q.y+q.z*q.z);
        float rz=2.0f*(q.x*q.z-q.w*q.y);
        fx=rz; fz=-rx; length=SDL_sqrtf(fx*fx+fz*fz);
    }
    if(length < 0.0001f) { fx=0; fz=-1; length=1; }
    fx/=length; fz/=length;
    float yaw=SDL_atan2f(-fx,-fz);
    XrPosef result={0};
    result.orientation.y=SDL_sinf(yaw*.5f);
    result.orientation.w=SDL_cosf(yaw*.5f);
    result.position.x=(left.position.x+right.position.x)*.5f+fx*2;
    result.position.y=(left.position.y+right.position.y)*.5f;
    result.position.z=(left.position.z+right.position.z)*.5f+fz*2;
    return result;
}
