using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Celeste.Core.Framework.Startup;
using Celeste.Everest.Modules.Persistence.GameSaves;
using Celeste.Game.Execution.Diagnostics;
using Celeste.Everest.Runtime.Failures;
using Celeste.Framework.Startup;
using Celeste.Game.Runtime.Physical.Runtime;

namespace Celeste.Unity
{
    internal sealed class NativeUnitySourceRuntimeIO : ManagedSourceRuntimeIO, ISourceRuntimeErrorDevice,
        ISourceGameHostRuntimeIO, ISourceWindowFocusDevice,
        Celeste.Graphics.Rendering.Devices.ISourceWindowFocusIO
    {
        private readonly Celeste.Graphics.Rendering.Devices.ISourceHostWindowIO actualWindow;

        internal NativeUnitySourceRuntimeIO(Celeste.Graphics.Rendering.Devices.ISourceHostWindowIO window,
            string physicalOwnerAssemblyPath)
            : base(physicalOwnerAssemblyPath,
                Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..")),
                Environment.GetCommandLineArgs(),
                typeof(GameHost).GetMethod("Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                    null, Type.EmptyTypes, null)
                ?? throw new MissingMethodException(typeof(GameHost).FullName, "Awake"))
        {
            actualWindow = window ?? throw new ArgumentNullException(nameof(window));
        }

        public void ShowRuntimeError(string title, string message) =>
            UnityEngine.Debug.LogError(title + "\n" + message);

        public bool GetTouchDeviceConnected() => UnityEngine.InputSystem.Touchscreen.current != null;
        public void SetMouseVisible(bool visible) => UnityEngine.Cursor.visible = visible;
        public void SetHint(string name, string value) => SDL_SetHint(name, value);

        public void RaiseWindow(IntPtr window)
        {
            if (window != actualWindow.Handle)
                throw new ArgumentException("Only the actual same Unity window can be raised.", nameof(window));
            if (window == IntPtr.Zero) return;
            if (!NativeUnitySourceWindowIO.IsWindows) ((NativeUnitySourceWindowIO)actualWindow).Raise();
            else if (IsWindowVisible(window)) SetForegroundWindow(window);
        }

        // Unity's window is a Win32 HWND, not an SDL_Window pointer.
        Action<IntPtr> Celeste.Graphics.Rendering.Devices.ISourceWindowFocusIO.SdlRaiseWindow => null;

        bool Celeste.Graphics.Rendering.Devices.ISourceWindowFocusIO.SetForegroundWindow(IntPtr window)
        {
            if (window != actualWindow.Handle)
                throw new ArgumentException("Only the actual same Unity window can be raised.", nameof(window));
            if (window == IntPtr.Zero) return false;
            return NativeUnitySourceWindowIO.IsWindows
                ? IsWindowVisible(window) && SetForegroundWindow(window)
                : ((NativeUnitySourceWindowIO)actualWindow).Raise();
        }

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        private static extern byte SDL_SetHint([MarshalAs(UnmanagedType.LPUTF8Str)] string name,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr window);
    }
}
