using System.Collections.Generic;
using System.Text;

namespace LuaScript.Compat.Syntax.Rules
{
    [LuaSyntaxRule(98)]
    internal sealed class LuaPipeRule : ILuaSyntaxRule
    {
        public bool TryApply(LuaRewriteContext context)
        {
            if (!context.Current.IsOperator("|>"))
                return false;

            int targetStart = context.NextSignificant(context.Index + 1);
            if (targetStart < 0 || !LuaSyntaxFacts.IsValueStart(context.Input[targetStart]))
                return false;

            if (!context.TryPopOrOperand(out string left))
                return false;

            context.Advance();
            var segment = ReadSegment(context);
            string call = BuildCall(segment, left);
            context.EmitRaw(call);
            return true;
        }

        private static List<LuaSyntaxToken> ReadSegment(LuaRewriteContext context)
        {
            var tokens = new List<LuaSyntaxToken>();
            int depth = 0;
            while (!context.AtEnd)
            {
                var token = context.Current;
                if (token.Kind == LuaSyntaxTokenKind.Newline && depth == 0)
                    break;
                if (token.Kind == LuaSyntaxTokenKind.Name && depth == 0 && LuaKeywords.IsExpressionBoundary(token.Text))
                    break;
                if (token.Kind == LuaSyntaxTokenKind.Operator)
                {
                    if (token.Text is "(" or "[" or "{")
                    {
                        depth++;
                    }
                    else if (token.Text is ")" or "]" or "}")
                    {
                        if (depth == 0)
                            break;
                        depth--;
                    }
                    else if (depth == 0 && token.Text is "," or ";" or "|>")
                    {
                        break;
                    }
                }
                tokens.Add(token);
                context.Advance();
            }
            while (tokens.Count > 0 && tokens[0].IsTrivia)
                tokens.RemoveAt(0);
            while (tokens.Count > 0 && tokens[tokens.Count - 1].IsTrivia)
                tokens.RemoveAt(tokens.Count - 1);
            return tokens;
        }

        private static string BuildCall(List<LuaSyntaxToken> segment, string left)
        {
            string argument = $"({left})";
            if (segment.Count == 0)
                return argument;

            if (segment[segment.Count - 1].IsOperator(")"))
            {
                int open = FindOpeningParen(segment);
                if (open > 0)
                {
                    string callee = Concat(segment, 0, open);
                    string arguments = Concat(segment, open + 1, segment.Count - 1);
                    return arguments.Length == 0
                        ? $"{callee}({argument})"
                        : $"{callee}({argument}, {arguments})";
                }
            }

            return $"{Concat(segment, 0, segment.Count)}({argument})";
        }

        private static int FindOpeningParen(List<LuaSyntaxToken> segment)
        {
            int depth = 0;
            for (int i = segment.Count - 1; i >= 0; i--)
            {
                var token = segment[i];
                if (token.Kind != LuaSyntaxTokenKind.Operator)
                    continue;
                if (token.Text is ")" or "]" or "}")
                {
                    depth++;
                }
                else if (token.Text is "(" or "[" or "{")
                {
                    depth--;
                    if (depth == 0)
                        return token.Text == "(" ? i : -1;
                }
            }
            return -1;
        }

        private static string Concat(List<LuaSyntaxToken> segment, int begin, int end)
        {
            while (begin < end && segment[begin].IsTrivia)
                begin++;
            while (end > begin && segment[end - 1].IsTrivia)
                end--;
            var builder = new StringBuilder();
            for (int i = begin; i < end; i++)
                builder.Append(segment[i].Text);
            return builder.ToString();
        }
    }
}
