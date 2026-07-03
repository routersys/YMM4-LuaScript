namespace LuaScript.Engine.Kernel
{
    internal sealed record IfClause(LuaExpr Condition, IReadOnlyList<LuaStmt> Body);
}
