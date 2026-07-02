using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;

namespace LuaScript.Engine.Kernel
{
    internal sealed class GpuKernel : IDisposable
    {
        private const int VerifySize = 64;
        private const int VerifyTolerance = 2;

        private readonly IGraphicsDevicesAndContext _devices;
        private readonly GpuKernelEffect _effect;
        private readonly float[] _packed = new float[HlslKernelEmitter.ConstantFloatCount];
        private readonly byte[] _bytes = new byte[HlslKernelEmitter.ConstantFloatCount * sizeof(float)];
        private ID2D1Image? _output;

        public GpuKernel(IGraphicsDevicesAndContext devices, KernelProgram program)
        {
            _devices = devices;
            _effect = new GpuKernelEffect(devices, HlslKernelEmitter.Emit(program));
        }

        public bool IsReady => _effect.IsReady;

        public ID2D1Image Apply(ID2D1Image input, double[] uniforms)
        {
            KernelUniformBinding.Pack(uniforms, _packed);
            Buffer.BlockCopy(_packed, 0, _bytes, 0, _bytes.Length);
            _effect.SetConstants(_bytes);
            _effect.SetInput(0, input, true);
            return _output ??= _effect.Output;
        }

        public unsafe bool Verify(CpuKernel reference)
        {
            if (!IsReady)
                return false;

            var uniforms = KernelUniformBinding.Create();
            for (int i = 0; i < uniforms.Length; i++)
                uniforms[i] = (i % 7) + 1;
            uniforms[(int)KernelUniform.Width] = VerifySize;
            uniforms[(int)KernelUniform.Height] = VerifySize;

            byte[] probe = BuildProbe(VerifySize);
            byte[] expected = (byte[])probe.Clone();
            reference.Execute(expected, VerifySize, VerifySize, uniforms);

            var dc = (ID2D1DeviceContext)_devices.DeviceContext;
            ID2D1Bitmap1? source = null;
            ID2D1Bitmap1? target = null;
            ID2D1Bitmap1? staging = null;
            try
            {
                source = dc.CreateEmptyBitmap(VerifySize, VerifySize, BitmapOptions.Target);
                fixed (byte* ptr = probe)
                    source.CopyFromMemory(new nint(ptr), VerifySize * 4);

                target = dc.CreateEmptyBitmap(VerifySize, VerifySize, BitmapOptions.Target);

                var stagingProps = new BitmapProperties1(
                    new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                    96f, 96f,
                    BitmapOptions.CpuRead | BitmapOptions.CannotDraw);
                staging = dc.CreateBitmap(new SizeI(VerifySize, VerifySize), nint.Zero, VerifySize * 4, stagingProps);

                var output = Apply(source, uniforms);

                using var savedTarget = dc.Target;
                dc.Target = target;
                try
                {
                    dc.BeginDraw();
                    dc.Clear(null);
                    dc.DrawImage(output, new Vector2(0f, 0f));
                    dc.EndDraw();
                }
                finally
                {
                    dc.Target = savedTarget;
                }

                staging.CopyFromBitmap(target);
                byte[] actual = new byte[VerifySize * VerifySize * 4];
                var mapped = staging.Map(MapOptions.Read);
                try
                {
                    if (mapped.Pitch == VerifySize * 4)
                    {
                        Marshal.Copy(mapped.Bits, actual, 0, actual.Length);
                    }
                    else
                    {
                        for (int row = 0; row < VerifySize; row++)
                            Marshal.Copy(mapped.Bits + mapped.Pitch * row, actual, row * VerifySize * 4, VerifySize * 4);
                    }
                }
                finally
                {
                    staging.Unmap();
                }

                return WithinTolerance(expected, actual, VerifyTolerance);
            }
            catch (Exception ex)
            {
                Log.Default.Write("LuaScript: GPU kernel verification could not run; using the CPU kernel.", ex);
                return false;
            }
            finally
            {
                source?.Dispose();
                target?.Dispose();
                staging?.Dispose();
            }
        }

        private static byte[] BuildProbe(int size)
        {
            var buffer = new byte[size * size * 4];
            ReadOnlySpan<int> alphas = [0, 64, 128, 200, 255];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int alpha = alphas[(x + y) % alphas.Length];
                    int r = x * 255 / (size - 1);
                    int g = y * 255 / (size - 1);
                    int b = (x + y) * 255 / (2 * (size - 1));
                    int p = (y * size + x) * 4;
                    buffer[p] = (byte)(b * alpha / 255);
                    buffer[p + 1] = (byte)(g * alpha / 255);
                    buffer[p + 2] = (byte)(r * alpha / 255);
                    buffer[p + 3] = (byte)alpha;
                }
            }
            return buffer;
        }

        private static bool WithinTolerance(byte[] expected, byte[] actual, int tolerance)
        {
            if (expected.Length != actual.Length)
                return false;
            for (int i = 0; i < expected.Length; i++)
                if (Math.Abs(expected[i] - actual[i]) > tolerance)
                    return false;
            return true;
        }

        public void Dispose()
        {
            _output?.Dispose();
            _output = null;
            _effect.Dispose();
        }
    }
}
