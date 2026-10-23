param([Parameter(Mandatory)][string]$GameDirectory)
$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'ContentPaths.ps1')
$contentPaths = Get-CelesteContentPaths -ProjectRoot $projectDirectory
$sourceDirectory = Join-Path $GameDirectory 'Content'
$targetDirectory = $contentPaths.Game


$AudioExtensions = @('.bank', '.fsb', '.ogg', '.mp3', '.wav', '.aiff', '.flac')
$AudioPathSegments = @('Audio', 'FMOD', 'fmod')

if (-not (Test-Path -LiteralPath (Join-Path $sourceDirectory 'Maps/1-ForsakenCity.bin'))) { throw 'Celeste content was not found.' }
New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
$inventory = [Collections.Generic.List[object]]::new()
foreach ($contentFolder in @('Maps', 'Graphics', 'Dialog', 'Tutorials', 'Overworld', 'Monocle', 'Effects')) {
    $source = Join-Path $sourceDirectory $contentFolder
    if (-not (Test-Path -LiteralPath $source)) { continue }
    foreach ($file in Get-ChildItem -LiteralPath $source -File -Recurse) {
        $relative = [IO.Path]::GetRelativePath($sourceDirectory, $file.FullName)
        if ($AudioExtensions -contains $file.Extension.ToLowerInvariant()) {
            throw "Import audio with Import-Fmod.ps1: ('$relative')."
        }
        $segments = $relative.Split([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
        foreach ($segment in $segments) {
            if ($AudioPathSegments -contains $segment) {
                throw "Import-Fmod.ps1 handles this audio path: ('$relative')."
            }
        }
        $nativeHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        if ($file.Extension -eq '.meta') {
            $conflictingPath = Join-Path $targetDirectory $relative
            if ((Test-Path -LiteralPath $conflictingPath) -and (Get-FileHash -LiteralPath $conflictingPath -Algorithm SHA256).Hash -eq $nativeHash) {
                Remove-Item -LiteralPath $conflictingPath
            }
            $relative = [IO.Path]::ChangeExtension($relative, '.atlas.bytes')
        }
        $destination = Join-Path $targetDirectory $relative
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
        if (-not (Test-Path -LiteralPath $destination) -or (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $nativeHash) {
            Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
        }
        if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $nativeHash) { throw "Imported content differs: $relative" }
        $inventory.Add([ordered]@{ source = [IO.Path]::GetRelativePath($sourceDirectory, $file.FullName).Replace('\', '/'); path = $relative.Replace('\', '/'); bytes = $file.Length; sha256 = $nativeHash })
    }
}
$FmodSubtree = Join-Path $targetDirectory 'FMOD'
$exported = @(Get-ChildItem -LiteralPath $targetDirectory -File -Recurse)
$isAudio = { param($f) $AudioExtensions -contains $f.Extension.ToLowerInvariant() }

$audioOutsideFmod = @($exported | Where-Object {
        (& $isAudio $_) -and (-not $_.FullName.StartsWith($FmodSubtree + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))
    })
if ($audioOutsideFmod.Count -gt 0) {
    $summary = ($audioOutsideFmod | Group-Object { $_.Extension.ToLowerInvariant() } | ForEach-Object { "{0} {1}" -f $_.Count, $_.Name }) -join ', '
    $example = $audioOutsideFmod[0].FullName.Substring($targetDirectory.Length + 1)
    throw "Audio exists outside FMOD/Desktop ($summary; first: $example)."
}

$nonAudioInFmod = @($exported | Where-Object {
        $_.FullName.StartsWith($FmodSubtree + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -and
        (-not (& $isAudio $_)) -and $_.Extension -ne '.meta'
    })
if ($nonAudioInFmod.Count -gt 0) {
    $example = $nonAudioInFmod[0].FullName.Substring($targetDirectory.Length + 1)
    throw "FMOD contains non-audio files (first: $example)."
}

$fmodBanks = @($exported | Where-Object { & $isAudio $_ })
[IO.Directory]::CreateDirectory((Join-Path $projectDirectory 'Artifacts')) | Out-Null
$inventory | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $projectDirectory 'Artifacts/content-inventory.json') -Encoding utf8
Write-Output ("Imported {0} content files; verified {1} existing audio files." -f $inventory.Count, $fmodBanks.Count)
