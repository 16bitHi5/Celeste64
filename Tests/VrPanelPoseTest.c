#include <SDL3/SDL.h>
#include "openxr/openxr.h"
#include "vr_panel_pose.h"
#include <stdio.h>

int main(void)
{
    XrPosef left={0}, right={0};
    float yaw=.8f, roll=.5f;
    float sy=SDL_sinf(yaw/2), cy=SDL_cosf(yaw/2), sr=SDL_sinf(roll/2), cr=SDL_cosf(roll/2);
    left.orientation=(XrQuaternionf){sy*sr,sy*cr,cy*sr,cy*cr}; right.orientation=left.orientation;
    left.position=(XrVector3f){-.03f,1.6f,0}; right.position=(XrVector3f){.03f,1.6f,0};
    XrPosef panel=CelesteXR_LevelPanelPose(left,right);
    if(SDL_fabsf(panel.orientation.x)>.0001f || SDL_fabsf(panel.orientation.z)>.0001f ||
       SDL_fabsf(panel.orientation.y-sy)>.0001f || SDL_fabsf(panel.orientation.w-cy)>.0001f ||
       SDL_fabsf(panel.position.y-1.6f)>.0001f ||
       SDL_fabsf(panel.position.x+2*SDL_sinf(yaw))>.0001f || SDL_fabsf(panel.position.z+2*SDL_cosf(yaw))>.0001f) return 1;
    left.orientation=(XrQuaternionf){SDL_sinf(SDL_PI_F/4),0,0,SDL_cosf(SDL_PI_F/4)};
    panel=CelesteXR_LevelPanelPose(left,right);
    if(SDL_isnanf(panel.position.x) || SDL_isinff(panel.position.x) || SDL_isnanf(panel.position.z) || SDL_isinff(panel.position.z) || SDL_fabsf(panel.orientation.x)>.0001f) return 2;
    puts("Level VR panel orientation checks passed."); return 0;
}
