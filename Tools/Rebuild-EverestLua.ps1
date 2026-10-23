[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $WorkDirectory,
    [Parameter(Mandatory)][string] $OutputDirectory
)
$ErrorActionPreference = 'Stop'
$work = [IO.Path]::GetFullPath($WorkDirectory)
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $work, $output | Out-Null
$commit = 'b3524288712743fb2394dcf615d14d0dac3276e2'
function Get-VerifiedArchive([string] $Uri, [string] $Path, [string] $Hash) {
    if (!(Test-Path -LiteralPath $Path)) { Invoke-WebRequest -Uri $Uri -OutFile $Path }
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Hash) {
        throw "Dependency checksum mismatch: $Path"
    }
}
Get-VerifiedArchive "https://codeload.github.com/EverestAPI/NLua/zip/$commit" "$work/NLua.zip" '720273451158553A3C53B4347846C3590BE69FA222E94C6C7EDACD63F0D49D25'
Get-VerifiedArchive 'https://api.nuget.org/v3-flatcontainer/keralua/1.4.7/keralua.1.4.7.nupkg' "$work/KeraLua.nupkg" '1EBF02B3E77098353B65DB4B692980BEC67E10DDF3C2E6B36FA12351A9BF7027'
Expand-Archive -LiteralPath "$work/NLua.zip" -DestinationPath "$work/source" -Force
Expand-Archive -LiteralPath "$work/KeraLua.nupkg" -DestinationPath "$work/KeraLuaPackage" -Force
$typeExtensionsPath = "$work/source/NLua-$commit/src/Extensions/TypeExtensions.cs"
$typeExtensions = Get-Content -LiteralPath $typeExtensionsPath -Raw
$oldOwnTypes = 'types.AddRange(type.Assembly.GetTypes().Where(t => t.IsPublic));'
$oldOtherTypes = 'types.AddRange(item.GetTypes().Where(t => t.IsPublic && t.IsClass && t.IsSealed && t.IsAbstract && !t.IsNested));'
if (!$typeExtensions.Contains($oldOwnTypes) -or !$typeExtensions.Contains($oldOtherTypes)) {
    throw 'NLua TypeExtensions source changed; recheck the Unity reflection patch.'
}
$typeExtensions = $typeExtensions.Replace($oldOwnTypes,
    'types.AddRange(GetLoadableTypes(type.Assembly).Where(t => t.IsPublic));')
$typeExtensions = $typeExtensions.Replace($oldOtherTypes,
    'types.AddRange(GetLoadableTypes(item).Where(t => t.IsPublic && t.IsClass && t.IsSealed && t.IsAbstract && !t.IsNested));')
$anchor = '        public static MethodInfo[] GetExtensionMethods(this Type type, string name, IEnumerable<Assembly> assemblies = null)'
if (!$typeExtensions.Contains($anchor)) { throw 'NLua extension lookup anchor changed.' }
$safeTypes = @'
        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException error) { return error.Types.Where(type => type != null); }
        }

'@
$typeExtensions = $typeExtensions.Replace($anchor, $safeTypes + $anchor)
Set-Content -LiteralPath $typeExtensionsPath -Value $typeExtensions -Encoding utf8
$source = [Security.SecurityElement]::Escape("$work/source/NLua-$commit/src/**/*.cs")
$kera = [Security.SecurityElement]::Escape("$work/KeraLuaPackage/lib/netstandard2.0/KeraLua.dll")
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <AssemblyName>NLua</AssemblyName>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <DefineConstants>NETSTANDARD</DefineConstants>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$source" />
    <Reference Include="KeraLua"><HintPath>$kera</HintPath></Reference>
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath "$work/NLuaPortable.csproj" -Encoding utf8
& dotnet build "$work/NLuaPortable.csproj" -c Release --nologo "-p:BaseIntermediateOutputPath=$work/obj/" "-p:OutputPath=$work/bin/"
if ($LASTEXITCODE -ne 0) { throw 'NLua build failed' }
Copy-Item -LiteralPath "$work/bin/NLua.dll" -Destination "$output/NLua.dll"
Copy-Item -LiteralPath "$work/KeraLuaPackage/lib/netstandard2.0/KeraLua.dll" -Destination "$output/KeraLua.dll"
New-Item -ItemType Directory -Force -Path "$output/x86_64" | Out-Null
Copy-Item -LiteralPath "$work/KeraLuaPackage/runtimes/win-x64/native/lua54.dll" -Destination "$output/x86_64/lua54.dll"
Get-FileHash -LiteralPath "$output/NLua.dll", "$output/KeraLua.dll", "$output/x86_64/lua54.dll" -Algorithm SHA256
