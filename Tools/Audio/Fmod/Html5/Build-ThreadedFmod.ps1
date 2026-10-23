[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SdkPath,
    [Parameter(Mandatory)][string]$ToolchainPath,
    [Parameter(Mandatory)][string]$UnityEditorExecutable,
    [string]$ProjectDirectory = (Split-Path (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent) -Parent),
    [string]$BuildDirectory,
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath($ProjectDirectory)
$sdk = (Resolve-Path -LiteralPath $SdkPath).Path
$toolchain = (Resolve-Path -LiteralPath $ToolchainPath).Path
$source = Join-Path $project 'Native/Audio/Fmod/Html5/ThreadedFmodOutput.cpp'
$fileIO = Join-Path $project 'Native/Audio/Fmod/Html5/NativeBankFileIO.cpp'
$fileIOHeader = Join-Path $project 'Native/Audio/Fmod/Html5/NativeBankFileIO.h'
$worklet = Join-Path $project 'Tools/Audio/Fmod/Html5/ThreadedFmodOutput.Worklet.js'
$rangeWorker = Join-Path $project 'Tools/Audio/Fmod/Html5/FmodBankRangeWorker.js'
$pageSource = Join-Path $project 'Tools/Audio/Fmod/Html5/FmodBankPageSource.js'
$verifiedCache = Join-Path $project 'Tools/Content/Web/VerifiedContentCache.js'
$bindings = Join-Path $sdk 'api/studio/lib/w32/fmodstudioP_bindings.a'
$runtime = Join-Path $sdk 'api/studio/lib/w32/fmodstudioP_wasm.a'
$coreHeaders = Join-Path $sdk 'api/core/inc'
$studioHeaders = Join-Path $sdk 'api/studio/inc'
$compiler = Join-Path $toolchain 'install/emscripten/em++.bat'
$config = Join-Path $toolchain '.emscripten'
$python = Join-Path (Split-Path $UnityEditorExecutable -Parent) 'Data/PlaybackEngines/WebGLSupport/BuildTools/Emscripten/python/python.exe'
foreach ($inputPath in @($source, $fileIO, $fileIOHeader, $worklet, $rangeWorker, $pageSource, $verifiedCache,
        $bindings, $runtime, $compiler, $config, $python,
        (Join-Path $coreHeaders 'fmod_output.h'), (Join-Path $studioHeaders 'fmod_studio.hpp'))) {
    if (-not [IO.File]::Exists($inputPath)) { throw "Threaded FMOD build input is missing: $inputPath" }
}
if (-not $BuildDirectory) { $BuildDirectory = Join-Path $project 'Artifacts/NativeFmodWeb' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $project 'Content/FMOD/2.03.15' }
$BuildDirectory = [IO.Path]::GetFullPath($BuildDirectory)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($BuildDirectory) | Out-Null
$modulePath = Join-Path $BuildDirectory 'fmodstudio.js'
$oldConfig = $env:EM_CONFIG
$oldPython = $env:EMSDK_PYTHON
try {
    $env:EM_CONFIG = $config
    $env:EMSDK_PYTHON = $python
    & $compiler $source $fileIO '-Wl,--whole-archive' $bindings '-Wl,--no-whole-archive' $runtime `
        '-Wl,--wrap=_ZN4FMOD6Studio6System10initializeEijjPv' '-Wl,--wrap=_ZN4FMOD6Studio6System7releaseEv' `
        '-I' $coreHeaders '-I' $studioHeaders -pthread --bind '-std=c++17' `
        '-sPTHREAD_POOL_SIZE=6' '-sALLOW_MEMORY_GROWTH=1' '-sINITIAL_MEMORY=67108864' `
        '-sMODULARIZE=1' '-sEXPORT_NAME=FMODModule' `
        '-sEXPORTED_RUNTIME_METHODS=ccall,cwrap,FS,wasmMemory,PThread' '-sFORCE_FILESYSTEM=1' `
        -O1 --no-entry '-o' $modulePath
    if ($LASTEXITCODE -ne 0) { throw "Threaded FMOD compile failed: $LASTEXITCODE" }
    $moduleFiles = @('fmodstudio.js', 'fmodstudio.wasm', 'fmodstudio.worker.js')
    foreach ($name in $moduleFiles) {
        if (-not [IO.File]::Exists((Join-Path $BuildDirectory $name))) { throw "Threaded FMOD output is missing: $name" }
    }
    [IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
    foreach ($name in $moduleFiles) {
        [IO.File]::Copy((Join-Path $BuildDirectory $name), (Join-Path $OutputDirectory $name), $true)
    }
    [IO.File]::Copy($worklet, (Join-Path $OutputDirectory 'ThreadedFmodOutput.Worklet.js'), $true)
    [IO.File]::Copy($rangeWorker, (Join-Path $OutputDirectory 'FmodBankRangeWorker.js'), $true)
    [IO.File]::Copy($pageSource, (Join-Path $OutputDirectory 'FmodBankPageSource.js'), $true)
    [IO.File]::Copy($verifiedCache, (Join-Path $OutputDirectory 'VerifiedContentCache.js'), $true)
    Get-ChildItem -LiteralPath $OutputDirectory -File |
        Where-Object Name -In @($moduleFiles + 'ThreadedFmodOutput.Worklet.js' + 'FmodBankRangeWorker.js' + 'FmodBankPageSource.js' + 'VerifiedContentCache.js') |
        ForEach-Object { Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256 }
}
finally {
    $env:EM_CONFIG = $oldConfig
    $env:EMSDK_PYTHON = $oldPython
}
