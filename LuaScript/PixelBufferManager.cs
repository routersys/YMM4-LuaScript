using System;
using Vortice;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;

namespace LuaScript
{
    internal sealed class PixelBufferManager : IDisposable
    {
        private readonly GraphicsDevicesAndContext _ctx;
        private ID2D1Bitmap1? _renderTarget;
        private ID2D1Bitmap1? _stagingBitmap;
        private ID2D1Bitmap1? _outputBitmap;
        private AffineTransform2D? _transformEffect;
        private ID2D1Image? _transformOutput;
        private byte[]? _pixelBuffer;
        private int _bitmapWidth;
        private int _bitmapHeight;
        private RawRectF _cachedBounds;

        public PixelBufferManager(GraphicsDevicesAndContext ctx)
        {
            _ctx = ctx;
        }

        public byte[] LoadInputPixels(ID2D1Image input, RawRectF bounds, int width, int height)
        {
            EnsureBitmaps(width, height);

            var dc = _ctx.DeviceContext;
            using var savedTarget = dc.Target;

            dc.Target = _renderTarget;
            dc.BeginDraw();
            dc.Clear(null);
            dc.DrawImage(input, new Vector2(-bounds.Left, -bounds.Top));
            dc.EndDraw();
            dc.Target = savedTarget;

            _stagingBitmap!.CopyFromBitmap(_renderTarget!);

            var mapped = _stagingBitmap.Map(MapOptions.Read);
            try
            {
                if (mapped.Pitch == width * 4)
                {
                    Marshal.Copy(mapped.Bits, _pixelBuffer!, 0, width * height * 4);
                }
                else
                {
                    for (int row = 0; row < height; row++)
                        Marshal.Copy(
                            mapped.Bits + mapped.Pitch * row,
                            _pixelBuffer!,
                            row * width * 4,
                            width * 4);
                }
            }
            finally
            {
                _stagingBitmap.Unmap();
            }

            return _pixelBuffer!;
        }

        public unsafe void WritePixelsToOutput(byte[] pixels, int width, int height)
        {
            EnsureBitmaps(width, height);
            fixed (byte* ptr = pixels)
                _outputBitmap!.CopyFromMemory(new nint(ptr), width * 4);
        }

        public ID2D1Image GetTransformOutput(float left, float top)
        {
            if (_transformEffect is null)
            {
                _transformEffect = new AffineTransform2D(_ctx.DeviceContext);
                _transformEffect.SetInput(0, _outputBitmap, true);
                _transformOutput = _transformEffect.Output;
                _cachedBounds = default;
            }

            if (_cachedBounds.Left != left || _cachedBounds.Top != top)
            {
                _transformEffect.TransformMatrix = Matrix3x2.CreateTranslation(left, top);
                _cachedBounds = new RawRectF(left, top, left, top);
            }

            return _transformOutput!;
        }

        private void EnsureBitmaps(int width, int height)
        {
            if (_bitmapWidth == width && _bitmapHeight == height) return;

            _renderTarget?.Dispose();
            _stagingBitmap?.Dispose();
            _outputBitmap?.Dispose();
            _transformOutput?.Dispose();
            _transformEffect?.Dispose();
            _renderTarget = null;
            _stagingBitmap = null;
            _outputBitmap = null;
            _transformOutput = null;
            _transformEffect = null;
            _bitmapWidth = 0;
            _bitmapHeight = 0;

            var dc = _ctx.DeviceContext;

            _renderTarget = dc.CreateEmptyBitmap(width, height, BitmapOptions.Target);

            var stagingProps = new BitmapProperties1(
                new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                96f, 96f,
                BitmapOptions.CpuRead | BitmapOptions.CannotDraw);
            _stagingBitmap = dc.CreateBitmap(
                new SizeI(width, height),
                nint.Zero,
                width * 4,
                stagingProps);

            _outputBitmap = dc.CreateEmptyBitmap(width, height, BitmapOptions.Target);
            _pixelBuffer = new byte[width * height * 4];

            _bitmapWidth = width;
            _bitmapHeight = height;
        }

        public void Dispose()
        {
            _renderTarget?.Dispose();
            _stagingBitmap?.Dispose();
            _outputBitmap?.Dispose();
            _transformOutput?.Dispose();
            _transformEffect?.Dispose();
            _renderTarget = null;
            _stagingBitmap = null;
            _outputBitmap = null;
            _transformOutput = null;
            _transformEffect = null;
        }
    }
}
