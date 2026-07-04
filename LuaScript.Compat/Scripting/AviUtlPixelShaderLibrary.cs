using System;
using System.Collections.Generic;

namespace LuaScript.Compat
{
    internal sealed class AviUtlPixelShaderLibrary
    {
        private const string SectionMarker = "--[[pixelshader@";

        public static AviUtlPixelShaderLibrary Empty { get; } = new(null);

        private readonly Dictionary<string, string>? _shaders;

        private AviUtlPixelShaderLibrary(Dictionary<string, string>? shaders) => _shaders = shaders;

        public int Count => _shaders?.Count ?? 0;

        public static AviUtlPixelShaderLibrary Parse(string? source)
        {
            if (string.IsNullOrEmpty(source))
                return Empty;

            Dictionary<string, string>? shaders = null;
            int index = 0;
            while (index < source.Length)
            {
                int lineStart = index;
                int lineBreak = source.IndexOf('\n', index);
                index = lineBreak < 0 ? source.Length : lineBreak + 1;

                int i = ScriptParserHelper.SkipSpaces(source, lineStart);
                if (!MatchesMarker(source, i))
                    continue;
                i += SectionMarker.Length;

                int nameStart = i;
                while (i < source.Length && IsEntryPointChar(source[i]))
                    i++;
                if (i == nameStart || i >= source.Length || source[i] != ':')
                    continue;

                string name = source[nameStart..i];
                int bodyStart = i + 1;
                int bodyEnd = source.IndexOf("]]", bodyStart, StringComparison.Ordinal);
                if (bodyEnd < 0)
                    continue;

                shaders ??= new Dictionary<string, string>(StringComparer.Ordinal);
                shaders.TryAdd(name, source[bodyStart..bodyEnd]);
                index = bodyEnd + 2;
            }

            return shaders is null ? Empty : new AviUtlPixelShaderLibrary(shaders);
        }

        public bool TryGet(string name, out string hlsl)
        {
            hlsl = string.Empty;
            if (_shaders is null || string.IsNullOrEmpty(name))
                return false;

            int at = name.IndexOf('@');
            string local = at < 0 ? name : name[..at];
            if (_shaders.TryGetValue(local, out var found))
            {
                hlsl = found;
                return true;
            }
            return false;
        }

        public static string EntryPointOf(string name)
        {
            int at = name.IndexOf('@');
            return at < 0 ? name : name[..at];
        }

        private static bool MatchesMarker(string text, int index) =>
            index + SectionMarker.Length <= text.Length &&
            string.CompareOrdinal(text, index, SectionMarker, 0, SectionMarker.Length) == 0;

        private static bool IsEntryPointChar(char c) =>
            ScriptParserHelper.IsNameChar(c) || c == '_';
    }
}
