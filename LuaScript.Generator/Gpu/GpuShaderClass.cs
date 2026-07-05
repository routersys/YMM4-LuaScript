namespace LuaScript.Generator
{
    internal sealed record GpuShaderClass(
        string Namespace,
        EquatableArray<GpuShaderTypePart> TypeChain,
        EquatableArray<GpuShaderMethod> Methods,
        EquatableArray<string> Errors);
}
