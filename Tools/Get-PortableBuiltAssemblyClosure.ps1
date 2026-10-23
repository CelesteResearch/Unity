param(
    [Parameter(Mandatory)][string]$SharedRoot,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot '../../Tools/Get-PortableBuiltAssemblyClosure.ps1') @PSBoundParameters
