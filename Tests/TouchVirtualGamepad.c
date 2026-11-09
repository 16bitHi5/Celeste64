#include <SDL3/SDL.h>
#include <stdio.h>

int main(void)
{
    if (!SDL_Init(SDL_INIT_GAMEPAD)) return 1;
    SDL_VirtualJoystickDesc desc; SDL_zero(desc); SDL_INIT_INTERFACE(&desc);
    desc.type=SDL_JOYSTICK_TYPE_GAMEPAD; desc.naxes=4; desc.nbuttons=SDL_GAMEPAD_BUTTON_COUNT;
    desc.axis_mask=15; desc.button_mask=(1u<<SDL_GAMEPAD_BUTTON_COUNT)-1; desc.name="Celeste Quest Touch Test";
    SDL_JoystickID id=SDL_AttachVirtualJoystick(&desc);
    SDL_Joystick *joystick=SDL_OpenJoystick(id);
    SDL_Gamepad *gamepad=SDL_OpenGamepad(id);
    if (!id || !joystick || !gamepad) { fprintf(stderr,"Virtual gamepad creation failed: %s\n",SDL_GetError()); return 2; }
    SDL_SetJoystickVirtualAxis(joystick,SDL_GAMEPAD_AXIS_LEFTX,20000);
    SDL_SetJoystickVirtualButton(joystick,SDL_GAMEPAD_BUTTON_SOUTH,true);
    SDL_SetJoystickVirtualButton(joystick,SDL_GAMEPAD_BUTTON_START,true);
    SDL_SetJoystickVirtualButton(joystick,SDL_GAMEPAD_BUTTON_LEFT_SHOULDER,true);
    SDL_UpdateGamepads();
    if (SDL_GetGamepadAxis(gamepad,SDL_GAMEPAD_AXIS_LEFTX)!=20000 ||
        !SDL_GetGamepadButton(gamepad,SDL_GAMEPAD_BUTTON_SOUTH) ||
        !SDL_GetGamepadButton(gamepad,SDL_GAMEPAD_BUTTON_START) ||
        !SDL_GetGamepadButton(gamepad,SDL_GAMEPAD_BUTTON_LEFT_SHOULDER)) {
        fprintf(stderr,"Virtual gamepad mapping failed\n"); return 3;
    }
    SDL_CloseGamepad(gamepad); SDL_CloseJoystick(joystick); SDL_DetachVirtualJoystick(id); SDL_Quit();
    puts("Quest virtual gamepad mappings passed."); return 0;
}
