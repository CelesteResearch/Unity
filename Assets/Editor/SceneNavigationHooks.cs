using System;
using UnityEditor;

namespace Celeste.Unity.Editor
{
    // One visible Overlay owns one subscription lease. Disposal first removes
    // every static root, then drops callbacks so a retained lease cannot retain
    // the Overlay, its SceneView, or a whole live chapter. No finalizer: editor
    // resources and event ownership are released on the editor thread.
    internal sealed class SceneNavigationHooks : IDisposable
    {
        private Action<SceneView> draw;
        private Action tick, selection, stopped;
        private Action<PlayModeStateChange> playMode;
        private Func<bool> alive;
        private bool disposed;

        internal SceneNavigationHooks(Action<SceneView> draw, Action tick,
            Action<PlayModeStateChange> playMode, Action selection, Func<bool> alive, Action stopped)
        {
            this.draw = draw ?? throw new ArgumentNullException(nameof(draw));
            this.tick = tick ?? throw new ArgumentNullException(nameof(tick));
            this.playMode = playMode ?? throw new ArgumentNullException(nameof(playMode));
            this.selection = selection ?? throw new ArgumentNullException(nameof(selection));
            this.alive = alive ?? throw new ArgumentNullException(nameof(alive));
            this.stopped = stopped ?? throw new ArgumentNullException(nameof(stopped));
            try
            {
                SceneView.duringSceneGui += Draw;
                EditorApplication.update += Tick;
                EditorApplication.playModeStateChanged += PlayModeChanged;
                Selection.selectionChanged += SelectionChanged;
                AssemblyReloadEvents.beforeAssemblyReload += Stop;
                EditorApplication.quitting += Stop;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void Draw(SceneView view)
        {
            if (disposed) return;
            if (alive()) draw(view);
            else Stop();
        }

        private void Tick()
        {
            if (disposed) return;
            if (alive()) tick();
            else Stop();
        }

        private void PlayModeChanged(PlayModeStateChange state)
        {
            if (disposed) return;
            playMode(state);
        }

        private void SelectionChanged()
        {
            if (disposed) return;
            selection();
        }

        private void Stop()
        {
            if (disposed) return;
            var release = stopped;
            Dispose(); // Must precede user cleanup, including a throwing cleanup.
            release();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            SceneView.duringSceneGui -= Draw;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            Selection.selectionChanged -= SelectionChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            EditorApplication.quitting -= Stop;
            draw = null;
            tick = null;
            playMode = null;
            selection = null;
            alive = null;
            stopped = null;
        }
    }
}
