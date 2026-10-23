using Celeste.Input.Devices.GamePads;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.XInput;
using UnityEngine.InputSystem.HID;
using UnityEngine.InputSystem.Utilities;

namespace Celeste.Unity.Input
{
    internal sealed class NativeUnitySourceGamePadDevice : ISourceSDLGamePadDevice, IDisposable
    {
        // Exact installed SDK RMBL physical command ABI. The SDK's matching struct is
        // internal; using public ExecuteCommand avoids assuming void SetMotorSpeeds succeeded.
        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct PhysicalRumbleCommand : IInputDeviceCommandInfo
        {
            [FieldOffset(0)] public InputDeviceCommand Base;
            [FieldOffset(8)] public float Low;
            [FieldOffset(12)] public float High;
            public FourCC typeStatic => new FourCC('R', 'M', 'B', 'L');

            internal static PhysicalRumbleCommand Create(float low, float high) => new PhysicalRumbleCommand
                { Base = new InputDeviceCommand(new FourCC('R', 'M', 'B', 'L'), 16), Low = low, High = high };
        }

        // Only SDK handle lifetime/name allocation and physical haptic expiry live here.
        private sealed class Loan
        {
            internal Gamepad Pad;
            internal IntPtr Name;
            internal double RumbleUntil;
        }

        private readonly HashSet<IntPtr> loans = new HashSet<IntPtr>();
        private static Loan Borrow(IntPtr handle) => (Loan)GCHandle.FromIntPtr(handle).Target;

        internal void ExpirePhysicalRumble()
        {
            foreach (var handle in loans)
            {
                var loan = Borrow(handle);
                if (loan.RumbleUntil > 0 && Time.realtimeSinceStartupAsDouble >= loan.RumbleUntil)
                {
                    StopPhysicalRumble(loan.Pad);
                    loan.RumbleUntil = 0;
                }
            }
        }

        private static void StopPhysicalRumble(Gamepad pad)
        {
            var stop = PhysicalRumbleCommand.Create(0, 0);
            pad.ExecuteCommand(ref stop);
        }

        public void ClearError()
        {
            /* Unity ExecuteCommand returns its own physical result; there is no SDL error buffer. */
        }

        public IntPtr Open(int deviceIndex)
        {
            if (deviceIndex < 0 || deviceIndex >= Gamepad.all.Count) return IntPtr.Zero;
            var pad = Gamepad.all[deviceIndex];
            var loan = new Loan { Pad = pad, Name = Marshal.StringToCoTaskMemUTF8(pad.displayName) };
            var handle = GCHandle.ToIntPtr(GCHandle.Alloc(loan));
            loans.Add(handle);
            return handle;
        }

        public IntPtr GetJoystick(IntPtr controller) => controller;
        public int GetJoystickInstanceID(IntPtr joystick) => Borrow(joystick).Pad.deviceId;

        public int GetJoystickType(IntPtr joystick)
        {
            var pad = Borrow(joystick).Pad;
            if (pad is XInputController input)
                return (int)input.subType switch { 2 => 2, 3 => 3, 4 => 4, 5 => 5, 6 or 7 or 11 => 6, 8 => 7, _ => 1 };
            return 1; // Other devices are explicitly actual Unity Gamepad layouts.
        }

        public IntPtr GetNamePointer(IntPtr controller) => Borrow(controller).Name;

        public IntPtr GetMappingPointer(IntPtr controller) =>
            Marshal.StringToCoTaskMemUTF8(Borrow(controller).Pad.layout);

        public void Free(IntPtr pointer) => Marshal.FreeCoTaskMem(pointer);

        public void Close(IntPtr controller)
        {
            if (!loans.Remove(controller))
                throw new ArgumentException("The Unity physical controller loan is not owned.", nameof(controller));
            var handle = GCHandle.FromIntPtr(controller);
            var loan = (Loan)handle.Target;
            if (loan.Pad.added) StopPhysicalRumble(loan.Pad);
            Marshal.FreeCoTaskMem(loan.Name);
            handle.Free();
        }

        public short GetAxis(IntPtr controller, SourceSDLGamePadAxis axis)
        {
            var pad = Borrow(controller).Pad;
            if (!Application.isFocused || !pad.added) return 0;
            float value = axis switch
            {
                SourceSDLGamePadAxis.LEFTX => pad.leftStick.x.ReadUnprocessedValue(),
                SourceSDLGamePadAxis.LEFTY => -pad.leftStick.y.ReadUnprocessedValue(),
                SourceSDLGamePadAxis.RIGHTX => pad.rightStick.x.ReadUnprocessedValue(),
                SourceSDLGamePadAxis.RIGHTY => -pad.rightStick.y.ReadUnprocessedValue(),
                SourceSDLGamePadAxis.TRIGGERLEFT => pad.leftTrigger.ReadUnprocessedValue(),
                SourceSDLGamePadAxis.TRIGGERRIGHT => pad.rightTrigger.ReadUnprocessedValue(), _ => 0
            };
            // Physical signed-16 transport only; the source SDK retains dead-zone processing.
            return (short)Mathf.RoundToInt(Mathf.Clamp(value, -1f, 1f) * (value < 0 ? 32768f : 32767f));
        }

        private static ButtonControl Button(Gamepad pad, SourceSDLGamePadButton button) => button switch
        {
            SourceSDLGamePadButton.A => pad.buttonSouth, SourceSDLGamePadButton.B => pad.buttonEast,
            SourceSDLGamePadButton.X => pad.buttonWest, SourceSDLGamePadButton.Y => pad.buttonNorth,
            SourceSDLGamePadButton.BACK => pad.selectButton, SourceSDLGamePadButton.START => pad.startButton,
            SourceSDLGamePadButton.LEFTSTICK => pad.leftStickButton,
            SourceSDLGamePadButton.RIGHTSTICK => pad.rightStickButton,
            SourceSDLGamePadButton.LEFTSHOULDER => pad.leftShoulder,
            SourceSDLGamePadButton.RIGHTSHOULDER => pad.rightShoulder,
            SourceSDLGamePadButton.DPAD_UP => pad.dpad.up, SourceSDLGamePadButton.DPAD_DOWN => pad.dpad.down,
            SourceSDLGamePadButton.DPAD_LEFT => pad.dpad.left, SourceSDLGamePadButton.DPAD_RIGHT => pad.dpad.right,
            SourceSDLGamePadButton.TOUCHPAD => pad is DualShockGamepad ds ? ds.touchpadButton : null, _ => null
        };

        public byte GetButton(IntPtr controller, SourceSDLGamePadButton button)
            => Application.isFocused && Borrow(controller).Pad.added &&
               Button(Borrow(controller).Pad, button)?.isPressed == true
                ? (byte)1
                : (byte)0;

        public int GetButtonBindingType(IntPtr controller, SourceSDLGamePadButton button) =>
            Button(Borrow(controller).Pad, button) != null ? 1 : 0;

        public int GetAxisBindingType(IntPtr controller, SourceSDLGamePadAxis axis) =>
            axis >= SourceSDLGamePadAxis.LEFTX && axis <= SourceSDLGamePadAxis.TRIGGERRIGHT ? 2 : 0;

        public int Rumble(IntPtr controller, ushort left, ushort right, uint duration)
        {
            var loan = Borrow(controller);
            var command = PhysicalRumbleCommand.Create(left / 65535f, right / 65535f);
            if (!loan.Pad.added || loan.Pad.ExecuteCommand(ref command) < 0) return -1;
            loan.RumbleUntil = duration == uint.MaxValue ? 0 : Time.realtimeSinceStartupAsDouble + duration / 1000.0;
            return 0;
        }

        public int RumbleTriggers(IntPtr controller, ushort left, ushort right, uint duration) => -1;

        public int HasLED(IntPtr controller) =>
#if UNITY_EDITOR || UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_WSA || PACKAGE_DOCS_GENERATION
            Borrow(controller).Pad is DualShock4GamepadHID or DualSenseGamepadHID ? 1 : 0;
#else
            0;
#endif

        public int SetLED(IntPtr controller, byte red, byte green, byte blue)
        {
            if (HasLED(controller) == 0) return -1;
            ((DualShockGamepad)Borrow(controller).Pad).SetLightBarColor(
                new Color(red / 255f, green / 255f, blue / 255f));
            return 0;
        }

        public int HasSensor(IntPtr controller, SourceSDLGamePadSensor sensor) => 0;
        public int IsSensorEnabled(IntPtr controller, SourceSDLGamePadSensor sensor) => 0;
        public int SetSensorEnabled(IntPtr controller, SourceSDLGamePadSensor sensor, int enabled) => -1;
        public int GetSensorData(IntPtr controller, SourceSDLGamePadSensor sensor, IntPtr data, int count) => -1;

        private static HID.HIDDeviceDescriptor Descriptor(Gamepad pad)
            => string.IsNullOrEmpty(pad.description.capabilities)
                ? default
                : HID.HIDDeviceDescriptor.FromJson(pad.description.capabilities);

        public ushort GetJoystickVendor(IntPtr joystick) => (ushort)Descriptor(Borrow(joystick).Pad).vendorId;
        public ushort GetJoystickProduct(IntPtr joystick) => (ushort)Descriptor(Borrow(joystick).Pad).productId;

        public SourceSDLGamePadControllerType GetControllerType(IntPtr controller)
        {
            var pad = Borrow(controller).Pad;
            // Unity's generic XInput capabilities do not identify Xbox generation.
            if (pad is XInputController) return SourceSDLGamePadControllerType.UNKNOWN;
            if (pad is DualShockGamepad)
                return pad.layout.Contains("DualSense")
                    ? SourceSDLGamePadControllerType.PS5
                    : SourceSDLGamePadControllerType.PS4;
            return SourceSDLGamePadControllerType.UNKNOWN;
        }

        public void Dispose()
        {
            foreach (var handle in new List<IntPtr>(loans)) Close(handle);
        }
    }
}
