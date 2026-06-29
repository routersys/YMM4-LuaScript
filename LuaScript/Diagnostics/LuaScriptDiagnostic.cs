using System;
using System.Text.RegularExpressions;

namespace LuaScript.Diagnostics
{
    internal enum LuaScriptDiagnosticKind
    {
        Compile,
        Runtime,
        Timeout,
    }

    internal readonly record struct LuaScriptDiagnostic(
        LuaScriptDiagnosticKind Kind,
        int Line,
        int Column,
        int Length,
        string Message);

    internal static partial class LuaScriptDiagnosticParser
    {
        [GeneratedRegex(@"^LuaScript:\((\d+),(\d+)(?:-(\d+)(?:,(\d+))?)?\):\s*", RegexOptions.None)]
        private static partial Regex LocationPattern();

        public static LuaScriptDiagnostic Parse(LuaScriptDiagnosticKind kind, string? message)
        {
            var text = message ?? string.Empty;
            var match = LocationPattern().Match(text);
            if (!match.Success)
                return new LuaScriptDiagnostic(kind, 0, 0, 0, text.Trim());

            int line = int.Parse(match.Groups[1].Value);
            int columnStart = int.Parse(match.Groups[2].Value);

            int length = 0;
            if (match.Groups[3].Success && !match.Groups[4].Success)
                length = Math.Max(0, int.Parse(match.Groups[3].Value) - columnStart);

            return new LuaScriptDiagnostic(kind, line, columnStart + 1, length, text[match.Length..].Trim());
        }
    }
}
