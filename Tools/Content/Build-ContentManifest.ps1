# Describes local source content; it does not download, decode pixels, or change runtime loading.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ContentRoot,
    [Parameter(Mandatory)][string]$GroupsPath,
    [Parameter(Mandatory)][string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$contentDirectory = [IO.Path]::GetFullPath($ContentRoot)
$outputFile = [IO.Path]::GetFullPath($OutputPath)
if (-not [IO.Directory]::Exists($contentDirectory)) {
    throw "Content directory not found: $contentDirectory"
}
if ($outputFile.StartsWith($contentDirectory.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The output manifest must be outside the input content directory.'
}

function Get-LogicalPath([string]$PhysicalPath) {
    $path = [IO.Path]::GetRelativePath($contentDirectory, $PhysicalPath).Replace('\', '/')
    if ($path -clike 'Graphics/Atlases/*.atlas.bytes') {
        return $path.Substring(0, $path.Length - '.atlas.bytes'.Length) + '.meta'
    }
    return $path
}

function Resolve-AtlasPath([string]$MetadataPath, [string]$ResourcePath) {
    $parent = [IO.Path]::GetDirectoryName($MetadataPath)
    $path = [IO.Path]::GetFullPath([IO.Path]::Combine($contentDirectory, $parent, $ResourcePath.Replace('\', '/')))
    return Get-LogicalPath $path
}

$files = [Collections.Generic.List[object]]::new()
$fileIndex = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
$rules = Get-Content -LiteralPath $GroupsPath -Raw | ConvertFrom-Json
$excludedPatterns = if ($null -ne $rules.PSObject.Properties['ExcludedPatterns']) { @($rules.ExcludedPatterns) } else { @() }
$physicalFiles = [IO.Directory]::GetFiles($contentDirectory, '*', [IO.SearchOption]::AllDirectories)
foreach ($path in $physicalFiles) {
    $logical = Get-LogicalPath $path
    $excluded = $false
    foreach ($pattern in $excludedPatterns) {
        if ($logical -clike $pattern) { $excluded = $true; break }
    }
    if ($excluded) { continue }
    $resourcePath = [IO.Path]::GetRelativePath($contentDirectory, $path).Replace('\', '/')
    if ($fileIndex.ContainsKey($logical)) {
        throw "Ambiguous resource mapping for '$logical': '$($fileIndex[$logical].ResourcePath)' and '$resourcePath'."
    }
    $info = [IO.FileInfo]::new($path)
    $stream = [IO.File]::OpenRead($path)
    try {
        $sha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))
        $texture = $null
        if ([IO.Path]::GetExtension($path) -ceq '.data') {
            $stream.Position = 0
            $reader = [IO.BinaryReader]::new($stream, [Text.Encoding]::UTF8, $true)
            try {
                $width = $reader.ReadInt32()
                $height = $reader.ReadInt32()
                $alphaFlag = $reader.ReadByte()
                $texture = [ordered]@{ Width = $width; Height = $height; AlphaFlag = $alphaFlag; HasAlpha = ($alphaFlag -eq 1) }
            }
            finally {
                $reader.Dispose()
            }
        }
        $record = [ordered]@{ Path = $logical; ResourcePath = $resourcePath; Size = $info.Length; Sha256 = $sha; Texture = $texture }
        $fileIndex.Add($logical, $record)
    }
    finally {
        $stream.Dispose()
    }
}

$logicalPaths = [string[]]@($fileIndex.Keys)
[Array]::Sort($logicalPaths, [StringComparer]::Ordinal)
foreach ($logical in $logicalPaths) {
    $files.Add($fileIndex[$logical])
}

$atlases = [Collections.Generic.List[object]]::new()
$atlasIndex = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
foreach ($file in $files) {
    if ($file.Path -cnotlike 'Graphics/Atlases/*.meta') {
        continue
    }
    $reader = [IO.BinaryReader]::new([IO.File]::OpenRead([IO.Path]::Combine($contentDirectory, $file.ResourcePath)))
    try {
        $version = $reader.ReadInt32()
        $label = $reader.ReadString()
        $headerValue = $reader.ReadInt32()
        $groupCount = $reader.ReadInt16()
        $pages = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $entryCount = 0
        $format = $null
        for ($group = 0; $group -lt $groupCount; $group++) {
            $source = $reader.ReadString()
            $count = $reader.ReadInt16()
            $packedPath = Resolve-AtlasPath $file.Path ($source + '.data')
            $packed = $fileIndex.ContainsKey($packedPath)
            $currentFormat = if ($packed) { 'Packer' } else { 'PackerNoAtlas' }
            if ($null -ne $format -and $format -cne $currentFormat) {
                throw "Mixed atlas source layout: $($file.Path)"
            }
            $format = $currentFormat
            if ($packed) {
                [void]$pages.Add($packedPath)
            }
            for ($entry = 0; $entry -lt $count; $entry++) {
                $name = $reader.ReadString().Replace('\', '/')
                for ($field = 0; $field -lt 8; $field++) {
                    [void]$reader.ReadInt16()
                }
                if (-not $packed) {
                    $loosePath = Resolve-AtlasPath $file.Path ([IO.Path]::Combine($source, $name + '.data'))
                    if (-not $fileIndex.ContainsKey($loosePath)) {
                        throw "Missing atlas resource: $loosePath"
                    }
                    [void]$pages.Add($loosePath)
                }
                $entryCount++
            }
        }
        $links = 0
        if ($reader.BaseStream.Position -lt $reader.BaseStream.Length -and $reader.ReadString() -ceq 'LINKS') {
            $links = $reader.ReadInt16()
            for ($link = 0; $link -lt $links; $link++) { [void]$reader.ReadString(); [void]$reader.ReadString() }
        }
        $pagePaths = [string[]]@($pages)
        [Array]::Sort($pagePaths, [StringComparer]::Ordinal)
        $atlas = [ordered]@{ Name = [IO.Path]::GetFileNameWithoutExtension($file.Path); Metadata = $file.Path; Format = $format; Version = $version; Label = $label; HeaderValue = $headerValue; Entries = $entryCount; Links = $links; Pages = $pagePaths }
        $atlases.Add($atlas)
        $atlasIndex.Add($atlas.Name, $atlas)
    }
    finally {
        $reader.Dispose()
    }
}

$groups = [Collections.Generic.List[object]]::new()
$groupIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($rule in $rules.Groups) {
    if (-not $groupIds.Add($rule.Id)) {
        throw "Duplicate group: $($rule.Id)"
    }
    $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($pattern in $rule.Patterns) {
        foreach ($file in $files) { if ($file.Path -clike $pattern) { [void]$paths.Add($file.Path) } }
    }
    foreach ($name in $rule.Atlases) {
        if (-not $atlasIndex.ContainsKey($name)) {
            throw "Unknown atlas in group $($rule.Id): $name"
        }
        $atlas = $atlasIndex[$name]
        [void]$paths.Add($atlas.Metadata)
        foreach ($page in $atlas.Pages) { [void]$paths.Add($page) }
    }
    $selected = [string[]]@($paths)
    [Array]::Sort($selected, [StringComparer]::Ordinal)
    $size = [long]0
    foreach ($path in $selected) { $size += $fileIndex[$path].Size }
    $groups.Add([ordered]@{ Id = $rule.Id; Requires = [string[]]@($rule.Requires); Files = $selected; Size = $size })
}
foreach ($group in $groups) {
    foreach ($required in $group.Requires) { if (-not $groupIds.Contains($required)) { throw "Unknown required group: $required" } }
}

$assigned = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($group in $groups) { foreach ($path in $group.Files) { [void]$assigned.Add($path) } }
$unassigned = [Collections.Generic.List[string]]::new()
foreach ($file in $files) { if (-not $assigned.Contains($file.Path)) { $unassigned.Add($file.Path) } }
$manifest = [ordered]@{ SchemaVersion = 1; GroupPurpose = 'Content classification; runtime readiness dependencies remain under review'; Files = $files.ToArray(); Atlases = $atlases.ToArray(); Groups = $groups.ToArray(); Unassigned = $unassigned.ToArray() }
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($outputFile)) | Out-Null
[IO.File]::WriteAllText($outputFile, ($manifest | ConvertTo-Json -Depth 12) + "`n", [Text.UTF8Encoding]::new($false))
[ordered]@{ Files = $files.Count; Atlases = $atlases.Count; Groups = $groups.Count; Output = $outputFile } | ConvertTo-Json
