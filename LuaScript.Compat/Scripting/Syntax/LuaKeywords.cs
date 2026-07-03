using System.Collections.Generic;

namespace LuaScript.Compat.Syntax
{
    internal static class LuaKeywords
    {
        private static readonly HashSet<string> Reserved = new()
        {
            "and", "break", "do", "else", "elseif", "end", "false", "for", "function",
            "goto", "if", "in", "local", "nil", "not", "or", "repeat", "return",
            "then", "true", "until", "while",
        };

        private static readonly HashSet<string> ExpressionSafe = new()
        {
            "and", "or", "not", "nil", "true", "false",
        };

        public static bool IsReserved(string name) => Reserved.Contains(name);

        public static bool IsExpressionBoundary(string name) => Reserved.Contains(name) && !ExpressionSafe.Contains(name);
    }
}
