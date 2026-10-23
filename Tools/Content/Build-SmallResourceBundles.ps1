[CmdletBinding()]
param(
    [Parameter(Mandatory)]$Manifest,
    [Parameter(Mandatory)][string]$OutputRoot
)

$ErrorActionPreference = 'Stop'
$bundles = [Collections.Generic.List[object]]::new()
$group = [Collections.Generic.List[object]]::new()
$size = 0L
$root = [IO.Path]::GetFullPath($OutputRoot)

function Write-Bundle {
    if ($group.Count -lt 2) { return }
    $bytes = [IO.MemoryStream]::new()
    $members = [Collections.Generic.List[object]]::new()
    try {
        foreach ($file in $group) {
            $path = [IO.Path]::GetFullPath([IO.Path]::Combine($root, 'Content/Celeste', $file.ResourcePath))
            $prefix = [IO.Path]::GetFullPath([IO.Path]::Combine($root, 'Content/Celeste')) + [IO.Path]::DirectorySeparatorChar
            if (-not $path.StartsWith($prefix, [StringComparison]::Ordinal)) { throw 'Bundle member escapes content root.' }
            $content = [IO.File]::ReadAllBytes($path)
            $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($content))
            if ($content.LongLength -ne $file.Size -or $hash -ine $file.Sha256) { throw 'Bundle member identity changed.' }
            $members.Add([ordered]@{ Path = $file.Path; Offset = $bytes.Position })
            $bytes.Write($content)
        }
        $payload = $bytes.ToArray()
    }
    finally { $bytes.Dispose() }
    $identity = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($payload)).ToLowerInvariant()
    $resource = "bundles/$identity.data"
    $destination = [IO.Path]::Combine($root, 'Content/Celeste', $resource)
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
    if (-not [IO.File]::Exists($destination) -or
        [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($destination))) -ine $identity) {
        $pending = $destination + '.publish.tmp'
        try {
            [IO.File]::WriteAllBytes($pending, $payload)
            [IO.File]::Move($pending, $destination, $true)
        }
        finally { if ([IO.File]::Exists($pending)) { [IO.File]::Delete($pending) } }
    }
    $bundles.Add([ordered]@{ ResourcePath = $resource; Size = $payload.LongLength; Sha256 = $identity; Files = $members.ToArray() })
}

# Bundles retain the individual files' identities and persistent cache entries.
# Banks and large atlases keep their existing transport and Range behavior.
$candidates = [Collections.Generic.List[object]]::new()
foreach ($file in $Manifest.Files) {
    if ($file.Size -gt 0 -and $file.Size -lt 65536 -and -not $file.Path.EndsWith('.bank', [StringComparison]::OrdinalIgnoreCase)) {
        $candidates.Add($file)
    }
}
$candidates.Sort([Comparison[object]] { param($left, $right) [StringComparer]::Ordinal.Compare($left.Path, $right.Path) })
foreach ($file in $candidates) {
    if ($group.Count -ge 32 -or $size + $file.Size -gt 1048576) {
        Write-Bundle
        $group.Clear()
        $size = 0L
    }
    $group.Add($file)
    $size += $file.Size
}
Write-Bundle
$Manifest | Add-Member -NotePropertyName SmallResourceBundles -NotePropertyValue $bundles.ToArray() -Force
return $bundles.ToArray()
