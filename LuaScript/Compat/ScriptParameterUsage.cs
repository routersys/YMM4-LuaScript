using System;
using System.Collections.Generic;
using LuaScript.Engine.Kernel;

namespace LuaScript.Compat
{
    internal readonly struct ScriptParameterUsage
    {
        public bool Check0 { get; }
        public bool Check1 { get; }
        public bool Check2 { get; }
        public bool Check3 { get; }
        public bool Color { get; }

        private ScriptParameterUsage(bool check0, bool check1, bool check2, bool check3, bool color)
        {
            Check0 = check0;
            Check1 = check1;
            Check2 = check2;
            Check3 = check3;
            Color = color;
        }

        public static readonly ScriptParameterUsage None = default;

        public bool Check(int index) => index switch
        {
            0 => Check0,
            1 => Check1,
            2 => Check2,
            3 => Check3,
            _ => false,
        };

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

            var checks = new bool[4];
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
                    string member = tokens[i + 2].Text;
                    if (member.Length == 6 && member.StartsWith("check", StringComparison.Ordinal))
                    {
                        char digit = member[5];
                        if (digit is >= '0' and <= '3')
                            checks[digit - '0'] = true;
                    }
                }
            }

            return new ScriptParameterUsage(checks[0], checks[1], checks[2], checks[3], color);
        }
    }
}
