using System.Collections.Generic;
using System.Text;

namespace LuaScript.Compat.Syntax
{
    internal readonly struct LuaRangeParts
    {
        public LuaRangeParts(string start, string stop, string? step, bool exclusive)
        {
            Start = start;
            Stop = stop;
            Step = step;
            Exclusive = exclusive;
        }

        public string Start { get; }

        public string Stop { get; }

        public string? Step { get; }

        public bool Exclusive { get; }
    }

    internal static class LuaRangeSyntax
    {
        public static bool TryParse(IReadOnlyList<LuaSyntaxToken> input, int open, out LuaRangeParts parts, out int end)
        {
            parts = default;
            end = open;

            if (open < 0 || open >= input.Count || !input[open].IsOperator("<"))
                return false;

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

            parts = new LuaRangeParts(start, stop, step, exclusive);
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
