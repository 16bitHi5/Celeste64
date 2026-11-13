param(
    [Parameter(Mandatory = $true)][string]$SdlSource,
    [Parameter(Mandatory = $true)][string]$AndroidNdk,
    [string]$BuildDirectory = (Join-Path $env:TEMP 'CelesteVR-Quest-Native'),
    [string]$Ninja = 'ninja'
)

# Builds native libraries only. This does not produce an Android application or APK.
$ErrorActionPreference = 'Stop'
$revision = '359933aab0095d030fd9310e09b119d3ef0a662c'
$actualRevision = (git -C $SdlSource rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $actualRevision -ne $revision) {
    throw "SDL source must be checked out at $revision."
}
$xrPatch = Join-Path $PSScriptRoot 'Quest\SDL-android-openxr.patch'
git -C $SdlSource apply --reverse --check $xrPatch 2>$null
if ($LASTEXITCODE -ne 0) {
    git -C $SdlSource apply --check $xrPatch
    if ($LASTEXITCODE -ne 0) { throw 'Could not apply the Android OpenXR compatibility patch to SDL.' }
    git -C $SdlSource apply $xrPatch
    if ($LASTEXITCODE -ne 0) { throw 'SDL Android OpenXR patch failed.' }
}
$toolchain = Join-Path $AndroidNdk 'build\cmake\android.toolchain.cmake'
if (-not (Test-Path $toolchain)) { throw "Android NDK toolchain not found: $toolchain" }
$sdlBuild = Join-Path $BuildDirectory 'sdl'
$bridgeBuild = Join-Path $BuildDirectory 'bridge'
$common = @('-G', 'Ninja', "-DCMAKE_MAKE_PROGRAM=$Ninja", "-DCMAKE_TOOLCHAIN_FILE=$toolchain",
    '-DANDROID_ABI=arm64-v8a', '-DANDROID_PLATFORM=android-29', '-DCMAKE_BUILD_TYPE=Release')
cmake -S $SdlSource -B $sdlBuild @common -DSDL_TESTS=OFF -DSDL_TEST_LIBRARY=OFF -DSDL_SHARED=ON -DSDL_STATIC=OFF -Wno-dev
if ($LASTEXITCODE -ne 0) { throw 'Android SDL configuration failed.' }
cmake --build $sdlBuild --parallel 8
if ($LASTEXITCODE -ne 0) { throw 'Android SDL build failed.' }
cmake -S (Join-Path $PSScriptRoot 'Native\CelesteXR') -B $bridgeBuild @common "-DSDL_SOURCE_DIR=$SdlSource" "-DSDL_BUILD_DIR=$sdlBuild" -Wno-dev
if ($LASTEXITCODE -ne 0) { throw 'Android XR bridge configuration failed.' }
cmake --build $bridgeBuild --parallel 8
if ($LASTEXITCODE -ne 0) { throw 'Android XR bridge build failed.' }
Write-Host "Native libraries built in $sdlBuild and $bridgeBuild. No APK was produced."
