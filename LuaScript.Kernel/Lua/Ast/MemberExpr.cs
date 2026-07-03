namespace LuaScript.Engine.Kernel
{
    internal sealed record MemberExpr(LuaExpr Target, string Name) : LuaExpr;
}
