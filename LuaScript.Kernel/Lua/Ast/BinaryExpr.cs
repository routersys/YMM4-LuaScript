namespace LuaScript.Engine.Kernel
{
    internal sealed record BinaryExpr(string Operator, LuaExpr Left, LuaExpr Right) : LuaExpr;
}
