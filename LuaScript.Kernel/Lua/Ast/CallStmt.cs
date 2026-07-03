namespace LuaScript.Engine.Kernel
{
    internal sealed record CallStmt(LuaExpr Call) : LuaStmt;
}
