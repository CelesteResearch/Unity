using UnityEngine.Assemblies;

namespace Celeste.Unity
{
    // Code modules share Unity's managed assembly context through this host adapter.
    internal static class EverestCodeAssemblies
    {
        internal static System.Reflection.Assembly LoadFromBytes(byte[] bytes)
            => CurrentAssemblies.LoadFromBytes(bytes);
    }
}
