using System.Collections.Generic;

namespace LuaScript.Engine.Kernel
{
    internal abstract record LuaExpr;

    internal sealed record NumberExpr(double Value) : LuaExpr;

    internal sealed record BoolExpr(bool Value) : LuaExpr;

    internal sealed record NilExpr : LuaExpr;

    internal sealed record StringExpr(string Value) : LuaExpr;

    internal sealed record NameExpr(string Name) : LuaExpr;

    internal sealed record MemberExpr(LuaExpr Target, string Name) : LuaExpr;

    internal sealed record IndexExpr(LuaExpr Target, LuaExpr Key) : LuaExpr;

    internal sealed record CallExpr(LuaExpr Target, IReadOnlyList<LuaExpr> Arguments) : LuaExpr;

    internal sealed record MethodCallExpr(LuaExpr Target, string Method, IReadOnlyList<LuaExpr> Arguments) : LuaExpr;

    internal sealed record UnaryExpr(string Operator, LuaExpr Operand) : LuaExpr;

    internal sealed record BinaryExpr(string Operator, LuaExpr Left, LuaExpr Right) : LuaExpr;

    internal abstract record LuaStmt;

    internal sealed record LocalStmt(IReadOnlyList<string> Names, IReadOnlyList<LuaExpr> Values) : LuaStmt;

    internal sealed record AssignStmt(IReadOnlyList<LuaExpr> Targets, IReadOnlyList<LuaExpr> Values) : LuaStmt;

    internal sealed record CallStmt(LuaExpr Call) : LuaStmt;

    internal sealed record IfClause(LuaExpr Condition, IReadOnlyList<LuaStmt> Body);

    internal sealed record IfStmt(IReadOnlyList<IfClause> Clauses, IReadOnlyList<LuaStmt>? ElseBody) : LuaStmt;

    internal sealed record NumericForStmt(
        string Variable,
        LuaExpr Start,
        LuaExpr Stop,
        LuaExpr? Step,
        IReadOnlyList<LuaStmt> Body) : LuaStmt;
}
