$ErrorActionPreference = 'Stop'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('celeste-resource-bundles-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($fixture)
try {
    $source = Join-Path $fixture 'source'
    $time = Join-Path $fixture 'time'
    $output = Join-Path $fixture 'output'
    [void][IO.Directory]::CreateDirectory($source)
    [void][IO.Directory]::CreateDirectory($time)
    [IO.File]::WriteAllBytes((Join-Path $time 'mscorlib.resources'), [byte[]]@(1, 2, 3))
    $files = @()
    for ($index = 0; $index -lt 42; $index++) {
        $name = if ($index -eq 40) { 'audio.bank' } elseif ($index -eq 41) { 'large.data' } else { "small$index.data" }
        $bytes = [byte[]]::new($(if ($index -eq 41) { 70000 } else { 20000 }))
        [Array]::Fill[byte]($bytes, [byte]$index)
        [IO.File]::WriteAllBytes((Join-Path $source $name), $bytes)
        $files += [ordered]@{ Path = $name; ResourcePath = $name; Size = $bytes.Length;
            Sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
        }
    }
    $manifest = Join-Path $fixture 'manifest.json'
    [ordered]@{ SchemaVersion = 1; Files = $files } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifest
    $publisher = Join-Path $PSScriptRoot '../Publish-WebContent.ps1'
    $arguments = @{ ContentRoot = $source; FrameworkTimeRoot = $time; ManifestPath = $manifest; OutputRoot = $output }
    $null = & $publisher @arguments
    $first = Get-Content -LiteralPath (Join-Path $output 'Content/manifest.json') -Raw
    $published = $first | ConvertFrom-Json
    if ($published.SmallResourceBundles.Count -ne 2) { throw 'Expected two bounded bundles.' }
    $members = 0
    foreach ($bundle in $published.SmallResourceBundles) {
        if ($bundle.Size -gt 1048576 -or $bundle.Files.Count -gt 32) { throw 'Bundle bound exceeded.' }
        $path = Join-Path $output ('Content/Celeste/' + $bundle.ResourcePath)
        $bytes = [IO.File]::ReadAllBytes($path)
        if ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)) -ine $bundle.Sha256) {
            throw 'Bundle identity mismatch.'
        }
        $stream = [IO.Compression.GZipStream]::new([IO.File]::OpenRead($path + '.gz'), [IO.Compression.CompressionMode]::Decompress)
        $decoded = [IO.MemoryStream]::new()
        try { $stream.CopyTo($decoded); if (-not [Linq.Enumerable]::SequenceEqual[byte]($bytes, $decoded.ToArray())) { throw 'Gzip mismatch.' } }
        finally { $stream.Dispose(); $decoded.Dispose() }
        $offset = 0
        foreach ($member in $bundle.Files) {
            if ($member.Path -in @('audio.bank', 'large.data') -or $member.Offset -ne $offset) { throw 'Unexpected member.' }
            $offset += 20000
            $members++
        }
        if ($offset -ne $bundle.Size) { throw 'Member ranges do not cover bundle.' }
    }
    if ($members -ne 40) { throw 'Small-resource coverage mismatch.' }
    $secondResult = & $publisher @arguments | ConvertFrom-Json
    $second = Get-Content -LiteralPath (Join-Path $output 'Content/manifest.json') -Raw
    if ($first -cne $second -or $secondResult.Copied -ne 0 -or $secondResult.ReusedCompression -lt 2) {
        throw 'Publication is not deterministic or does not reuse immutable bundles.'
    }
    [ordered]@{ Status = 'pass'; SmallFiles = $members; Bundles = 2; BanksExcluded = $true;
        Deterministic = $true; GzipVerified = $true
    } | ConvertTo-Json -Compress
}
finally {
    $parent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $resolved = [IO.Path]::GetFullPath($fixture)
    if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or
        -not [IO.Path]::GetFileName($resolved).StartsWith('celeste-resource-bundles-', [StringComparison]::Ordinal)) {
        throw 'Fixture cleanup escaped its temporary directory.'
    }
    [IO.Directory]::Delete($resolved, $true)
}
