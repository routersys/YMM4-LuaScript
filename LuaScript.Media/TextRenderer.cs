using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Vortice.WIC;

namespace LuaScript
{
    internal sealed class TextRenderer : IDisposable
    {
        private const int MaxDimension = 8192;
        private const string FallbackFamily = "Yu Gothic UI";

        private ID2D1Factory? _d2dFactory;
        private IWICImagingFactory? _wicFactory;
        private IDWriteFactory? _dwriteFactory;

        private void EnsureFactories()
        {
            _d2dFactory ??= D2D1.D2D1CreateFactory<ID2D1Factory>(
                Vortice.Direct2D1.FactoryType.MultiThreaded, DebugLevel.None);
            _wicFactory ??= new IWICImagingFactory();
            _dwriteFactory ??= DWrite.DWriteCreateFactory<IDWriteFactory>(
                Vortice.DirectWrite.FactoryType.Shared);
        }

        public unsafe byte[] Render(
            string text, string fontFamily, double fontSize,
            bool bold, bool italic, int colorRgb,
            out int width, out int height)
        {
            width = 1;
            height = 1;
            if (string.IsNullOrEmpty(text))
                return new byte[4];

            EnsureFactories();

            float size = (float)Math.Clamp(fontSize, 1d, 4096d);
            using var format = _dwriteFactory!.CreateTextFormat(
                string.IsNullOrWhiteSpace(fontFamily) ? FallbackFamily : fontFamily,
                null,
                bold ? FontWeight.Bold : FontWeight.Normal,
                italic ? FontStyle.Italic : FontStyle.Normal,
                FontStretch.Normal,
                size,
                string.Empty);

            using var layout = _dwriteFactory.CreateTextLayout(text, format, MaxDimension, MaxDimension);
            var metrics = layout.Metrics;
            int w = Math.Clamp((int)Math.Ceiling(metrics.WidthIncludingTrailingWhitespace), 1, MaxDimension);
            int h = Math.Clamp((int)Math.Ceiling(metrics.Height), 1, MaxDimension);

            using var wic = _wicFactory!.CreateBitmap(
                w, h, Vortice.WIC.PixelFormat.Format32bppPBGRA, BitmapCreateCacheOption.CacheOnLoad);

            var properties = new RenderTargetProperties(
                RenderTargetType.Default,
                new Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                96f, 96f,
                RenderTargetUsage.None,
                FeatureLevel.Default);

            float r = ((colorRgb >> 16) & 0xFF) / 255f;
            float g = ((colorRgb >> 8) & 0xFF) / 255f;
            float b = (colorRgb & 0xFF) / 255f;

            using (var target = _d2dFactory!.CreateWicBitmapRenderTarget(wic, properties))
            {
                target.BeginDraw();
                target.Clear(new Color4(0f, 0f, 0f, 0f));
                using (var brush = target.CreateSolidColorBrush(new Color4(r, g, b, 1f)))
                    target.DrawTextLayout(new Vector2(-metrics.Left, -metrics.Top), layout, brush);
                target.EndDraw();
            }

            var buffer = new byte[w * h * 4];
            fixed (byte* p = buffer)
                wic.CopyPixels(new RectI(0, 0, w, h), w * 4, buffer.Length, (nint)p);

            width = w;
            height = h;
            return buffer;
        }

        public void Dispose()
        {
            _dwriteFactory?.Dispose();
            _wicFactory?.Dispose();
            _d2dFactory?.Dispose();
            _dwriteFactory = null;
            _wicFactory = null;
            _d2dFactory = null;
        }
    }
}
