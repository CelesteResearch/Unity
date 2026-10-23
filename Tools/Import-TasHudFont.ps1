param(
    [string]$GameDirectory,
    [string]$SourceZip
)
$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'ContentPaths.ps1')
$contentPaths = Get-CelesteContentPaths -ProjectRoot $projectDirectory
$targetDirectory = Join-Path $contentPaths.Game 'Dialog/Fonts'

Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Drawing

$entries = @('Dialog/Fonts/JetBrainsMono-Regular.fnt', 'Dialog/Fonts/JetBrainsMono-Regular_0.png')
New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null

if (-not $SourceZip -and $GameDirectory) {
    $mods = Join-Path $GameDirectory 'Mods'
    if (Test-Path -LiteralPath $mods) {
        $candidate = Get-ChildItem -LiteralPath $mods -Filter 'CelesteTAS*.zip' -File |
            Sort-Object Name -Descending | Select-Object -First 1
        if ($candidate) { $SourceZip = $candidate.FullName }
    }
}

if ($SourceZip) {
    if (-not (Test-Path -LiteralPath $SourceZip)) { throw "Font source not found: $SourceZip" }
    Write-Output "source: $SourceZip"
    $zip = [System.IO.Compression.ZipFile]::OpenRead($SourceZip)
    try {
        foreach ($entryName in $entries) {
            $entry = $zip.Entries | Where-Object { $_.FullName -eq $entryName }
            if (-not $entry) { throw "'$entryName' is not in '$SourceZip'; the mod layout changed." }
            $target = Join-Path $targetDirectory ([IO.Path]::GetFileName($entryName))
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
            Write-Output ("  extracted {0} -> {1} bytes" -f [IO.Path]::GetFileName($entryName), (Get-Item $target).Length)
        }
    }
    finally { $zip.Dispose() }
}
else {
    throw 'Supply -SourceZip for the installed CelesteTAS Mod archive, or -GameDirectory containing that Mod.'
}

$fnt = Join-Path $targetDirectory 'JetBrainsMono-Regular.fnt'
$png = Join-Path $targetDirectory 'JetBrainsMono-Regular_0.png'
if (-not (Test-Path -LiteralPath $fnt)) { throw 'The .fnt did not land.' }
if (-not (Test-Path -LiteralPath $png)) { throw 'The page PNG did not land.' }

[xml]$xml = Get-Content -LiteralPath $fnt -Raw
$face = $xml.font.info.GetAttribute('face')
$size = $xml.font.info.GetAttribute('size')
$base = $xml.font.common.GetAttribute('base')
$lineHeight = $xml.font.common.GetAttribute('lineHeight')
$expectedFace = 'JetBrains Mono'
$expectedSize = 32
if ($face -ne $expectedFace) { throw "The .fnt declares face='$face'; TasHudFontFace.Face is '$expectedFace'." }
if ([int]$size -ne $expectedSize) { throw "The .fnt declares size=$size; TasHudFontFace.Size is $expectedSize." }

$image = [System.Drawing.Image]::FromFile($png)
try {
    $scaleW = [int]$xml.font.common.GetAttribute('scaleW')
    $scaleH = [int]$xml.font.common.GetAttribute('scaleH')
    if ($image.Width -ne $scaleW -or $image.Height -ne $scaleH) {
        throw "The page PNG is $($image.Width)x$($image.Height) but the .fnt declares ${scaleW}x${scaleH}."
    }
}
finally { $image.Dispose() }

Write-Output ''
Write-Output "TAS HUD FONT PASS: '$face' $size (lineHeight=$lineHeight base=$base) at $targetDirectory"
