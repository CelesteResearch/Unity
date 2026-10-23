using System;
using System.Collections.Generic;
using Celeste.Input.Devices.Mouse;
using Celeste.Graphics.Rendering.Devices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Celeste.Unity.Input
{
    internal sealed class UnityWebSourceMouseTouchDevice : IUnitySourceMouseTouchDevice, ISourceGlobalMouseCapabilities
    {
        private readonly ISourceHostWindowIO window;
        private ulong update, consumed;
        private Vector2 delta;
        private uint buttons;
        private int nextFinger = 1;
        private readonly Dictionary<(int Device, int Slot), IntPtr> fingerTokens = new();
        private readonly Dictionary<IntPtr, (int Device, int Slot)> fingers = new();

        internal UnityWebSourceMouseTouchDevice(ISourceHostWindowIO window)
        {
            this.window = window ?? throw new ArgumentNullException(nameof(window));
            if (window.Handle != IntPtr.Zero)
                throw new ArgumentException("A canvas input surface has no native window.", nameof(window));
        }

        public bool SupportsGlobalMouse => false;

        public void OnSdkUpdate()
        {
            update++;
            var mouse = Mouse.current;
            delta = Application.isFocused && mouse != null ? mouse.delta.ReadUnprocessedValue() : Vector2.zero;
            buttons = ReadButtons(mouse);
        }

        private static uint ReadButtons(Mouse mouse) => !Application.isFocused || mouse == null
            ? 0u
            : (mouse.leftButton.isPressed ? 1u : 0u) | (mouse.middleButton.isPressed ? 2u : 0u) |
              (mouse.rightButton.isPressed ? 4u : 0u) | (mouse.backButton.isPressed ? 8u : 0u) |
              (mouse.forwardButton.isPressed ? 16u : 0u);

        public void Size(out int width, out int height)
        {
            window.GetWindowSize(out width, out height);
            if (width <= 0 || height <= 0) throw new InvalidOperationException("The canvas has no client size.");
        }

        public void SdkSize(out int width, out int height)
        {
            window.GetDrawableSize(out width, out height);
            if (width <= 0 || height <= 0) throw new InvalidOperationException("The canvas has no drawable size.");
        }

        public uint GetMouseState(out int x, out int y)
        {
            Size(out int width, out int height);
            SdkSize(out int sdkWidth, out int sdkHeight);
            var p = Mouse.current == null ? Vector2.zero : Mouse.current.position.ReadUnprocessedValue();
            x = (int)(p.x * width / sdkWidth);
            y = height - (int)(p.y * height / sdkHeight);
            return ReadButtons(Mouse.current);
        }

        public uint GetGlobalMouseState(out int x, out int y) =>
            throw new PlatformNotSupportedException("Browser input is canvas-local.");

        public void GetWindowPosition(IntPtr handle, out int x, out int y) =>
            throw new PlatformNotSupportedException("A browser canvas has no desktop window origin.");

        public uint GetRelativeMouseState(out int x, out int y)
        {
            Size(out int width, out int height);
            SdkSize(out int sdkWidth, out int sdkHeight);
            var current = consumed == update ? Vector2.zero : delta;
            consumed = update;
            x = (int)(current.x * width / sdkWidth);
            y = -(int)(current.y * height / sdkHeight);
            return Application.isFocused ? buttons : 0;
        }

        public int GetRelativeMouseMode() => Cursor.lockState == CursorLockMode.Locked ? 1 : 0;

        public int SetRelativeMouseMode(int enabled)
        {
            if (enabled != 0 && !Application.isFocused) return -1;
            Cursor.lockState = enabled == 0 ? CursorLockMode.None : CursorLockMode.Locked;
            return GetRelativeMouseMode() == (enabled == 0 ? 0 : 1) ? 0 : -1;
        }

        public void WarpMouseInWindow(IntPtr handle, int x, int y) =>
            throw new PlatformNotSupportedException("The browser does not expose a cursor warp operation.");

        public int GetNumTouchDevices()
        {
            int count = 0;
            foreach (var device in InputSystem.devices)
                if (device is Touchscreen)
                    count++;
            return count;
        }

        public long GetTouchDevice(int index)
        {
            foreach (var device in InputSystem.devices)
                if (device is Touchscreen && index-- == 0)
                    return device.deviceId;
            return 0;
        }

        private static Touchscreen Screen(long id) => InputSystem.GetDeviceById((int)id) as Touchscreen ??
                                                      throw new InvalidOperationException(
                                                          "The touchscreen was removed.");

        public int GetNumTouchFingers(long device)
        {
            if (device == 0 || !Application.isFocused) return 0;
            int count = 0;
            foreach (var touch in Screen(device).touches)
                if (touch.press.isPressed)
                    count++;
            return count;
        }

        public IntPtr GetTouchFinger(long device, int index)
        {
            if (device == 0 || !Application.isFocused) return IntPtr.Zero;
            var screen = Screen(device);
            for (int slot = 0; slot < screen.touches.Count; slot++)
                if (screen.touches[slot].press.isPressed && index-- == 0)
                {
                    var key = (screen.deviceId, slot);
                    if (!fingerTokens.TryGetValue(key, out var token))
                    {
                        if (nextFinger == int.MaxValue)
                            throw new InvalidOperationException("Touch handle capacity exhausted.");
                        token = new IntPtr(nextFinger++);
                        fingerTokens.Add(key, token);
                        fingers.Add(token, key);
                    }

                    return token;
                }

            return IntPtr.Zero;
        }

        private TouchControl Finger(IntPtr token)
        {
            var key = fingers[token];
            return Screen(key.Device).touches[key.Slot];
        }

        public long ReadFingerId(IntPtr finger)
        {
            var key = fingers[finger];
            return UnitySourceTouchIdentity.PhysicalFingerId(key.Device, Finger(finger).touchId.ReadValue());
        }

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
    }
}
