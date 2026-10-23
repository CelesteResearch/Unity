using Celeste.Input.Devices.Mouse;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Celeste.Unity.Input
{
    internal sealed class NativeUnitySourceMouseTouchDevice : IUnitySourceMouseTouchDevice,
        ISourceGlobalMouseCapabilities
    {
        private readonly NativeUnitySourceWindowIO window;
        private readonly UnityEditorSourceInputSnapshot editorSnapshot;
        public bool SupportsGlobalMouse => editorSnapshot == null;

        internal NativeUnitySourceMouseTouchDevice(UnityEditorSourceInputSnapshot snapshot)
        {
            editorSnapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        private ulong physicalUpdate, consumedRelative;

        internal NativeUnitySourceMouseTouchDevice(NativeUnitySourceWindowIO window)
        {
            this.window = window ?? throw new ArgumentNullException(nameof(window));
        }

        public void OnSdkUpdate()
        {
            ++physicalUpdate;
            editorSnapshot?.CaptureMouse();
        }

        private void RequireWindow(IntPtr handle)
        {
            if (handle != (editorSnapshot == null ? window.Handle : editorSnapshot.Window.Handle))
                throw new ArgumentException("Borrow the same verified Unity window.", nameof(handle));
        }

        public void Size(out int width, out int height)
        {
            if (editorSnapshot != null) editorSnapshot.Window.GetWindowSize(out width, out height);
            else window.GetWindowSize(out width, out height);
            if (width <= 0 || height <= 0)
                throw new InvalidOperationException("The Unity input surface has no client size.");
        }

        public void SdkSize(out int width, out int height)
        {
            if (editorSnapshot != null) editorSnapshot.Window.GetDrawableSize(out width, out height);
            else window.GetDrawableSize(out width, out height);
            if (width <= 0 || height <= 0)
                throw new InvalidOperationException("The Unity physical input surface has no drawable size.");
        }

        private static uint Buttons(Mouse mouse)
        {
            if (!Application.isFocused || mouse == null) return 0;
            return (mouse.leftButton.isPressed ? 1u : 0u) | (mouse.middleButton.isPressed ? 2u : 0u) |
                   (mouse.rightButton.isPressed ? 4u : 0u) | (mouse.backButton.isPressed ? 8u : 0u) |
                   (mouse.forwardButton.isPressed ? 16u : 0u);
        }

        public uint GetMouseState(out int x, out int y)
        {
            if (editorSnapshot != null) return editorSnapshot.GetMouseState(out x, out y);
            var mouse = Mouse.current;
            Size(out int width, out int height);
            SdkSize(out int sdkWidth, out int sdkHeight);
            var position = mouse == null ? Vector2.zero : mouse.position.ReadUnprocessedValue();
            x = (int)(position.x * width / sdkWidth);
            y = height - (int)(position.y * height / sdkHeight);
            return Buttons(mouse);
        }

        public uint GetGlobalMouseState(out int x, out int y)
        {
            if (editorSnapshot != null)
                throw new NotSupportedException("The Editor exposes GameView-local mouse coordinates only.");
            uint buttons = GetMouseState(out x, out y);
            if (!NativeUnitySourceWindowIO.IsWindows)
            {
                window.ClientToScreen(ref x, ref y);
                return buttons;
            }

            var position = new POINT { X = x, Y = y };
            if (!ClientToScreen(window.Handle, ref position)) throw new Win32Exception(Marshal.GetLastWin32Error());
            x = position.X;
            y = position.Y;
            return buttons;
        }

        public uint GetRelativeMouseState(out int x, out int y)
        {
            var mouse = Mouse.current;
            var delta = Vector2.zero;
            if (mouse != null && Application.isFocused && consumedRelative != physicalUpdate)
            {
                delta = editorSnapshot == null ? mouse.delta.ReadUnprocessedValue() : editorSnapshot.MouseDelta;
                consumedRelative = physicalUpdate;
            }

            Size(out int width, out int height);
            SdkSize(out int sdkWidth, out int sdkHeight);
            x = (int)(delta.x * width / sdkWidth);
            y = -(int)(delta.y * height / sdkHeight);
            return editorSnapshot == null ? Buttons(mouse) : Application.isFocused ? editorSnapshot.MouseButtons : 0;
        }

        public int GetRelativeMouseMode() => Cursor.lockState == CursorLockMode.Locked ? 1 : 0;

        public int SetRelativeMouseMode(int enabled)
        {
            if (enabled != 0 && !Application.isFocused) return -1;
            Cursor.lockState = enabled != 0 ? CursorLockMode.Locked : CursorLockMode.None;
            return GetRelativeMouseMode() == (enabled != 0 ? 1 : 0) ? 0 : -1;
        }

        public void GetWindowPosition(IntPtr handle, out int x, out int y)
        {
            RequireWindow(handle);
            if (editorSnapshot != null)
                throw new NotSupportedException("The Editor surface has no desktop window origin.");
            if (!NativeUnitySourceWindowIO.IsWindows)
            {
                x = 0;
                y = 0;
                window.ClientToScreen(ref x, ref y);
                return;
            }

            var p = new POINT();
            // Source global mouse subtraction needs the client origin, not window decorations.
            if (!ClientToScreen(handle, ref p)) throw new Win32Exception(Marshal.GetLastWin32Error());
            x = p.X;
            y = p.Y;
        }

        public void WarpMouseInWindow(IntPtr handle, int x, int y)
        {
            RequireWindow(handle);
            if (!Application.isFocused) return;
            Size(out int width, out int height);
            SdkSize(out int sdkWidth, out int sdkHeight);
            if (Mouse.current == null) throw new InvalidOperationException("Unity has no physical mouse device.");
            Mouse.current.WarpCursorPosition(new Vector2((float)x * sdkWidth / width,
                (float)(height - y) * sdkHeight / height));
        }

        public int GetNumTouchDevices()
        {
            int count = 0;
            foreach (var device in InputSystem.devices)
                if (device is Touchscreen)
                    ++count;
            return count;
        }

        public long GetTouchDevice(int index)
        {
            foreach (var device in InputSystem.devices)
                if (device is Touchscreen && index-- == 0)
                    return device.deviceId;
            return 0;
        }

        private static Touchscreen Screen(long id)
            => InputSystem.GetDeviceById((int)id) as Touchscreen ??
               throw new InvalidOperationException("The actual touchscreen was removed.");

        public int GetNumTouchFingers(long device)
        {
            if (device == 0 || !Application.isFocused) return 0;
            int count = 0;
            foreach (var touch in Screen(device).touches)
                if (touch.press.isPressed)
                    ++count;
            return count;
        }

        public IntPtr GetTouchFinger(long device, int index)
        {
            if (device == 0 || !Application.isFocused) return IntPtr.Zero;
            var screen = Screen(device);
            for (int i = 0; i < screen.touches.Count; ++i)
                if (screen.touches[i].press.isPressed && index-- == 0)
                    return new IntPtr(((long)screen.deviceId << 32) | (uint)(i + 1));
            return IntPtr.Zero;
        }

        private static TouchControl Finger(IntPtr finger)
        {
            long value = finger.ToInt64();
            return Screen(value >> 32).touches[(int)(value & 0xffffffff) - 1];
        }

        internal static long PhysicalFingerId(int deviceId, int touchId) =>
            UnitySourceTouchIdentity.PhysicalFingerId(deviceId, touchId);

        public long ReadFingerId(IntPtr finger) =>
            PhysicalFingerId((int)(finger.ToInt64() >> 32), Finger(finger).touchId.ReadValue());

        public float ReadFingerX(IntPtr finger)
        {
            SdkSize(out int width, out _);
            return Finger(finger).position.ReadUnprocessedValue().x / width;
        }

        public float ReadFingerY(IntPtr finger)
        {
            SdkSize(out _, out int height);
            return 1f - Finger(finger).position.ReadUnprocessedValue().y / height;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X, Y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ClientToScreen(IntPtr window, ref POINT position);
    }
}
