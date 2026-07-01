using System;

namespace LuaScript.Engine
{
    internal static class ScriptDirective
    {
        public static ScriptEngineKind Resolve(string? script) =>
            TryGetDirective(script, out var kind) ? kind : ScriptEngineKind.MoonSharp;

        public static ScriptEngineKind ResolveAuto(string? script)
        {
            if (TryGetDirective(script, out var kind))
                return kind;
            if (UsesDrawingApi(script))
                return ScriptEngineKind.MoonSharp;
            return UsesPixelApi(script) ? ScriptEngineKind.Native : ScriptEngineKind.MoonSharp;
        }

        public static bool TryResolveExplicit(string? script, out ScriptEngineKind kind) =>
            TryGetDirective(script, out kind);

        public static bool UsesPixelApi(string? script) =>
            script is not null && (script.Contains("getpixel", StringComparison.Ordinal) || script.Contains("setpixel", StringComparison.Ordinal));

        public static bool UsesDrawingApi(string? script) =>
            script is not null &&
                (script.Contains("obj.setoption", StringComparison.Ordinal) ||
                 script.Contains("obj.setanchor", StringComparison.Ordinal));

        private static bool TryGetDirective(string? script, out ScriptEngineKind kind)
        {
            kind = ScriptEngineKind.MoonSharp;
            if (string.IsNullOrEmpty(script))
                return false;

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

                if (TryParseDirectiveLine(script, lineStart, lineEnd, out kind))
                    return true;

                index++;
            }

            return false;
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
