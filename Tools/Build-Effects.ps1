[CmdletBinding()]
param(
    [string]$ProjectDirectory = (Split-Path $PSScriptRoot -Parent),
    [string]$BuildDirectory,
    [string]$PluginDirectory,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath($ProjectDirectory)
$shared = Join-Path (Split-Path $project -Parent) 'Shared'
if (-not $PluginDirectory) { $PluginDirectory = Join-Path $project 'Assets/Runtime/Plugins/x86_64' }
$arguments = @{ ProjectDirectory = $shared; PluginDirectory = $PluginDirectory; Configuration = $Configuration }
if ($BuildDirectory) { $arguments.BuildDirectory = $BuildDirectory }
& (Join-Path $shared 'tools/Build-Effects.ps1') @arguments
