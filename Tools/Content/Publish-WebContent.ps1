[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ContentRoot,
    [Parameter(Mandatory)][string]$FrameworkTimeRoot,
    [Parameter(Mandatory)][string]$ManifestPath,
    [Parameter(Mandatory)][string]$OutputRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$contentDirectory = [IO.Path]::GetFullPath($ContentRoot)
$timeDirectory = [IO.Path]::GetFullPath($FrameworkTimeRoot)
$outputDirectory = [IO.Path]::GetFullPath($OutputRoot)
$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
$entries = [Collections.Generic.List[object]]::new()
$pathComparer = if ([OperatingSystem]::IsWindows()) { [StringComparer]::OrdinalIgnoreCase } else { [StringComparer]::Ordinal }
$pathComparison = if ([OperatingSystem]::IsWindows()) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
$destinations = [Collections.Generic.HashSet[string]]::new($pathComparer)

function Resolve-ContentFile([string]$Root, [string]$RelativePath) {
    $prefix = $Root.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $resolved = [IO.Path]::GetFullPath([IO.Path]::Combine($Root, $RelativePath))
    if (-not $resolved.StartsWith($prefix, $pathComparison)) {
        throw "Resource path escapes its content directory: $RelativePath"
    }
    return $resolved
}

function Get-ContentHash([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    try {
        return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))
    }
    finally {
        $stream.Dispose()
    }
}

foreach ($file in $manifest.Files) {
    $relative = [string]$file.ResourcePath
    if ($relative.StartsWith('objects/', [StringComparison]::Ordinal)) {
        $parts = $relative.Split('/', 3)
        if ($parts.Length -ne 3 -or $parts[1] -cne $file.Sha256.ToLowerInvariant()) {
            throw 'Published object path does not match its content identity.'
        }
        $relative = $parts[2]
    }
    if ($relative.Replace('\', '/').StartsWith('Mods/', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The Web manifest must not publish Mods.'
    }
    $source = Resolve-ContentFile $contentDirectory $relative
    $objectPath = 'objects/' + $file.Sha256.ToLowerInvariant() + '/' + $relative
    $destination = Resolve-ContentFile $outputDirectory ('Content/Celeste/' + $objectPath)
    $file.ResourcePath = $objectPath
    if ($source.Equals($destination, $pathComparison)) {
        throw 'Web output must be separate from source content.'
    }
    if (-not $destinations.Add($destination)) { throw "Duplicate resource destination: $relative" }
    if (-not [IO.File]::Exists($source) -or [IO.FileInfo]::new($source).Length -ne $file.Size) {
        throw "Manifest resource is missing or has changed size: $relative"
    }
    $entries.Add(@{ Source = $source; Destination = $destination; Size = [long]$file.Size; Sha256 = [string]$file.Sha256 })
}

# GameHost's Web path requests only this canonical resource, not the desktop language variant.
$timeSource = Resolve-ContentFile $timeDirectory 'mscorlib.resources'
$timeDestination = Resolve-ContentFile $outputDirectory 'Content/FrameworkTimeResources/mscorlib.resources'
$entries.Add(@{
        Source = $timeSource
        Destination = $timeDestination
        Size = [IO.FileInfo]::new($timeSource).Length
        Sha256 = Get-ContentHash $timeSource
    })

# Publish individual files first; bundles are constructed from their verified bytes below.
$compressionIndexPath = Join-Path $outputDirectory 'Content/gzip-index.json'
$previousCompression = if ([IO.File]::Exists($compressionIndexPath)) {
    Get-Content -LiteralPath $compressionIndexPath -Raw | ConvertFrom-Json -AsHashtable
}
else { @{} }
$compressionIndex = [ordered]@{}
$compressedFiles = 0
$reusedCompression = 0
$copied = 0
$unchanged = 0
$copiedBytes = 0L
foreach ($entry in $entries) {
    $destination = $entry.Destination
    if ([IO.File]::Exists($destination) -and [IO.FileInfo]::new($destination).Length -eq $entry.Size -and
        (Get-ContentHash $destination) -eq $entry.Sha256) {
        $unchanged++
        continue
    }
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
    $pendingPath = $destination + '.publish.tmp'
    try {
        [IO.File]::Copy($entry.Source, $pendingPath, $true)
        if ((Get-ContentHash $pendingPath) -ne $entry.Sha256) {
            throw "Published resource does not match the manifest: $destination"
        }
        if ([IO.Path]::GetExtension($destination) -ceq '.data') {
            [IO.File]::Delete($destination + '.gz')
        }
        [IO.File]::Move($pendingPath, $destination, $true)
    }
    finally {
        if ([IO.File]::Exists($pendingPath)) { [IO.File]::Delete($pendingPath) }
    }
    $copied++
    $copiedBytes += $entry.Size
}
foreach ($entry in $entries) {
    if ([IO.Path]::GetExtension($entry.Destination) -cne '.data') { continue }
    $relative = [IO.Path]::GetRelativePath($outputDirectory, $entry.Destination).Replace('\', '/')
    $gzipPath = $entry.Destination + '.gz'
    $previous = $previousCompression[$relative]
    if ($null -ne $previous -and $previous.SourceSha256 -eq $entry.Sha256) {
        if ($previous.GzipBytes -eq 0 -or ([IO.File]::Exists($gzipPath) -and
                [IO.FileInfo]::new($gzipPath).Length -eq $previous.GzipBytes -and
                (Get-ContentHash $gzipPath) -eq $previous.GzipSha256)) {
            if ($previous.GzipBytes -eq 0) { [IO.File]::Delete($gzipPath) }
            $compressionIndex[$relative] = $previous
            $reusedCompression++
            continue
        }
    }
    [IO.File]::Delete($gzipPath)
    $pendingGzip = $gzipPath + '.publish.tmp'
    $inputStream = [IO.File]::OpenRead($entry.Destination)
    try {
        $outputStream = [IO.File]::Create($pendingGzip)
        $gzip = [IO.Compression.GZipStream]::new($outputStream, [IO.Compression.CompressionLevel]::Optimal)
        try {
            $inputStream.CopyTo($gzip, 65536)
        }
        finally {
            $gzip.Dispose()
        }
    }
    catch {
        if ([IO.File]::Exists($pendingGzip)) { [IO.File]::Delete($pendingGzip) }
        throw
    }
    finally {
        $inputStream.Dispose()
    }
    $gzipBytes = [IO.FileInfo]::new($pendingGzip).Length
    $gzipHash = $null
    if ($gzipBytes -lt $entry.Size) {
        $gzipHash = Get-ContentHash $pendingGzip
        [IO.File]::Move($pendingGzip, $gzipPath, $true)
    }
    else {
        [IO.File]::Delete($pendingGzip)
        $gzipBytes = 0
    }
    $compressionIndex[$relative] = [ordered]@{
        SourceSha256 = $entry.Sha256
        GzipBytes = $gzipBytes
        GzipSha256 = $gzipHash
    }
    $compressedFiles++
}
$bundles = @(& (Join-Path $PSScriptRoot 'Build-SmallResourceBundles.ps1') -Manifest $manifest -OutputRoot $outputDirectory)
foreach ($bundle in $bundles) {
    $relative = 'Content/Celeste/' + $bundle.ResourcePath
    $destination = Resolve-ContentFile $outputDirectory $relative
    $gzipPath = $destination + '.gz'
    $previous = $previousCompression[$relative]
    if ($null -ne $previous -and $previous.SourceSha256 -ieq $bundle.Sha256 -and
        [IO.File]::Exists($gzipPath) -and [IO.FileInfo]::new($gzipPath).Length -eq $previous.GzipBytes -and
        (Get-ContentHash $gzipPath) -ieq $previous.GzipSha256) {
        $compressionIndex[$relative] = $previous
        $reusedCompression++
        continue
    }
    $pendingGzip = $gzipPath + '.publish.tmp'
    $inputStream = [IO.File]::OpenRead($destination)
    try {
        $outputStream = [IO.File]::Create($pendingGzip)
        $gzip = [IO.Compression.GZipStream]::new($outputStream, [IO.Compression.CompressionLevel]::Optimal)
        try { $inputStream.CopyTo($gzip) }
        finally { $gzip.Dispose() }
        [IO.File]::Move($pendingGzip, $gzipPath, $true)
    }
    finally {
        $inputStream.Dispose()
        if ([IO.File]::Exists($pendingGzip)) { [IO.File]::Delete($pendingGzip) }
    }
    $compressionIndex[$relative] = [ordered]@{
        SourceSha256 = $bundle.Sha256
        GzipBytes = [IO.FileInfo]::new($gzipPath).Length
        GzipSha256 = Get-ContentHash $gzipPath
    }
}
$pendingIndex = $compressionIndexPath + '.publish.tmp'
$compressionIndex | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $pendingIndex -Encoding utf8
[IO.File]::Move($pendingIndex, $compressionIndexPath, $true)
$publishedManifest = Resolve-ContentFile $outputDirectory 'Content/manifest.json'
$pendingManifest = $publishedManifest + '.publish.tmp'
$manifest | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $pendingManifest -Encoding utf8
[IO.File]::Move($pendingManifest, $publishedManifest, $true)
[ordered]@{
    Files = $entries.Count
    Copied = $copied
    Unchanged = $unchanged
    CopiedBytes = $copiedBytes
    Compressed = $compressedFiles
    ReusedCompression = $reusedCompression
    SmallResourceBundles = $bundles.Count
} | ConvertTo-Json -Compress


