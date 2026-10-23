param(
    [Parameter(Mandatory)][string]$UnityEditorDirectory,
    [string]$UnityInputSystemAssembly,
    [switch]$Quiet
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'Tools/HostCompile/Host.csproj'
$logPath = Join-Path $root 'Logs/host-compile.log'
[IO.Directory]::CreateDirectory((Split-Path -Parent $logPath)) | Out-Null
$dotnet = if ($env:DOTNET_HOST_PATH) { $env:DOTNET_HOST_PATH } else { 'dotnet' }
$editorDirectory = (Resolve-Path -LiteralPath $UnityEditorDirectory).Path
$arguments = @('build', $project, '-v', 'q', '--nologo', "-p:UnityEditorDirectory=$editorDirectory")
if ($UnityInputSystemAssembly) {
    $inputAssembly = (Resolve-Path -LiteralPath $UnityInputSystemAssembly).Path
    $arguments += "-p:UnityInputSystemAssembly=$inputAssembly"
}
$output = & $dotnet @arguments 2>&1
$buildExitCode = $LASTEXITCODE
$output | Set-Content -LiteralPath $logPath -Encoding utf8
$errors = @($output | Select-String -Pattern ': error ' |
        ForEach-Object { $_.Line.Trim() } | Select-Object -Unique)
if ($buildExitCode -ne 0 -or $errors.Count -gt 0) {
    if (-not $Quiet) {
        Write-Output "HOST COMPILE FAILED: exit=$buildExitCode; log: $logPath"
        $output | Select-Object -Last 40 | Write-Output
    }
    exit 1
}
$assembly = Join-Path $root 'Tools/HostCompile/bin/Debug/netstandard2.1/Host.dll'
if (-not (Test-Path -LiteralPath $assembly)) { throw 'HOST COMPILE VACUOUS: Host.dll is missing.' }
$stream = [IO.File]::OpenRead($assembly)
$pe = [System.Reflection.PortableExecutable.PEReader]::new($stream)
try {
    $reader = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
    $definedTypes = foreach ($handle in $reader.TypeDefinitions) {
        $definition = $reader.GetTypeDefinition($handle)
        $reader.GetString($definition.Namespace) + '.' + $reader.GetString($definition.Name)
    }
    foreach ($type in 'Celeste.Unity.GameHost', 'Celeste.Unity.NativeUnitySourceRuntimeIO', 'Celeste.Unity.NativeSourceDirectoryLinkTargetIO') {
        if ($type -notin $definedTypes) { throw "HOST COMPILE VACUOUS: missing definition $type." }
    }
}
finally { $pe.Dispose(); $stream.Dispose() }
if (-not $Quiet) {
    $warnings = @($output | Select-String -Pattern ': warning ' |
            ForEach-Object { $_.Line.Trim() } | Select-Object -Unique)
    Write-Output "HOST COMPILE PASS: actual shared source application host, 0 errors, $($warnings.Count) warnings"
    $warnings | Select-Object -First 40 | Write-Output
    Write-Output "Nonempty host assembly verified; log: $logPath"
}
exit 0
