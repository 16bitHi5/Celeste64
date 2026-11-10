#define UNICODE
#define _UNICODE
#include <windows.h>
#include <wchar.h>

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE previous, PWSTR arguments, int show)
{
    (void)instance; (void)previous; (void)arguments; (void)show;
    wchar_t directory[32768], game[32768], command[32768], loader[32768], programFiles[32768];
    DWORD length = GetModuleFileNameW(NULL, directory, 32768);
    if (!length || length >= 32768) {
        MessageBoxW(NULL, L"Could not locate the Celeste VR folder.", L"Celeste VR", MB_OK | MB_ICONERROR);
        return 1;
    }
    wchar_t *separator = wcsrchr(directory, L'\\');
    if (!separator) return 1;
    *separator = L'\0';
    if (swprintf_s(game, 32768, L"%ls\\Celeste64.exe", directory) < 0 ||
        GetFileAttributesW(game) == INVALID_FILE_ATTRIBUTES) {
        MessageBoxW(NULL, L"Celeste64.exe was not found next to this launcher. Keep the launcher in the game folder, and use the desktop shortcut to start it.", L"Celeste VR", MB_OK | MB_ICONERROR);
        return 1;
    }
    SetEnvironmentVariableW(L"CELESTE64_VR", L"1");
    DWORD found = GetEnvironmentVariableW(L"ProgramFiles(x86)", programFiles, 32768);
    if (found && found < 32768 &&
        swprintf_s(loader, 32768, L"%ls\\Steam\\steamapps\\common\\SteamVR\\bin\\win64\\openxr_loader.dll", programFiles) >= 0 &&
        GetFileAttributesW(loader) != INVALID_FILE_ATTRIBUTES)
        SetEnvironmentVariableW(L"SDL_OPENXR_LIBRARY", loader);

    if (swprintf_s(command, 32768, L"\"%ls\"", game) < 0) return 1;
    STARTUPINFOW startup = {0};
    PROCESS_INFORMATION process = {0};
    startup.cb = sizeof(startup);
    if (!CreateProcessW(game, command, NULL, NULL, FALSE, 0, NULL, directory, &startup, &process)) {
        MessageBoxW(NULL, L"Could not start Celeste VR. Make sure the game files are still in this folder.", L"Celeste VR", MB_OK | MB_ICONERROR);
        return 1;
    }
    CloseHandle(process.hThread);
    CloseHandle(process.hProcess);
    return 0;
}
