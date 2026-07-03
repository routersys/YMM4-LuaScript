namespace LuaScript.Engine.Kernel
{
    internal sealed record MethodCallExpr(LuaExpr Target, string Method, IReadOnlyList<LuaExpr> Arguments) : LuaExpr;
}
