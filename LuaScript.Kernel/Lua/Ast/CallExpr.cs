namespace LuaScript.Engine.Kernel
{
    internal sealed record CallExpr(LuaExpr Target, IReadOnlyList<LuaExpr> Arguments) : LuaExpr;
}
