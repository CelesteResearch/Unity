using Celeste.Everest;
using UnityEngine;
using UnityEngine.Assemblies;

namespace Celeste.Unity
{
    // Unity owns assembly lifetime policy; the Everest DLL remains runtime-neutral.
    internal static class EverestLuaAssemblies
    {
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
            => global::Celeste.Everest.Scripting.Lua.EverestLuaTypes.LoadedAssemblies =
                CurrentAssemblies.GetLoadedAssemblies;
    }
}
