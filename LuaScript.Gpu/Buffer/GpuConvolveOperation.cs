using System;
using System.Collections.Generic;
using LuaScript.Engine.Shader;

namespace LuaScript.Engine.Processing
{
    [GpuPixelOperationShader(nameof(RunConvolve), "Shaders/convolve.hlsl", "convolve")]
    internal sealed partial class GpuConvolveOperation(PixelShaderRunner runner) : GpuPixelOperation(runner)
    {
        private const int ConvolveWorkThreshold = 262144;
        private const int MaxConvolveSize = 31;
        private const int MaxConvolveValidationCount = 64;

        private readonly PixelShaderInput[] _resources = new PixelShaderInput[1];
        private readonly float[] _constants = new float[1024];
        private readonly List<ConvolveValidation> _validations = [];

        public bool TryConvolve(byte[] target, int width, int height, double[] kernel, int size, double divisor, double offset)
        {
            if (!IsTextureSupported(target, width, height) || !IsSupported(kernel, size, divisor, offset))
                return false;

            long work = (long)width * height * size * size;
            if (work < ConvolveWorkThreshold || !EnsureValidated(target, width, height, kernel, size, divisor, offset))
                return false;

            SetConstants(width, height, kernel, size, divisor, offset);
            _resources[0] = new PixelShaderInput(target, width, height);
            return RunConvolve(_resources.AsSpan(0, 1), _constants.AsSpan(0, size * size + 8), target, width, height);
        }

        private bool EnsureValidated(byte[] target, int width, int height, double[] kernel, int size, double divisor, double offset)
        {
            for (int i = 0; i < _validations.Count; i++)
            {
                if (_validations[i].Matches(kernel, size, divisor, offset))
                    return _validations[i].Valid;
            }

            if (_validations.Count >= MaxConvolveValidationCount)
                _validations.Clear();

            bool valid = Validate(target, width, height, kernel, size, divisor, offset);
            _validations.Add(new ConvolveValidation(kernel, size, divisor, offset, valid));
            return valid;
        }

        private bool Validate(byte[] target, int width, int height, double[] kernel, int size, double divisor, double offset)
        {
            int probeWidth = Math.Min(width, 8);
            int probeHeight = Math.Min(height, 7);
            var probe = CopyProbe(target, width, height, probeWidth, probeHeight);
            var cpu = (byte[])probe.Clone();
            var gpu = (byte[])probe.Clone();
            double[]? scratch = null;
            PixelBufferSoftwareProcessor.Convolve(cpu, probeWidth, probeHeight, kernel, size, divisor, offset, ref scratch);
            SetConstants(probeWidth, probeHeight, kernel, size, divisor, offset);
            _resources[0] = new PixelShaderInput(gpu, probeWidth, probeHeight);
            return RunConvolve(_resources.AsSpan(0, 1), _constants.AsSpan(0, size * size + 8), gpu, probeWidth, probeHeight) && Equal(cpu, gpu);
        }

        private void SetConstants(int width, int height, double[] kernel, int size, double divisor, double offset)
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

        private static bool IsSupported(double[] kernel, int size, double divisor, double offset)
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

        private static bool IsFloatCompatible(double value)
        {
            return double.IsFinite(value) && float.IsFinite((float)value);
        }

        private partial bool RunConvolve(ReadOnlySpan<PixelShaderInput> resources, ReadOnlySpan<float> constants, byte[] target, int width, int height);

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
