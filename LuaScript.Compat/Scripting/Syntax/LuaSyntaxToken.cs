namespace LuaScript.Compat.Syntax
{
    internal readonly struct LuaSyntaxToken
    {
        public LuaSyntaxToken(LuaSyntaxTokenKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }

        public LuaSyntaxTokenKind Kind { get; }

        public string Text { get; }

        public bool IsTrivia => Kind is LuaSyntaxTokenKind.Whitespace or LuaSyntaxTokenKind.Newline or LuaSyntaxTokenKind.Comment;

        public bool IsName(string value) => Kind == LuaSyntaxTokenKind.Name && Text == value;

        public bool IsOperator(string value) => Kind == LuaSyntaxTokenKind.Operator && Text == value;
    }
}
