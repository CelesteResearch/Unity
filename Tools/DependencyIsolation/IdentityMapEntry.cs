namespace Celeste.Tools.DependencyIsolation;

internal sealed class IdentityMapEntry
{
    public string SourceName { get; set; } = "";
    public string TargetName { get; set; } = "";
    public string TargetVersion { get; set; } = "";
    public string SourcePublicKeyToken { get; set; } = "";
}
