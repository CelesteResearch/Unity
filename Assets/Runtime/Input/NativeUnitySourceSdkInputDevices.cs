using Celeste.Input.Devices.Text;
using Celeste.Graphics.Rendering.Devices;
using Celeste.Input.Devices.Mouse;
using System;
using System.Collections.Generic;
using Celeste.Core.Geometry;
using Celeste.Framework.Startup;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Celeste.Unity.Input
{
    internal sealed class NativeUnitySourceSdkInputDevices : IUnitySourceSdkInputDevices
    {
        internal NativeUnitySourceKeyboardDevice Keyboard { get; }
        internal NativeUnitySourceMouseTouchDevice MouseTouch { get; }
        internal NativeUnitySourceGamePadDevice GamePads { get; }
        internal ISourceFnaInputClock InputClock => this;
        internal ISourceTextInputPlatform TextInputPlatform => this;
        IUnitySourceKeyboardDevice IUnitySourceSdkInputDevices.Keyboard => Keyboard;
        IUnitySourceMouseTouchDevice IUnitySourceSdkInputDevices.MouseTouch => MouseTouch;
        NativeUnitySourceGamePadDevice IUnitySourceSdkInputDevices.GamePads => GamePads;
        private bool textActive;
        private readonly ISourceHostWindowIO window;

        internal NativeUnitySourceSdkInputDevices(ISourceHostWindowIO actualWindow)
        {
            if (!Application.isEditor && Application.platform != RuntimePlatform.WindowsPlayer &&
                Application.platform != RuntimePlatform.LinuxPlayer &&
                Application.platform != RuntimePlatform.OSXPlayer)
                throw new NotSupportedException(
                    "This physical IO maker requires a desktop Unity Player window.");
            window = actualWindow ?? throw new ArgumentNullException(nameof(actualWindow));
            if (Application.isEditor)
            {
                var snapshot = new UnityEditorSourceInputSnapshot(window);
                Keyboard = new NativeUnitySourceKeyboardDevice(snapshot);
                MouseTouch = new NativeUnitySourceMouseTouchDevice(snapshot);
            }
            else
            {
                var playerWindow = window as NativeUnitySourceWindowIO ??
                                   throw new ArgumentException("The Player must retain its verified native window.",
                                       nameof(actualWindow));
                Keyboard = new NativeUnitySourceKeyboardDevice(playerWindow);
                MouseTouch = new NativeUnitySourceMouseTouchDevice(playerWindow);
            }

            GamePads = new NativeUnitySourceGamePadDevice();
        }

        public DateTime Now => DateTime.Now;
        public int TickCount => Environment.TickCount;
        public bool IsTextInputActive() => textActive;

        public bool IsScreenKeyboardShown(IntPtr actualWindow)
        {
            if (actualWindow != window.Handle)
                throw new ArgumentException("The text-input window must be the actual Unity window.");
            return TouchScreenKeyboard.visible;
        }

        public void StartTextInput()
        {
            textActive = true;
            KeyboardCurrent()?.SetIMEEnabled(true);
        }

        public void StopTextInput()
        {
            KeyboardCurrent()?.SetIMEEnabled(false);
            textActive = false;
        }

        public void SetTextInputRectangle(IntRect rectangle) =>
            KeyboardCurrent()?.SetIMECursorPosition(new Vector2(rectangle.X, rectangle.Y));

        private static UnityEngine.InputSystem.Keyboard KeyboardCurrent() => UnityEngine.InputSystem.Keyboard.current;

        public void Dispose()
        {
            StopTextInput();
            GamePads.Dispose();
        }
    }
}
