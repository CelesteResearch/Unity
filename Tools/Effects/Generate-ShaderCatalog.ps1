[CmdletBinding(DefaultParameterSetName = 'Catalog')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Catalog')][string]$InputCatalog,
    [Parameter(Mandatory, ParameterSetName = 'InstalledMods')][switch]$InstalledMods,
    [Parameter(Mandatory, ParameterSetName = 'InstalledMods')][string]$BaseCatalog,
    [Parameter(ParameterSetName = 'InstalledMods')][string]$ModDirectory,
    [Parameter(ParameterSetName = 'InstalledMods')][string]$NativeLibrary,
    [string]$ReSharper = (Join-Path $env:USERPROFILE '.dotnet/tools/jb.exe')
)
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if ($PSCmdlet.ParameterSetName -eq 'InstalledMods') {
    if (-not $ModDirectory) { $ModDirectory = Join-Path $project 'Content/Celeste/Mods' }
    if (-not $NativeLibrary) { $NativeLibrary = Join-Path $project 'Assets/Runtime/Plugins/x86_64/CelesteGraphicsEffects.dll' }
    $baseCatalog = [IO.Path]::GetFullPath($BaseCatalog)
    if (-not [IO.File]::Exists($baseCatalog)) { throw "Base shader catalog not found: $baseCatalog" }
    $outputDirectory = Join-Path $project 'Artifacts/Effects'
    [IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
    $InputCatalog = Join-Path $outputDirectory 'Catalog.json'
    $modArguments = [Collections.Generic.List[string]]::new()
    foreach ($mod in Get-ChildItem -LiteralPath $ModDirectory -Filter '*.zip' -File | Sort-Object Name) {
        $modArguments.Add('--mod')
        $modArguments.Add($mod.FullName)
    }
    if ($modArguments.Count -eq 0) {
        $InputCatalog = $baseCatalog
    }
    else {
        & python (Join-Path $PSScriptRoot 'extend_catalog.py') --base-catalog $baseCatalog `
            --native-library ([IO.Path]::GetFullPath($NativeLibrary)) @modArguments --output $InputCatalog
        if ($LASTEXITCODE -ne 0) { throw 'Installed Mod shader catalog extraction failed' }
    }
}
& python (Join-Path $PSScriptRoot 'generate_catalog.py') ([IO.Path]::GetFullPath($InputCatalog))
if ($LASTEXITCODE -ne 0) { throw 'Native shader catalog generation failed' }
$generated = Join-Path $project 'Assets/Runtime/Graphics/SourceDevice/Effects/UnityEffectShaderCatalog.Generated.cs'
# Format a plain filename outside the Unity project; Generated.cs is excluded from cleanup by ReSharper.
$temporaryDirectory = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetDirectoryName($project)) '.ShaderCatalogReformat'))
$temporarySource = Join-Path $temporaryDirectory 'ShaderCatalogFormatting.cs'
$temporaryConfig = Join-Path $temporaryDirectory '.editorconfig'
if ([IO.File]::Exists($temporarySource) -or [IO.File]::Exists($temporaryConfig)) {
    throw "The fixed shader formatting files are already in use: $temporaryDirectory"
}
$directoryCreated = -not [IO.Directory]::Exists($temporaryDirectory)
[IO.Directory]::CreateDirectory($temporaryDirectory) | Out-Null
$sourceCreated = $false
$configCreated = $false
try {
    [IO.File]::Copy($generated, $temporarySource, $false)
    $sourceCreated = $true
    [IO.File]::Copy((Join-Path $project '.editorconfig'), $temporaryConfig, $false)
    $configCreated = $true
    Push-Location -LiteralPath $temporaryDirectory
    try {
        & $ReSharper cleanupcode --profile='Built-in: Reformat Code' --no-build --no-updates --no-buildin-settings --verbosity=WARN $temporarySource
        if ($LASTEXITCODE -ne 0) { throw 'Generated shader catalog reformat failed' }
    }
    finally { Pop-Location }
    # The generator and formatter own only this data table, not hand-maintained runtime leaves.
    [IO.File]::Copy($temporarySource, $generated, $true)
}
finally {
    if ($sourceCreated) { [IO.File]::Delete($temporarySource) }
    if ($configCreated) { [IO.File]::Delete($temporaryConfig) }
    if ($directoryCreated -and [IO.Directory]::Exists($temporaryDirectory) -and
        [IO.Directory]::GetFileSystemEntries($temporaryDirectory).Length -eq 0) {
        [IO.Directory]::Delete($temporaryDirectory, $false)
    }
}
