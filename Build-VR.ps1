param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'Artifacts\CelesteVR'))

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$sdk = Join-Path $env:LOCALAPPDATA 'CelesteVR\dotnet\dotnet.exe'
if (-not (Test-Path $sdk)) { $sdk = 'dotnet' }
$sdlSource = Join-Path $env:TEMP 'CelesteVR-SDL'
$sdlBuild = Join-Path $sdlSource 'build'
$bridgeBuild = Join-Path $env:TEMP 'CelesteVR-XRBridge-build'
$revision = '359933aab0095d030fd9310e09b119d3ef0a662c'
if (-not (Test-Path (Join-Path $sdlSource '.git'))) {
    git clone https://github.com/libsdl-org/SDL.git $sdlSource
    if ($LASTEXITCODE -ne 0) { throw 'SDL download failed.' }
    git -C $sdlSource checkout --detach $revision
    if ($LASTEXITCODE -ne 0) { throw 'Could not select the tested SDL revision.' }
}
$actualRevision = (git -C $sdlSource rev-parse HEAD).Trim()
if ($actualRevision -ne $revision) { throw "Unexpected SDL revision in $sdlSource; expected $revision." }

# This pinned SDL development revision has a Windows dialog compilation typo.
$dialogFile = Join-Path $sdlSource 'src\dialog\windows\SDL_windowsdialog.c'
$dialog = [IO.File]::ReadAllText($dialogFile)
$dialog = $dialog.Replace('NULL, &IID_IShellItem, (void**)&pFolderItem)) {', 'NULL, &IID_IShellItem, (void**)&pFolderItem))) {')
[IO.File]::WriteAllText($dialogFile, $dialog)
cmake -S $sdlSource -B $sdlBuild -G 'Visual Studio 17 2022' -A x64 -DSDL_TESTS=OFF -DSDL_TEST_LIBRARY=OFF
if ($LASTEXITCODE -ne 0) { throw 'SDL configuration failed.' }
cmake --build $sdlBuild --config Release --parallel 8
if ($LASTEXITCODE -ne 0) { throw 'SDL build failed.' }
cmake -S (Join-Path $root 'Native\CelesteXR') -B $bridgeBuild -G 'Visual Studio 17 2022' -A x64 "-DSDL_SOURCE_DIR=$sdlSource" "-DSDL_BUILD_DIR=$sdlBuild"
if ($LASTEXITCODE -ne 0) { throw 'XR bridge configuration failed.' }
cmake --build $bridgeBuild --config Release
if ($LASTEXITCODE -ne 0) { throw 'XR bridge build failed.' }
$output = if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $root $OutputDirectory }
& $sdk build (Join-Path $root 'Celeste64.csproj') -c Debug -o $output
if ($LASTEXITCODE -ne 0) { throw 'Celeste build failed.' }
Copy-Item (Join-Path $sdlBuild 'Release\SDL3.dll') $output -Force
Copy-Item (Join-Path $bridgeBuild 'Release\CelesteXR.dll') $output -Force
Copy-Item (Join-Path $root 'Start-VR.cmd') $output -Force
# The upstream DXIL shaders fail D3D12 stage linkage on this machine. Compile
# the shipped HLSL directly for this build, without changing the game assets.
$kitBin = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
$dxc = Get-ChildItem $kitBin -Directory | Sort-Object Name -Descending |
    ForEach-Object { Join-Path $_.FullName 'x64\dxc.exe' } |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $dxc) { throw 'Windows SDK dxc.exe is required to compile VR shaders.' }
Copy-Item (Join-Path $root 'Content') $output -Recurse -Force
# The edge shader uses the batcher's fourth input, whose D3D semantic is 3.
$edgeSource = Join-Path $output 'Content\Shaders\Edge.hlsl'
$edge = [IO.File]::ReadAllText($edgeSource).Replace('TEXCOORD4', 'TEXCOORD3')
[IO.File]::WriteAllText($edgeSource, $edge)
foreach ($shader in Get-ChildItem (Join-Path $output 'Content\Shaders') -Filter '*.hlsl') {
    foreach ($stage in @('vertex', 'fragment')) {
        $profile = if ($stage -eq 'vertex') { 'vs_6_0' } else { 'ps_6_0' }
        $destination = Join-Path $output ("Content\Shaders\Compiled\{0}.{1}.dxil" -f $shader.BaseName, $stage)
        & $dxc -T $profile -E ($stage + '_main') -Fo $destination $shader.FullName
        if ($LASTEXITCODE -ne 0) { throw "Failed to compile $($shader.Name) $stage." }
    }
}
Write-Host "Experimental build created in $output"
