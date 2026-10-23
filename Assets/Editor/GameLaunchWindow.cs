using Celeste.Simulation;
using Celeste.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Celeste.Unity.Editor
{
    internal static class GameLaunchDefaults
    {
        [InitializeOnLoadMethod]
        private static void Initialize() => EditorApplication.delayCall += SetDefaultEntry;

        private static void SetDefaultEntry()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var entry = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Game.unity");
            if (entry) EditorSceneManager.playModeStartScene = entry;
        }
    }

    public sealed class GameLaunchWindow : EditorWindow
    {
        private GameLaunchPreset preset;
        private string error;

        [MenuItem("Celeste/Launch")]
        public static void Open() => GetWindow<GameLaunchWindow>("Celeste Launch");

        public static void Play(global::Celeste.Game.Launch.GameLaunchOptions options = null)
        {
            options?.Validate();
            var entry = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Game.unity");
            if (!entry) throw new System.IO.FileNotFoundException("Game scene is missing.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Stop Play mode before starting another launch.");
            if (options == null) SessionState.EraseString("Celeste.NextLaunch");
            else SessionState.SetString("Celeste.NextLaunch", JsonUtility.ToJson(options));
            EditorSceneManager.playModeStartScene = entry;
            EditorApplication.isPlaying = true;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Game startup", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Play Normal Game")) Launch(null);
                preset = (GameLaunchPreset)EditorGUILayout.ObjectField("Quick launch preset", preset,
                    typeof(GameLaunchPreset), false);
                using (new EditorGUI.DisabledScope(!preset))
                    if (GUILayout.Button("Play Preset"))
                        Launch(preset.Options);
            }

            EditorGUILayout.HelpBox(
                "Normal startup begins at Game. Create quick launch presets through Assets > Create > Celeste > Launch Preset, then choose a chapter, room, language or postcard.",
                MessageType.Info);
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
        }

        private void Launch(global::Celeste.Game.Launch.GameLaunchOptions options)
        {
            try
            {
                Play(options);
                error = null;
            }
            catch (System.Exception exception)
            {
                error = exception.Message;
            }
        }
    }
}
