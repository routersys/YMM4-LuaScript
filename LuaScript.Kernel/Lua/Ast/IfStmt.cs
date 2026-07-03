namespace LuaScript.Engine.Kernel
{
    internal sealed record IfStmt(IReadOnlyList<IfClause> Clauses, IReadOnlyList<LuaStmt>? ElseBody) : LuaStmt;
}
