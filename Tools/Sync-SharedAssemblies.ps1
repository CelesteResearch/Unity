# Publish already-built portable assemblies; this command does not build the shared solution.
param(
    [string]$UnityRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$SharedRoot,
    [ValidateSet('win-x64', 'linux-x64', 'osx-x64')][string]$RuntimeIdentifier = [Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$DependencyIsolationTool,
    [string]$DependencyIdentityMap,
    [string]$CecilKey,
    [string]$MonoModKey,
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$unityPath = (Resolve-Path -LiteralPath $UnityRoot).Path
if (-not (Test-Path -LiteralPath (Join-Path $unityPath 'ProjectSettings/ProjectVersion.txt') -PathType Leaf)) {
    throw "The destination is not a Unity project: $unityPath"
}
if (-not $SharedRoot) { $SharedRoot = Join-Path (Split-Path -Parent $unityPath) 'Shared' }
$sharedPath = (Resolve-Path -LiteralPath $SharedRoot).Path
$isolationProject = Join-Path $PSScriptRoot 'DependencyIsolation'
if (-not $DependencyIsolationTool) { $DependencyIsolationTool = Join-Path $isolationProject 'bin/Release/net8.0/Celeste.Tools.DependencyIsolation.dll' }
if (-not $DependencyIdentityMap) { $DependencyIdentityMap = Join-Path $isolationProject 'data/identity-map.json' }
if (-not $CecilKey) { $CecilKey = Join-Path $isolationProject 'data/cecil.snk' }
if (-not $MonoModKey) { $MonoModKey = Join-Path $sharedPath 'vendor/MonoMod/MonoMod.snk' }
$isolation = Join-Path $PSScriptRoot 'Get-IsolatedPortableAssemblyClosure.ps1'
if (-not (Test-Path -LiteralPath $isolation -PathType Leaf)) { throw "The dependency isolation publication tool is missing: $isolation" }
$packagePath = Join-Path $unityPath 'Packages/local.celeste.shared'
$pluginsPath = Join-Path $packagePath 'Runtime/Plugins'
$packageMetadata = Join-Path $packagePath 'package.json'
$collector = Join-Path $PSScriptRoot 'Get-PortableBuiltAssemblyClosure.ps1'
if (-not (Test-Path -LiteralPath $collector -PathType Leaf)) { throw "The portable closure tool is missing: $collector" }
if (-not (Test-Path -LiteralPath $packageMetadata -PathType Leaf)) { throw "The unified DLL package is missing: $packageMetadata" }
$package = Get-Content -LiteralPath $packageMetadata -Raw | ConvertFrom-Json
if ($package.name -cne 'local.celeste.shared') { throw "Unexpected DLL package identity: $packageMetadata" }
$manifest = Get-Content -LiteralPath (Join-Path $unityPath 'Packages/manifest.json') -Raw | ConvertFrom-Json
if ($manifest.dependencies.'local.celeste.shared' -cne 'file:local.celeste.shared') { throw 'The project manifest must use the unified local.celeste.shared DLL package.' }

function Assert-UnityClosed {
    $lockPath = Join-Path $unityPath 'Temp/UnityLockfile'
    if (-not (Test-Path -LiteralPath $lockPath -PathType Leaf)) { return }
    # A leftover unlocked file is harmless. Probe this project only, read-only;
    # never enumerate, close or terminate another Unity project/process.
    try {
        $lock = [IO.File]::Open($lockPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
        $lock.Dispose()
    }
    catch [IO.IOException] {
        throw "The destination Unity project is locked; close its Editor before replacing DLLs: $unityPath"
    }
    catch [UnauthorizedAccessException] {
        throw "Cannot verify the destination Unity project lock: $lockPath"
    }
}
if (-not $PrepareOnly) { Assert-UnityClosed }
# Embedded packages import even without a manifest dependency. Retirement is an
# explicit integration operation; synchronization never deletes or moves them.
$retirementPath = Join-Path $PSScriptRoot 'SharedPackageRetirement.json'
if (-not (Test-Path -LiteralPath $retirementPath -PathType Leaf)) { throw "The explicit package retirement declaration is missing: $retirementPath" }
$retirement = Get-Content -LiteralPath $retirementPath -Raw | ConvertFrom-Json
if ($retirement.Replacement -cne 'local.celeste.shared') { throw 'Unexpected package retirement replacement.' }
foreach ($name in $retirement.Packages) {
    if ($name -notmatch '^local[.]celeste[.][a-z-]+$' -or $name -eq 'local.celeste.shared') { throw "Invalid obsolete package name: $name" }
    if (Test-Path -LiteralPath (Join-Path $unityPath "Packages/$name")) {
        throw "Retire the obsolete embedded package before synchronizing: Packages/$name"
    }
    if ($null -ne $manifest.dependencies.PSObject.Properties[$name]) { throw "Retire the obsolete package dependency: $name" }
}
$sourceFile = Get-ChildItem -LiteralPath $packagePath -Recurse -File |
    Where-Object Extension -in @('.cs', '.asmdef') | Select-Object -First 1
if ($sourceFile) { throw "The DLL package must not import a second source assembly tree: $($sourceFile.FullName)" }
$closure = & $collector -SharedRoot $sharedPath -OutputDirectory (Join-Path $unityPath 'Logs/SharedAssemblies') -Configuration $Configuration
$closure = & $isolation -Closure $closure -StagingDirectory (Join-Path $unityPath 'Logs/SharedAssemblies/DependencyIsolation') -Tool $DependencyIsolationTool -IdentityMap $DependencyIdentityMap -CecilKey $CecilKey -MonoModKey $MonoModKey
$expected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($assembly in $closure.Assemblies) { [void]$expected.Add($assembly.FileName) }
if (Test-Path -LiteralPath $pluginsPath) {
    foreach ($file in Get-ChildItem -LiteralPath $pluginsPath -Recurse -File) {
        if ($file.Extension -eq '.dll' -and ($file.DirectoryName -cne $pluginsPath -or -not $expected.Contains($file.Name))) {
            throw "Unexpected prior shared output requires explicit retirement: $($file.FullName)"
        }
    }
}
# Reject a second Unity-imported managed DLL with the same simple identity. Even
# identical bytes in two locations are not a valid single assembly publication.
foreach ($root in @((Join-Path $unityPath 'Assets'), (Join-Path $unityPath 'Packages'))) {
    foreach ($dll in Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.dll') {
        if ($dll.FullName.StartsWith($packagePath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { continue }
        try { $identity = [Reflection.AssemblyName]::GetAssemblyName($dll.FullName) }
        catch [System.BadImageFormatException] { continue } # Native plugins are not managed assembly identities.
        if ($expected.Contains($identity.Name + '.dll')) { throw "A duplicate shared assembly is still imported by Unity: $($dll.FullName)" }
    }
}

# Auto Reference controls compiler references only. Genuine Backports and
# JetBrains SDK DLLs remain imported on all runtime platforms; their types must
# not shadow the reference framework or Unity CoreModule during script compilation.
function Set-RuntimeSdkCompileIsolation([string]$dllPath) {
    $metaPath = $dllPath + '.meta'
    if (Test-Path -LiteralPath $metaPath -PathType Leaf) {
        $meta = Get-Content -LiteralPath $metaPath -Raw
        if ($meta -notmatch '(?m)^guid: ([0-9a-f]{32})\s*$') { throw "Invalid plugin asset identity: $metaPath" }
        $guid = $Matches[1]
        if ($meta -match '(?m)^PluginImporter:') {
            if ($meta -match '(?m)^  serializedVersion: 3\s*$' -and $meta -match '(?m)^  platformData:\s*\r?\n  - first:') {
                $oldRuntimePlatform = '(?m)^  platformData:\r?\n  - first:\r?\n      Any:[ \t]*\r?\n    second:\r?\n      enabled: 1\r?\n      settings: \{\}[ \t]*\r?$'
                if ($meta -notmatch $oldRuntimePlatform) { throw "Unsupported runtime SDK platform metadata: $metaPath" }
                $meta = [regex]::Replace($meta, $oldRuntimePlatform, "  platformData:`r`n    Any:`r`n      enabled: 1`r`n      settings: {}")
            }
            if ($meta -match '(?m)^  isExplicitlyReferenced: [01][ \t]*\r?$') {
                $meta = [regex]::Replace($meta, '(?m)^  isExplicitlyReferenced: [01][ \t]*\r?$', '  isExplicitlyReferenced: 1')
            }
            else {
                $meta = $meta.Replace('PluginImporter:', "PluginImporter:`r`n  isExplicitlyReferenced: 1")
            }
            [IO.File]::WriteAllText($metaPath, $meta, [Text.UTF8Encoding]::new($false))
            return
        }
        if ($meta.Trim() -notmatch '^fileFormatVersion: 2\s+guid: [0-9a-f]{32}$') { throw "Unsupported existing plugin importer metadata: $metaPath" }
    }
    else { $guid = [Guid]::NewGuid().ToString('N') }
    $meta = @"
fileFormatVersion: 2
guid: $guid
PluginImporter:
  serializedVersion: 3
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 1
  validateReferences: 1
  platformData:
    Any:
      enabled: 1
      settings: {}
  userData:
  assetBundleName:
  assetBundleVariant:
"@
    [IO.File]::WriteAllText($metaPath, $meta + "`r`n", [Text.UTF8Encoding]::new($false))
}

function Write-NativePlatformPluginMeta([string]$dllPath) {
    $metaPath = $dllPath + '.meta'
    if (Test-Path -LiteralPath $metaPath -PathType Leaf) {
        $old = [IO.File]::ReadAllText($metaPath)
        if ($old -notmatch '(?m)^guid: ([0-9a-f]{32})\s*$') { throw "Invalid native plugin asset identity: $metaPath" }
        $guid = $Matches[1]
    }
    else { $guid = [Guid]::NewGuid().ToString('N') }
    $editorOs = switch -Wildcard ($RuntimeIdentifier) { 'win-*' { 'Windows' } 'linux-*' { 'Linux' } 'osx-*' { 'OSX' } }
    $cpu = if ($RuntimeIdentifier.EndsWith('arm64')) { 'ARM64' } else { 'x86_64' }
    $platformKey = switch -Wildcard ($RuntimeIdentifier) { 'win-*' { 'Win64' } 'linux-*' { 'Linux64' } 'osx-*' { 'OSXUniversal' } }
    $meta = @"
fileFormatVersion: 2
guid: $guid
PluginImporter:
  serializedVersion: 3
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
    Any:
      enabled: 0
      settings: {}
    Editor:
      enabled: 1
      settings:
        CPU: $cpu
        OS: $editorOs
    ${platformKey}:
      enabled: 1
      settings:
        CPU: $cpu
  userData:
  assetBundleName:
  assetBundleVariant:
"@
    [IO.File]::WriteAllText($metaPath, $meta + "`r`n", [Text.UTF8Encoding]::new($false))
}

# The real KeraLua SDK imports lua54 by name. Its project output is a native
# None/CopyToOutputDirectory asset and is not part of the managed assembly list.
# Bind this publication to the resolved managed closure and the same built
# SourceApplication output before changing any imported file.
$keraLua = @($closure.Assemblies | Where-Object FileName -CEQ 'KeraLua.dll')
$sourceApplication = @($closure.Assemblies | Where-Object FileName -CEQ 'Celeste.Game.Runtime.dll')
if ($keraLua.Count -ne 1 -or $sourceApplication.Count -ne 1) { throw 'The actual KeraLua and SourceApplication closure entries are required.' }
$expectedKeraLuaSHA = '83e4f1ec6577118aae08a1d80941eee75b17c996ebcd2daa1afcc8fec89f449e'
$expectedLuaNativeSHA = '8372e1ada87ca67cce8dcc3a93ee755a419757bebcf8e3ea0eeed8bc51dc11b4'
$keraLuaPath = Join-Path $sharedPath 'lib/Lua/KeraLua.dll'
foreach ($path in @($keraLua[0].Path, $keraLuaPath)) {
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $expectedKeraLuaSHA) { throw "The reviewed KeraLua SDK bytes differ: $path" }
}
if ($keraLua[0].SHA256 -cne $expectedKeraLuaSHA) { throw 'The resolved KeraLua closure identity differs.' }
$nativeName = switch -Wildcard ($RuntimeIdentifier) { 'win-*' { 'lua54.dll' } 'linux-*' { 'liblua54.so' } 'osx-*' { 'liblua54.dylib' } }
$nativeFolder = if ($RuntimeIdentifier -eq 'win-x64') { 'x86_64' } else { $RuntimeIdentifier }
$luaNativeLibrary = Join-Path $sharedPath "lib/Native/$RuntimeIdentifier/$nativeName"
if (-not (Test-Path -LiteralPath $luaNativeLibrary -PathType Leaf)) { $luaNativeLibrary = Join-Path (Split-Path -Parent $sharedPath) "artifacts/runtime/$RuntimeIdentifier/$nativeName" }
$luaNativeSource = if ($RuntimeIdentifier -eq 'win-x64') { Join-Path ([IO.Path]::GetDirectoryName($sourceApplication[0].BuiltPath)) 'Native/x86_64/lua54.dll' } else { $luaNativeLibrary }
if ($RuntimeIdentifier -ne 'win-x64') { $expectedLuaNativeSHA = (Get-FileHash -LiteralPath $luaNativeLibrary -Algorithm SHA256).Hash.ToLowerInvariant() }
foreach ($path in @($luaNativeSource, $luaNativeLibrary)) {
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $expectedLuaNativeSHA) { throw "The built native Lua asset differs from its SDK source: $path" }
}
$luaNativeDestination = Join-Path $unityPath "Assets/Runtime/Plugins/$nativeFolder/$nativeName"
foreach ($root in @((Join-Path $unityPath 'Assets'), (Join-Path $unityPath 'Packages'))) {
    foreach ($dll in Get-ChildItem -LiteralPath $root -Recurse -File -Filter $nativeName) {
        if (-not $dll.FullName.Equals($luaNativeDestination, [StringComparison]::OrdinalIgnoreCase) -and
            $dll.Directory.Name -eq $nativeFolder) { throw "A second native Lua library targets $RuntimeIdentifier`: $($dll.FullName)" }
    }
}
$luaNativeMetaPath = $luaNativeDestination + '.meta'
if ($RuntimeIdentifier -eq 'win-x64') {
    $luaNativeStream = [IO.File]::OpenRead($luaNativeSource)
    $luaNativePE = [Reflection.PortableExecutable.PEReader]::new($luaNativeStream)
    try {
        if ($luaNativePE.HasMetadata -or [int]$luaNativePE.PEHeaders.CoffHeader.Machine -ne 0x8664) { throw 'The native Lua SDK must be an actual AMD64 native PE library.' }
        $sections = $luaNativePE.PEHeaders.SectionHeaders
        function Get-LuaNativeFileOffset([uint32]$rva) {
            foreach ($section in $sections) {
                if ($rva -ge $section.VirtualAddress -and $rva -lt ($section.VirtualAddress + [Math]::Max($section.VirtualSize, $section.SizeOfRawData))) {
                    return [long]($section.PointerToRawData + $rva - $section.VirtualAddress)
                }
            }
            throw 'The native Lua export RVA is outside its PE sections.'
        }
        $reader = [IO.BinaryReader]::new($luaNativeStream, [Text.Encoding]::ASCII, $true)
        try {
            $directory = $luaNativePE.PEHeaders.PEHeader.ExportTableDirectory
            if ($directory.RelativeVirtualAddress -eq 0) { throw 'The native Lua SDK has no actual export table.' }
            $luaNativeStream.Position = (Get-LuaNativeFileOffset $directory.RelativeVirtualAddress) + 24
            $count = $reader.ReadUInt32()
            [void]$reader.ReadUInt32() # AddressOfFunctions
            $namesRva = $reader.ReadUInt32()
            if ($count -eq 0 -or $count -gt 65536) { throw 'The native Lua SDK export count is invalid.' }
            $namesOffset = Get-LuaNativeFileOffset $namesRva
            $exports = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
            for ($index = 0; $index -lt $count; $index++) {
                $luaNativeStream.Position = $namesOffset + $index * 4
                $nameRva = $reader.ReadUInt32()
                $luaNativeStream.Position = Get-LuaNativeFileOffset $nameRva
                $name = [Text.StringBuilder]::new()
                for ($length = 0; $length -lt 1024; $length++) {
                    $byte = $reader.ReadByte()
                    if ($byte -eq 0) { break }
                    [void]$name.Append([char]$byte)
                }
                if ($length -eq 1024) { throw 'The native Lua export name is invalid.' }
                [void]$exports.Add($name.ToString())
            }
            foreach ($entryPoint in @('luaL_newstate', 'luaL_openlibs', 'lua_close')) {
                if (-not $exports.Contains($entryPoint)) { throw "The native Lua SDK is missing its required actual export: $entryPoint" }
            }
        }
        finally { $reader.Dispose() }
    }
    finally { $luaNativePE.Dispose(); $luaNativeStream.Dispose() }
    # The reviewed PE imports only KERNEL32.dll; no third-party runtime DLL is needed.
    if (-not (Test-Path -LiteralPath (Join-Path ([Environment]::GetFolderPath('System')) 'KERNEL32.dll') -PathType Leaf)) { throw 'The native Lua Windows system dependency is unavailable.' }

}
else {
    $image = [IO.File]::ReadAllBytes($luaNativeSource)
    if ($image.Length -lt 32) { throw 'The native Lua library is truncated.' }
    if ($RuntimeIdentifier -eq 'linux-x64') {
        if ($image[0] -ne 0x7f -or $image[1] -ne 0x45 -or $image[2] -ne 0x4c -or $image[3] -ne 0x46 -or $image[4] -ne 2 -or [BitConverter]::ToUInt16($image, 18) -ne 62) { throw 'The native Lua library must be an ELF x86-64 image.' }
    }
    else {
        $expectedCpu = if ($RuntimeIdentifier -eq 'osx-arm64') { 0x0100000c } else { 0x01000007 }
        if ([BitConverter]::ToUInt32($image, 0) -ne 0xfeedfacf -or [BitConverter]::ToUInt32($image, 4) -ne $expectedCpu) { throw 'The native Lua library must be a matching 64-bit Mach-O image.' }
    }
}

# Source-built RFC1950 decoder has its own identity, separate from Unity platform SDKs.
$httpDecoder = @($closure.Assemblies | Where-Object FileName -CEQ 'Celeste.Compression.Archives.dll')
$httpDecoderProject = Join-Path $sharedPath 'src/Celeste.Compression.Archives/Celeste.Compression.Archives.csproj'
$httpDecoderLicense = Join-Path $sharedPath 'src/Celeste.Compression.Archives/ThirdParty/SharpZipLib.LICENSE.txt'
if ($httpDecoder.Count -ne 1 -or -not (Test-Path -LiteralPath $httpDecoderProject -PathType Leaf)) { throw 'The source-built HTTP decoder project must occur exactly once in the portable built closure.' }
if (@($closure.Assemblies | Where-Object FileName -CEQ 'ICSharpCode.SharpZipLib.dll').Count -ne 0) { throw 'The conflicting SharpZipLib binary must not be a portable runtime dependency.' }
if ([Reflection.AssemblyName]::GetAssemblyName($httpDecoder[0].Path).Name -cne 'Celeste.Compression.Archives') { throw 'The HTTP decoder has an incorrect physical assembly identity.' }
$expectedHttpDecoderLicenseSHA = 'd7fbbc16f871bf24fa5624c7eaf945054b778985f32676dcacba41fddca61133'
if (-not (Test-Path -LiteralPath $httpDecoderLicense -PathType Leaf) -or
    (Get-FileHash -LiteralPath $httpDecoderLicense -Algorithm SHA256).Hash.ToLowerInvariant() -cne $expectedHttpDecoderLicenseSHA) { throw 'The physical HTTP decoder license is missing or differs.' }

function Get-PortableSymbolPublication([string]$assemblyPath, [string]$destinationPath) {
    $pdbPath = [IO.Path]::ChangeExtension($assemblyPath, '.pdb')
    if (-not (Test-Path -LiteralPath $pdbPath -PathType Leaf)) { throw "The source-built portable PDB is missing: $pdbPath" }
    $assemblyStream = [IO.File]::OpenRead($assemblyPath)
    $pe = [Reflection.PortableExecutable.PEReader]::new($assemblyStream)
    try {
        $codeViews = @($pe.ReadDebugDirectory() | Where-Object Type -EQ ([Reflection.PortableExecutable.DebugDirectoryEntryType]::CodeView))
        if ($codeViews.Count -ne 1 -or -not $codeViews[0].IsPortableCodeView) { throw "The source assembly must identify one portable PDB: $assemblyPath" }
        $codeView = $pe.ReadCodeViewDebugDirectoryData($codeViews[0])
        if ($codeView.Age -ne 1) { throw "The portable PDB CodeView age is invalid: $assemblyPath" }
        $pdbStream = [IO.File]::OpenRead($pdbPath)
        $provider = $null
        try {
            $provider = [Reflection.Metadata.MetadataReaderProvider]::FromPortablePdbStream($pdbStream, [Reflection.Metadata.MetadataStreamOptions]::LeaveOpen, 0)
            $reader = $provider.GetMetadataReader()
            [byte[]]$id = @($reader.DebugMetadataHeader.Id)
            if ($id.Length -ne 20) { throw "The portable PDB content identity is invalid: $pdbPath" }
            [byte[]]$guidBytes = $id[0..15]
            $guid = [Guid]::new($guidBytes)
            $stamp = [BitConverter]::ToUInt32($id, 16)
            if ($guid -ne $codeView.Guid -or $stamp -ne $codeViews[0].Stamp) { throw "The portable PDB does not match the actual DLL build: $pdbPath" }
            return [pscustomobject]@{
                Source = $pdbPath; Destination = [IO.Path]::ChangeExtension($destinationPath, '.pdb')
                SHA256 = (Get-FileHash -LiteralPath $pdbPath -Algorithm SHA256).Hash.ToLowerInvariant()
                CodeViewGuid = $guid.ToString(); CodeViewStamp = $stamp
            }
        }
        finally { if ($provider) { $provider.Dispose() }; $pdbStream.Dispose() }
    }
    finally { $pe.Dispose(); $assemblyStream.Dispose() }
}
$copies = foreach ($assembly in $closure.Assemblies) {
    $sourceSHA = (Get-FileHash -LiteralPath $assembly.Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($sourceSHA -cne $assembly.SHA256) { throw "Built output changed while preparing publication: $($assembly.Path)" }
    [pscustomobject]@{ Source = $assembly.Path; Destination = Join-Path $pluginsPath $assembly.FileName; SHA256 = $sourceSHA; PublishSymbols = $assembly.PublishSymbols }
}
# Validate actual isolated DLL/PDB pairs before replacing any imported file.
# Source-built and rewritten SDK symbols retain their real PE identity.
$symbols = @(foreach ($copy in $copies) {
        if ($copy.PublishSymbols) {
            Get-PortableSymbolPublication $copy.Source $copy.Destination
        }
    })
if (-not $PrepareOnly) { Assert-UnityClosed }
# Both standalone publication and the Editor consume this same prepared file plan.
$metadataPath = Join-Path $unityPath 'Logs/SharedAssemblies/PublicationMetadata'
[IO.Directory]::CreateDirectory($metadataPath) | Out-Null
function New-PublicationFile([string]$source, [string]$destination, [string]$hash) {
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -cne $hash) { throw "Prepared publication source changed: $source" }
    [pscustomobject]@{
        Source = $source; Destination = $destination; SHA256 = $hash
        PreviousSHA256 = if (Test-Path -LiteralPath $destination -PathType Leaf) { (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant() } else { $null }
    }
}
$files = [Collections.Generic.List[object]]::new()
foreach ($copy in $copies) {
    $files.Add((New-PublicationFile $copy.Source $copy.Destination $copy.SHA256))
    if ([IO.Path]::GetFileName($copy.Destination) -cin @('MonoMod.Backports.dll', 'JetBrains.Annotations.dll')) {
        $preparedDll = Join-Path $metadataPath ([IO.Path]::GetFileName($copy.Destination))
        $preparedMeta = $preparedDll + '.meta'
        if (Test-Path -LiteralPath $preparedMeta) { Remove-Item -LiteralPath $preparedMeta }
        if (Test-Path -LiteralPath ($copy.Destination + '.meta')) { Copy-Item -LiteralPath ($copy.Destination + '.meta') -Destination $preparedMeta }
        Set-RuntimeSdkCompileIsolation $preparedDll
        $files.Add((New-PublicationFile $preparedMeta ($copy.Destination + '.meta') (Get-FileHash -LiteralPath $preparedMeta -Algorithm SHA256).Hash.ToLowerInvariant()))
    }
}
foreach ($symbol in $symbols) { $files.Add((New-PublicationFile $symbol.Source $symbol.Destination $symbol.SHA256)) }
$closure | Add-Member -NotePropertyName PortableSymbols -NotePropertyValue $symbols
$httpLicenseDestination = Join-Path $packagePath 'ThirdPartyLicenses/SharpZipLib.LICENSE.txt'
$files.Add((New-PublicationFile $httpDecoderLicense $httpLicenseDestination $expectedHttpDecoderLicenseSHA))
$files.Add((New-PublicationFile $luaNativeSource $luaNativeDestination $expectedLuaNativeSHA))
$preparedLua = Join-Path $metadataPath 'lua54.dll'
$preparedLuaMeta = $preparedLua + '.meta'
if (Test-Path -LiteralPath $preparedLuaMeta) { Remove-Item -LiteralPath $preparedLuaMeta }
if (Test-Path -LiteralPath $luaNativeMetaPath) { Copy-Item -LiteralPath $luaNativeMetaPath -Destination $preparedLuaMeta }
Write-NativePlatformPluginMeta $preparedLua
$luaNativeMetaSHA = (Get-FileHash -LiteralPath $preparedLuaMeta -Algorithm SHA256).Hash.ToLowerInvariant()
$files.Add((New-PublicationFile $preparedLuaMeta $luaNativeMetaPath $luaNativeMetaSHA))
$closure | Add-Member -NotePropertyName NativeLibraries -NotePropertyValue @([pscustomobject]@{
        FileName = 'lua54.dll'; NativeImport = 'lua54'; ManagedAssembly = 'KeraLua.dll'
        Path = $luaNativeSource; Destination = $luaNativeDestination; SHA256 = $expectedLuaNativeSHA
        ImporterSHA256 = $luaNativeMetaSHA; Platform = $RuntimeIdentifier
    })
$preparedClosure = Join-Path $metadataPath 'PortableBuiltAssemblyClosure.json'
$closure | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $preparedClosure -Encoding utf8
$files.Add((New-PublicationFile $preparedClosure (Join-Path $packagePath 'PortableBuiltAssemblyClosure.json') (Get-FileHash -LiteralPath $preparedClosure -Algorithm SHA256).Hash.ToLowerInvariant()))
$plan = [pscustomobject]@{
    Schema = 1; UnityRoot = $unityPath; PluginsPath = $pluginsPath; Files = $files.ToArray()
    ManagedAssemblies = @($closure.Assemblies | ForEach-Object { [pscustomobject]@{ Destination = Join-Path $pluginsPath $_.FileName; Identity = $_.Identity } })
}
$planPath = Join-Path $unityPath 'Logs/SharedAssemblies/PortableAssemblyPublicationPlan.json'
$plan | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $planPath -Encoding utf8
if ($PrepareOnly) { Write-Output "Prepared shared assembly publication: $planPath; imported files unchanged."; return }
Assert-UnityClosed
# Validate the whole plan before the first imported file is changed.
foreach ($file in $plan.Files) {
    if ((Get-FileHash -LiteralPath $file.Source -Algorithm SHA256).Hash.ToLowerInvariant() -cne $file.SHA256) { throw "Publication source changed: $($file.Source)" }
    $previous = if (Test-Path -LiteralPath $file.Destination -PathType Leaf) { (Get-FileHash -LiteralPath $file.Destination -Algorithm SHA256).Hash.ToLowerInvariant() } else { $null }
    if ($previous -cne $file.PreviousSHA256) { throw "Publication destination changed after preparation: $($file.Destination)" }
}
foreach ($file in $plan.Files) {
    if ($file.SHA256 -ceq $file.PreviousSHA256) { continue }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($file.Destination)) | Out-Null
    Copy-Item -LiteralPath $file.Source -Destination $file.Destination -Force
    if ((Get-FileHash -LiteralPath $file.Destination -Algorithm SHA256).Hash.ToLowerInvariant() -cne $file.SHA256) { throw "Published bytes differ: $($file.Destination)" }
}
Write-Output "Published $($copies.Count) isolated portable DLLs and $($symbols.Count) matching portable PDBs to $pluginsPath ($Configuration); no build or source compilation."
