namespace LuaScript.Engine.Kernel
{
    internal sealed record IndexExpr(LuaExpr Target, LuaExpr Key) : LuaExpr;
}
