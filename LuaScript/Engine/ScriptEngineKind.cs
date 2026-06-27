using System;

namespace LuaScript.Engine
{
    internal enum ScriptEngineKind
    {
        MoonSharp,
        Native,
        Gpu,
        Cpu,
    }

    internal static class ScriptDirective
    {
        public static ScriptEngineKind Resolve(string? script)
        {
            if (string.IsNullOrEmpty(script))
                return ScriptEngineKind.MoonSharp;

            int index = 0;
            int length = script.Length;
            while (index < length)
            {
                int lineStart = index;
                while (index < length && script[index] != '\n')
                    index++;
                int lineEnd = index;
                if (lineEnd > lineStart && script[lineEnd - 1] == '\r')
                    lineEnd--;

                if (TryParseDirectiveLine(script, lineStart, lineEnd, out var kind))
                    return kind;

                index++;
            }

            return ScriptEngineKind.MoonSharp;
        }

        private static bool TryParseDirectiveLine(string script, int start, int end, out ScriptEngineKind kind)
        {
            kind = ScriptEngineKind.MoonSharp;

            int i = start;
            while (i < end && (script[i] == ' ' || script[i] == '\t'))
                i++;

            if (i + 3 > end || script[i] != '-' || script[i + 1] != '-' || script[i + 2] != '!')
                return false;
            i += 3;

            while (i < end && (script[i] == ' ' || script[i] == '\t'))
                i++;

            int tokenStart = i;
            while (i < end && IsTokenChar(script[i]))
                i++;
            int tokenEnd = i;

            while (i < end && (script[i] == ' ' || script[i] == '\t'))
                i++;
            if (i != end)
                return false;

            var token = script.AsSpan(tokenStart, tokenEnd - tokenStart);
            if (token.Equals("native", StringComparison.OrdinalIgnoreCase))
            {
                kind = ScriptEngineKind.Native;
                return true;
            }
            if (token.Equals("gpu", StringComparison.OrdinalIgnoreCase))
            {
                kind = ScriptEngineKind.Gpu;
                return true;
            }
            if (token.Equals("cpu", StringComparison.OrdinalIgnoreCase))
            {
                kind = ScriptEngineKind.Cpu;
                return true;
            }
            if (token.Equals("moonsharp", StringComparison.OrdinalIgnoreCase))
            {
                kind = ScriptEngineKind.MoonSharp;
                return true;
            }
            return false;
        }

        private static bool IsTokenChar(char c) =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
    }
}
