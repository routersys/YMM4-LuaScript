using System;
using LuaScript.Engine.Shader;

namespace LuaScript.Engine.Processing
{
    [GpuPixelOperationShader(nameof(RunResizeNearest), "Shaders/resize.hlsl", "resize_nearest")]
    [GpuPixelOperationShader(nameof(RunResizeLinear), "Shaders/resize.hlsl", "resize_linear")]
    internal sealed partial class GpuResizeOperation(PixelShaderRunner runner) : GpuPixelOperation(runner)
    {
        private const int ResizeThreshold = 262144;

        private readonly PixelShaderInput[] _resources = new PixelShaderInput[1];
        private readonly float[] _constants = new float[4];

        private byte[] _target = [];
        private bool? _nearestValidated;
        private bool? _linearValidated;

        public bool TryResize(byte[] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, bool linear, out byte[]? target)
        {
            target = null;
            if (!IsTextureSupported(source, sourceWidth, sourceHeight) || !IsTextureSizeSupported(targetWidth, targetHeight))
                return false;

            long pixels = (long)targetWidth * targetHeight;
            if (pixels < ResizeThreshold || !(linear ? EnsureLinearValidated(source, sourceWidth, sourceHeight) : EnsureNearestValidated(source, sourceWidth, sourceHeight)))
                return false;

            return RunShader(source, sourceWidth, sourceHeight, targetWidth, targetHeight, linear, out target);
        }

        private bool EnsureNearestValidated(byte[] source, int sourceWidth, int sourceHeight)
        {
            if (_nearestValidated.HasValue)
                return _nearestValidated.Value;
            _nearestValidated = Validate(source, sourceWidth, sourceHeight, false);
            return _nearestValidated.Value;
        }

        private bool EnsureLinearValidated(byte[] source, int sourceWidth, int sourceHeight)
        {
            if (_linearValidated.HasValue)
                return _linearValidated.Value;
            _linearValidated = Validate(source, sourceWidth, sourceHeight, true);
            return _linearValidated.Value;
        }

        private bool Validate(byte[] source, int sourceWidth, int sourceHeight, bool linear)
        {
            int probeWidth = Math.Min(sourceWidth, 8);
            int probeHeight = Math.Min(sourceHeight, 7);
            int targetWidth = Math.Max(1, probeWidth * 2 - 3);
            int targetHeight = Math.Max(1, probeHeight * 2 - 5);
            var probe = CopyProbe(source, sourceWidth, sourceHeight, probeWidth, probeHeight);
            byte[]? sourceScratch = null;
            byte[]? targetScratch = null;
            var cpu = PixelBufferSoftwareProcessor.Resize(probe, probeWidth, probeHeight, targetWidth, targetHeight, linear, ref sourceScratch, ref targetScratch);
            return RunShader(probe, probeWidth, probeHeight, targetWidth, targetHeight, linear, out var gpu) && gpu is not null && Equal(cpu, gpu);
        }

        private bool RunShader(byte[] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, bool linear, out byte[]? target)
        {
            int count = checked(targetWidth * targetHeight * 4);
            if (_target.Length != count)
                _target = new byte[count];

            _constants[0] = sourceWidth;
            _constants[1] = sourceHeight;
            _constants[2] = targetWidth;
            _constants[3] = targetHeight;
            _resources[0] = new PixelShaderInput(source, sourceWidth, sourceHeight);
            target = _target;
            return linear
                ? RunResizeLinear(_resources.AsSpan(0, 1), _constants.AsSpan(0, 4), _target, targetWidth, targetHeight)
                : RunResizeNearest(_resources.AsSpan(0, 1), _constants.AsSpan(0, 4), _target, targetWidth, targetHeight);
        }

        private partial bool RunResizeNearest(ReadOnlySpan<PixelShaderInput> resources, ReadOnlySpan<float> constants, byte[] target, int width, int height);

        private partial bool RunResizeLinear(ReadOnlySpan<PixelShaderInput> resources, ReadOnlySpan<float> constants, byte[] target, int width, int height);
    }
}
