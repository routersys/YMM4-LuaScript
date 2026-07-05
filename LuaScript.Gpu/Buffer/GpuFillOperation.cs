using System;
using LuaScript.Engine.Shader;

namespace LuaScript.Engine.Processing
{
    [GpuPixelOperationShader(nameof(RunFillFull), "Shaders/fill.hlsl", "fill_full")]
    [GpuPixelOperationShader(nameof(RunFillPartial), "Shaders/fill.hlsl", "fill_partial")]
    internal sealed partial class GpuFillOperation(PixelShaderRunner runner) : GpuPixelOperation(runner)
    {
        private const int FillFullThreshold = 1048576;
        private const int FillPartialThreshold = 1048576;

        private readonly PixelShaderInput[] _resources = new PixelShaderInput[1];
        private readonly float[] _constants = new float[8];

        private bool? _fullValidated;
        private bool? _partialValidated;

        public bool TryFill(byte[] target, int width, int height, double r, double g, double b, double a, int x, int y, int fillWidth, int fillHeight, bool force = false)
        {
            if (!IsTextureSupported(target, width, height) || !AreFinite(r, g, b, a))
                return false;

            long pixels = (long)width * height;
            bool full = x == 0 && y == 0 && fillWidth == width && fillHeight == height;
            if (full)
            {
                if (!force && pixels < FillFullThreshold || !EnsureFullValidated())
                    return false;
                SetConstants(r, g, b, a);
                return RunFillFull(_constants.AsSpan(0, 4), target, width, height);
            }

            long area = (long)fillWidth * fillHeight;
            if (!force && (pixels < FillPartialThreshold || area * 4L < pixels * 3L) || !EnsurePartialValidated())
                return false;

            SetConstants(r, g, b, a);
            _constants[4] = x;
            _constants[5] = y;
            _constants[6] = x + fillWidth;
            _constants[7] = y + fillHeight;
            _resources[0] = new PixelShaderInput(target, width, height);
            return RunFillPartial(_resources.AsSpan(0, 1), _constants.AsSpan(0, 8), target, width, height);
        }

        private bool EnsureFullValidated()
        {
            if (_fullValidated.HasValue)
                return _fullValidated.Value;

            const int width = 7;
            const int height = 5;
            var source = CreateProbe(width, height);
            var cpu = (byte[])source.Clone();
            var gpu = (byte[])source.Clone();
            PixelBufferSoftwareProcessor.Fill(cpu, width, height, 23d, 197d, 89d, 173d, 0, 0, width, height);
            SetConstants(23d, 197d, 89d, 173d);
            _fullValidated = RunFillFull(_constants.AsSpan(0, 4), gpu, width, height) && Equal(cpu, gpu);
            return _fullValidated.Value;
        }

        private bool EnsurePartialValidated()
        {
            if (_partialValidated.HasValue)
                return _partialValidated.Value;

            const int width = 7;
            const int height = 5;
            var source = CreateProbe(width, height);
            var cpu = (byte[])source.Clone();
            var gpu = (byte[])source.Clone();
            PixelBufferSoftwareProcessor.Fill(cpu, width, height, 23d, 197d, 89d, 173d, 1, 2, 4, 2);
            SetConstants(23d, 197d, 89d, 173d);
            _constants[4] = 1f;
            _constants[5] = 2f;
            _constants[6] = 5f;
            _constants[7] = 4f;
            _resources[0] = new PixelShaderInput(gpu, width, height);
            _partialValidated = RunFillPartial(_resources.AsSpan(0, 1), _constants.AsSpan(0, 8), gpu, width, height) && Equal(cpu, gpu);
            return _partialValidated.Value;
        }

        private void SetConstants(double r, double g, double b, double a)
        {
            double aK = Math.Clamp(a, 0d, 255d) / 255d;
            byte pb = (byte)Math.Clamp(b * aK, 0d, 255d);
            byte pg = (byte)Math.Clamp(g * aK, 0d, 255d);
            byte pr = (byte)Math.Clamp(r * aK, 0d, 255d);
            byte pa = (byte)Math.Clamp(a, 0d, 255d);
            _constants[0] = pr / 255f;
            _constants[1] = pg / 255f;
            _constants[2] = pb / 255f;
            _constants[3] = pa / 255f;
        }

        private static bool AreFinite(double r, double g, double b, double a)
        {
            return double.IsFinite(r) && double.IsFinite(g) && double.IsFinite(b) && double.IsFinite(a);
        }

        private static byte[] CreateProbe(int width, int height)
        {
            var data = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * 4;
                    byte a = (byte)((x * 31 + y * 47) & 255);
                    data[index] = a == 0 ? (byte)0 : (byte)((x * 17 + y * 29) % (a + 1));
                    data[index + 1] = a == 0 ? (byte)0 : (byte)((x * 43 + y * 11) % (a + 1));
                    data[index + 2] = a == 0 ? (byte)0 : (byte)((x * 7 + y * 53) % (a + 1));
                    data[index + 3] = a;
                }
            }
            return data;
        }

        private partial bool RunFillFull(ReadOnlySpan<float> constants, byte[] target, int width, int height);

        private partial bool RunFillPartial(ReadOnlySpan<PixelShaderInput> resources, ReadOnlySpan<float> constants, byte[] target, int width, int height);
    }
}
