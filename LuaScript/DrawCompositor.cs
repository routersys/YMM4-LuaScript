using System;
using System.Collections.Generic;
using System.Numerics;
using LuaScript.Compat;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player;
using Blend = YukkuriMovieMaker.Project.Blend;

namespace LuaScript
{
    internal sealed class DrawCompositor : IDisposable
    {
        private const int MaxDimension = 8192;

        private readonly GraphicsDevicesAndContext _ctx;
        private ID2D1Bitmap1? _source;
        private ID2D1Bitmap1? _target;
        private AffineTransform2D? _placement;
        private Opacity? _opacityEffect;
        private ID2D1Image? _opacityOutput;
        private int _sourceWidth;
        private int _sourceHeight;
        private int _targetWidth;
        private int _targetHeight;

        public DrawCompositor(GraphicsDevicesAndContext ctx)
        {
            _ctx = ctx;
        }

        public unsafe ID2D1Image Compose(byte[] pixels, int width, int height, IReadOnlyList<DrawCommand> commands)
        {
            EnsureSource(width, height);
            fixed (byte* ptr = pixels)
                _source!.CopyFromMemory(new nint(ptr), width * 4);

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            for (int i = 0; i < commands.Count; i++)
            {
                var cmd = commands[i];
                if (cmd.Poly is { } poly)
                {
                    DrawPolyMath.Bounds(poly, out double pMinX, out double pMinY, out double pMaxX, out double pMaxY);
                    minX = Math.Min(minX, pMinX);
                    maxX = Math.Max(maxX, pMaxX);
                    minY = Math.Min(minY, pMinY);
                    maxY = Math.Max(maxY, pMaxY);
                    continue;
                }
                double aspect = Math.Clamp(cmd.Aspect, -1d, 1d);
                double zx = cmd.Zoom * (1d + aspect);
                double zy = cmd.Zoom * (1d - aspect);
                double halfW = width * 0.5 * zx;
                double halfH = height * 0.5 * zy;
                minX = Math.Min(minX, cmd.Ox - halfW);
                maxX = Math.Max(maxX, cmd.Ox + halfW);
                minY = Math.Min(minY, cmd.Oy - halfH);
                maxY = Math.Max(maxY, cmd.Oy + halfH);
            }

            int originX = (int)Math.Floor(minX);
            int originY = (int)Math.Floor(minY);
            int targetW = Math.Clamp((int)Math.Ceiling(maxX) - originX, 1, MaxDimension);
            int targetH = Math.Clamp((int)Math.Ceiling(maxY) - originY, 1, MaxDimension);

            EnsureTarget(targetW, targetH);

            var dc = _ctx.DeviceContext;
            var rt = (ID2D1RenderTarget)dc;
            var savedTarget = dc.Target;

            dc.Target = _target;
            dc.BeginDraw();
            dc.Clear(null);
            var toTarget = Matrix3x2.CreateTranslation(-originX, -originY);
            for (int i = 0; i < commands.Count; i++)
            {
                var cmd = commands[i];
                if (!DrawTransform.TryResolve(cmd, width, height, out var transform))
                    continue;

                var interpolation = cmd.Antialias != 0d
                    ? BitmapInterpolationMode.Linear
                    : BitmapInterpolationMode.NearestNeighbor;
                var blend = BlendModeMap.Resolve(cmd.Blend);

                rt.Transform = transform * toTarget;
                DrawSource(rt, (float)Math.Clamp(cmd.Alpha, 0d, 1d), interpolation, blend);
            }
            rt.Transform = Matrix3x2.Identity;
            dc.EndDraw();
            dc.Target = savedTarget;

            _placement!.TransformMatrix = Matrix3x2.CreateTranslation(originX, originY);
            return _placement.Output;
        }

        private void DrawSource(ID2D1RenderTarget rt, float opacity, BitmapInterpolationMode interpolation, Blend blend)
        {
            if (blend == Blend.Normal)
            {
                rt.DrawBitmap(_source!, opacity, interpolation);
                return;
            }

            var interp = ToImageInterpolation(interpolation);
            _opacityEffect!.Value = opacity;
            if (blend.IsCompositionEffect())
                _ctx.DeviceContext.DrawImage(_opacityOutput!, interpolationMode: interp, compositeMode: blend.ToD2DCompositionMode());
            else
                _ctx.DeviceContext.BlendImage(_opacityOutput!, blend.ToD2DBlendMode(), null, null, interp);
        }

        private static InterpolationMode ToImageInterpolation(BitmapInterpolationMode mode) =>
            mode == BitmapInterpolationMode.Linear ? InterpolationMode.Linear : InterpolationMode.NearestNeighbor;

        private void EnsureSource(int width, int height)
        {
            if (_source is not null && _sourceWidth == width && _sourceHeight == height)
                return;

            _source?.Dispose();
            _source = _ctx.DeviceContext.CreateEmptyBitmap(width, height, BitmapOptions.Target);
            _sourceWidth = width;
            _sourceHeight = height;

            if (_opacityEffect is null)
            {
                _opacityEffect = new Opacity(_ctx.DeviceContext);
                _opacityOutput = _opacityEffect.Output;
            }
            _opacityEffect.SetInput(0, _source, true);
        }

        private void EnsureTarget(int width, int height)
        {
            if (_target is not null && _targetWidth == width && _targetHeight == height)
                return;

            _placement?.Dispose();
            _target?.Dispose();
            _placement = null;

            _target = _ctx.DeviceContext.CreateEmptyBitmap(width, height, BitmapOptions.Target);
            _targetWidth = width;
            _targetHeight = height;

            _placement = new AffineTransform2D(_ctx.DeviceContext);
            _placement.SetInput(0, _target, true);
        }

        public void Dispose()
        {
            _placement?.Dispose();
            _opacityOutput?.Dispose();
            _opacityEffect?.Dispose();
            _source?.Dispose();
            _target?.Dispose();
            _placement = null;
            _opacityOutput = null;
            _opacityEffect = null;
            _source = null;
            _target = null;
        }
    }
}
