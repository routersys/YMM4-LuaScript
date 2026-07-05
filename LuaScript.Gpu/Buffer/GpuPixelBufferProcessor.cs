using System;
using System.Collections.Generic;
using LuaScript.Engine.Shader;

namespace LuaScript.Engine.Processing
{
    internal sealed class GpuPixelBufferProcessor(PixelShaderRunner runner) : IPixelBufferProcessor
    {
        private const int MaxTextureSize = 16384;
        private const int FillFullThreshold = 1048576;
        private const int FillPartialThreshold = 1048576;
        private const int ResizeThreshold = 262144;
        private const int ConvolveWorkThreshold = 262144;
        private const int MaxConvolveSize = 31;
        private const int MaxConvolveValidationCount = 64;

        private readonly object _gate = new();
        private readonly PixelShaderInput[] _resources = new PixelShaderInput[1];
        private readonly float[] _constants = new float[1024];
        private readonly List<ConvolveValidation> _convolveValidations = [];

        private byte[] _resizeTarget = [];
        private bool? _fillFullValidated;
        private bool? _fillPartialValidated;
        private bool? _resizeNearestValidated;
        private bool? _resizeLinearValidated;

        public bool TryFill(byte[] target, int width, int height, double r, double g, double b, double a, int x, int y, int fillWidth, int fillHeight)
        {
            lock (_gate)
            {
                if (!IsTextureSupported(target, width, height) || !AreFinite(r, g, b, a))
                    return false;

                long pixels = (long)width * height;
                bool full = x == 0 && y == 0 && fillWidth == width && fillHeight == height;
                if (full)
                {
                    if (pixels < FillFullThreshold || !EnsureFillFullValidated())
                        return false;
                    SetFillConstants(r, g, b, a);
                    return Run(GpuPixelOperationShaderRegistry.Fill, "fill_full", ReadOnlySpan<PixelShaderInput>.Empty, _constants.AsSpan(0, 4), target, width, height);
                }

                long area = (long)fillWidth * fillHeight;
                if (pixels < FillPartialThreshold || area * 4L < pixels * 3L || !EnsureFillPartialValidated())
                    return false;

                SetFillConstants(r, g, b, a);
                _constants[4] = x;
                _constants[5] = y;
                _constants[6] = x + fillWidth;
                _constants[7] = y + fillHeight;
                _resources[0] = new PixelShaderInput(target, width, height);
                return Run(GpuPixelOperationShaderRegistry.Fill, "fill_partial", _resources.AsSpan(0, 1), _constants.AsSpan(0, 8), target, width, height);
            }
        }

        public bool TryConvolve(byte[] target, int width, int height, double[] kernel, int size, double divisor, double offset)
        {
            lock (_gate)
            {
                if (!IsTextureSupported(target, width, height) || !IsConvolveSupported(kernel, size, divisor, offset))
                    return false;

                long work = (long)width * height * size * size;
                if (work < ConvolveWorkThreshold || !EnsureConvolveValidated(target, width, height, kernel, size, divisor, offset))
                    return false;

                SetConvolveConstants(width, height, kernel, size, divisor, offset);
                _resources[0] = new PixelShaderInput(target, width, height);
                return Run(GpuPixelOperationShaderRegistry.Convolve, "convolve", _resources.AsSpan(0, 1), _constants.AsSpan(0, size * size + 8), target, width, height);
            }
        }

        public bool TryResize(byte[] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, bool linear, out byte[]? target)
        {
            target = null;
            lock (_gate)
            {
                if (!IsTextureSupported(source, sourceWidth, sourceHeight) || !IsTextureSizeSupported(targetWidth, targetHeight))
                    return false;

                long pixels = (long)targetWidth * targetHeight;
                if (pixels < ResizeThreshold || !(linear ? EnsureResizeLinearValidated(source, sourceWidth, sourceHeight) : EnsureResizeNearestValidated(source, sourceWidth, sourceHeight)))
                    return false;

                return RunResizeShader(source, sourceWidth, sourceHeight, targetWidth, targetHeight, linear, out target);
            }
        }

        private bool EnsureFillFullValidated()
        {
            if (_fillFullValidated.HasValue)
                return _fillFullValidated.Value;

            const int width = 7;
            const int height = 5;
            var source = CreateProbe(width, height);
            var cpu = (byte[])source.Clone();
            var gpu = (byte[])source.Clone();
            PixelBufferSoftwareProcessor.Fill(cpu, width, height, 23d, 197d, 89d, 173d, 0, 0, width, height);
            SetFillConstants(23d, 197d, 89d, 173d);
            _fillFullValidated = Run(GpuPixelOperationShaderRegistry.Fill, "fill_full", ReadOnlySpan<PixelShaderInput>.Empty, _constants.AsSpan(0, 4), gpu, width, height) && Equal(cpu, gpu);
            return _fillFullValidated.Value;
        }

        private bool EnsureFillPartialValidated()
        {
            if (_fillPartialValidated.HasValue)
                return _fillPartialValidated.Value;

            const int width = 7;
            const int height = 5;
            var source = CreateProbe(width, height);
            var cpu = (byte[])source.Clone();
            var gpu = (byte[])source.Clone();
            PixelBufferSoftwareProcessor.Fill(cpu, width, height, 23d, 197d, 89d, 173d, 1, 2, 4, 2);
            SetFillConstants(23d, 197d, 89d, 173d);
            _constants[4] = 1f;
            _constants[5] = 2f;
            _constants[6] = 5f;
            _constants[7] = 4f;
            _resources[0] = new PixelShaderInput(gpu, width, height);
            _fillPartialValidated = Run(GpuPixelOperationShaderRegistry.Fill, "fill_partial", _resources.AsSpan(0, 1), _constants.AsSpan(0, 8), gpu, width, height) && Equal(cpu, gpu);
            return _fillPartialValidated.Value;
        }

        private bool EnsureResizeNearestValidated(byte[] source, int sourceWidth, int sourceHeight)
        {
            if (_resizeNearestValidated.HasValue)
                return _resizeNearestValidated.Value;
            _resizeNearestValidated = ValidateResize(source, sourceWidth, sourceHeight, false);
            return _resizeNearestValidated.Value;
        }

        private bool EnsureResizeLinearValidated(byte[] source, int sourceWidth, int sourceHeight)
        {
            if (_resizeLinearValidated.HasValue)
                return _resizeLinearValidated.Value;
            _resizeLinearValidated = ValidateResize(source, sourceWidth, sourceHeight, true);
            return _resizeLinearValidated.Value;
        }

        private bool ValidateResize(byte[] source, int sourceWidth, int sourceHeight, bool linear)
        {
            int probeWidth = Math.Min(sourceWidth, 8);
            int probeHeight = Math.Min(sourceHeight, 7);
            int targetWidth = Math.Max(1, probeWidth * 2 - 3);
            int targetHeight = Math.Max(1, probeHeight * 2 - 5);
            var probe = CopyProbe(source, sourceWidth, sourceHeight, probeWidth, probeHeight);
            byte[]? sourceScratch = null;
            byte[]? targetScratch = null;
            var cpu = PixelBufferSoftwareProcessor.Resize(probe, probeWidth, probeHeight, targetWidth, targetHeight, linear, ref sourceScratch, ref targetScratch);
            return RunResizeShader(probe, probeWidth, probeHeight, targetWidth, targetHeight, linear, out var gpu) && gpu is not null && Equal(cpu, gpu);
        }

        private bool EnsureConvolveValidated(byte[] target, int width, int height, double[] kernel, int size, double divisor, double offset)
        {
            for (int i = 0; i < _convolveValidations.Count; i++)
            {
                if (_convolveValidations[i].Matches(kernel, size, divisor, offset))
                    return _convolveValidations[i].Valid;
            }

            if (_convolveValidations.Count >= MaxConvolveValidationCount)
                _convolveValidations.Clear();

            bool valid = ValidateConvolve(target, width, height, kernel, size, divisor, offset);
            _convolveValidations.Add(new ConvolveValidation(kernel, size, divisor, offset, valid));
            return valid;
        }

        private bool ValidateConvolve(byte[] target, int width, int height, double[] kernel, int size, double divisor, double offset)
        {
            int probeWidth = Math.Min(width, 8);
            int probeHeight = Math.Min(height, 7);
            var probe = CopyProbe(target, width, height, probeWidth, probeHeight);
            var cpu = (byte[])probe.Clone();
            var gpu = (byte[])probe.Clone();
            double[]? scratch = null;
            PixelBufferSoftwareProcessor.Convolve(cpu, probeWidth, probeHeight, kernel, size, divisor, offset, ref scratch);
            SetConvolveConstants(probeWidth, probeHeight, kernel, size, divisor, offset);
            _resources[0] = new PixelShaderInput(gpu, probeWidth, probeHeight);
            return Run(GpuPixelOperationShaderRegistry.Convolve, "convolve", _resources.AsSpan(0, 1), _constants.AsSpan(0, size * size + 8), gpu, probeWidth, probeHeight) && Equal(cpu, gpu);
        }

        private bool RunResizeShader(byte[] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, bool linear, out byte[]? target)
        {
            int count = checked(targetWidth * targetHeight * 4);
            if (_resizeTarget.Length != count)
                _resizeTarget = new byte[count];

            _constants[0] = sourceWidth;
            _constants[1] = sourceHeight;
            _constants[2] = targetWidth;
            _constants[3] = targetHeight;
            _resources[0] = new PixelShaderInput(source, sourceWidth, sourceHeight);
            target = _resizeTarget;
            string entry = linear ? "resize_linear" : "resize_nearest";
            return Run(GpuPixelOperationShaderRegistry.Resize, entry, _resources.AsSpan(0, 1), _constants.AsSpan(0, 4), _resizeTarget, targetWidth, targetHeight);
        }

        private bool Run(string hlsl, string entryPoint, ReadOnlySpan<PixelShaderInput> resources, ReadOnlySpan<float> constants, byte[] target, int width, int height)
        {
            return runner.TryRunHardware(
                hlsl,
                entryPoint,
                resources,
                constants,
                PixelShaderBlend.Copy,
                PixelShaderSampler.None,
                target,
                width,
                height,
                out _) == PixelShaderRunStatus.Success;
        }

        private void SetFillConstants(double r, double g, double b, double a)
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

        private void SetConvolveConstants(int width, int height, double[] kernel, int size, double divisor, double offset)
        {
            _constants[0] = width;
            _constants[1] = height;
            _constants[2] = (float)divisor;
            _constants[3] = (float)offset;
            _constants[4] = size;
            _constants[5] = size / 2;
            _constants[6] = 0f;
            _constants[7] = 0f;
            int taps = size * size;
            for (int i = 0; i < taps; i++)
                _constants[i + 8] = (float)kernel[i];
        }

        private static bool IsTextureSupported(byte[] buffer, int width, int height)
        {
            return IsTextureSizeSupported(width, height) && (long)buffer.Length >= (long)width * height * 4;
        }

        private static bool IsTextureSizeSupported(int width, int height)
        {
            return width > 0 && height > 0 && width <= MaxTextureSize && height <= MaxTextureSize;
        }

        private static bool IsConvolveSupported(double[] kernel, int size, double divisor, double offset)
        {
            if (size < 1 || (size & 1) == 0 || size > MaxConvolveSize || divisor == 0d || !IsFloatCompatible(divisor) || !IsFloatCompatible(offset))
                return false;
            int taps = size * size;
            if (kernel.Length < taps || taps + 8 > 1024)
                return false;
            for (int i = 0; i < taps; i++)
            {
                if (!IsFloatCompatible(kernel[i]))
                    return false;
            }
            return true;
        }

        private static bool AreFinite(double r, double g, double b, double a)
        {
            return double.IsFinite(r) && double.IsFinite(g) && double.IsFinite(b) && double.IsFinite(a);
        }

        private static bool IsFloatCompatible(double value)
        {
            return double.IsFinite(value) && float.IsFinite((float)value);
        }

        private static bool Equal(byte[] expected, byte[] actual)
        {
            return expected.AsSpan().SequenceEqual(actual);
        }

        private static byte[] CopyProbe(byte[] source, int width, int height, int probeWidth, int probeHeight)
        {
            var data = new byte[probeWidth * probeHeight * 4];
            for (int y = 0; y < probeHeight; y++)
                Buffer.BlockCopy(source, y * width * 4, data, y * probeWidth * 4, probeWidth * 4);
            return data;
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

        private sealed class ConvolveValidation
        {
            private readonly double[] _kernel;
            private readonly int _size;
            private readonly double _divisor;
            private readonly double _offset;

            public ConvolveValidation(double[] kernel, int size, double divisor, double offset, bool valid)
            {
                int taps = size * size;
                _kernel = new double[taps];
                Array.Copy(kernel, _kernel, taps);
                _size = size;
                _divisor = divisor;
                _offset = offset;
                Valid = valid;
            }

            public bool Valid { get; }

            public bool Matches(double[] kernel, int size, double divisor, double offset)
            {
                if (_size != size || BitConverter.DoubleToInt64Bits(_divisor) != BitConverter.DoubleToInt64Bits(divisor) ||
                    BitConverter.DoubleToInt64Bits(_offset) != BitConverter.DoubleToInt64Bits(offset))
                    return false;
                int taps = size * size;
                for (int i = 0; i < taps; i++)
                {
                    if (BitConverter.DoubleToInt64Bits(_kernel[i]) != BitConverter.DoubleToInt64Bits(kernel[i]))
                        return false;
                }
                return true;
            }
        }
    }
}
