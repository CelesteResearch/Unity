using System;
using System.Collections.Generic;
using Celeste.Input;
using Celeste.Input.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace Celeste.Unity
{
    // Only physical sampling lives here. GameInput retains its original buffers,
    // thresholds, overlapping directions and menu-repeat clock.
    public static class ModernKeyboard
    {
#if UNITY_EDITOR
        private sealed class EditorKeys
        {
            public readonly HashSet<Key> Held = new HashSet<Key>(), Down = new HashSet<Key>(), Up = new HashSet<Key>();
            public void Sample(Keyboard keyboard)
            {
                Down.Clear(); Up.Clear();
                foreach (var control in keyboard.allKeys)
                {
                    bool value = control.ReadValue() >= control.pressPointOrDefault;
                    bool previous = Held.Contains(control.keyCode);
                    if (value && !previous) Down.Add(control.keyCode);
                    if (!value && previous) Up.Add(control.keyCode);
                    if (value) Held.Add(control.keyCode); else Held.Remove(control.keyCode);
                }
            }
        }
        private static readonly Dictionary<Keyboard, EditorKeys> editorKeys = new Dictionary<Keyboard, EditorKeys>();
        private static readonly List<Keyboard> removedKeyboards = new List<Keyboard>();
        static ModernKeyboard() => InputSystem.onAfterUpdate += SampleEditor;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEditorState()
        {
            editorKeys.Clear(); removedKeyboards.Clear();
            InputSystem.onAfterUpdate -= SampleEditor; InputSystem.onAfterUpdate += SampleEditor;
        }
        private static void SampleEditor()
        {
            if (Application.isPlaying || InputState.currentUpdateType != InputUpdateType.Editor) return;
            removedKeyboards.Clear();
            foreach (var keyboard in editorKeys.Keys) if (!keyboard.added) removedKeyboards.Add(keyboard);
            foreach (var keyboard in removedKeyboards) editorKeys.Remove(keyboard);
            foreach (var device in InputSystem.devices)
                if (device is Keyboard keyboard)
                {
                    if (!editorKeys.TryGetValue(keyboard, out var state)) editorKeys.Add(keyboard, state =
 new EditorKeys());
                    state.Sample(keyboard);
                }
        }
        private static EditorKeys EditorState(Keyboard keyboard)
        {
            if (!editorKeys.TryGetValue(keyboard, out var state))
            { editorKeys.Add(keyboard, state = new EditorKeys()); state.Sample(keyboard); }
            return state;
        }
#endif
        public static bool GetKey(KeyCode key) => GetKey(key, Keyboard.current);
        public static bool GetKeyDown(KeyCode key) => GetKeyDown(key, Keyboard.current);
        public static bool GetKeyUp(KeyCode key) => GetKeyUp(key, Keyboard.current);
        public static bool GetKey(KeyCode key, Keyboard keyboard) => Control(key, keyboard)?.isPressed ?? false;

        public static bool GetKeyDown(KeyCode key, Keyboard keyboard)
        {
            var control = Control(key, keyboard);
            if (control == null) return false;
#if UNITY_EDITOR
            // Input System 1.20.0 Editor buffer flips do not advance the device's
            // player-step count used by ButtonControl.wasPressedThisFrame.
            // Capture raw Editor updates explicitly; player updates keep the
            // package's native frame-edge behavior.
            if (!Application.isPlaying && InputState.currentUpdateType == InputUpdateType.Editor)
                return EditorState(keyboard).Down.Contains(control.keyCode);
#endif
            return control.wasPressedThisFrame;
        }

        public static bool GetKeyUp(KeyCode key, Keyboard keyboard)
        {
            var control = Control(key, keyboard);
            if (control == null) return false;
#if UNITY_EDITOR
            if (!Application.isPlaying && InputState.currentUpdateType == InputUpdateType.Editor)
                return EditorState(keyboard).Up.Contains(control.keyCode);
#endif
            return control.wasReleasedThisFrame;
        }

        private static KeyControl Control(KeyCode key, Keyboard keyboard)
        {
            if (keyboard == null || !keyboard.added || !TryMapKey(key, out Key mapped)) return null;
            return keyboard[mapped];
        }

        // Keep the existing Inspector KeyCode bindings and original key artwork.
        // Input System names digit/numpad/modifier keys differently; enum values
        // are not interchangeable (Digit0 also follows Digit9).
        public static bool TryMapKey(KeyCode source, out Key target)
        {
            if (source >= KeyCode.A && source <= KeyCode.Z) target = Key.A + (source - KeyCode.A);
            else if (source >= KeyCode.Alpha1 && source <= KeyCode.Alpha9)
                target = Key.Digit1 + (source - KeyCode.Alpha1);
            else if (source >= KeyCode.Keypad0 && source <= KeyCode.Keypad9)
                target = Key.Numpad0 + (source - KeyCode.Keypad0);
            else if (source >= KeyCode.F1 && source <= KeyCode.F12) target = Key.F1 + (source - KeyCode.F1);
            else if (source >= KeyCode.F13 && source <= KeyCode.F15) target = Key.F13 + (source - KeyCode.F13);
            else
                switch (source)
                {
                    case KeyCode.Alpha0: target = Key.Digit0; break;
                    case KeyCode.Return: target = Key.Enter; break;
                    case KeyCode.KeypadEnter: target = Key.NumpadEnter; break;
                    case KeyCode.KeypadDivide: target = Key.NumpadDivide; break;
                    case KeyCode.KeypadMultiply: target = Key.NumpadMultiply; break;
                    case KeyCode.KeypadMinus: target = Key.NumpadMinus; break;
                    case KeyCode.KeypadPlus: target = Key.NumpadPlus; break;
                    case KeyCode.KeypadPeriod: target = Key.NumpadPeriod; break;
                    case KeyCode.KeypadEquals: target = Key.NumpadEquals; break;
                    case KeyCode.LeftControl: target = Key.LeftCtrl; break;
                    case KeyCode.RightControl: target = Key.RightCtrl; break;
                    case KeyCode.LeftMeta:
                    case KeyCode.LeftWindows: target = Key.LeftMeta; break;
                    case KeyCode.RightMeta:
                    case KeyCode.RightWindows: target = Key.RightMeta; break;
                    case KeyCode.Menu: target = Key.ContextMenu; break;
                    case KeyCode.Print: target = Key.PrintScreen; break;
                    case KeyCode.AltGr: target = Key.RightAlt; break;
                    default:
                        // Includes punctuation, arrows, locks and left/right Shift/Alt.
                        // Unknown and non-keyboard KeyCodes are intentionally unbound.
                        return Enum.TryParse(source.ToString(), true, out target) && target != Key.None &&
                               Enum.IsDefined(typeof(Key), target);
                }

            return true;
        }
    }

    public sealed class ModernKeyboardBinding : IInputBinding
    {
        public KeyCode[] Keys;
        public bool Held, Press, Release;
        private readonly Dictionary<KeyCode, bool> previous = new Dictionary<KeyCode, bool>();

        public void Sample() => Sample(Keys);
        public void Sample(KeyCode[] keys) => Sample(keys, Keyboard.current);

        public void Sample(KeyCode[] keys, Keyboard keyboard)
        {
            Held = false;
            foreach (var key in keys ?? Array.Empty<KeyCode>())
            {
                bool down = ModernKeyboard.GetKey(key, keyboard);
                previous.TryGetValue(key, out bool before);
                Press |= down && !before;
                Release |= !down && before;
                Held |= down;
                previous[key] = down;
            }
        }

        public void SampleManual(bool down)
        {
            Press |= down && !Held;
            Release |= !down && Held;
            Held = down;
        }

        public void EndSimulationFrame() => Press = Release = false;
        public bool Check(int gamepadIndex, float threshold) => Held;
        public bool Pressed(int gamepadIndex, float threshold) => Press;
        public bool Released(int gamepadIndex, float threshold) => Release;
        public float Axis(int gamepadIndex, float threshold) => Held ? 1 : 0;
    }
}
