using Celeste.Input.Devices.Keyboard;
using UnityEngine.InputSystem.Controls;

namespace Celeste.Unity.Input
{
    internal interface IUnitySourceKeyboardDevice : ISourceKeyboardDevice
    {
        SourceKeys ReadLogicalKey(KeyControl key);
    }
}
