using System;
using System.ComponentModel;
using Process = System.Diagnostics.Process;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Celeste.Core.Geometry;
using Celeste.Graphics.Rendering.Devices;
using UnityEngine;

namespace Celeste.Unity
{
    public sealed class NativeUnitySourceWindowIO : ISourceHostWindowIO
    {
        private readonly IntPtr unityWindow;
        private int requestedWidth;
        private int requestedHeight;
        private FullScreenMode requestedMode;
        private bool resolutionPending;
        private int appliedWidth;
        private int appliedHeight;
        private FullScreenMode appliedMode;

        internal static bool IsWindows => Application.platform == RuntimePlatform.WindowsPlayer ||
                                          Application.platform == RuntimePlatform.WindowsEditor;

        public NativeUnitySourceWindowIO(IntPtr actualUnityWindow)
        {
            if (actualUnityWindow == IntPtr.Zero)
                throw new ArgumentException("The Unity Player window must already exist.", nameof(actualUnityWindow));
            if (IsWindows)
            {
                if (!IsWindow(actualUnityWindow)) throw new Win32Exception(Marshal.GetLastWin32Error());
                ValidateUnityPlayerWindow(actualUnityWindow);
            }
            else if (PortableWindow.CelesteUnityGetWindow() != actualUnityWindow)
                throw new ArgumentException("Borrow this process's actual Unity Player window.",
                    nameof(actualUnityWindow));

            unityWindow = actualUnityWindow;
        }

        public static IntPtr BorrowCurrentUnityPlayerWindow()
        {
            if (!IsWindows)
            {
                IntPtr handle = PortableWindow.CelesteUnityGetWindow();
                if (handle == IntPtr.Zero)
                    throw new InvalidOperationException("The current process has no unambiguous Unity Player window.");
                return handle;
            }

            var matches = new List<IntPtr>();
            uint currentPid = unchecked((uint)Process.GetCurrentProcess().Id);
            if (!EnumWindows((window, _) =>
                {
                    GetWindowThreadProcessId(window, out uint pid);
                    if (pid != currentPid || !IsWindow(window) || GetWindow(window, 4) != IntPtr.Zero)
                        return true;
                    var className = new StringBuilder(256);
                    if (GetClassName(window, className, className.Capacity) > 0 &&
                        className.ToString() == "UnityWndClass")
                        matches.Add(window);
                    return true;
                }, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (matches.Count != 1)
                throw new InvalidOperationException(
                    "One actual Unity Player window owned by this process is required.");
            ValidateUnityPlayerWindow(matches[0]);
            return matches[0];
        }

        private static void ValidateUnityPlayerWindow(IntPtr window)
        {
            GetWindowThreadProcessId(window, out uint pid);
            var className = new StringBuilder(256);
            if (pid != unchecked((uint)Process.GetCurrentProcess().Id) ||
                GetClassName(window, className, className.Capacity) == 0 ||
                className.ToString() != "UnityWndClass")
                throw new ArgumentException("Borrow the same process's actual Unity Player window.", nameof(window));
        }

        public IntPtr Handle => unityWindow;

        public IntRect Bounds
        {
            get
            {
                if (!IsWindows)
                {
                    if (PortableWindow.CelesteUnityWindowBounds(unityWindow, out int x, out int y, out int width,
                            out int height) == 0)
                        throw new InvalidOperationException("Cannot query the Unity Player window bounds.");
                    return new IntRect(x, y, width, height);
                }

                if (!GetClientRect(unityWindow, out RECT client))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                var origin = new POINT();
                if (!ClientToScreen(unityWindow, ref origin))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                return new IntRect(origin.X, origin.Y, client.Right - client.Left, client.Bottom - client.Top);
            }
        }

        public string DisplayName
        {
            get
            {
                if (!IsWindows) return Display.main.ToString();
                var info = new MONITORINFOEX { Size = Marshal.SizeOf<MONITORINFOEX>() };
                if (!GetMonitorInfo(MonitorFromWindow(unityWindow, 2), ref info))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                return info.Device;
            }
        }

        public bool Resizable
        {
            get => IsWindows
                ? (GetWindowLongPtr(unityWindow, -16).ToInt64() & 0x40000L) != 0
                : ReadPortableStyle(false);
            set
            {
                if (IsWindows) ChangeStyle(0x40000L, value);
                else WritePortableStyle(false, value);
            }
        }

        public bool Borderless
        {
            get => IsWindows
                ? (GetWindowLongPtr(unityWindow, -16).ToInt64() & 0xC00000L) == 0
                : ReadPortableStyle(true);
            set
            {
                if (IsWindows) ChangeStyle(0xC00000L, !value);
                else WritePortableStyle(true, value);
            }
        }

        private bool ReadPortableStyle(bool borderless)
        {
            int result = PortableWindow.CelesteUnityWindowStyle(unityWindow, borderless ? 1 : 0, -1);
            if (result < 0) throw new InvalidOperationException("Cannot read the Unity Player window style.");
            return result != 0;
        }

        private void WritePortableStyle(bool borderless, bool enabled)
        {
            if (PortableWindow.CelesteUnityWindowStyle(unityWindow, borderless ? 1 : 0, enabled ? 1 : 0) < 0)
                throw new InvalidOperationException("Cannot change the Unity Player window style.");
        }

        internal bool Raise() => PortableWindow.CelesteUnityRaiseWindow(unityWindow) != 0;

        internal void ClientToScreen(ref int x, ref int y)
        {
            if (PortableWindow.CelesteUnityClientToScreen(unityWindow, ref x, ref y) == 0)
                throw new InvalidOperationException("Cannot translate Unity Player coordinates to the desktop.");
        }

        private void ChangeStyle(long bits, bool enabled)
        {
            long style = GetWindowLongPtr(unityWindow, -16).ToInt64();
            SetWindowLongPtr(unityWindow, -16, new IntPtr(enabled ? style | bits : style & ~bits));
            if (!SetWindowPos(unityWindow, IntPtr.Zero, 0, 0, 0, 0, 0x37))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        public void SetTitle(string title)
        {
            if (!IsWindows)
            {
                if (PortableWindow.CelesteUnityWindowTitle(unityWindow, title) == 0)
                    throw new InvalidOperationException("Cannot set the Unity Player window title.");
                return;
            }

            if (!SetWindowText(unityWindow, title)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        public void ApplyResolution(int width, int height, bool fullscreen, string displayName)
        {
            requestedWidth = width;
            requestedHeight = height;
            requestedMode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            resolutionPending = true;
        }

        internal bool HasPendingResolutionChange
        {
            get
            {
                if (!resolutionPending) return false;
                if (Screen.width != requestedWidth || Screen.height != requestedHeight ||
                    Screen.fullScreenMode != requestedMode) return true;
                resolutionPending = false;
                return false;
            }
        }

        internal bool AppliedResolutionMatches => Screen.width == appliedWidth && Screen.height == appliedHeight &&
                                                  Screen.fullScreenMode == appliedMode;

        internal void ApplyPendingResolution()
        {
            if (!resolutionPending) return;
            resolutionPending = false;
            appliedWidth = requestedWidth;
            appliedHeight = requestedHeight;
            appliedMode = requestedMode;
            if (AppliedResolutionMatches) return;
            Screen.SetResolution(appliedWidth, appliedHeight, appliedMode);
        }

        public void GetWindowSize(out int width, out int height)
        {
            if (!IsWindows)
            {
                if (PortableWindow.CelesteUnityWindowBounds(unityWindow, out _, out _, out width, out height) == 0)
                    throw new InvalidOperationException("Cannot query the Unity Player client size.");
                return;
            }

            if (!GetClientRect(unityWindow, out RECT client)) throw new Win32Exception(Marshal.GetLastWin32Error());
            width = client.Right - client.Left;
            height = client.Bottom - client.Top;
        }

        public void GetDrawableSize(out int width, out int height)
        {
            width = Screen.width;
            height = Screen.height;
        }

        public void LogWarning(string message) => Debug.LogWarning(message);

        private static class PortableWindow
        {
            private const string Library = "CelesteUnityPlatform";

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr CelesteUnityGetWindow();

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int CelesteUnityWindowBounds(IntPtr window, out int x, out int y, out int width,
                out int height);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int CelesteUnityWindowStyle(IntPtr window, int borderless, int enabled);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int CelesteUnityWindowTitle(IntPtr window,
                [MarshalAs(UnmanagedType.LPUTF8Str)] string title);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int CelesteUnityRaiseWindow(IntPtr window);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int CelesteUnityClientToScreen(IntPtr window, ref int x, ref int y);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X, Y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MONITORINFOEX
        {
            public int Size;
            public RECT Monitor, Work;
            public uint Flags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string Device;
        }

        private delegate bool EnumWindowsCallback(IntPtr window, IntPtr argument);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr argument);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr window, uint command);

        [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder name, int maximum);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsWindow(IntPtr window);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ClientToScreen(IntPtr window, ref POINT point);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(IntPtr window, out RECT bounds);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

        [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFOEX info);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

        [DllImport("user32.dll", EntryPoint = "SetWindowTextW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetWindowText(IntPtr window, string value);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height,
            uint flags);
    }
}
