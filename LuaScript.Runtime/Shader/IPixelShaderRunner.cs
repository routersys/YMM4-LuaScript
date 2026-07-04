using System;

namespace LuaScript
{
    internal interface IPixelShaderRunner
    {
        PixelShaderRunStatus TryRun(
            string hlsl,
            string entryPoint,
            ReadOnlySpan<PixelShaderInput> resources,
            ReadOnlySpan<float> constants,
            PixelShaderBlend blend,
            PixelShaderSampler sampler,
            byte[] target,
            int targetWidth,
            int targetHeight,
            out string? error);
    }
}
