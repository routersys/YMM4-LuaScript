using System;
using System.Collections.Generic;
using LuaScript.Engine.Kernel;

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

            List<LuaToken> tokens;
            try
            {
                tokens = LuaLexer.Tokenize(script);
            }
            catch (KernelUnsupportedException)
            {
                return None;
            }

            HashSet<string>? members = null;
            bool color = false;

            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (token.Kind != LuaTokenKind.Name)
                    continue;

                if (token.Text == "color")
                {
                    if (i == 0 || !tokens[i - 1].IsSymbol("."))
                        color = true;
                    continue;
                }

                if (token.Text == "obj" &&
                    i + 2 < tokens.Count &&
                    tokens[i + 1].IsSymbol(".") &&
                    tokens[i + 2].Kind == LuaTokenKind.Name)
                {
                    (members ??= new HashSet<string>(StringComparer.Ordinal)).Add(tokens[i + 2].Text);
                }
            }

            return new ScriptParameterUsage(members, color);
        }
    }
}
