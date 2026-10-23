using System;
using System.Diagnostics;
using System.IO;
using Celeste.Everest.Runtime.Processes;
using Celeste.Game.Menus.Title;
using Celeste.Game.Runtime;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Content.VirtualAssets;
using Celeste.Graphics.Rendering.Textures;
using Celeste.Simulation.Presentation.Rendering;
using UnityEngine;
using Vector2 = System.Numerics.Vector2;

namespace Celeste.Unity.Web
{
    internal sealed class UnityWebStoreMenuOwner : IDisposable
    {
        private const string StoreUrl = "https://store.steampowered.com/app/504230/Celeste/";
        private readonly Func<GameApplication> readGame;
        private readonly IEverestProcessStartIo navigation;
        private UnityWebStoreIconTexture iconTexture;
        private AtlasEntry icon;
        private float lastLeft, lastTop, lastWidth, lastHeight;
        private bool disposed;

        public MainMenuAdditionalButton Button { get; }

        public UnityWebStoreMenuOwner(Func<GameApplication> readGame, IEverestProcessStartIo navigation)
        {
            this.readGame = readGame ?? throw new ArgumentNullException(nameof(readGame));
            this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            Button = new MainMenuAdditionalButton("Steam", ReadIcon, OpenStore, PublishRegion);
            UnityWebBrowserInterop.InstallSteamLink(StoreUrl);
        }

        private AtlasEntry ReadIcon()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(UnityWebStoreMenuOwner));
            }

            if (icon != null)
            {
                return icon;
            }

            var game = readGame() ?? throw new InvalidOperationException("The Web menu game is not constructed.");
            var asset = Resources.Load<TextAsset>("Celeste/UI/SteamLogo") ??
                        throw new InvalidOperationException("The official Steam menu icon is not packaged.");
            using var stream = new MemoryStream(asset.bytes, false);
            var texture = SourceTexture2DResource.FromStream(game.GraphicsDevice, stream, true);
            try
            {
                iconTexture = new UnityWebStoreIconTexture(game.VirtualContent, texture);
                icon = new AtlasEntry(iconTexture);
                return icon;
            }
            catch
            {
                texture.Dispose();
                iconTexture = null;
                throw;
            }
        }

        private void OpenStore()
        {
            var game = readGame() ?? throw new InvalidOperationException("The Web menu game is not constructed.");
            game.AudioSession.Play("event:/ui/main/button_select");
            navigation.Start(new ProcessStartInfo(StoreUrl) { UseShellExecute = true });
        }

        private void PublishRegion(float x, float y, float width, float height)
        {
            if (disposed)
            {
                return;
            }

            if (width <= 0 || height <= 0)
            {
                PublishNormalized(0, 0, 0, 0);
                return;
            }

            var game = readGame() ?? throw new InvalidOperationException("The Web menu game is not constructed.");
            var client = game.ActualGame.Window.ClientBounds;
            if (client.Width <= 0 || client.Height <= 0)
            {
                PublishNormalized(0, 0, 0, 0);
                return;
            }

            var graphics = game.EngineGraphics;
            var offset = HiresRendererState.DrawToBuffer(game.CurrentContext, 1920, 1080) ? -1f : 0;
            var topLeft = Vector2.Transform(new Vector2(x + offset, y + offset), graphics.ScreenMatrix);
            var bottomRight = Vector2.Transform(new Vector2(x + width + offset, y + height + offset),
                graphics.ScreenMatrix);
            var left = Math.Clamp((graphics.Viewport.X + topLeft.X) / client.Width, 0, 1);
            var top = Math.Clamp((graphics.Viewport.Y + topLeft.Y) / client.Height, 0, 1);
            var right = Math.Clamp((graphics.Viewport.X + bottomRight.X) / client.Width, 0, 1);
            var bottom = Math.Clamp((graphics.Viewport.Y + bottomRight.Y) / client.Height, 0, 1);
            PublishNormalized(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
        }

        private void PublishNormalized(float left, float top, float width, float height)
        {
            if (left == lastLeft && top == lastTop && width == lastWidth && height == lastHeight)
            {
                return;
            }

            UnityWebBrowserInterop.SetSteamLinkRegion(left, top, width, height);
            lastLeft = left;
            lastTop = top;
            lastWidth = width;
            lastHeight = height;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            try
            {
                var ownedTexture = iconTexture;
                iconTexture = null;
                icon = null;
                ownedTexture?.Dispose();
            }
            finally
            {
                UnityWebBrowserInterop.RemoveSteamLink();
            }
        }
    }
}
