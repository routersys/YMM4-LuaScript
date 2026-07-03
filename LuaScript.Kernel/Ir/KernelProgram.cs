namespace LuaScript.Engine.Kernel
{
    internal sealed record KernelProgram(
        IReadOnlyList<KExpr> Bindings,
        KExpr OutputR,
        KExpr OutputG,
        KExpr OutputB,
        KExpr OutputA,
        IReadOnlyList<KernelUniform> Uniforms);
}
