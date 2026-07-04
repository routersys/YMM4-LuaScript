using System;
using System.Numerics;
using System.Runtime.InteropServices;
using LuaScript.Compat;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player;
using Blend = YukkuriMovieMaker.Project.Blend;

namespace LuaScript
{
    internal sealed class HardwareCompositor : IBufferCompositor, IDisposable
    {
        private const int MaxDimension = 8192;

        private readonly GraphicsDevicesAndContext _ctx;
        private ID2D1Bitmap1? _source;
        private ID2D1Bitmap1? _target;
        private ID2D1Bitmap1? _staging;
        private Opacity? _opacityEffect;
        private ID2D1Image? _opacityOutput;
        private int _sourceWidth;
        private int _sourceHeight;
        private int _targetWidth;
        private int _targetHeight;

        public HardwareCompositor(GraphicsDevicesAndContext ctx)
        {
            _ctx = ctx;
        }

        public unsafe bool TryCompose(byte[] dst, int dstW, int dstH, byte[] src, int srcW, int srcH, in DrawCommand command)
        {
            if (srcW <= 0 || srcH <= 0 || dstW <= 0 || dstH <= 0)
                return true;
            if (srcW > MaxDimension || srcH > MaxDimension || dstW > MaxDimension || dstH > MaxDimension)
                return false;
            if (src.Length < srcW * srcH * 4 || dst.Length < dstW * dstH * 4)
                return false;
            if (!DrawTransform.TryResolve(command, srcW, srcH, out var transform))
                return true;

            float opacity = (float)Math.Clamp(command.Alpha, 0d, 1d);
            if (opacity <= 0f)
                return true;

            EnsureSource(srcW, srcH);
            EnsureTarget(dstW, dstH);

            fixed (byte* ptr = src)
                _source!.CopyFromMemory(new nint(ptr), srcW * 4);
            fixed (byte* ptr = dst)
                _target!.CopyFromMemory(new nint(ptr), dstW * 4);

            var interpolation = command.Antialias != 0d
                ? BitmapInterpolationMode.Linear
                : BitmapInterpolationMode.NearestNeighbor;
            var blend = BlendModeMap.Resolve(command.Blend);

            var dc = _ctx.DeviceContext;
            var rt = (ID2D1RenderTarget)dc;
            using var savedTarget = dc.Target;
            dc.Target = _target;
            try
            {
                dc.BeginDraw();
                rt.Transform = transform;
                DrawSource(rt, opacity, interpolation, blend);
                rt.Transform = Matrix3x2.Identity;
                dc.EndDraw();
            }
            finally
            {
                dc.Target = savedTarget;
            }

            _staging!.CopyFromBitmap(_target!);
            var mapped = _staging.Map(MapOptions.Read);
            try
            {
                if (mapped.Pitch == dstW * 4)
                {
                    Marshal.Copy(mapped.Bits, dst, 0, dstW * dstH * 4);
                }
                else
                {
                    for (int row = 0; row < dstH; row++)
                        Marshal.Copy(mapped.Bits + mapped.Pitch * row, dst, row * dstW * 4, dstW * 4);
                }
            }
            finally
            {
                _staging.Unmap();
            }
            return true;
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

            _target?.Dispose();
            _staging?.Dispose();
            _target = null;
            _staging = null;

            var dc = _ctx.DeviceContext;
            _target = dc.CreateEmptyBitmap(width, height, BitmapOptions.Target);

            var stagingProps = new BitmapProperties1(
                new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                96f, 96f,
                BitmapOptions.CpuRead | BitmapOptions.CannotDraw);
            _staging = dc.CreateBitmap(new SizeI(width, height), nint.Zero, width * 4, stagingProps);

            _targetWidth = width;
            _targetHeight = height;
        }

        public void Dispose()
        {
            _opacityOutput?.Dispose();
            _opacityEffect?.Dispose();
            _source?.Dispose();
            _target?.Dispose();
            _staging?.Dispose();
            _opacityOutput = null;
            _opacityEffect = null;
            _source = null;
            _target = null;
            _staging = null;
        }
    }
}
