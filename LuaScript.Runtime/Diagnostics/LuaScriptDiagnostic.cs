namespace LuaScript.Diagnostics
{
    internal readonly record struct LuaScriptDiagnostic(
        LuaScriptDiagnosticKind Kind,
        int Line,
        int Column,
        int Length,
        string Message);
}
