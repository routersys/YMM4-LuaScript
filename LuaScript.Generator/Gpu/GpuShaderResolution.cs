using System.Collections.Immutable;

namespace LuaScript.Generator
{
    internal sealed record GpuShaderResolution(ImmutableArray<GpuShaderMethodSource> Methods, ImmutableArray<string> Errors);
}
