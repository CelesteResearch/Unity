using System;
using Celeste.Game.Profiles;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Celeste.Unity.Editor
{
    public sealed class PlayerOriginalSceneBuildProcessor : IProcessSceneWithReport
    {
        private const string GameScenePath = "Assets/Scenes/Game.unity";
        public int callbackOrder => 0;

        public void OnProcessScene(UnityEngine.SceneManagement.Scene scene, BuildReport report)
        {
            // Play mode scene loads have no BuildReport and must preserve the user's profile.
            if (report == null) return;
            if (!AppliesToScene(scene.path)) return;
            int hosts = 0;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var host in root.GetComponentsInChildren<GameHost>(true))
            {
                using var serialized = new SerializedObject(host);
                serialized.Update();
                var profile = serialized.FindProperty("sourceProfile");
                if (profile == null || profile.propertyType != SerializedPropertyType.Enum)
                    throw new BuildFailedException("The player game host has no serialized source profile.");
                profile.intValue = (int)GameProfile.Original;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                hosts++;
            }

            if (hosts != 1)
                throw new BuildFailedException("The player game scene must contain exactly one source game host.");
            // This callback owns the build's temporary scene objects; it never saves a SceneAsset.
        }

        internal static bool AppliesToScene(string scenePath)
            => string.Equals(scenePath, GameScenePath, StringComparison.Ordinal);
    }
}
