@echo off
setlocal
cd /d "%~dp0"
set "CELESTE64_VR=1"
if exist "%ProgramFiles(x86)%\Steam\steamapps\common\SteamVR\bin\win64\openxr_loader.dll" set "SDL_OPENXR_LIBRARY=%ProgramFiles(x86)%\Steam\steamapps\common\SteamVR\bin\win64\openxr_loader.dll"
Celeste64.exe > CelesteVR.log 2>&1
if errorlevel 1 (
  echo Celeste VR stopped. Details are in CelesteVR.log.
  pause
)
