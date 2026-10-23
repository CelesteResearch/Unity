# Prepare isolated publication bytes without changing any build output or Unity asset.
param(
    [Parameter(Mandatory)]$Closure,
    [Parameter(Mandatory)][string]$StagingDirectory,
    [Parameter(Mandatory)][string]$Tool,
    [Parameter(Mandatory)][string]$IdentityMap,
    [Parameter(Mandatory)][string]$CecilKey,
    [Parameter(Mandatory)][string]$MonoModKey
)
$ErrorActionPreference = 'Stop'
foreach ($path in @($Tool, $IdentityMap, $CecilKey, $MonoModKey)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Isolation publication input is missing: $path" }
}
$stage = [IO.Path]::GetFullPath($StagingDirectory)
if ($stage -ceq [IO.Path]::GetPathRoot($stage)) { throw 'Isolation staging cannot be a filesystem root.' }
[IO.Directory]::CreateDirectory($stage) | Out-Null
if ((Get-Item -LiteralPath $stage).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Isolation staging cannot be a reparse point.' }
function Reset-IsolationStage([string]$name) {
    $path = [IO.Path]::GetFullPath((Join-Path $stage $name))
    if ([IO.Path]::GetDirectoryName($path) -cne $stage) { throw 'Invalid isolation staging boundary.' }
    if (Test-Path -LiteralPath $path) {
        $directory = Get-Item -LiteralPath $path
        if (-not $directory.PSIsContainer -or ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Invalid staging directory: $path" }
        $files = @(Get-ChildItem -LiteralPath $path -Force)
        foreach ($file in $files) {
            if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
                (-not ($file.Extension -cin @('.dll', '.pdb')) -and $file.Name -cne 'IsolationManifest.json')) {
                throw "Unexpected isolation staging entry: $($file.FullName)"
            }
        }
        foreach ($file in $files) { Remove-Item -LiteralPath $file.FullName -Force }
    }
    else { [IO.Directory]::CreateDirectory($path) | Out-Null }
    return $path
}
$inputPath = Reset-IsolationStage 'Input'
$outputPath = Reset-IsolationStage 'Output'
$sources = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
foreach ($assembly in $Closure.Assemblies) {
    if ([IO.Path]::GetFileName($assembly.FileName) -cne $assembly.FileName -or
        [IO.Path]::GetExtension($assembly.FileName) -cne '.dll' -or -not $sources.TryAdd($assembly.FileName, $assembly)) { throw 'Invalid or duplicate built closure file.' }
    $hash = (Get-FileHash -LiteralPath $assembly.Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -cne $assembly.SHA256) { throw "Built output changed before isolation: $($assembly.Path)" }
    Copy-Item -LiteralPath $assembly.Path -Destination (Join-Path $inputPath $assembly.FileName)
    $pdb = [IO.Path]::ChangeExtension($assembly.Path, '.pdb')
    if ($assembly.FileName.StartsWith('Celeste.', [StringComparison]::Ordinal) -and -not (Test-Path -LiteralPath $pdb -PathType Leaf)) { throw "The source-built portable PDB is missing: $pdb" }
    if (Test-Path -LiteralPath $pdb -PathType Leaf) { Copy-Item -LiteralPath $pdb -Destination (Join-Path $inputPath ([IO.Path]::GetFileName($pdb))) }
}
$toolMessages = & dotnet $Tool --input-directory $inputPath --output-directory $outputPath --identity-map $IdentityMap --cecil-key $CecilKey --monomod-key $MonoModKey
if ($LASTEXITCODE -ne 0) { throw "Dependency isolation failed ($LASTEXITCODE): $($toolMessages -join [Environment]::NewLine)" }
$manifestPath = Join-Path $outputPath 'IsolationManifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.schema -ne 1 -or @($manifest.assemblies).Count -ne $sources.Count) { throw 'Isolation manifest schema or closure cardinality differs.' }
foreach ($inputFact in @(@('identityMapSha256', $IdentityMap), @('cecilKeySha256', $CecilKey), @('monoModKeySha256', $MonoModKey))) {
    if ($manifest.($inputFact[0]).ToLowerInvariant() -cne (Get-FileHash -LiteralPath $inputFact[1] -Algorithm SHA256).Hash.ToLowerInvariant()) { throw "Isolation manifest input differs: $($inputFact[1])" }
}
$seenSources = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$seenTargets = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$published = @(foreach ($entry in $manifest.assemblies) {
        if (-not $sources.ContainsKey($entry.sourceFileName) -or -not $seenSources.Add($entry.sourceFileName) -or
            [IO.Path]::GetFileName($entry.targetFileName) -cne $entry.targetFileName -or
            [IO.Path]::GetExtension($entry.targetFileName) -cne '.dll' -or -not $seenTargets.Add($entry.targetFileName)) { throw 'Isolation manifest contains an invalid or repeated source/target.' }
        $source = $sources[$entry.sourceFileName]
        if ($entry.sourceIdentity -cne $source.Identity -or $entry.sourceSha256.ToLowerInvariant() -cne $source.SHA256 -or
            (Get-FileHash -LiteralPath $source.Path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $source.SHA256) { throw "Isolation source facts differ: $($source.Path)" }
        $path = Join-Path $outputPath $entry.targetFileName
        $identity = [Reflection.AssemblyName]::GetAssemblyName($path)
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($identity.FullName -cne $entry.targetIdentity -or $identity.Name + '.dll' -cne $entry.targetFileName -or
            $hash -cne $entry.targetSha256.ToLowerInvariant()) { throw "Isolation target facts differ: $path" }
        $stream = [IO.File]::OpenRead($path)
        $pe = [Reflection.PortableExecutable.PEReader]::new($stream)
        try {
            $metadata = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
            $references = @(foreach ($handle in $metadata.AssemblyReferences) {
                    $reference = $metadata.GetAssemblyReference($handle)
                    [pscustomobject]@{ Name = $metadata.GetString($reference.Name); Version = $reference.Version.ToString() }
                })
            $actualReferences = @($references | ForEach-Object { "$($_.Name),$($_.Version)" } | Sort-Object)
            $declaredReferences = @($entry.targetReferences | ForEach-Object { "$($_.Name),$($_.Version)" } | Sort-Object)
            if (($actualReferences -join "`n") -cne ($declaredReferences -join "`n")) { throw "Isolation target references differ: $path" }
            if (@($references | Where-Object Name -CIN @('Mono.Cecil', 'Mono.Cecil.Mdb', 'Mono.Cecil.Pdb', 'Mono.Cecil.Rocks')).Count) { throw "An unisolated Cecil reference remains: $path" }
        }
        finally { $pe.Dispose(); $stream.Dispose() }
        if ($entry.pdbFileName) {
            if ($entry.pdbFileName -cne [IO.Path]::ChangeExtension($entry.targetFileName, '.pdb')) { throw 'Isolation symbol filename differs.' }
            $sourcePdb = [IO.Path]::ChangeExtension($source.Path, '.pdb')
            $targetPdb = Join-Path $outputPath $entry.pdbFileName
            if ((Get-FileHash -LiteralPath $sourcePdb -Algorithm SHA256).Hash.ToLowerInvariant() -cne $entry.sourcePdbSha256.ToLowerInvariant() -or
                (Get-FileHash -LiteralPath $targetPdb -Algorithm SHA256).Hash.ToLowerInvariant() -cne $entry.targetPdbSha256.ToLowerInvariant()) { throw "Isolation symbols differ: $targetPdb" }
        }
        [pscustomobject]@{
            FileName = $entry.targetFileName; Path = $path; SHA256 = $hash; Identity = $identity.FullName
            TargetFramework = $source.TargetFramework; References = $references; Consumer = $source.Consumer
            BuiltFileName = $source.FileName; BuiltPath = $source.Path; BuiltSHA256 = $source.SHA256; BuiltIdentity = $source.Identity
            PublishSymbols = [bool]$entry.pdbFileName -and ($entry.rewritten -or $source.FileName.StartsWith('Celeste.', [StringComparison]::Ordinal))
        }
    })
$Closure.Assemblies = $published
$Closure | Add-Member -NotePropertyName DependencyIsolation -NotePropertyValue ([pscustomobject]@{
        Manifest = $manifestPath; SHA256 = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
        IdentityMapSHA256 = $manifest.identityMapSha256; CecilKeySHA256 = $manifest.cecilKeySha256; MonoModKeySHA256 = $manifest.monoModKeySha256
    })
return $Closure
