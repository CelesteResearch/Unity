using System;
using System.Linq;
using Celeste.Core.Scheduling;
using Celeste.Game.Content.Maps;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Scene.Scenes;
using Celeste.Simulation;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Celeste.Graphics.Scene.Diagnostics;
using IntRect = Celeste.Core.Geometry.IntRect;

namespace Celeste.Unity.Editor
{
    [Overlay(typeof(SceneView), Id, "Celeste Navigation", true)]
    public sealed class CelesteSceneNavigation : Overlay
    {
        public const string Id = "Celeste.SceneNavigation";

        internal enum FollowMode
        {
            Off,
            Player,
            Room,
            Map,
            GameView
        }

        private GameHost pinnedTarget, target;
        private FollowMode followMode = FollowMode.Room, selectedFollowMode = FollowMode.Room;
        private SceneTileScope selectedScope = SceneTileScope.VisibleRooms;
        private SceneView scopeView;
        private SceneNavigationPreferences preferences;
        private bool showMap = true, showMarkers = true, showNames = true;
        private OriginalChapterMapData cachedMap;
        private OriginalLevelRoomData[] rooms = Array.Empty<OriginalLevelRoomData>();
        private IntRect[] roomWorldBounds = Array.Empty<IntRect>();
        private GameSceneRenderer sceneRenderer;
        private static readonly string[] TileRangeLabels = { "Camera", "Complete visible rooms", "Entire map" };
        private string[] roomNames = Array.Empty<string>();
        private int browseRoom;
        private double nextRepaint;
        private OriginalLevelSceneRoot repaintLevel;
        private long repaintFrameSequence = -1;
        private SceneView framedView;
        private Bounds framedBounds;
        private Vector3 framedPivot;
        private Quaternion framedRotation;
        private float framedSize;
        private Rect framedViewport;
        private bool framedOrthographic;
        private string sourceStateError;
        private IMGUIContainer panel;
        private SceneNavigationHooks hooks;
        private bool created;
        private readonly Vector3[] corners = new Vector3[5];
        private SceneView View => containerWindow as SceneView;

        private OriginalLevelSceneRoot Level
        {
            get
            {
                sourceStateError = null;
                if (!target || !EditorApplication.isPlaying) return null;
                try
                {
                    return ((ISourceEngineContext)target.ActualProcess).Frame.Scenes.CurrentScene as
                        OriginalLevelSceneRoot;
                }
                catch (InvalidOperationException error)
                {
                    sourceStateError = error.Message;
                    return null;
                }
            }
        }

        private RoomPlayerSession Player
        {
            get
            {
                var level = Level;
                if (level == null) return null;
                foreach (var entity in level.SourceScene.Entities.Entities)
                    if (entity is RoomPlayerSession player)
                        return player;
                return null;
            }
        }

        [MenuItem("Celeste/Scene Navigation")]
        private static void Open()
        {
            var view = SceneView.lastActiveSceneView;
            if (!view) view = EditorWindow.GetWindow<SceneView>();
            if (view.TryGetOverlay(Id, out var overlay))
            {
                overlay.displayed = true;
                overlay.collapsed = false;
            }

            view.Focus();
        }

        public override void OnCreated()
        {
            Shutdown();
            RestorePreferences();
            created = true;
            rootVisualElement.RegisterCallback<DetachFromPanelEvent>(PanelDetached);
            displayedChanged += DisplayChanged;
            DisplayChanged(displayed);
        }

        public override void OnWillBeDestroyed() => Shutdown();

        public override VisualElement CreatePanelContent()
        {
            if (panel != null) panel.onGUIHandler = null;
            panel = new IMGUIContainer(DrawPanel);
            panel.style.minWidth = 270;
            return panel;
        }

        private void PanelDetached(DetachFromPanelEvent evt)
        {
            if (ReferenceEquals(evt.target, rootVisualElement)) sceneRenderer?.SuspendCapture();
        }

        private void Shutdown()
        {
            if (created) rootVisualElement.UnregisterCallback<DetachFromPanelEvent>(PanelDetached);
            created = false;
            displayedChanged -= DisplayChanged;
            if (panel != null) panel.onGUIHandler = null;
            panel = null;
            pinnedTarget = null;
            Suspend();
        }

        private void Suspend()
        {
            var previous = hooks;
            hooks = null;
            previous?.Dispose();
            target = null;
            followMode = FollowMode.Off;
            ReleaseCache();
        }

        private void RestorePreferences()
        {
            preferences = new SceneNavigationPreferences(Application.dataPath);
            selectedFollowMode = preferences.ReadFollowMode();
            followMode = selectedFollowMode;
            selectedScope = preferences.ReadScope();
            showMap = preferences.ReadShowMap();
            showMarkers = preferences.ReadShowMarkers();
            showNames = preferences.ReadShowNames();
            scopeView = null;
        }

        private void EnsureScope(SceneView view)
        {
            if (!view || ReferenceEquals(scopeView, view)) return;
            scopeView = view;
            GameSceneRenderer.SetScope(view, selectedScope);
        }

        private void DisplayChanged(bool visible)
        {
            if (!created) return;
            if (!visible)
            {
                Suspend();
                return;
            }

            followMode = selectedFollowMode;
            if (hooks == null)
                hooks = new SceneNavigationHooks(DuringSceneGUI, Tick,
                    state =>
                    {
                        target = null;
                        ReleaseCache();
                        if (state == PlayModeStateChange.EnteredPlayMode) followMode = selectedFollowMode;
                    }, ResolveTarget, () => created && displayed,
                    () =>
                    {
                        hooks = null;
                        target = null;
                        ReleaseCache();
                    });
            ResolveTarget();
        }

        private void ResolveTarget()
        {
            var found = pinnedTarget;
            if (!found && Selection.activeGameObject)
                found = Selection.activeGameObject.GetComponentInParent<GameHost>();
            if (!found)
                found = UnityEngine.Object
                    .FindObjectsByType<GameHost>(FindObjectsInactive.Exclude)
                    .FirstOrDefault();
            if (!ReferenceEquals(target, found))
            {
                target = found;
                ReleaseCache();
            }
        }

        private void ReleaseCache()
        {
            scopeView = null;
            framedView = null;
            repaintLevel = null;
            repaintFrameSequence = -1;
            sceneRenderer?.Dispose();
            sceneRenderer = null;
            roomWorldBounds = Array.Empty<IntRect>();
            cachedMap = null;
            rooms = Array.Empty<OriginalLevelRoomData>();
            roomNames = Array.Empty<string>();
            browseRoom = 0;
        }

        private void RefreshMap(OriginalLevelSceneRoot level)
        {
            var map = level?.Session?.MapData as OriginalChapterMapData;
            if (map == null && sceneRenderer != null)
            {
                sceneRenderer.Dispose();
                sceneRenderer = null;
            }

            if (ReferenceEquals(map, cachedMap)) return;
            cachedMap = map;
            rooms = map == null ? Array.Empty<OriginalLevelRoomData>() : map.Levels.ToArray();
            roomNames = rooms.Select(room => room.Name).ToArray();
            roomWorldBounds = rooms.Select(room => room.Bounds).ToArray();
            browseRoom = Math.Max(0, Array.FindIndex(rooms, room => ReferenceEquals(room, level.Session.LevelData)));
        }

        private void Tick()
        {
            if (rootVisualElement.panel == null) return;
            if (!target) ResolveTarget();
            var level = Level;
            RefreshMap(level);
            if (level != null && followMode != FollowMode.Off) FrameFollow(level);
            if (EditorApplication.timeSinceStartup >= nextRepaint)
            {
                nextRepaint = EditorApplication.timeSinceStartup + .1;
                panel?.MarkDirtyRepaint();
            }

            long frameSequence = sceneRenderer?.CompletedFrameSequence ?? 0;
            if (!ReferenceEquals(repaintLevel, level) ||
                level != null && (sceneRenderer == null || !sceneRenderer.HasCapture ||
                                  repaintFrameSequence != frameSequence))
            {
                repaintLevel = level;
                repaintFrameSequence = frameSequence;
                View?.Repaint();
            }
        }

        private void DrawPanel()
        {
            EnsureScope(View);
            var selected = (GameHost)EditorGUILayout.ObjectField("Host (empty = auto)", pinnedTarget,
                typeof(GameHost), true);
            if (!ReferenceEquals(selected, pinnedTarget))
            {
                pinnedTarget = selected;
                ResolveTarget();
            }

            var chosenFollow = (FollowMode)EditorGUILayout.EnumPopup("Follow", selectedFollowMode);
            if (chosenFollow != selectedFollowMode)
            {
                selectedFollowMode = chosenFollow;
                followMode = chosenFollow;
                preferences.WriteFollowMode(chosenFollow);
            }

            if (followMode == FollowMode.Off && selectedFollowMode != FollowMode.Off &&
                GUILayout.Button("Resume following"))
                followMode = selectedFollowMode;

            var tileScope = GameSceneRenderer.GetScope(View);
            var selectedScope = (SceneTileScope)EditorGUILayout.Popup("Draw range", (int)tileScope, TileRangeLabels);
            if (selectedScope != tileScope)
            {
                this.selectedScope = selectedScope;
                GameSceneRenderer.SetScope(View, selectedScope);
                preferences.WriteScope(selectedScope);
            }

            var chosenShowMap = EditorGUILayout.Toggle("Show map rooms", showMap);
            if (chosenShowMap != showMap)
            {
                showMap = chosenShowMap;
                preferences.WriteShowMap(showMap);
            }

            var chosenShowMarkers = EditorGUILayout.Toggle("Show live markers", showMarkers);
            if (chosenShowMarkers != showMarkers)
            {
                showMarkers = chosenShowMarkers;
                preferences.WriteShowMarkers(showMarkers);
            }

            var chosenShowNames = EditorGUILayout.Toggle("Show room names", showNames);
            if (chosenShowNames != showNames)
            {
                showNames = chosenShowNames;
                preferences.WriteShowNames(showNames);
            }

            if (GUILayout.Button("Open Game Debugger")) RoomRuntimeDebugger.Open(target);
            if (GUILayout.Button("Refresh drawing"))
            {
                sceneRenderer?.Dispose();
                sceneRenderer = null;
                View?.Repaint();
            }

            var level = Level;
            RefreshMap(level);
            if (level == null)
            {
                EditorGUILayout.LabelField(sourceStateError ?? "No active level");
                return;
            }

            EditorGUILayout.LabelField(level.Session.Level, EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Room"))
                Frame(level.Bounds.X, level.Bounds.Y, level.Bounds.Width, level.Bounds.Height);
            if (GUILayout.Button("Map")) FrameMap();
            if (GUILayout.Button("Camera")) FrameCamera(level);
            EditorGUILayout.EndHorizontal();

            if (sceneRenderer != null)
            {
                if (sceneRenderer.SnapshotTexturePageCount != 0)
                    EditorGUILayout.LabelField("Snapshot texture pages",
                        sceneRenderer.SnapshotTexturePageCount + " (Refresh drawing after texture updates)");
                if (!sceneRenderer.HasPresentation)
                    EditorGUILayout.LabelField("Sprites", "Waiting for a completed normal gameplay render");
                EditorGUILayout.LabelField("Unsupported texture / state / scope / dropped",
                    sceneRenderer.UnsupportedImageTextureCount + " / " +
                    sceneRenderer.UnsupportedRenderStateCount + " / " +
                    sceneRenderer.InvalidScopeCount + " / " + sceneRenderer.DroppedQuadCount);
            }

            if (rooms.Length > 0)
            {
                browseRoom = EditorGUILayout.Popup("Browse room", Math.Min(browseRoom, rooms.Length - 1), roomNames);
                if (GUILayout.Button("Frame Selected Room"))
                {
                    var bounds = rooms[browseRoom].Bounds;
                    Frame(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                }
            }
        }

        private void FrameFollow(OriginalLevelSceneRoot level)
        {
            switch (followMode)
            {
                case FollowMode.Player:
                    var player = Player;
                    if (player != null) Frame(player.Position.X - 80, player.Position.Y - 45, 160, 90);
                    break;
                case FollowMode.Room:
                    Frame(level.Bounds.X, level.Bounds.Y, level.Bounds.Width, level.Bounds.Height); break;
                case FollowMode.Map: FrameMap(); break;
                case FollowMode.GameView: FrameCamera(level); break;
            }
        }

        private void FrameCamera(OriginalLevelSceneRoot level)
        {
            var bounds = CameraWorldBounds.Read(level.Camera);
            Frame(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }

        private void FrameMap()
        {
            if (rooms.Length == 0) return;
            int left = rooms.Min(room => room.Bounds.Left), top = rooms.Min(room => room.Bounds.Top);
            int right = rooms.Max(room => room.Bounds.Right), bottom = rooms.Max(room => room.Bounds.Bottom);
            Frame(left, top, right - left, bottom - top);
        }

        private void Frame(float x, float y, float width, float height)
        {
            var view = View;
            if (!view) return;
            var bounds = new Bounds(new Vector3(x + width * .5f, -y - height * .5f, 0),
                new Vector3(Math.Max(1, width * 1.12f), Math.Max(1, height * 1.12f), 1));
            if (ReferenceEquals(framedView, view) && framedBounds.Equals(bounds) &&
                view.in2DMode && framedPivot.Equals(view.pivot) && framedRotation.Equals(view.rotation) &&
                framedSize.Equals(view.size) && framedViewport.Equals(view.position) &&
                framedOrthographic == view.orthographic) return;
            view.in2DMode = true;
            view.Frame(bounds, true);
            framedView = view;
            framedBounds = bounds;
            framedPivot = view.pivot;
            framedRotation = view.rotation;
            framedSize = view.size;
            framedViewport = view.position;
            framedOrthographic = view.orthographic;
        }

        private void DuringSceneGUI(SceneView view)
        {
            if (view != View || rootVisualElement.panel == null) return;
            EnsureScope(view);
            if (ShouldStopFollowing(Event.current, Tools.current)) followMode = FollowMode.Off;
            var level = Level;
            if (level == null) return;
            RefreshMap(level);
            if (Event.current.type != EventType.Repaint) return;
            if (sceneRenderer == null) sceneRenderer = new GameSceneRenderer(target.CreateSceneTextureBatch);
            var gameView = CameraWorldBounds.Read(level.Camera);
            sceneRenderer.Bind(target.ActualProcess.Draw.SpriteBatch);
            sceneRenderer.Draw(view, level, roomWorldBounds, in gameView);

            var previous = Handles.zTest;
            try
            {
                using (new Handles.DrawingScope(Color.white, Matrix4x4.identity))
                {
                    Handles.zTest = CompareFunction.Always;
                    if (showMap)
                        foreach (var room in rooms)
                            Outline(room.Bounds.X, room.Bounds.Y, room.Bounds.Width, room.Bounds.Height,
                                new Color(.55f, .65f, .75f, .65f), showNames ? room.Name : null);
                    if (!showMarkers) return;
                    Outline(level.Bounds.X, level.Bounds.Y, level.Bounds.Width, level.Bounds.Height, Color.yellow,
                        "Current room");
                    var cameraBounds = CameraWorldBounds.Read(level.Camera);
                    Outline(cameraBounds.X, cameraBounds.Y, cameraBounds.Width, cameraBounds.Height, Color.cyan,
                        "Game camera");
                    var player = Player;
                    if (player != null)
                    {
                        var bounds = player.Body.BoundsAt(player.Body.Position);
                        Outline(bounds.X, bounds.Y, bounds.Width, bounds.Height, Color.magenta, "Player");
                    }
                }
            }
            finally
            {
                Handles.zTest = previous;
            }
        }

        internal static bool ShouldStopFollowing(Event e, Tool tool)
        {
            var type = e.rawType;
            return ((type == EventType.MouseDown || type == EventType.MouseDrag) && (e.button != 0 || e.alt)) ||
                   (type == EventType.MouseDrag && tool == Tool.View) ||
                   (type == EventType.KeyDown && (e.keyCode == KeyCode.Escape || e.keyCode == KeyCode.F));
        }

        private void Outline(float x, float y, float width, float height, Color color, string label)
        {
            corners[0] = new Vector3(x, -y, 0);
            corners[1] = new Vector3(x + width, -y, 0);
            corners[2] = new Vector3(x + width, -y - height, 0);
            corners[3] = new Vector3(x, -y - height, 0);
            corners[4] = corners[0];
            Handles.color = color;
            Handles.DrawAAPolyLine(2, corners);
            if (label != null) Handles.Label(corners[0], label);
        }
    }
}
