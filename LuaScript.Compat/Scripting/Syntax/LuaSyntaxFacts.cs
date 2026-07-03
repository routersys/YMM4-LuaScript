namespace LuaScript.Compat.Syntax
{
    internal static class LuaSyntaxFacts
    {
        public static bool IsValueEnd(LuaSyntaxToken token) => token.Kind switch
        {
            LuaSyntaxTokenKind.Number or LuaSyntaxTokenKind.String => true,
            LuaSyntaxTokenKind.Name => !LuaKeywords.IsReserved(token.Text) || token.Text is "nil" or "true" or "false",
            LuaSyntaxTokenKind.Operator => token.Text is ")" or "]" or "}",
            _ => false,
        };

        public static bool IsValueStart(LuaSyntaxToken token) => token.Kind switch
        {
            LuaSyntaxTokenKind.Number or LuaSyntaxTokenKind.String => true,
            LuaSyntaxTokenKind.Name => !LuaKeywords.IsReserved(token.Text) || token.Text is "nil" or "true" or "false" or "not" or "function",
            LuaSyntaxTokenKind.Operator => token.Text is "(" or "{" or "-" or "#" or "~",
            _ => false,
        };
    }
}
