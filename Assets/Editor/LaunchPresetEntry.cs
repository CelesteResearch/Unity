using Celeste.Game.Launch;
using Celeste.Game.Runtime.Launch;
using UnityEditor;
using UnityEngine;

namespace Celeste.Unity.Editor
{
// SessionState transports editor launch selection only. The shared request
// waits for source content loading and owns all scene and session operations.
    internal static class LaunchPresetEntry
    {
        private static SourceApplicationLaunchRequest request;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
                Release();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SupplyLaunchSelection()
        {
            Release();
            string selection = SessionState.GetString("Celeste.NextLaunch", "");
            SessionState.EraseString("Celeste.NextLaunch");
            if (string.IsNullOrEmpty(selection)) return;
            var host = Object.FindAnyObjectByType<GameHost>();
            if (!host) throw new System.InvalidOperationException("The source launch scene has no actual game host.");
            request = new SourceApplicationLaunchRequest(host.ActualProcess,
                JsonUtility.FromJson<GameLaunchOptions>(selection));
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            try
            {
                if (request.Failure != null)
                {
                    Debug.LogException(request.Failure);
                    Release();
                }
                else if (request.Completed) Release();
            }
            catch (System.Exception error)
            {
                Debug.LogException(error);
                Release();
            }
        }

        private static void Release()
        {
            EditorApplication.update -= Update;
            request?.Dispose();
            request = null;
        }
    }
}
