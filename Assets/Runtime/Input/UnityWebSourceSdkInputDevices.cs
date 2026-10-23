using System;
using Celeste.Core.Geometry;
using Celeste.Unity.Web.Input;
using Celeste.Graphics.Rendering.Devices;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Celeste.Unity.Input
{
    internal sealed class UnityWebSourceSdkInputDevices : IUnitySourceSdkInputDevices
    {
        private readonly ISourceHostWindowIO window;
        private bool textActive;
        private readonly UnityWebTouchInput touch;
        public IUnitySourceKeyboardDevice Keyboard { get; } = new UnityWebSourceKeyboardDevice();
        public IUnitySourceMouseTouchDevice MouseTouch { get; }
        public NativeUnitySourceGamePadDevice GamePads { get; } = new NativeUnitySourceGamePadDevice();

        internal UnityWebSourceSdkInputDevices(ISourceHostWindowIO window)
        {
            this.window = window ?? throw new ArgumentNullException(nameof(window));
            MouseTouch = new UnityWebSourceMouseTouchDevice(window);
            touch = new UnityWebTouchInput();
        }

        internal void BindTouchController(Action<int> controllerUsed) => touch.ControllerUsed = controllerUsed;

        public DateTime Now => DateTime.Now;
        public int TickCount => Environment.TickCount;
        public bool IsTextInputActive() => textActive;

        public bool IsScreenKeyboardShown(IntPtr handle)
        {
            if (handle != window.Handle) throw new ArgumentException("Use the actual canvas surface.");
            return TouchScreenKeyboard.visible;
        }

        public void StartTextInput()
        {
            textActive = true;
            UnityEngine.InputSystem.Keyboard.current?.SetIMEEnabled(true);
        }

        public void StopTextInput()
        {
            UnityEngine.InputSystem.Keyboard.current?.SetIMEEnabled(false);
            textActive = false;
        }

        public void SetTextInputRectangle(IntRect rectangle) =>
            UnityEngine.InputSystem.Keyboard.current?.SetIMECursorPosition(new Vector2(rectangle.X, rectangle.Y));

        public void Dispose()
        {
            StopTextInput();
            touch.Dispose();
            GamePads.Dispose();
        }
    }
}
