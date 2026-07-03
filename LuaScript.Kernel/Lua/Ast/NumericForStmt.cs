namespace LuaScript.Engine.Kernel
{
    internal sealed record NumericForStmt(
        string Variable,
        LuaExpr Start,
        LuaExpr Stop,
        LuaExpr? Step,
        IReadOnlyList<LuaStmt> Body) : LuaStmt;
}
