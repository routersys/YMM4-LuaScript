using System;
using System.IO;
using Vortice.Mathematics;
using Vortice.WIC;

namespace LuaScript
{
    internal sealed class ImageDecoder : IDisposable
    {
        private const int MaxDimension = 8192;

        private IWICImagingFactory? _factory;

        public unsafe byte[] Decode(string path, out int width, out int height)
        {
            width = 1;
            height = 1;
            if (string.IsNullOrEmpty(path) || !IsSupportedImage(path))
                return new byte[4];

            try
            {
                _factory ??= new IWICImagingFactory();
                using var stream = _factory.CreateStream(path, FileAccess.Read);
                using var decoder = _factory.CreateDecoderFromStream(stream, DecodeOptions.CacheOnDemand);
                using var frame = decoder.GetFrame(0);
                using var converter = _factory.CreateFormatConverter();
                converter.Initialize(
                    frame,
                    Vortice.WIC.PixelFormat.Format32bppPBGRA,
                    BitmapDitherType.None,
                    null,
                    0d,
                    BitmapPaletteType.Custom);

                converter.GetSize(out int w, out int h);
                if (w <= 0 || h <= 0 || w > MaxDimension || h > MaxDimension)
                    return new byte[4];

                var buffer = new byte[w * h * 4];
                fixed (byte* p = buffer)
                    converter.CopyPixels(new RectI(0, 0, w, h), w * 4, buffer.Length, (nint)p);

                width = w;
                height = h;
                return buffer;
            }
            catch
            {
                width = 1;
                height = 1;
                return new byte[4];
            }
        }

        private static bool IsSupportedImage(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return false;
                Span<byte> head = stackalloc byte[12];
                using var stream = File.OpenRead(path);
                int read = stream.Read(head);
                if (read < 4)
                    return false;

                if (head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47)
                    return true;
                if (head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
                    return true;
                if (head[0] == 0x47 && head[1] == 0x49 && head[2] == 0x46 && head[3] == 0x38)
                    return true;
                if (head[0] == 0x42 && head[1] == 0x4D)
                    return true;
                if ((head[0] == 0x49 && head[1] == 0x49 && head[2] == 0x2A && head[3] == 0x00) ||
                    (head[0] == 0x4D && head[1] == 0x4D && head[2] == 0x00 && head[3] == 0x2A))
                    return true;
                if (read >= 12 &&
                    head[0] == 0x52 && head[1] == 0x49 && head[2] == 0x46 && head[3] == 0x46 &&
                    head[8] == 0x57 && head[9] == 0x45 && head[10] == 0x42 && head[11] == 0x50)
                    return true;
                return false;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            _factory?.Dispose();
            _factory = null;
        }
    }
}
