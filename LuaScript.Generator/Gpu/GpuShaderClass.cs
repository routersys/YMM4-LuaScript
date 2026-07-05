using System.Collections.Immutable;

namespace LuaScript.Generator
{
    internal sealed record GpuShaderClass(
        string Namespace,
        ImmutableArray<GpuShaderTypePart> TypeChain,
        ImmutableArray<GpuShaderMethod> Methods,
        ImmutableArray<string> Errors);
}
