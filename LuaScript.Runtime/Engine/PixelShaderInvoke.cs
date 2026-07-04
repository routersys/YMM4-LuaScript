using System;

namespace LuaScript.Engine
{
    internal delegate PixelShaderRunStatus PixelShaderInvoke(
        string name,
        ReadOnlySpan<PixelShaderInput> resources,
        ReadOnlySpan<float> constants,
        PixelShaderBlend blend,
        PixelShaderSampler sampler,
        byte[] target,
        int targetWidth,
        int targetHeight,
        out string? error);
}
