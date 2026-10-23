using Celeste.Graphics.Rendering.Devices;

namespace Celeste.Unity.Graphics.SourceDevice
{
    public delegate UnitySourceGraphicsDeviceComponents UnitySourceGraphicsDeviceInitializer(
        ref SourceNativePresentationParameters parameters);
}
