namespace LuaScript.Engine.Kernel
{
    internal sealed record KCall(KFunc Func, IReadOnlyList<KExpr> Arguments) : KExpr;
}
