namespace LuaScript.Engine.Kernel
{
    internal sealed record AssignStmt(IReadOnlyList<LuaExpr> Targets, IReadOnlyList<LuaExpr> Values) : LuaStmt;
}
