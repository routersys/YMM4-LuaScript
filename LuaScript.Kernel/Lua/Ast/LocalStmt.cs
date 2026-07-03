namespace LuaScript.Engine.Kernel
{
    internal sealed record LocalStmt(IReadOnlyList<string> Names, IReadOnlyList<LuaExpr> Values) : LuaStmt;
}
