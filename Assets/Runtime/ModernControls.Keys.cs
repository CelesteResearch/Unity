using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Simulation.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;
using Celeste.Input.Gameplay.Bindings;

namespace Celeste.Unity
{
    public sealed partial class ModernControls
    {
        private static readonly Dictionary<int, string> keyNames = new Dictionary<int, string>
        {
            { 65, "A" },
            { 107, "Add" },
            { 93, "Apps" },
            { 246, "Attn" },
            { 66, "B" },
            { 8, "Back" },
            { 166, "BrowserBack" },
            { 171, "BrowserFavorites" },
            { 167, "BrowserForward" },
            { 172, "BrowserHome" },
            { 168, "BrowserRefresh" },
            { 170, "BrowserSearch" },
            { 169, "BrowserStop" },
            { 67, "C" },
            { 20, "CapsLock" },
            { 247, "Crsel" },
            { 68, "D" },
            { 48, "D0" },
            { 49, "D1" },
            { 50, "D2" },
            { 51, "D3" },
            { 52, "D4" },
            { 53, "D5" },
            { 54, "D6" },
            { 55, "D7" },
            { 56, "D8" },
            { 57, "D9" },
            { 110, "Decimal" },
            { 46, "Delete" },
            { 111, "Divide" },
            { 40, "Down" },
            { 69, "E" },
            { 35, "End" },
            { 13, "Enter" },
            { 249, "EraseEof" },
            { 27, "Escape" },
            { 43, "Execute" },
            { 248, "Exsel" },
            { 70, "F" },
            { 112, "F1" },
            { 121, "F10" },
            { 122, "F11" },
            { 123, "F12" },
            { 124, "F13" },
            { 125, "F14" },
            { 126, "F15" },
            { 127, "F16" },
            { 128, "F17" },
            { 129, "F18" },
            { 130, "F19" },
            { 113, "F2" },
            { 131, "F20" },
            { 132, "F21" },
            { 133, "F22" },
            { 134, "F23" },
            { 135, "F24" },
            { 114, "F3" },
            { 115, "F4" },
            { 116, "F5" },
            { 117, "F6" },
            { 118, "F7" },
            { 119, "F8" },
            { 120, "F9" },
            { 71, "G" },
            { 72, "H" },
            { 47, "Help" },
            { 36, "Home" },
            { 73, "I" },
            { 28, "ImeConvert" },
            { 29, "ImeNoConvert" },
            { 45, "Insert" },
            { 74, "J" },
            { 75, "K" },
            { 21, "Kana" },
            { 25, "Kanji" },
            { 76, "L" },
            { 182, "LaunchApplication1" },
            { 183, "LaunchApplication2" },
            { 180, "LaunchMail" },
            { 162, "LeftControl" },
            { 37, "Left" },
            { 164, "LeftAlt" },
            { 160, "LeftShift" },
            { 91, "LeftWindows" },
            { 77, "M" },
            { 176, "MediaNextTrack" },
            { 179, "MediaPlayPause" },
            { 177, "MediaPreviousTrack" },
            { 178, "MediaStop" },
            { 106, "Multiply" },
            { 78, "N" },
            { 0, "None" },
            { 144, "NumLock" },
            { 96, "NumPad0" },
            { 97, "NumPad1" },
            { 98, "NumPad2" },
            { 99, "NumPad3" },
            { 100, "NumPad4" },
            { 101, "NumPad5" },
            { 102, "NumPad6" },
            { 103, "NumPad7" },
            { 104, "NumPad8" },
            { 105, "NumPad9" },
            { 79, "O" },
            { 243, "OemAuto" },
            { 242, "OemCopy" },
            { 244, "OemEnlW" },
            { 186, "OemSemicolon" },
            { 226, "OemBackslash" },
            { 191, "OemQuestion" },
            { 192, "OemTilde" },
            { 219, "OemOpenBrackets" },
            { 220, "OemPipe" },
            { 221, "OemCloseBrackets" },
            { 222, "OemQuotes" },
            { 223, "Oem8" },
            { 254, "OemClear" },
            { 188, "OemComma" },
            { 189, "OemMinus" },
            { 190, "OemPeriod" },
            { 187, "OemPlus" },
            { 80, "P" },
            { 253, "Pa1" },
            { 34, "PageDown" },
            { 33, "PageUp" },
            { 19, "Pause" },
            { 250, "Play" },
            { 42, "Print" },
            { 44, "PrintScreen" },
            { 229, "ProcessKey" },
            { 81, "Q" },
            { 82, "R" },
            { 163, "RightControl" },
            { 39, "Right" },
            { 165, "RightAlt" },
            { 161, "RightShift" },
            { 92, "RightWindows" },
            { 83, "S" },
            { 145, "Scroll" },
            { 41, "Select" },
            { 181, "SelectMedia" },
            { 108, "Separator" },
            { 95, "Sleep" },
            { 32, "Space" },
            { 109, "Subtract" },
            { 84, "T" },
            { 9, "Tab" },
            { 85, "U" },
            { 38, "Up" },
            { 86, "V" },
            { 174, "VolumeDown" },
            { 173, "VolumeMute" },
            { 175, "VolumeUp" },
            { 87, "W" },
            { 88, "X" },
            { 89, "Y" },
            { 90, "Z" },
            { 251, "Zoom" },
            { 202, "ChatPadGreen" },
            { 203, "ChatPadOrange" },
        };

        public static readonly int[] ButtonCodes =
        {
            1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 2048, 4096, 8192, 16384, 32768, 2097152, 4194304, 8388608, 16777216,
            33554432, 67108864, 134217728, 268435456, 536870912, 1073741824
        };

        public static readonly string[] ButtonNames =
        {
            "DPadUp", "DPadDown", "DPadLeft", "DPadRight", "Start", "Back", "LeftStick", "RightStick", "LeftShoulder",
            "RightShoulder", "BigButton", "A", "B", "X", "Y", "LeftThumbstickLeft", "RightTrigger", "LeftTrigger",
            "RightThumbstickUp", "RightThumbstickDown", "RightThumbstickRight", "RightThumbstickLeft",
            "LeftThumbstickUp", "LeftThumbstickDown", "LeftThumbstickRight"
        };

        public static string KeyName(int code) => keyNames.TryGetValue(code, out var name)
            ? name
            : code.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public static string ButtonName(int code)
        {
            int index = Array.IndexOf(ButtonCodes, code);
            if (index >= 0) return ButtonNames[index];
            int remaining = code;
            var names = new List<string>();
            for (int i = 0; i < ButtonCodes.Length; i++)
                if ((remaining & ButtonCodes[i]) == ButtonCodes[i])
                {
                    remaining &= ~ButtonCodes[i];
                    names.Add(ButtonNames[i]);
                }

            return code != 0 && remaining == 0
                ? string.Join(", ", names)
                : code.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static IReadOnlyDictionary<int, string> KeyNames => keyNames;

        public static bool TryMapKey(int code, out Key key)
        {
            if (code >= 65 && code <= 90)
            {
                key = Key.A + (code - 65);
                return true;
            }

            if (code >= 49 && code <= 57)
            {
                key = Key.Digit1 + (code - 49);
                return true;
            }

            if (code >= 96 && code <= 105)
            {
                key = Key.Numpad0 + (code - 96);
                return true;
            }

            if (code >= 112 && code <= 135)
                return Enum.TryParse("F" + (code - 111), out key) && Enum.IsDefined(typeof(Key), key);
            switch (code)
            {
                case 8:
                    key = Key.Backspace;
                    return true;
                case 9:
                    key = Key.Tab;
                    return true;
                case 13:
                    key = Key.Enter;
                    return true;
                case 19:
                    key = Key.Pause;
                    return true;
                case 20:
                    key = Key.CapsLock;
                    return true;
                case 27:
                    key = Key.Escape;
                    return true;
                case 32:
                    key = Key.Space;
                    return true;
                case 33:
                    key = Key.PageUp;
                    return true;
                case 34:
                    key = Key.PageDown;
                    return true;
                case 35:
                    key = Key.End;
                    return true;
                case 36:
                    key = Key.Home;
                    return true;
                case 37:
                    key = Key.LeftArrow;
                    return true;
                case 38:
                    key = Key.UpArrow;
                    return true;
                case 39:
                    key = Key.RightArrow;
                    return true;
                case 40:
                    key = Key.DownArrow;
                    return true;
                case 44:
                    key = Key.PrintScreen;
                    return true;
                case 45:
                    key = Key.Insert;
                    return true;
                case 46:
                    key = Key.Delete;
                    return true;
                case 48:
                    key = Key.Digit0;
                    return true;
                case 91:
                    key = Key.LeftMeta;
                    return true;
                case 92:
                    key = Key.RightMeta;
                    return true;
                case 93:
                    key = Key.ContextMenu;
                    return true;
                case 106:
                    key = Key.NumpadMultiply;
                    return true;
                case 107:
                    key = Key.NumpadPlus;
                    return true;
                case 109:
                    key = Key.NumpadMinus;
                    return true;
                case 110:
                    key = Key.NumpadPeriod;
                    return true;
                case 111:
                    key = Key.NumpadDivide;
                    return true;
                case 144:
                    key = Key.NumLock;
                    return true;
                case 145:
                    key = Key.ScrollLock;
                    return true;
                case 160:
                    key = Key.LeftShift;
                    return true;
                case 161:
                    key = Key.RightShift;
                    return true;
                case 162:
                    key = Key.LeftCtrl;
                    return true;
                case 163:
                    key = Key.RightCtrl;
                    return true;
                case 164:
                    key = Key.LeftAlt;
                    return true;
                case 165:
                    key = Key.RightAlt;
                    return true;
                case 186:
                    key = Key.Semicolon;
                    return true;
                case 187:
                    key = Key.Equals;
                    return true;
                case 188:
                    key = Key.Comma;
                    return true;
                case 189:
                    key = Key.Minus;
                    return true;
                case 190:
                    key = Key.Period;
                    return true;
                case 191:
                    key = Key.Slash;
                    return true;
                case 192:
                    key = Key.Backquote;
                    return true;
                case 219:
                    key = Key.LeftBracket;
                    return true;
                case 220:
                    key = Key.Backslash;
                    return true;
                case 221:
                    key = Key.RightBracket;
                    return true;
                case 222:
                    key = Key.Quote;
                    return true;
                case 226:
                    key = Key.OEM1;
                    return true;
                default:
                    key = Key.None;
                    return false;
            }
        }

        public static KeyCode LegacyKeyCode(int code)
        {
            if (code >= 65 && code <= 90) return KeyCode.A + (code - 65);
            if (code >= 48 && code <= 57) return KeyCode.Alpha0 + (code - 48);
            if (code >= 96 && code <= 105) return KeyCode.Keypad0 + (code - 96);
            if (code >= 112 && code <= 126) return KeyCode.F1 + (code - 112);
            switch (code)
            {
                case 8: return KeyCode.Backspace;
                case 13: return KeyCode.Return;
                case 37: return KeyCode.LeftArrow;
                case 38: return KeyCode.UpArrow;
                case 39: return KeyCode.RightArrow;
                case 40: return KeyCode.DownArrow;
                case 162: return KeyCode.LeftControl;
                case 163: return KeyCode.RightControl;
                case 91: return KeyCode.LeftWindows;
                case 92: return KeyCode.RightWindows;
                case 145: return KeyCode.ScrollLock;
                default: return Enum.TryParse(KeyName(code), out KeyCode key) ? key : KeyCode.None;
            }
        }

        // GuiButton uses first controller entry even if no icon exists; keyboard skips Keys.None.
        public ControlBindingValue GetFirstBinding(ControlBindingState binding, bool attached = false)
        {
            if (!(attached ? AttachedGuiPrefix : CurrentGuiPrefix).Equals("keyboard", StringComparison.Ordinal))
                return binding.Controller.Count > 0 ? binding.Controller[0] : new ControlBindingValue(0, "");
            foreach (var key in binding.Keyboard)
                if (key.Code != 0)
                    return key;
            return new ControlBindingValue(0, "None");
        }
    }
}
