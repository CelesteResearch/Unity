param(
    [string]$GameDirectory,
    [string]$BankDirectory
)
$ErrorActionPreference = 'Stop'
if (-not $GameDirectory -and -not $BankDirectory) { throw 'Supply -GameDirectory for native libraries or -BankDirectory for audio banks.' }
$projectDirectory = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'ContentPaths.ps1')
$contentPaths = Get-CelesteContentPaths -ProjectRoot $projectDirectory

if ($GameDirectory) {
    $nativeDirectory = Join-Path $GameDirectory 'lib64-win-x64'
    $targetDirectory = Join-Path $projectDirectory 'Assets/Plugins/FMOD/x86_64'
    [IO.Directory]::CreateDirectory($targetDirectory) | Out-Null
    foreach ($name in @('fmod64.dll', 'fmodstudio.dll')) {
        $source = Join-Path $nativeDirectory $name
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Missing x64 FMOD runtime: $source" }
        $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($source).FileVersion
        if ($version -notlike '1.10.14*') { throw "FMOD ABI requires 1.10.14; found $version" }
        $destination = Join-Path $targetDirectory $name
        $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        if (-not (Test-Path -LiteralPath $destination) -or (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $hash) {
            Copy-Item -LiteralPath $source -Destination $destination -Force
        }
        if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $hash) { throw "FMOD runtime copy differs: $name" }
    }
    Write-Output 'Imported FMOD 1.10.14 Windows x64 runtime.'
}
if ($BankDirectory) {
    $bankTarget = Join-Path $contentPaths.Game 'FMOD/Desktop'
    foreach ($name in @('Master Bank.bank', 'Master Bank.strings.bank')) {
        if (-not (Test-Path -LiteralPath (Join-Path $BankDirectory $name) -PathType Leaf)) { throw "Missing audio bank: $name" }
    }
    [IO.Directory]::CreateDirectory($bankTarget) | Out-Null
    $banks = @(Get-ChildItem -LiteralPath $BankDirectory -Filter '*.bank' -File)
    foreach ($file in $banks) {
        $destination = Join-Path $bankTarget $file.Name
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        if (-not (Test-Path -LiteralPath $destination) -or (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $hash) {
            Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
        }
        if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $hash) { throw "Bank copy differs: $($file.Name)" }
    }
    Write-Output ("Imported {0} audio banks." -f $banks.Count)
}
