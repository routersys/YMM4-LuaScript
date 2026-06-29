using System;

namespace LuaScript.Engine.Kernel
{
    internal enum LuaTokenKind
    {
        Name,
        Number,
        String,
        Keyword,
        Symbol,
        Eof,
    }

    internal readonly struct LuaToken(LuaTokenKind kind, string text, double number)
    {
        public LuaTokenKind Kind { get; } = kind;
        public string Text { get; } = text;
        public double Number { get; } = number;

        public bool Is(LuaTokenKind kind, string text) =>
            Kind == kind && string.Equals(Text, text, StringComparison.Ordinal);

        public bool IsSymbol(string text) => Is(LuaTokenKind.Symbol, text);

        public bool IsKeyword(string text) => Is(LuaTokenKind.Keyword, text);
    }
}
