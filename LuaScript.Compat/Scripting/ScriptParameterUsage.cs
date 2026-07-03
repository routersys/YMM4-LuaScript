using System;
using System.Collections.Generic;
using LuaScript.Compat.Syntax;

namespace LuaScript.Compat
{
    internal readonly struct ScriptParameterUsage
    {
        private readonly HashSet<string>? _members;
        private readonly bool _color;

        private ScriptParameterUsage(HashSet<string>? members, bool color)
        {
            _members = members;
            _color = color;
        }

        public static readonly ScriptParameterUsage None = default;

        public bool Color => _color;

        public bool Uses(string member) => _members is not null && _members.Contains(member);

        public bool Check0 => Uses("check0");
        public bool Check1 => Uses("check1");
        public bool Check2 => Uses("check2");
        public bool Check3 => Uses("check3");

        public bool Check(int index) => (uint)index < 4 && Uses("check" + (char)('0' + index));

        public bool Slider(int index) => (uint)index < 4 && Uses("slider" + (char)('0' + index));

        public static ScriptParameterUsage Detect(string? script)
        {
            if (string.IsNullOrEmpty(script))
                return None;

            var tokens = LuaSyntaxLexer.Tokenize(script);

            HashSet<string>? members = null;
            bool color = false;

            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (token.Kind != LuaSyntaxTokenKind.Name)
                    continue;

                if (token.Text == "color")
                {
                    int previous = PreviousSignificant(tokens, i);
                    if (previous < 0 || !tokens[previous].IsOperator("."))
                        color = true;
                    continue;
                }

                if (token.Text == "obj")
                {
                    int dot = NextSignificant(tokens, i + 1);
                    if (dot < 0 || !tokens[dot].IsOperator("."))
                        continue;

                    int member = NextSignificant(tokens, dot + 1);
                    if (member < 0 || tokens[member].Kind != LuaSyntaxTokenKind.Name)
                        continue;

                    (members ??= new HashSet<string>(StringComparer.Ordinal)).Add(tokens[member].Text);
                }
            }

            return new ScriptParameterUsage(members, color);
        }

        private static int PreviousSignificant(List<LuaSyntaxToken> tokens, int from)
        {
            for (int i = from - 1; i >= 0; i--)
            {
                if (!tokens[i].IsTrivia)
                    return i;
            }
            return -1;
        }

        private static int NextSignificant(List<LuaSyntaxToken> tokens, int from)
        {
            for (int i = from; i < tokens.Count; i++)
            {
                if (!tokens[i].IsTrivia)
                    return i;
            }
            return -1;
        }
    }
}
