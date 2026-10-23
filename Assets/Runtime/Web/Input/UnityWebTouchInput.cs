using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Celeste.Unity.Web.Input
{
    // A separate Web gamepad feeds the existing source controller bindings.
    internal sealed class UnityWebTouchInput : IDisposable
    {
        private Gamepad pad;
        private int lastMask;
        private readonly bool installed;
        internal Action<int> ControllerUsed { get; set; }

        internal UnityWebTouchInput()
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer) return;
            CelesteWebTouchInstall();
            installed = true;
            InputSystem.onBeforeUpdate += Poll;
            Poll();
        }

        private void Poll()
        {
            if (InputState.currentUpdateType != InputUpdateType.Dynamic && pad != null) return;
            int snapshot = CelesteWebTouchRead();
            if ((snapshot & 256) == 0 && pad == null) return;
            if (pad == null)
            {
                pad = InputSystem.AddDevice<Gamepad>("Celeste Touch");
                InputSystem.SetDeviceUsage(pad, "TouchControls");
            }

            int mask = Application.isFocused ? snapshot & 255 : 0;
            if (mask != 0) ControllerUsed?.Invoke(pad.deviceId);
            if (mask == lastMask) return;
            lastMask = mask;
            var state = new GamepadState();
            if ((mask & 1) != 0) state = state.WithButton(GamepadButton.DpadLeft);
            if ((mask & 2) != 0) state = state.WithButton(GamepadButton.DpadRight);
            if ((mask & 4) != 0) state = state.WithButton(GamepadButton.DpadUp);
            if ((mask & 8) != 0) state = state.WithButton(GamepadButton.DpadDown);
            if ((mask & 16) != 0) state = state.WithButton(GamepadButton.South);
            if ((mask & 32) != 0) state = state.WithButton(GamepadButton.East);
            if ((mask & 64) != 0) state = state.WithButton(GamepadButton.LeftShoulder);
            if ((mask & 128) != 0) state = state.WithButton(GamepadButton.Start);
            InputSystem.QueueStateEvent(pad, state);
        }

        public void Dispose()
        {
            if (!installed) return;
            InputSystem.onBeforeUpdate -= Poll;
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            pad = null;
            CelesteWebTouchDispose();
        }

        [DllImport("__Internal")]
        private static extern void CelesteWebTouchInstall();

        [DllImport("__Internal")]
        private static extern int CelesteWebTouchRead();

        [DllImport("__Internal")]
        private static extern void CelesteWebTouchDispose();
    }
}
