using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Runtime;
using Celeste.Scene.Entities;
using Celeste.Scene.Scenes;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Celeste.Unity.Editor
{
    public sealed class RoomRuntimeDebugger : EditorWindow
    {
        private const int PageSize = 32;

        private static readonly string[] Scopes =
            { "Entities", "Scene", "Application", "Tracker", "Graphics", "Audio" };

        [SerializeField] private GameHost host;
        [SerializeField] private int scope;
        [SerializeField] private string search = "";
        private readonly List<object> values = new List<object>();
        private readonly List<string> labels = new List<string>();
        private readonly List<int> filtered = new List<int>();
        private readonly List<object> collection = new List<object>();
        private readonly Stack<object> history = new Stack<object>();
        private SourceSceneState scene;
        private object sceneIdentity;
        private GameApplication application;
        private object owner;
        private object selected;
        private MemberInfo[] members = Array.Empty<MemberInfo>();
        private MemberInfo selectedMember;
        private object memberValue;
        private PropertyTree detailTree;
        private string error;
        private bool truncated;
        private bool includePrivate;
        private bool memberRead;
        private int page;
        private int collectionPage;
        private int detailPage;
        private Vector2 listScroll;
        private Vector2 detailScroll;
        private double nextHostSearch;

        [MenuItem("Celeste/Game Debugger")]
        private static void OpenSelected()
            => Open(Selection.activeGameObject ? Selection.activeGameObject.GetComponentInParent<GameHost>() : null);

        public static void Open(GameHost target)
        {
            var window = GetWindow<RoomRuntimeDebugger>("Game Debugger");
            window.minSize = new Vector2(360, 420);
            if (target) window.host = target;
            window.RefreshRuntime(true);
            window.Show();
        }

        private void OnEnable()
        {
            minSize = new Vector2(360, 420);
            EditorApplication.playModeStateChanged += PlayModeChanged;
            RefreshRuntime(true);
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            ClearSelection();
            values.Clear();
            labels.Clear();
            application = null;
            scene = null;
            sceneIdentity = null;
            owner = null;
        }

        private void PlayModeChanged(PlayModeStateChange state)
        {
            application = null;
            scene = null;
            sceneIdentity = null;
            owner = null;
            ClearSelection();
            values.Clear();
            labels.Clear();
            filtered.Clear();
            if (state == PlayModeStateChange.EnteredPlayMode) RefreshRuntime(true);
            Repaint();
        }

        private void OnInspectorUpdate()
        {
            RefreshRuntime(false);
            Repaint();
        }

        private void RefreshRuntime(bool force)
        {
            if (!EditorApplication.isPlaying) return;
            if (!host && (force || EditorApplication.timeSinceStartup >= nextHostSearch))
            {
                nextHostSearch = EditorApplication.timeSinceStartup + 1;
                var selectedHost = Selection.activeGameObject
                    ? Selection.activeGameObject.GetComponentInParent<GameHost>()
                    : null;
                if (selectedHost) host = selectedHost;
                else
                {
                    var hosts = UnityEngine.Object.FindObjectsByType<GameHost>(FindObjectsInactive.Exclude);
                    if (hosts.Length != 0) host = hosts[0];
                }
            }

            if (!host)
            {
                application = null;
                return;
            }

            try
            {
                var process = host.ActualProcess;
                var current = ((ISourceEngineContext)process).Frame.Scenes.CurrentScene;
                var changed = !ReferenceEquals(application, process) || !ReferenceEquals(sceneIdentity, current);
                application = process;
                sceneIdentity = current;
                scene = current as SourceSceneState;
                if (force || changed) RefreshList();
                error = null;
            }
            catch (InvalidOperationException exception)
            {
                error = exception.Message;
                application = null;
            }
        }

        private object ScopeOwner()
        {
            switch (scope)
            {
                case 0: return scene?.SourceScene.Entities;
                case 1: return sceneIdentity;
                case 2: return application;
                case 3: return scene?.SourceScene.Catalog;
                case 4: return application?.GraphicsResources;
                default: return application?.AudioSession;
            }
        }

        private void RefreshList()
        {
            ClearSelection();
            values.Clear();
            labels.Clear();
            owner = ScopeOwner();
            if (scope == 0 && scene != null)
            {
                var entities = scene.SourceScene.Entities.Entities;
                for (var i = 0; i < entities.Count; i++)
                {
                    values.Add(entities[i]);
                    labels.Add($"{i:0000}  {entities[i].GetType().Name}");
                }
            }
            else if (owner != null)
            {
                foreach (var member in RuntimeDebugValues.Members(owner.GetType(), includePrivate))
                {
                    values.Add(member);
                    labels.Add(member.Name);
                }
            }

            page = 0;
            Filter();
        }

        private void Filter()
        {
            filtered.Clear();
            for (var i = 0; i < labels.Count; i++)
                if (search.Length == 0 || labels[i].IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    filtered.Add(i);
            page = 0;
        }

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode to inspect the running game. The game host is connected automatically.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Inspect", EditorStyles.miniLabel, GUILayout.Width(48));
            var nextScope = EditorGUILayout.Popup(scope, Scopes, EditorStyles.toolbarPopup, GUILayout.Width(112));
            if (nextScope != scope)
            {
                scope = nextScope;
                RefreshList();
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(64))) RefreshRuntime(true);
            if (GUILayout.Button("Ping Host", EditorStyles.toolbarButton, GUILayout.Width(72)) && host)
                EditorGUIUtility.PingObject(host);
            EditorGUILayout.EndHorizontal();
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
            if (application == null)
            {
                EditorGUILayout.HelpBox("Waiting for the running game host…", MessageType.Info);
                return;
            }

            var level = sceneIdentity as OriginalLevelSceneRoot;
            EditorGUILayout.LabelField(sceneIdentity?.GetType().Name ?? "No scene", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Room", level?.Session?.Level ?? "—");
            EditorGUILayout.LabelField("Runtime", EditorApplication.isPaused ? "Editor paused" :
                scene == null ? "Initializing" :
                scene.Paused ? "Paused" : "Running");
            var nextSearch = EditorGUILayout.TextField("Search", search);
            if (nextSearch != search)
            {
                search = nextSearch;
                Filter();
            }

            var nextPrivate =
                EditorGUILayout.ToggleLeft("Include non-public members (read on selection)", includePrivate);
            if (nextPrivate != includePrivate)
            {
                includePrivate = nextPrivate;
                RefreshList();
            }

            var wide = position.width >= 680;
            if (wide) EditorGUILayout.BeginHorizontal();
            else EditorGUILayout.BeginVertical();
            DrawList(wide);
            DrawDetails();
            if (wide) EditorGUILayout.EndHorizontal();
            else EditorGUILayout.EndVertical();
        }

        private void DrawList(bool wide)
        {
            if (wide) EditorGUILayout.BeginVertical(GUILayout.Width(Mathf.Clamp(position.width * .35f, 240, 360)));
            else EditorGUILayout.BeginVertical(GUILayout.Height(Mathf.Clamp(position.height * .32f, 130, 240)));
            Pager(ref page, filtered.Count, scope == 0 ? "entities" : "members");
            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            var end = Math.Min(filtered.Count, (page + 1) * PageSize);
            for (var i = page * PageSize; i < end; i++)
            {
                var index = filtered[i];
                if (GUILayout.Toggle(
                        ReferenceEquals(selected, values[index]) || ReferenceEquals(selectedMember, values[index]),
                        labels[index], EditorStyles.miniButton, GUILayout.Height(22)))
                {
                    if (values[index] is MemberInfo member)
                    {
                        if (!ReferenceEquals(selectedMember, member))
                        {
                            Select(owner, false);
                            ReadMember(member);
                        }
                    }
                    else if (!ReferenceEquals(selected, values[index])) Select(values[index], false);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawDetails()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(selected?.GetType().Name ?? "Details", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(history.Count == 0))
                if (GUILayout.Button("Back", GUILayout.Width(48)))
                    Select(history.Pop(), false);
            if (GUILayout.Button("Refresh", GUILayout.Width(64)) && selected != null)
            {
                if (selectedMember != null) ReadMember(selectedMember);
                else Select(selected, false);
            }

            EditorGUILayout.EndHorizontal();
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
            if (selected == null)
                EditorGUILayout.HelpBox("Select an entity or member. Values are read on demand.", MessageType.Info);
            else
            {
                EditorGUILayout.SelectableLabel(selected.GetType().FullName, EditorStyles.wordWrappedMiniLabel,
                    GUILayout.Height(34));
                if (RuntimeDebugValues.IsScalar(selected))
                    EditorGUILayout.LabelField(RuntimeDebugValues.Label(selected));
                if (selected is SceneEntityState entity)
                {
                    EditorGUILayout.LabelField("Position", entity.Position.ToString());
                    EditorGUILayout.LabelField("State",
                        $"Active: {entity.Active}   Visible: {entity.Visible}   Collidable: {entity.Collidable}");
                }

                if (memberRead && detailTree == null)
                {
                    EditorGUILayout.LabelField(selectedMember.Name, RuntimeDebugValues.Label(memberValue));
                    if (truncated)
                        EditorGUILayout.HelpBox(
                            $"Collection preview limited to {RuntimeDebugValues.CollectionLimit} items.",
                            MessageType.Info);
                    if (collection.Count != 0)
                    {
                        Pager(ref collectionPage, collection.Count, "items");
                        for (var i = collectionPage * PageSize;
                             i < Math.Min(collection.Count, (collectionPage + 1) * PageSize);
                             i++)
                            if (GUILayout.Button($"[{i}] {RuntimeDebugValues.Label(collection[i])}",
                                    GUILayout.Height(22)))
                                Select(collection[i], true);
                    }
                    else if (!RuntimeDebugValues.IsScalar(memberValue) &&
                             GUILayout.Button("Open value", GUILayout.Width(96))) Select(memberValue, true);
                }

                EditorGUILayout.Space(6);
                var ownerListed = scope != 0 && ReferenceEquals(selected, owner);
                if (detailTree == null && !ownerListed)
                {
                    Pager(ref detailPage, members.Length, "members");
                    for (var i = detailPage * PageSize; i < Math.Min(members.Length, (detailPage + 1) * PageSize); i++)
                        if (GUILayout.Button(members[i].Name, EditorStyles.miniButton, GUILayout.Height(22)))
                            ReadMember(members[i]);
                }

                if (!ownerListed && !(selected is GameApplication) && !(selected is GameHost) &&
                    !RuntimeDebugValues.IsScalar(selected))
                {
                    if (detailTree == null && GUILayout.Button("Inspect selected (Odin)", GUILayout.Width(166)))
                        detailTree = PropertyTree.Create(new RuntimeDebugSelection { Value = selected });
                    if (detailTree != null)
                    {
                        if (GUILayout.Button("Close inspector", GUILayout.Width(112))) DisposeTree();
                        else
                            using (new EditorGUI.DisabledScope(true))
                                detailTree.Draw(false);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void Select(object value, bool navigate)
        {
            if (navigate && selected != null) history.Push(selected);
            DisposeTree();
            selected = value;
            detailPage = 0;
            selectedMember = null;
            memberValue = null;
            memberRead = false;
            collection.Clear();
            truncated = false;
            members = value == null || RuntimeDebugValues.IsScalar(value)
                ? Array.Empty<MemberInfo>()
                : RuntimeDebugValues.Members(value.GetType(), includePrivate);
        }

        private void ReadMember(MemberInfo member)
        {
            DisposeTree();
            selectedMember = member;
            memberRead = true;
            collectionPage = 0;
            try
            {
                memberValue = RuntimeDebugValues.Read(selected, member);
                truncated = RuntimeDebugValues.Snapshot(memberValue, collection);
            }
            catch (Exception exception)
            {
                collection.Clear();
                truncated = false;
                memberValue = "Read failed: " + exception.GetBaseException().Message;
            }
        }

        private void ClearSelection()
        {
            Select(null, false);
            history.Clear();
        }

        private void DisposeTree()
        {
            detailTree?.Dispose();
            detailTree = null;
        }

        private static void Pager(ref int current, int count, string noun)
        {
            var pages = Math.Max(1, (count + PageSize - 1) / PageSize);
            current = Math.Min(current, pages - 1);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(current == 0))
                if (GUILayout.Button("‹", GUILayout.Width(28)))
                    current--;
            EditorGUILayout.LabelField($"{count} {noun} · {current + 1}/{pages}", EditorStyles.centeredGreyMiniLabel);
            using (new EditorGUI.DisabledScope(current + 1 == pages))
                if (GUILayout.Button("›", GUILayout.Width(28)))
                    current++;
            EditorGUILayout.EndHorizontal();
        }
    }
}
