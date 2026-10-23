using Celeste.Input.Runtime;
using Celeste.Input.Devices.Text;
using Celeste.Input.Devices.Keyboard;
using Celeste.Input.Devices.GamePads;
using Celeste.Input.Bindings;
using System;
using System.Collections.Generic;
using Celeste.Core.Geometry;
using Celeste.Framework.Startup;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Celeste.Unity.Input
{
// Subscribes only after the actual shared input/Game owners have been constructed.
// No mirrored key list, source controller state, source mouse metrics, or dispatch queue.
    internal sealed class NativeUnitySourceSdkInputBridge : IDisposable
    {
        private readonly IUnitySourceSdkInputDevices devices;
        private readonly Func<bool> physicalInputEnabled;
        private readonly SourceGameProcess actualGame;
        private readonly SourceFnaKeyboardStateProcess keyboardState;
        private readonly SourceFnaInputProcess fnaInput;
        private readonly SourceFnaGamePadProcess gamePads;
        private readonly SourceFnaTextInputBindings textBindings;
        private readonly SourceFnaTextInputEvents textInput;
        private Keyboard subscribedKeyboard;
        private float physicalWheelRemainder;
        private readonly HashSet<char> pendingControlText = new HashSet<char>();
        private bool disposed;

        internal NativeUnitySourceSdkInputBridge(IUnitySourceSdkInputDevices devices, SourceGameProcess actualGame,
            SourceFnaKeyboardStateProcess keyboardState, SourceFnaInputProcess fnaInput,
            SourceFnaGamePadProcess gamePads,
            SourceFnaTextInputBindings textBindings, SourceFnaTextInputEvents textInput,
            Func<bool> physicalInputEnabled = null)
        {
            this.physicalInputEnabled = physicalInputEnabled;
            this.devices = devices ?? throw new ArgumentNullException(nameof(devices));
            this.actualGame = actualGame ?? throw new ArgumentNullException(nameof(actualGame));
            this.keyboardState = keyboardState ?? throw new ArgumentNullException(nameof(keyboardState));
            this.fnaInput = fnaInput ?? throw new ArgumentNullException(nameof(fnaInput));
            this.gamePads = gamePads ?? throw new ArgumentNullException(nameof(gamePads));
            this.textBindings = textBindings ?? throw new ArgumentNullException(nameof(textBindings));
            this.textInput = textInput ?? throw new ArgumentNullException(nameof(textInput));
            if (InputSystem.settings.updateMode != InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)
                throw new InvalidOperationException(
                    "The actual source host requires the Unity SDK Dynamic input phase.");
            BindKeyboard();
            for (int i = 0; i < Gamepad.all.Count; ++i) gamePads.Platform.AddInstance(i);
            InputSystem.onAfterUpdate += AfterSdkUpdate;
            InputSystem.onDeviceChange += DeviceChanged;
            InputSystem.onEvent += PhysicalEvent;
        }

        private void BindKeyboard()
        {
            if (subscribedKeyboard == Keyboard.current) return;
            if (subscribedKeyboard != null)
            {
                subscribedKeyboard.onTextInput -= Text;
                subscribedKeyboard.onIMECompositionChange -= Composition;
            }

            subscribedKeyboard = Keyboard.current;
            if (subscribedKeyboard != null)
            {
                subscribedKeyboard.onTextInput += Text;
                subscribedKeyboard.onIMECompositionChange += Composition;
            }
        }

        private void Text(char value)
        {
            if (!PhysicalInputEnabled()) return;
            SynchronizeKeys();
            if (!Application.isFocused || !devices.IsTextInputActive() || actualGame.SuppressPhysicalTextInput) return;
            // Unity emits WM_CHAR control characters as well as key packets. The source
            // key handler already emitted the initial control character; consume that
            // one physical duplicate, and route later SDK repeat chars to the same handler.
            foreach (var binding in textBindings.TextInputBindings)
                if (textBindings.TextInputCharacters[binding.Value] == value &&
                    keyboardState.Keys.Contains(binding.Key))
                {
                    if (!pendingControlText.Remove(value))
                        actualGame.OnPhysicalKeyDown(keyboardState, textBindings, textInput, binding.Key, 1);
                    return;
                }

            textInput.OnTextInput(value);
        }

        private void Composition(IMECompositionString value)
        {
            if (!PhysicalInputEnabled()) return;
            if (Application.isFocused && devices.IsTextInputActive())
            {
                string text = value.ToString();
                textInput.OnTextEditing(text, text.Length, 0);
            }
        }

        // Call at the actual host PollEvents source point. Unity has already sampled its
        // physical input in the Dynamic update; do not pump SDL or call InputSystem.Update twice.
        internal void PollEvents()
        {
            if (disposed) throw new ObjectDisposedException(nameof(NativeUnitySourceSdkInputBridge));
            if (!PhysicalInputEnabled())
            {
                devices.GamePads.ExpirePhysicalRumble();
                return;
            }

            actualGame.IsActive = Application.isFocused;
            BindKeyboard();
            SynchronizeKeys();
            devices.GamePads.ExpirePhysicalRumble();
        }

        private bool PhysicalInputEnabled()
        {
            bool enabled = physicalInputEnabled == null || physicalInputEnabled();
            // A control key's duplicate Unicode packet may arrive during the suspended interval.
            // Dropping that packet must not leave a stale duplicate marker after resume.
            if (!enabled) pendingControlText.Clear();
            return enabled;
        }

        private void SynchronizeKeys()
        {
            Span<byte> keys = stackalloc byte[256];
            devices.Keyboard.ReadWin32KeyboardState(keys);
            // Source Keys is the sole process list. Event control/text semantics stay in
            // the existing SourceGameProcess methods, including suppression/control chords.
            for (int i = keyboardState.Keys.Count - 1; i >= 0; --i)
            {
                var key = keyboardState.Keys[i];
                if ((int)key >= 256 || keys[(int)key] == 0)
                    actualGame.OnPhysicalKeyUp(keyboardState, textBindings, key);
            }

            for (int i = 160; i <= 165; ++i)
                if (keys[i] != 0 && !keyboardState.Keys.Contains((SourceKeys)i))
                    actualGame.OnPhysicalKeyDown(keyboardState, textBindings, textInput, (SourceKeys)i, 0);
            for (int i = 1; i < 256; ++i)
                if (keys[i] != 0 && i != 16 && i != 17 && i != 18 && !keyboardState.Keys.Contains((SourceKeys)i))
                    actualGame.OnPhysicalKeyDown(keyboardState, textBindings, textInput, (SourceKeys)i, 0);
        }

        private void PhysicalEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (disposed || !PhysicalInputEnabled() || !Application.isFocused ||
                InputState.currentUpdateType != InputUpdateType.Dynamic ||
                (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())) return;
            if (device is Keyboard keyboard)
            {
                // SDK physical event order is retained. Source control chords must see
                // modifier presses before regular keys within the same state packet.
                foreach (var key in keyboard.allKeys)
                    if (key.keyCode is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift
                        or Key.RightShift)
                        DispatchKey(key, eventPtr);
                foreach (var key in keyboard.allKeys)
                    if (key.keyCode is not (Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                        or Key.LeftShift or Key.RightShift))
                        DispatchKey(key, eventPtr);
            }
            else if (device is Mouse mouse)
            {
                DispatchClick(mouse.leftButton, 1, eventPtr);
                DispatchClick(mouse.middleButton, 2, eventPtr);
                DispatchClick(mouse.rightButton, 3, eventPtr);
                DispatchClick(mouse.backButton, 4, eventPtr);
                DispatchClick(mouse.forwardButton, 5, eventPtr);
            }
        }

        private void DispatchKey(UnityEngine.InputSystem.Controls.KeyControl control, InputEventPtr eventPtr)
        {
            if (!control.ReadValueFromEvent(eventPtr, out float value)) return;
            var key = devices.Keyboard.ReadLogicalKey(control);
            if ((int)key == 0) return;
            if (value >= control.pressPointOrDefault && !keyboardState.Keys.Contains(key))
            {
                actualGame.OnPhysicalKeyDown(keyboardState, textBindings, textInput, key, 0);
                if (textBindings.TextInputBindings.TryGetValue(key, out int index))
                    pendingControlText.Add(textBindings.TextInputCharacters[index]);
            }
            else if (value < control.pressPointOrDefault && keyboardState.Keys.Contains(key))
            {
                actualGame.OnPhysicalKeyUp(keyboardState, textBindings, key);
                if (textBindings.TextInputBindings.TryGetValue(key, out int index))
                    pendingControlText.Remove(textBindings.TextInputCharacters[index]);
            }
        }

        private void DispatchClick(UnityEngine.InputSystem.Controls.ButtonControl control, byte button,
            InputEventPtr eventPtr)
        {
            if (control.ReadValueFromEvent(eventPtr, out float value) && value >= control.pressPointOrDefault &&
                !control.isPressed) fnaInput.OnMouseButtonDown(button);
        }

        private void AfterSdkUpdate()
        {
            if (disposed || InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            if (!PhysicalInputEnabled())
            {
                devices.GamePads.ExpirePhysicalRumble();
                return;
            }

            PollEvents();
            devices.MouseTouch.OnSdkUpdate();
            if (!Application.isFocused) return;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                physicalWheelRemainder += mouse.scroll.ReadUnprocessedValue().y / 120f;
                int ticks = (int)physicalWheelRemainder;
                if (ticks != 0)
                {
                    fnaInput.OnMouseWheel(ticks);
                    physicalWheelRemainder -= ticks;
                }
            }

            devices.MouseTouch.SdkSize(out int width, out int height);
            foreach (var device in InputSystem.devices)
                if (device is Touchscreen screen)
                    foreach (var touch in screen.touches)
                    {
                        var phase = touch.phase.ReadValue();
                        var position = touch.position.ReadUnprocessedValue();
                        var delta = touch.delta.ReadUnprocessedValue();
                        long id = UnitySourceTouchIdentity.PhysicalFingerId(screen.deviceId,
                            touch.touchId.ReadValue());
                        float x = position.x / width, y = 1f - position.y / height;
                        switch (phase)
                        {
                            case UnityEngine.InputSystem.TouchPhase.Began: fnaInput.OnFingerDown(id, x, y); break;
                            case UnityEngine.InputSystem.TouchPhase.Moved:
                                fnaInput.OnFingerMotion(id, x, y, delta.x / width, -delta.y / height); break;
                            case UnityEngine.InputSystem.TouchPhase.Ended:
                            case UnityEngine.InputSystem.TouchPhase.Canceled: fnaInput.OnFingerUp(id, x, y); break;
                        }
                    }
        }

        private void DeviceChanged(InputDevice device, InputDeviceChange change)
        {
            BindKeyboard();
            if (device is not Gamepad pad) return;
            if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected)
                gamePads.Platform.RemoveInstance(pad.deviceId);
            else if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected)
                for (int i = 0; i < Gamepad.all.Count; ++i)
                    if (Gamepad.all[i] == pad)
                    {
                        gamePads.Platform.AddInstance(i);
                        break;
                    }
        }

        public void Dispose()
        {
            if (disposed) return;
            InputSystem.onAfterUpdate -= AfterSdkUpdate;
            InputSystem.onDeviceChange -= DeviceChanged;
            InputSystem.onEvent -= PhysicalEvent;
            if (subscribedKeyboard != null)
            {
                subscribedKeyboard.onTextInput -= Text;
                subscribedKeyboard.onIMECompositionChange -= Composition;
            }

            foreach (var pad in Gamepad.all) gamePads.Platform.RemoveInstance(pad.deviceId);
            disposed = true;
        }
    }
}
