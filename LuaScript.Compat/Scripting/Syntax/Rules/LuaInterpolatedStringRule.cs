using System.Collections.Generic;
using System.Text;

namespace LuaScript.Compat.Syntax.Rules
{
    [LuaSyntaxRule(5)]
    internal sealed class LuaInterpolatedStringRule : ILuaSyntaxRule
    {
        public bool TryApply(LuaRewriteContext context)
        {
            if (context.Current.Kind != LuaSyntaxTokenKind.InterpolatedString)
                return false;

            string text = context.Current.Text;
            int end = text.Length;
            if (end >= 2 && text[end - 1] == '`')
                end--;
            string content = text.Substring(1, end - 1);

            var pieces = new List<string>();
            var literal = new StringBuilder();
            int i = 0;
            while (i < content.Length)
            {
                char c = content[i];
                if (c == '\\' && i + 1 < content.Length)
                {
                    literal.Append(content[i]);
                    literal.Append(content[i + 1]);
                    i += 2;
                    continue;
                }
                if (c == '{')
                {
                    int close = FindClosingBrace(content, i + 1);
                    if (close < 0)
                    {
                        literal.Append(c);
                        i++;
                        continue;
                    }
                    string expression = content.Substring(i + 1, close - i - 1).Trim();
                    if (expression.Length > 0)
                    {
                        FlushLiteral(pieces, literal);
                        pieces.Add($"tostring({LuaSyntaxExtensions.Rewrite(expression)})");
                    }
                    i = close + 1;
                    continue;
                }
                literal.Append(c);
                i++;
            }
            FlushLiteral(pieces, literal);

            context.Advance();
            if (pieces.Count == 0)
            {
                context.EmitRaw("\"\"");
                return true;
            }
            if (pieces.Count == 1)
            {
                context.EmitRaw(pieces[0]);
                return true;
            }
            context.EmitRaw($"({string.Join(" .. ", pieces)})");
            return true;
        }

        private static void FlushLiteral(List<string> pieces, StringBuilder literal)
        {
            if (literal.Length == 0)
                return;
            pieces.Add($"\"{literal.ToString().Replace("\"", "\\\"")}\"");
            literal.Clear();
        }

        private static int FindClosingBrace(string content, int from)
        {
            int depth = 0;
            char quote = '\0';
            for (int i = from; i < content.Length; i++)
            {
                char c = content[i];
                if (quote != '\0')
                {
                    if (c == '\\')
                    {
                        i++;
                        continue;
                    }
                    if (c == quote)
                        quote = '\0';
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    quote = c;
                    continue;
                }
                if (c == '{')
                {
                    depth++;
                    continue;
                }
                if (c == '}')
                {
                    if (depth == 0)
                        return i;
                    depth--;
                }
            }
            return -1;
        }
    }
}
