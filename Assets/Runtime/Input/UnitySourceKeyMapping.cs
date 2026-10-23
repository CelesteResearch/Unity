using Celeste.Input.Devices.Keyboard;
using UnityEngine.InputSystem;

namespace Celeste.Unity.Input
{
    internal static class UnitySourceKeyMapping
    {
        internal static SourceKeys ReadPhysicalKey(Key key)
        {
            if (key >= Key.A && key <= Key.Z) return (SourceKeys)(65 + (int)key - (int)Key.A);
            if (key >= Key.Digit1 && key <= Key.Digit9) return (SourceKeys)(49 + (int)key - (int)Key.Digit1);
            if (key >= Key.F1 && key <= Key.F12) return (SourceKeys)(112 + (int)key - (int)Key.F1);
            if (key >= Key.F13 && key <= Key.F24) return (SourceKeys)(124 + (int)key - (int)Key.F13);
            if (key >= Key.Numpad1 && key <= Key.Numpad9) return (SourceKeys)(97 + (int)key - (int)Key.Numpad1);
            return (SourceKeys)(key switch
            {
                Key.Digit0 => 48, Key.Numpad0 => 96, Key.Space => 32, Key.Enter or Key.NumpadEnter => 13,
                Key.Tab => 9, Key.Backspace => 8, Key.Escape => 27, Key.LeftShift => 160, Key.RightShift => 161,
                Key.LeftCtrl => 162, Key.RightCtrl => 163, Key.LeftAlt => 164, Key.RightAlt => 165,
                Key.LeftMeta => 91, Key.RightMeta => 92, Key.ContextMenu => 93, Key.CapsLock => 20,
                Key.NumLock => 144, Key.ScrollLock => 145, Key.Pause => 19, Key.PrintScreen => 44,
                Key.UpArrow => 38, Key.DownArrow => 40, Key.LeftArrow => 37, Key.RightArrow => 39,
                Key.Home => 36, Key.End => 35, Key.PageUp => 33, Key.PageDown => 34, Key.Insert => 45, Key.Delete => 46,
                Key.NumpadMultiply => 106, Key.NumpadPlus => 107, Key.NumpadMinus => 109,
                Key.NumpadPeriod => 110, Key.NumpadDivide => 111, Key.Semicolon => 186, Key.Equals => 187,
                Key.Comma => 188, Key.Minus => 189, Key.Period => 190, Key.Slash => 191, Key.Backquote => 192,
                Key.LeftBracket => 219, Key.Backslash => 220, Key.RightBracket => 221, Key.Quote => 222,
                _ => 0
            });
        }
    }
}
