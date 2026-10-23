using UnityEditor;
using UnityEngine;

namespace Celeste.Unity.Editor
{
    [CustomEditor(typeof(GameHost)), CanEditMultipleObjects]
    public sealed class RoomPresentationEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            using (new EditorGUI.DisabledScope(targets.Length != 1 || !EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Open Game Debugger"))
                    RoomRuntimeDebugger.Open((GameHost)target);
            }

            EditorGUILayout.HelpBox(
                "Launch a chapter or room from Celeste > Launch.",
                MessageType.Info);
        }
    }
}
