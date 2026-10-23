[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$UnityEditorExecutable,
    [string]$ProjectDirectory = (Split-Path $PSScriptRoot -Parent),
    [string]$BuildDirectory,
    [string]$PluginDirectory
)

$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath($ProjectDirectory)
$sdk = Join-Path (Split-Path $UnityEditorExecutable -Parent) 'Data/PlaybackEngines/WebGLSupport/BuildTools/Emscripten'
$python = Join-Path $sdk 'python/python.exe'
$emcc = Join-Path $sdk 'emscripten/emcc.py'
$emar = Join-Path $sdk 'emscripten/emar.py'
$nm = Join-Path $sdk 'llvm/llvm-nm.exe'
$shared = Join-Path (Split-Path $project -Parent) 'Shared'
$mojo = (Join-Path $shared 'vendor/mojoshader-ad5dff84830c2863c841f4b1f4e3df78c705b383').Replace('\', '/')
$effects = (Join-Path $shared 'native/Effects').Replace('\', '/')
if (-not $BuildDirectory) { $BuildDirectory = Join-Path $project 'Artifacts/NativeEffectsWeb' }
if (-not $PluginDirectory) { $PluginDirectory = Join-Path $project 'Assets/Runtime/Plugins/WebGL' }
$BuildDirectory = [IO.Path]::GetFullPath($BuildDirectory)
$PluginDirectory = [IO.Path]::GetFullPath($PluginDirectory)
$archive = Join-Path $BuildDirectory 'CelesteGraphicsEffects.a'
$output = Join-Path $PluginDirectory 'CelesteGraphicsEffects.a'
$importTemplate = Join-Path $PSScriptRoot 'PluginImportTemplates/WebGL/CelesteGraphicsEffects.a.meta.template'
if (-not [IO.File]::Exists($importTemplate)) { throw "The WebGL plugin import template is missing: $importTemplate" }
[IO.Directory]::CreateDirectory($BuildDirectory) | Out-Null

$oldConfig = $env:EM_CONFIG
$oldPython = $env:EMSDK_PYTHON
$oldWorkaround = $env:EM_WORKAROUND_PYTHON_BUG_34780
try {
    $env:EM_CONFIG = Join-Path $sdk '.emscripten'
    $env:EMSDK_PYTHON = $python
    $env:EM_WORKAROUND_PYTHON_BUG_34780 = '1'
    $version = & $python '-E' $emcc '--version'
    if ($LASTEXITCODE -ne 0) { throw 'The installed Unity Web compiler could not be started.' }
    # Supply the pinned vendor's missing Emscripten alloca declaration to this target.
    # Do not run vendor CMake or regenerate its version header.
    $common = @('-O2', '-fwasm-exceptions', '-sWASM_LEGACY_EXCEPTIONS=1', '-include', 'alloca.h', '-DMOJOSHADER_EFFECT_SUPPORT',
        '-DSUPPORT_PROFILE_ARB1=0', '-DSUPPORT_PROFILE_ARB1_NV=0', '-DSUPPORT_PROFILE_METAL=0',
        '-DSUPPORT_PROFILE_SPIRV=0', '-DSUPPORT_PROFILE_GLSPIRV=0', "-I$mojo", "-I$effects")
    $sources = @('mojoshader.c', 'mojoshader_common.c', 'mojoshader_effects.c',
        'profiles/mojoshader_profile_common.c', 'profiles/mojoshader_profile_bytecode.c',
        'profiles/mojoshader_profile_d3d.c', 'profiles/mojoshader_profile_hlsl.c', 'profiles/mojoshader_profile_glsl.c')
    $objects = [Collections.Generic.List[string]]::new()
    $inputs = [Collections.Generic.List[string]]::new()
    foreach ($source in $sources) {
        $sourcePath = (Join-Path $mojo $source).Replace('\', '/')
        $object = Join-Path $BuildDirectory (([IO.Path]::GetFileNameWithoutExtension($source)) + '.o')
        & $python '-E' $emcc @common '-std=c99' '-c' $sourcePath '-o' $object
        if ($LASTEXITCODE -ne 0) { throw "Web effect C compilation failed: $source" }
        $objects.Add($object)
        $inputs.Add($sourcePath)
    }
    foreach ($source in @('EffectFrontend.cpp', 'EffectFrontendAbi.cpp')) {
        $sourcePath = (Join-Path $effects $source).Replace('\', '/')
        $object = Join-Path $BuildDirectory (([IO.Path]::GetFileNameWithoutExtension($source)) + '.o')
        & $python '-E' $emcc @common '-std=c++17' '-c' $sourcePath '-o' $object
        if ($LASTEXITCODE -ne 0) { throw "Web effect C++ compilation failed: $source" }
        $objects.Add($object)
        $inputs.Add($sourcePath)
    }
    # Replace only this fixed intermediate archive, not the output plugin.
    if ([IO.File]::Exists($archive)) { [IO.File]::Delete($archive) }
    & $python '-E' $emar 'rcs' $archive @objects
    if ($LASTEXITCODE -ne 0) { throw 'Web effect archive creation failed.' }
    $symbols = & $nm '--defined-only' $archive
    if ($LASTEXITCODE -ne 0) { throw 'Web effect archive symbol inspection failed.' }
    $abiHeader = Join-Path $effects 'EffectFrontendAbi.h'
    $expected = @([regex]::Matches([IO.File]::ReadAllText($abiHeader), 'CE_CALL\s+(CelesteEffect\w+)\s*\(') |
            ForEach-Object { $_.Groups[1].Value })
    foreach ($symbol in $expected) {
        if (-not ($symbols -match ("\b" + [regex]::Escape($symbol) + '$'))) {
            throw "Web effect archive is missing ABI symbol: $symbol"
        }
    }
    $headers = @(Get-ChildItem -LiteralPath $mojo, $effects -Filter '*.h' -File)
    $hashes = @($inputs.ToArray() + $headers.FullName | ForEach-Object {
            [ordered]@{ Path = [IO.Path]::GetRelativePath($project, $_).Replace('\', '/'); SHA256 = (Get-FileHash -LiteralPath $_).Hash }
        })
    $archiveHash = (Get-FileHash -LiteralPath $archive).Hash
    [IO.Directory]::CreateDirectory($PluginDirectory) | Out-Null
    if (-not [IO.File]::Exists($output) -or (Get-FileHash -LiteralPath $output).Hash -ne $archiveHash) {
        [IO.File]::Copy($archive, $output, $true)
    }
    $importMetadata = $output + '.meta'
    if (-not [IO.File]::Exists($importMetadata) -or
        (Get-FileHash -LiteralPath $importMetadata).Hash -cne (Get-FileHash -LiteralPath $importTemplate).Hash) {
        [IO.File]::Copy($importTemplate, $importMetadata, $true)
    }
    $report = [ordered]@{
        Compiler = ($version -join "`n")
        UnityEditorExecutable = [IO.Path]::GetFullPath($UnityEditorExecutable)
        CompileFlags = $common
        CStandard = 'c99'
        CppStandard = 'c++17'
        NativeThreads = $false
        ExceptionMode = 'WebAssembly EH (-fwasm-exceptions, WASM_LEGACY_EXCEPTIONS=1), matching Unity 6000.6 compile/link'
        Sources = $hashes
        AbiSymbols = $expected
        ArchiveSHA256 = $archiveHash
        Bytes = (Get-Item -LiteralPath $output).Length
        PluginPath = $output
        FullUnityWebLinkVerified = $false
    }
    $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $BuildDirectory 'build.json') -Encoding utf8
    [pscustomobject]@{ Plugin = $output; Bytes = $report.Bytes; SHA256 = $archiveHash; AbiSymbols = $expected.Count }
}
finally {
    $env:EM_CONFIG = $oldConfig
    $env:EMSDK_PYTHON = $oldPython
    $env:EM_WORKAROUND_PYTHON_BUG_34780 = $oldWorkaround
}
