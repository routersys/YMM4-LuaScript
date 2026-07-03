namespace LuaScript.Engine.Kernel
{
    internal sealed record UnaryExpr(string Operator, LuaExpr Operand) : LuaExpr;
}
