function Get-CelesteContentPaths {
    param([Parameter(Mandatory)][string]$ProjectRoot)

    $workspaceRoot = [IO.Path]::GetFullPath((Join-Path $ProjectRoot '..'))
    $resolveContentPaths = {
        . (Join-Path $workspaceRoot 'Tools/ContentPaths.ps1')
        Get-CelesteContentPaths -WorkspaceRoot $workspaceRoot
    }
    & $resolveContentPaths
}
