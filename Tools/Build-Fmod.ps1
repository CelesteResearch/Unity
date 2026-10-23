param(
    [Parameter(Mandatory)][string]$SourceProject,
    [Parameter(Mandatory)][string]$StudioCli
)
$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path $PSScriptRoot -Parent
$sourceFile = (Resolve-Path -LiteralPath $SourceProject).Path
$sourceDirectory = Split-Path $sourceFile -Parent
$workingDirectory = Join-Path $projectDirectory 'Audio/FMOD/Celeste'
$outputDirectory = Join-Path $projectDirectory 'Artifacts/FmodBanks'
if (-not (Test-Path -LiteralPath $StudioCli)) { throw 'FMOD Studio 1.10 command-line builder was not found.' }
$sourceXml = [xml](Get-Content -LiteralPath $sourceFile -Raw)
if ($sourceXml.objects.serializationModel -notlike 'Studio.01.10.*') { throw 'This builder expects the original 1.10 FMOD project format.' }
[IO.Directory]::CreateDirectory($workingDirectory) | Out-Null
[IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$sources = @((Get-Item -LiteralPath $sourceFile))
foreach ($folder in @('Assets', 'Metadata', 'Plugins')) {
    $directory = Join-Path $sourceDirectory $folder
    if (Test-Path -LiteralPath $directory) { $sources += Get-ChildItem -LiteralPath $directory -File -Recurse }
}
foreach ($file in $sources) {
    $relative = [IO.Path]::GetRelativePath($sourceDirectory, $file.FullName)
    $destination = Join-Path $workingDirectory $relative
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    if (-not (Test-Path -LiteralPath $destination) -or (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $hash) { Copy-Item -LiteralPath $file.FullName -Destination $destination -Force }
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $hash) { throw "FMOD source copy differs: $relative" }
}
$workspacePath = Join-Path $workingDirectory 'Metadata/Workspace.xml'
$workspace = [xml](Get-Content -LiteralPath $workspacePath -Raw)
$property = $workspace.SelectSingleNode('//object[@class="Workspace"]/property[@name="builtBanksOutputDirectory"]')
if (-not $property) { throw 'FMOD workspace output property is missing.' }
$property.RemoveAll(); $property.SetAttribute('name', 'builtBanksOutputDirectory')
$value = $workspace.CreateElement('value'); $value.InnerText = $outputDirectory.Replace('\', '/'); [void]$property.AppendChild($value)
$workspace.Save($workspacePath)
Write-Output ("Verified {0} official project files; building the workspace copy with FMOD Studio 1.10." -f $sources.Count)
$workingProject = Join-Path $workingDirectory ([IO.Path]::GetFileName($sourceFile))
$logPath = Join-Path $projectDirectory 'Logs/FmodBuild.log'
[IO.Directory]::CreateDirectory((Split-Path -Parent $logPath)) | Out-Null
& $StudioCli -build -platforms Desktop $workingProject *> $logPath
$buildExit = $LASTEXITCODE
Get-Content -LiteralPath $logPath -Tail 35
if ($buildExit -ne 0) { throw "FMOD bank build failed ($buildExit); see Logs/FmodBuild.log." }
$builtDirectory = Join-Path $outputDirectory 'Desktop'
if (-not (Test-Path -LiteralPath (Join-Path $builtDirectory 'Master Bank.bank'))) { throw 'FMOD reported success but the expected Desktop master bank is missing.' }
& (Join-Path $PSScriptRoot 'Import-Fmod.ps1') -BankDirectory $builtDirectory
Write-Output 'Official project banks are ready in Content/Celeste/FMOD/Desktop.'
