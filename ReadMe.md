# Celeste 64 VR

## Celeste VR (fan project)

This branch is an unofficial VR adaptation of Celeste 64, based on the [original game](https://github.com/EXOK/Celeste64). It is a work in progress and is not made or endorsed by the Celeste team.

The VR work includes:

- First-person VR with tracked hands and hand-based climbing, including hand collision and climbing momentum.
- An earlier third-person VR prototype, plus VR camera and room-scale adjustments.
- VR-friendly menus, control guides, and a desktop mirror for viewing or streaming.
- A ledge landing indicator and wrist displays for strawberry and death totals.
- Support for Quest-style controllers and gamepad controls, with configurable VR options.
- Custom VR title artwork and an OpenXR bridge for headset rendering.

### Download for Quest 3

Download `Celeste-64-VR-Quest.apk` from the [release](https://github.com/16bitHi5/Celeste64/releases/tag/v0.2.0-vr) and sideload it onto a Quest 3 using SideQuest, Meta Quest Developer Hub, or ADB. Developer Mode must be enabled for sideloading. Once installed, start **Celeste 64 VR - Quest** from Unknown Sources in the headset's app library. The game runs on the headset without a PC connection.

### Download for Windows

Download `Celeste-64-VR-Windows.zip` from the [release](https://github.com/16bitHi5/Celeste64/releases/tag/v0.2.0-vr), extract the entire ZIP, and run `Celeste 64 VR - Windows.exe`. The download includes the .NET runtime; you do not need to build the game or install the .NET SDK. A PC VR headset and SteamVR are required. See the SteamVR instructions below.

### Build the VR version from source on Windows

To build from source, you will need Git, the .NET 10 SDK, CMake, Visual Studio 2022 with C++ build tools, a Windows SDK containing `dxc.exe`, and a PC VR headset with an OpenXR runtime. The build script downloads a pinned SDL revision and requires an internet connection.

Check out the `first-person-vr` branch, then run from PowerShell in the repository folder:

```powershell
.\Build-VR.ps1
```

The build is written to `Artifacts\Celeste 64 VR - Windows`.

### Build for Quest 3

Install the .NET 10 Android workload, Android SDK and NDK, Java, CMake, and Ninja. Download the FMOD Engine 2.02 Android SDK separately from FMOD and extract it. Run:

```powershell
.\Build-Quest.ps1 -FmodAndroidSdk 'C:\path\to\fmodstudioapi20224android'
```

The signed APK is written to `Artifacts\Quest\Celeste 64 VR - Quest.apk`. The build script uses the Android debug signing key by default; keep a secure backup of the signing key if you intend to distribute updates to the same installation.

### Playing with SteamVR

1. Connect your PC VR headset and controllers to your Windows PC and start SteamVR. Make sure SteamVR is configured as your active OpenXR runtime.
2. Run `Celeste 64 VR - Windows.exe`. The launcher enables VR and looks for SteamVR's OpenXR loader in the standard Steam installation folder.
3. In the game's VR controls menu, set **View** to **First Person** to play in first person. Third person is the default.
4. In first person, reach toward a climbable surface, hold a controller trigger, and pull with your hand to climb. A turquoise ring marks a valid spot to climb onto: put a hand over the ledge and press its trigger to pull yourself up. If you are already holding that trigger, release it and press again. An optional stick-climbing setting is also available in the VR controls menu. The in-game guide shows the controller bindings.

The desktop window mirrors gameplay for viewing or streaming. If the game does not start, `Start-VR.cmd` is also included and writes diagnostic output to `CelesteVR.log`.

### Updates

**Version 0.2**

- Added a turquoise ring that shows where a first-person hand can pull you onto a ledge.
- Added a strawberry counter on the left wrist and a death counter on the right wrist.
- Improved the first-person collection camera to look for a clearer view, and turned Madeline toward the camera during strawberry celebrations.
- Added optimized builds, preloaded sound effects, and support for requesting a higher headset refresh rate when available. Actual performance and refresh rate depend on the headset, runtime, and PC.

More updates are to come.

The original project information and license follow below.

This is the source code and general information for *Celeste 64: Fragments of the Mountain*, a game made by the original Celeste developers in under 2 weeks for Celeste's 6th Anniversary. We haven't done a lot of 3D development so much of this is not very optimized, it's coded in libraries mostly intended for 2D games, and we put it all together very, very quickly. Consider this similar to a weekend jam game, so experiment and learn at your own risk!  

You can find prebuilt version of the game on [itch.io](https://maddymakesgamesinc.itch.io/celeste64).

### Installation
 - You need [.NET 10.0](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
 - Clone this repo, make sure NuGet packages are found with `dotnet restore`
 - Run `Celeste64.csproj` with `dotnet run` or `dotnet build`

### Libraries Used
 - [Foster](https://github.com/FosterFramework/Foster) + [SDL3](https://github.com/libsdl-org/sdl): Input/Windowing/Rendering
 - [SledgeFormats](https://github.com/LogicAndTrick/sledge-formats): Parsing TrenchBroom level formats
 - [SharpGLTF](https://github.com/vpenades/SharpGLTF): Parsing and Animating glTF2 models
 - [FMOD](https://www.fmod.com): For Music and Sound Effects
 - FMOD logo: Firelight Technologies Pty Ltd, from the [official FMOD logo package](https://www.fmod.com/attribution).

### Tools Used
 - [TrenchBroom](https://trenchbroom.github.io/): For Level Editing
 - [Blender](https://www.blender.org/): For creating 3D Models
 - [Aseprite](https://www.aseprite.org/): For drawing Textures

### Resources Used
 - [khronos glTF Tutorials](https://github.khronos.org/glTF-Tutorials/gltfTutorial/gltfTutorial_020_Skins.html#the-joint-matrices): To figure out how Mesh Skins/Bones work
 - [LearnOpenGL](https://learnopengl.com/Advanced-OpenGL/Depth-testing): For general rendering concepts / normalizing Depth
 - [Kenny's Input Prompts](https://kenney.nl/assets/input-prompts): For UI Button Prompts
 - [Renogare](https://www.dafont.com/renogare.font): Main font

### Created By ...
 - [Maddy Thorson](http://maddymakesgames.com/)
 - [Noel Berry](https://noelberry.ca)
 - [Amora B.](https://amorabettany.com)
 - [Pedro "Saint11" Medeiros](http://saint11.org/)
 - [Power Up Audio](https://powerupaudio.com/)
 - [Lena Raine](https://lena.fyi/)
 - [Heidy Motta](https://www.heidy.page/).

### License
 - The Celeste IP and everything in the `Content` folder are owned by [Maddy Makes Games, Inc](https://www.maddymakesgames.com/).
 - The `Source` folder, with exceptions where noted, is [licensed under MIT](Source/License.txt).
 - The `Source/Audio/FMOD` folder contains bindings and binaries from FMOD.
 - We're fine with non-commercial Mods / Levels / Fan Games using assets from the `Content` folder as long as it's clear it is not made by the Celeste team or endorsed by us.

