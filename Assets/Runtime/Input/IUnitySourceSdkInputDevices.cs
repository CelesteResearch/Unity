using System;
using Celeste.Input.Devices.Text;
using Celeste.Framework.Startup;
using Celeste.Input.Devices.Mouse;

namespace Celeste.Unity.Input
{
    internal interface IUnitySourceSdkInputDevices : ISourceFnaInputClock, ISourceTextInputPlatform, IDisposable
    {
        IUnitySourceKeyboardDevice Keyboard { get; }
        IUnitySourceMouseTouchDevice MouseTouch { get; }
        NativeUnitySourceGamePadDevice GamePads { get; }
    }
}
