using System;
using Celeste.Input.Devices.Keyboard;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Celeste.Unity.Input
{
    internal sealed class UnityWebSourceKeyboardDevice : IUnitySourceKeyboardDevice
    {
        public bool IsWindows => false;
        public string GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);

        public bool ReadWin32KeyboardState(Span<byte> keys)
        {
            if (keys.Length < 256)
                throw new ArgumentException("The source keyboard view needs 256 bytes.", nameof(keys));
            keys.Clear();
            if (!Application.isFocused || Keyboard.current == null) return true;
            foreach (var key in Keyboard.current.allKeys)
            {
                int code = (int)ReadLogicalKey(key);
                if (key.isPressed && code > 0 && code < 256) keys[code] = 0x80;
            }

            keys[16] = (byte)(keys[160] | keys[161]);
            keys[17] = (byte)(keys[162] | keys[163]);
            keys[18] = (byte)(keys[164] | keys[165]);
            return true;
        }

        public SourceKeys ReadLogicalKey(KeyControl key)
        {
            if (key.keyCode >= Key.A && key.keyCode <= Key.Z && key.displayName?.Length == 1)
            {
                char letter = char.ToUpperInvariant(key.displayName[0]);
                if (letter >= 'A' && letter <= 'Z') return (SourceKeys)letter;
            }

            return UnitySourceKeyMapping.ReadPhysicalKey(key.keyCode);
        }

        public SourceKeys GetKeyFromScancode(SourceKeys scancode)
        {
            if (Keyboard.current != null)
                foreach (var key in Keyboard.current.allKeys)
                    if (UnitySourceKeyMapping.ReadPhysicalKey(key.keyCode) == scancode)
                        return ReadLogicalKey(key);
            return scancode;
        }
    }
}
