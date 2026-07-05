using System.Collections.Immutable;

namespace LuaScript.Generator
{
    internal sealed record GpuShaderMethod(
        string Accessibility,
        bool IsStatic,
        string ReturnType,
        string MethodName,
        ImmutableArray<GpuShaderParameter> Parameters,
        string FileName,
        string EntryPoint,
        GpuShaderInvocation Invocation);
}
