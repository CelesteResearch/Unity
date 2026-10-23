using Celeste.Graphics.Content.VirtualAssets;
using Celeste.Graphics.Rendering.Textures;

namespace Celeste.Unity.Web
{
    internal sealed class UnityWebStoreIconTexture : VirtualTexture
    {
        public UnityWebStoreIconTexture(VirtualContent owner, SourceTexture2DResource texture) : base(owner)
        {
            Name = "web-menu-steam";
            Width = texture.Width;
            Height = texture.Height;
            Texture = texture;
        }
    }
}
