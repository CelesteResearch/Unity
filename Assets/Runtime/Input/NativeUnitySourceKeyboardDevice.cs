using Celeste.Input.Devices.Keyboard;
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Celeste.Unity.Input
{
    internal sealed class NativeUnitySourceKeyboardDevice : IUnitySourceKeyboardDevice
    {
        private static readonly uint[] LetterScanCodes =
        {
            0x1e, 0x30, 0x2e, 0x20, 0x12, 0x21, 0x22, 0x23, 0x17, 0x24, 0x25, 0x26, 0x32, 0x31, 0x18, 0x19, 0x10, 0x13,
            0x1f, 0x14, 0x16, 0x2f, 0x11, 0x2d, 0x15, 0x2c
        };

        private readonly NativeUnitySourceWindowIO window;
        private readonly UnityEditorSourceInputSnapshot editorSnapshot;

        internal NativeUnitySourceKeyboardDevice(UnityEditorSourceInputSnapshot snapshot)
        {
            editorSnapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        internal NativeUnitySourceKeyboardDevice(NativeUnitySourceWindowIO window)
        {
            this.window = window ?? throw new ArgumentNullException(nameof(window));
        }

        public string GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);
        public bool IsWindows => Application.platform == RuntimePlatform.WindowsPlayer;

        public bool ReadWin32KeyboardState(Span<byte> keys)
        {
            if (keys.Length < 256)
                throw new ArgumentException("The physical Win32 keyboard view needs 256 bytes.", nameof(keys));
            if (editorSnapshot != null) return editorSnapshot.ReadKeyboardState(keys);
            keys.Clear();
            var keyboard = Keyboard.current;
            if (!Application.isFocused || keyboard == null) return true;
            foreach (var control in keyboard.allKeys)
            {
                if (!control.isPressed) continue;
                int vk = (int)ReadLogicalKey(control);
                if (vk > 0 && vk < 256) keys[vk] = 0x80;
            }

            keys[16] = (byte)(keys[160] | keys[161]);
            keys[17] = (byte)(keys[162] | keys[163]);
            keys[18] = (byte)(keys[164] | keys[165]);
            return true;
        }

        public SourceKeys GetKeyFromScancode(SourceKeys scancode)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
                foreach (var control in keyboard.allKeys)
                    if (ReadPhysicalKey(control.keyCode) == scancode)
                        return ReadLogicalKey(control);
            return scancode;
        }

        public SourceKeys ReadLogicalKey(KeyControl key)
        {
            if (editorSnapshot != null || !NativeUnitySourceWindowIO.IsWindows)
                return UnityEditorSourceInputSnapshot.ReadLogicalKey(key);
            var physical = ReadPhysicalKey(key.keyCode);
            uint scan = PhysicalScanCode(key.keyCode);
            if (scan == 0) return physical;
            uint thread = GetWindowThreadProcessId(window.Handle, out _);
            if (thread == 0)
                throw new InvalidOperationException("The actual Unity input window is no longer available.");
            // Use the actual Unity window thread's current keyboard layout; no foreground window.
            return (SourceKeys)MapVirtualKeyEx(scan, 3, GetKeyboardLayout(thread));
        }

        internal static SourceKeys ReadPhysicalKey(Key key) => UnitySourceKeyMapping.ReadPhysicalKey(key);

        private static uint PhysicalScanCode(Key key)
        {
            if (key >= Key.A && key <= Key.Z)
                return LetterScanCodes[(int)key - (int)Key.A];
            if (key >= Key.Digit1 && key <= Key.Digit9) return (uint)(2 + (int)key - (int)Key.Digit1);
            return key switch
            {
                Key.Digit0 => 0x0b, Key.Semicolon => 0x27, Key.Equals => 0x0d, Key.Comma => 0x33,
                Key.Minus => 0x0c, Key.Period => 0x34, Key.Slash => 0x35, Key.Backquote => 0x29,
                Key.LeftBracket => 0x1a, Key.Backslash => 0x2b, Key.RightBracket => 0x1b, Key.Quote => 0x28, _ => 0
            };
        }

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint thread);

        [DllImport("user32.dll", EntryPoint = "MapVirtualKeyExW")]
        private static extern uint MapVirtualKeyEx(uint code, uint mode, IntPtr layout);
    }
}
