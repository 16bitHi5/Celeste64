param(
    [string]$SdlSource = (Join-Path $env:TEMP 'CelesteVR-SDL'),
    [string]$AndroidSdk = (Join-Path $env:LOCALAPPDATA 'Android\Sdk'),
    [string]$AndroidNdk = (Join-Path $env:LOCALAPPDATA 'Android\Sdk\ndk\23.2.8568313'),
    [string]$Dotnet = (Join-Path $env:LOCALAPPDATA 'CelesteVR\dotnet\dotnet.exe'),
    [string]$Ninja = (Join-Path $env:LOCALAPPDATA 'Android\Sdk\cmake\3.10.2.4988404\bin\ninja.exe'),
    [string]$FmodAndroidSdk = ''
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$inputs = Join-Path $env:TEMP 'CelesteQuest-inputs'
$nativeBuild = Join-Path $env:TEMP 'CelesteVR-Quest-Native'
$classes = Join-Path $env:TEMP 'CelesteVR-SDL-java-classes'
$revision = '359933aab0095d030fd9310e09b119d3ef0a662c'
if ($FmodAndroidSdk) {
    foreach ($file in @('api\core\lib\fmod.jar', 'api\core\lib\arm64-v8a\libfmod.so', 'api\studio\lib\arm64-v8a\libfmodstudio.so')) {
        if (-not (Test-Path (Join-Path $FmodAndroidSdk $file))) { throw "FMOD Android SDK file missing: $file" }
    }
}
if (-not (Test-Path (Join-Path $SdlSource '.git'))) {
    git clone https://github.com/libsdl-org/SDL.git $SdlSource
    if ($LASTEXITCODE -ne 0) { throw 'SDL clone failed.' }
    git -C $SdlSource checkout --detach $revision
    if ($LASTEXITCODE -ne 0) { throw 'SDL checkout failed.' }
}
New-Item -ItemType Directory -Force $inputs, $classes | Out-Null
& (Join-Path $root 'Build-Quest-Native.ps1') -SdlSource $SdlSource -AndroidNdk $AndroidNdk -BuildDirectory $nativeBuild -Ninja $Ninja
$androidJar = Join-Path $AndroidSdk 'platforms\android-36\android.jar'
if (-not (Test-Path $androidJar)) { throw 'Android platform 36 is required to compile the SDL Java bridge.' }
$javaPath = Join-Path $SdlSource 'android-project\app\src\main\java\org\libsdl\app'
$sources = Get-ChildItem $javaPath -Filter '*.java' | ForEach-Object { $_.FullName }
$ErrorActionPreference = 'Continue' # javac emits nonfatal notices on stderr
& javac -encoding UTF-8 -source 8 -target 8 -classpath $androidJar -d $classes $sources
$ErrorActionPreference = 'Stop'
if ($LASTEXITCODE -ne 0) { throw 'SDL Java bridge compilation failed.' }
& jar cf (Join-Path $inputs 'SDLBridge.jar') -C $classes .
if ($LASTEXITCODE -ne 0) { throw 'SDL Java bridge packaging failed.' }
Copy-Item (Join-Path $nativeBuild 'sdl\libSDL3.so') $inputs -Force
Copy-Item (Join-Path $nativeBuild 'bridge\libCelesteXR.so') $inputs -Force
$loader = Join-Path $inputs 'openxr.aar'
if (-not (Test-Path $loader)) {
    Invoke-WebRequest 'https://repo.maven.apache.org/maven2/org/khronos/openxr/openxr_loader_for_android/1.1.46/openxr_loader_for_android-1.1.46.aar' -OutFile $loader
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$zipPath = Join-Path $inputs 'content.zip'
$zipStream = [IO.File]::Open($zipPath, [IO.FileMode]::Create)
try {
    $zip = [IO.Compression.ZipArchive]::new($zipStream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem (Join-Path $root 'Content') -File -Recurse | ForEach-Object {
            $entry = $_.FullName.Substring($root.Length + 1).Replace('\', '/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $entry, [IO.Compression.CompressionLevel]::Fastest) | Out-Null
        }
    } finally { $zip.Dispose() }
} finally { $zipStream.Dispose() }
& $Dotnet restore (Join-Path $root 'Quest\CelesteQuest.csproj') "-p:QuestBuildInputs=$inputs" "-p:AndroidSdkDirectory=$AndroidSdk" "-p:FmodAndroidSdk=$FmodAndroidSdk"
if ($LASTEXITCODE -ne 0) { throw 'Android project restore failed.' }
& $Dotnet restore (Join-Path $root 'Vendor\Foster\Framework\Foster.Framework.csproj') -r android-arm64
if ($LASTEXITCODE -ne 0) { throw 'Foster Android restore failed.' }
& $Dotnet build (Join-Path $root 'Vendor\Foster\Framework\Foster.Framework.csproj') -c Release -r android-arm64 --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Foster Android build failed.' }
& $Dotnet build (Join-Path $root 'Quest\CelesteQuest.csproj') -c Release --no-restore "-p:QuestBuildInputs=$inputs" "-p:AndroidSdkDirectory=$AndroidSdk" "-p:FmodAndroidSdk=$FmodAndroidSdk" -p:AndroidSdkBuildToolsVersion=36.0.0
if ($LASTEXITCODE -ne 0) { throw 'Quest APK build failed.' }
$apk = Join-Path $root 'Quest\bin\Release\net10.0-android\android-arm64\com.subpixel.celeste64vr.quest-Signed.apk'
$output = Join-Path $root 'Artifacts\Quest'
New-Item -ItemType Directory -Force $output | Out-Null
Copy-Item $apk (Join-Path $output 'Celeste 64 VR - Quest.apk') -Force
$audioStatus = if ($FmodAndroidSdk) { 'FMOD audio enabled' } else { 'silent; FMOD Android SDK not supplied' }
Write-Host "Celeste 64 VR - Quest ($audioStatus): $(Join-Path $output 'Celeste 64 VR - Quest.apk')"
