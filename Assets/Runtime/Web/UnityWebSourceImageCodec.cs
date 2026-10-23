using System;
using System.IO;
using System.Runtime.InteropServices;
using Celeste.Graphics.Content.Images;
using UnityEngine;

namespace Celeste.Unity.Web
{
    internal sealed class UnityWebSourceImageCodec : ISourceImageCodecDevice
    {
        public IntPtr Load(SourceImageReadCallback read, SourceImageSkipCallback skip, SourceImageEofCallback eof,
            IntPtr context, out int width, out int height, out int length, int requestedWidth, int requestedHeight,
            byte zoom)
        {
            if (requestedWidth != -1 || requestedHeight != -1 || zoom != 0)
                throw new PlatformNotSupportedException("FNA image resize/zoom is not implemented by this codec.");
            using var encoded = new MemoryStream();
            byte[] block = new byte[8192];
            IntPtr buffer = Marshal.AllocHGlobal(block.Length);
            try
            {
                while (true)
                {
                    int count = read(context, buffer, block.Length);
                    if (count < 0 || count > block.Length)
                        throw new InvalidDataException("Invalid image callback byte count.");
                    if (count == 0) break;
                    Marshal.Copy(buffer, block, 0, count);
                    encoded.Write(block, 0, count);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!ImageConversion.LoadImage(texture, encoded.ToArray(), false))
                    throw new InvalidDataException("Unity could not decode the source image.");
                width = texture.width;
                height = texture.height;
                length = checked(width * height * 4);
                Color32[] colors = texture.GetPixels32();
                byte[] rgba = new byte[length];
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var color = colors[(height - 1 - y) * width + x];
                    int index = (y * width + x) * 4;
                    rgba[index] = color.r;
                    rgba[index + 1] = color.g;
                    rgba[index + 2] = color.b;
                    rgba[index + 3] = color.a;
                }

                IntPtr result = Marshal.AllocHGlobal(length);
                try
                {
                    Marshal.Copy(rgba, 0, result, length);
                    return result;
                }
                catch
                {
                    Marshal.FreeHGlobal(result);
                    throw;
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(texture);
            }
        }

        public void FreeImage(IntPtr pointer) => Marshal.FreeHGlobal(pointer);

        public void SavePNG(SourceImageWriteCallback write, IntPtr context, int sourceWidth, int sourceHeight,
            int destinationWidth, int destinationHeight, IntPtr data)
            => Save(write, context, sourceWidth, sourceHeight, destinationWidth, destinationHeight, data, null);

        public void SaveJPG(SourceImageWriteCallback write, IntPtr context, int sourceWidth, int sourceHeight,
            int destinationWidth, int destinationHeight, IntPtr data, int quality)
            => Save(write, context, sourceWidth, sourceHeight, destinationWidth, destinationHeight, data, quality);

        private static void Save(SourceImageWriteCallback write, IntPtr context, int width, int height,
            int destinationWidth, int destinationHeight, IntPtr data, int? quality)
        {
            if (width != destinationWidth || height != destinationHeight)
                throw new PlatformNotSupportedException("FNA image export resizing is not implemented by this codec.");
            if (width <= 0 || height <= 0 || data == IntPtr.Zero)
                throw new ArgumentException("Source RGBA pixels are required.");
            byte[] rgba = new byte[checked(width * height * 4)];
            Marshal.Copy(data, rgba, 0, rgba.Length);
            var colors = new Color32[checked(width * height)];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int index = (y * width + x) * 4;
                colors[(height - 1 - y) * width + x] =
                    new Color32(rgba[index], rgba[index + 1], rgba[index + 2], rgba[index + 3]);
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try
            {
                texture.SetPixels32(colors);
                texture.Apply(false, false);
                byte[] encoded = quality.HasValue
                    ? ImageConversion.EncodeToJPG(texture, quality.Value)
                    : ImageConversion.EncodeToPNG(texture);
                IntPtr buffer = Marshal.AllocHGlobal(encoded.Length);
                try
                {
                    Marshal.Copy(encoded, 0, buffer, encoded.Length);
                    write(context, buffer, encoded.Length);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(texture);
            }
        }
    }
}
