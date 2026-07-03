using System;
using System.Collections.Generic;
using System.Text;

namespace LuaScript.Compat.Syntax.Rules
{
    [LuaSyntaxRule(110)]
    internal sealed class LuaNumericRangeRule : ILuaSyntaxRule
    {
        public bool TryApply(LuaRewriteContext context)
        {
            if (!context.Current.IsName("in"))
                return false;

            int open = context.NextSignificant(context.Index + 1);
            if (open < 0 || !context.Input[open].IsOperator("<"))
                return false;
            if (!TryParseRange(context.Input, open, out string replacement, out int end))
                return false;

            context.EmitRaw(replacement);
            context.Index = end;
            return true;
        }

        private static bool TryParseRange(IReadOnlyList<LuaSyntaxToken> input, int open, out string replacement, out int end)
        {
            replacement = string.Empty;
            end = open;

            int position = open + 1;
            if (!TryReadSegment(input, ref position, "..", ">", out string start, out string startTerminator) || startTerminator != "..")
                return false;

            bool exclusive = false;
            int caret = NextSignificant(input, position);
            if (caret >= 0 && input[caret].IsOperator("<"))
            {
                exclusive = true;
                position = caret + 1;
            }

            if (!TryReadSegment(input, ref position, ",", ">", out string stop, out string stopTerminator))
                return false;

            string? step = null;
            if (stopTerminator == ",")
            {
                if (!TryReadSegment(input, ref position, ">", ">", out string stepText, out string stepTerminator) || stepTerminator != ">")
                    return false;
                step = stepText;
            }
            else if (stopTerminator != ">")
            {
                return false;
            }

            if (start.Length == 0 || stop.Length == 0 || (step is not null && step.Length == 0))
                return false;

            string stopExpression = exclusive ? $"({stop}) - 1" : stop;
            replacement = step is null ? $"= {start}, {stopExpression}" : $"= {start}, {stopExpression}, {step}";
            end = position;
            return true;
        }

        private static bool TryReadSegment(IReadOnlyList<LuaSyntaxToken> input, ref int position, string primaryStop, string secondaryStop, out string text, out string terminator)
        {
            text = string.Empty;
            terminator = string.Empty;
            int start = position;
            int depth = 0;
            int count = input.Count;

            while (position < count)
            {
                var token = input[position];
                if (token.Kind == LuaSyntaxTokenKind.Newline && depth == 0)
                    return false;
                if (token.Kind == LuaSyntaxTokenKind.Operator)
                {
                    if (token.Text is "(" or "[" or "{")
                    {
                        depth++;
                    }
                    else if (token.Text is ")" or "]" or "}")
                    {
                        if (depth == 0)
                            return false;
                        depth--;
                    }
                    else if (depth == 0 && (token.Text == primaryStop || token.Text == secondaryStop))
                    {
                        terminator = token.Text;
                        text = Trim(input, start, position);
                        position++;
                        return true;
                    }
                }
                position++;
            }
            return false;
        }

        private static int NextSignificant(IReadOnlyList<LuaSyntaxToken> input, int from)
        {
            for (int i = from; i < input.Count; i++)
            {
                if (!input[i].IsTrivia)
                    return i;
            }
            return -1;
        }

        private static string Trim(IReadOnlyList<LuaSyntaxToken> input, int begin, int end)
        {
            while (begin < end && input[begin].IsTrivia)
                begin++;
            while (end > begin && input[end - 1].IsTrivia)
                end--;
            var builder = new StringBuilder();
            for (int i = begin; i < end; i++)
                builder.Append(input[i].Text);
            return builder.ToString();
        }
    }
}
