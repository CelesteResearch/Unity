using System;
using System.Threading;
using Celeste.Graphics.Rendering.Devices;
using Celeste.Input.Devices.Keyboard;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Celeste.Unity.Input
{
    // SDK state is captured only by the existing Dynamic update/host PollEvents on the Unity thread.
    internal sealed class UnityEditorSourceInputSnapshot
    {
        private readonly int mainThread = Thread.CurrentThread.ManagedThreadId;
        private readonly byte[] keys = new byte[256];
        internal ISourceHostWindowIO Window { get; }
        private Vector2 mousePosition;
        internal Vector2 MouseDelta { get; private set; }
        internal uint MouseButtons { get; private set; }

        internal UnityEditorSourceInputSnapshot(ISourceHostWindowIO window)
        {
            Window = window ?? throw new ArgumentNullException(nameof(window));
            if (!Application.isEditor || window.Handle != IntPtr.Zero)
                throw new ArgumentException("The Editor input surface must not borrow a Player or Editor HWND.",
                    nameof(window));
            CaptureMouse();
        }

        private void RequireMainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != mainThread)
                throw new InvalidOperationException("Capture Unity SDK input on its main thread.");
        }

        internal bool ReadKeyboardState(Span<byte> destination)
        {
            RequireMainThread();
            Array.Clear(keys, 0, keys.Length);
            var keyboard = Keyboard.current;
            // In Editor Application.isFocused is GameView focus, not Editor desktop focus.
            if (Application.isFocused && keyboard != null)
                foreach (var control in keyboard.allKeys)
                {
                    if (!control.isPressed) continue;
                    int key = (int)ReadLogicalKey(control);
                    if (key > 0 && key < keys.Length) keys[key] = 0x80;
                }

            keys[16] = (byte)(keys[160] | keys[161]);
            keys[17] = (byte)(keys[162] | keys[163]);
            keys[18] = (byte)(keys[164] | keys[165]);
            keys.AsSpan().CopyTo(destination);
            return true;
        }

        internal static SourceKeys ReadLogicalKey(KeyControl key)
        {
            var physical = NativeUnitySourceKeyboardDevice.ReadPhysicalKey(key.keyCode);
            // The SDK queries and caches the active layout's key name. Unicode text itself
            // stays on Keyboard.onTextInput, never derived from a keycode or shifted label.
            if (key.keyCode >= Key.A && key.keyCode <= Key.Z)
            {
                string name = key.displayName;
                if (name != null && name.Length == 1)
                {
                    char letter = char.ToUpperInvariant(name[0]);
                    if (letter >= 'A' && letter <= 'Z') return (SourceKeys)letter;
                }
            }

            return physical;
        }

        internal void CaptureMouse()
        {
            RequireMainThread();
            var mouse = Mouse.current;
            MouseDelta = Vector2.zero;
            MouseButtons = 0;
            if (!Application.isFocused || mouse == null) return;
            mousePosition = mouse.position.ReadUnprocessedValue();
            MouseDelta = mouse.delta.ReadUnprocessedValue();
            MouseButtons = (mouse.leftButton.isPressed ? 1u : 0u) |
                           (mouse.middleButton.isPressed ? 2u : 0u) |
                           (mouse.rightButton.isPressed ? 4u : 0u) |
                           (mouse.backButton.isPressed ? 8u : 0u) |
                           (mouse.forwardButton.isPressed ? 16u : 0u);
        }

        internal uint GetMouseState(out int x, out int y)
        {
            RequireMainThread();
            Window.GetWindowSize(out int width, out int height);
            Window.GetDrawableSize(out int sdkWidth, out int sdkHeight);
            if (width <= 0 || height <= 0 || sdkWidth <= 0 || sdkHeight <= 0)
                throw new InvalidOperationException("The Editor input surface has no drawable size.");
            x = (int)(mousePosition.x * width / sdkWidth);
            y = height - (int)(mousePosition.y * height / sdkHeight);
            return Application.isFocused ? MouseButtons : 0;
        }
    }
}
