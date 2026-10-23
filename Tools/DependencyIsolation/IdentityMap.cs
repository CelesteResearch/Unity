namespace Celeste.Tools.DependencyIsolation;

internal sealed class IdentityMap
{
    public int Schema { get; set; }
    public IdentityMapEntry[] Assemblies { get; set; } = [];
}
