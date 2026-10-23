$ErrorActionPreference = 'Stop'
$snapshotProjectRoot = Split-Path -Parent $PSScriptRoot
$sharedProjectRoot = Join-Path (Split-Path -Parent $snapshotProjectRoot) 'Shared'
$snapshotProject = Join-Path $sharedProjectRoot 'src/Celeste.StateCloner/Celeste.StateCloner.csproj'
& dotnet build $snapshotProject --configuration Release --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'State cloner build failed.' }
$snapshotAssembly = Join-Path $sharedProjectRoot 'src/Celeste.StateCloner/bin/Release/netstandard2.1/Celeste.StateCloner.dll'
$snapshotDestination = Join-Path $snapshotProjectRoot 'Packages/local.celeste.shared/Runtime/Plugins/Celeste.StateCloner.dll'
Copy-Item -LiteralPath $snapshotAssembly -Destination $snapshotDestination
Get-FileHash -LiteralPath $snapshotDestination -Algorithm SHA256
