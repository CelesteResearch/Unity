using Celeste.Input.Devices.Mouse;

namespace Celeste.Unity.Input
{
    internal interface IUnitySourceMouseTouchDevice : ISourceSDLMouseTouchDevice
    {
        void OnSdkUpdate();
        void Size(out int width, out int height);
        void SdkSize(out int width, out int height);
    }
}
