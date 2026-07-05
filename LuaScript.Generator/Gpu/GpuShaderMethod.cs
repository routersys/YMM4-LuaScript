namespace LuaScript.Generator
{
    internal sealed record GpuShaderMethod(
        string Accessibility,
        bool IsStatic,
        string ReturnType,
        string MethodName,
        EquatableArray<GpuShaderParameter> Parameters,
        string FileName,
        string EntryPoint,
        GpuShaderInvocation Invocation);
}
