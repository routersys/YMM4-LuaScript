using System.Collections.Generic;

namespace LuaScript.Compat.Syntax
{
    internal static class LuaSyntaxLexer
    {
        private static readonly string[] Operators =
        {
            "...", "..=", "??=", "||=", "&&=",
            "==", "~=", "<=", ">=", "..", "::", "??", "||", "&&", "!=", "++",
            "+=", "-=", "*=", "/=", "%=", "^=",
            "+", "-", "*", "/", "%", "^", "#", "<", ">", "=",
            "(", ")", "{", "}", "[", "]", ";", ":", ",", ".",
            "!", "&", "|", "?", "~",
        };

        public static List<LuaSyntaxToken> Tokenize(string source)
        {
            var tokens = new List<LuaSyntaxToken>();
            int i = 0;
            int length = source.Length;

            while (i < length)
            {
                char c = source[i];

                if (c == '\r' || c == '\n')
                {
                    int start = i;
                    if (c == '\r' && i + 1 < length && source[i + 1] == '\n')
                        i += 2;
                    else
                        i++;
                    tokens.Add(new LuaSyntaxToken(LuaSyntaxTokenKind.Newline, source.Substring(start, i - start)));
                    continue;
                }

                if (c == ' ' || c == '\t' || c == '\f' || c == '\v')
                {
                    int start = i;
                    while (i < length && (source[i] == ' ' || source[i] == '\t' || source[i] == '\f' || source[i] == '\v'))
                        i++;
                    tokens.Add(new LuaSyntaxToken(LuaSyntaxTokenKind.Whitespace, source.Substring(start, i - start)));
                    continue;
                }

                if (c == '-' && i + 1 < length && source[i + 1] == '-')
                {
                    int start = i;
                    i = SkipComment(source, i + 2);
                    tokens.Add(new LuaSyntaxToken(LuaSyntaxTokenKind.Comment, source.Substring(start, i - start)));
                    continue;
                }

                if (c == '[')
                {
                    int level = LongBracketLevel(source, i);
                    if (level >= 0)
                    {
                        int start = i;
                        i = SkipLongBracket(source, i, level);
                        tokens.Add(new LuaSyntaxToken(LuaSyntaxTokenKind.String, source.Substring(start, i - start)));
                        continue;
                    }
                }

                if (c == '"' || c == '\'')
                {
                    int start = i;
                    i = SkipShortString(source, i);
                    tokens.Add(new LuaSyntaxToken(LuaSyntaxTokenKind.String, source.Substring(start, i - start)));
                    continue;
                }

                if (IsDigit(c) || (c == '.' && i + 1 < length && IsDigit(source[i + 1])))
                {
                    int start = i;
                    i = SkipNumber(source, i);
                    tokens.Add(new LuaSyntaxToken(LuaSyntaxTokenKind.Number, source.Substring(start, i - start)));
                    continue;
                }

                if (IsNameStart(c))
                {
                    int start = i;
                    while (i < length && IsNamePart(source[i]))
                        i++;
                    tokens.Add(new LuaSyntaxToken(LuaSyntaxTokenKind.Name, source.Substring(start, i - start)));
                    continue;
                }

                string? op = MatchOperator(source, i);
                if (op is not null)
                {
                    tokens.Add(new LuaSyntaxToken(LuaSyntaxTokenKind.Operator, op));
                    i += op.Length;
                    continue;
                }

                tokens.Add(new LuaSyntaxToken(LuaSyntaxTokenKind.Operator, source.Substring(i, 1)));
                i++;
            }

            return tokens;
        }

        private static string? MatchOperator(string source, int index)
        {
            int remaining = source.Length - index;
            foreach (var op in Operators)
            {
                if (op.Length > remaining)
                    continue;
                if (string.CompareOrdinal(source, index, op, 0, op.Length) == 0)
                    return op;
            }
            return null;
        }

        private static int SkipNumber(string source, int i)
        {
            int length = source.Length;
            if (source[i] == '0' && i + 1 < length && (source[i + 1] == 'x' || source[i + 1] == 'X'))
            {
                i += 2;
                while (i < length && (IsHexDigit(source[i]) || source[i] == '.'))
                    i++;
                if (i < length && (source[i] == 'p' || source[i] == 'P'))
                {
                    i++;
                    if (i < length && (source[i] == '+' || source[i] == '-'))
                        i++;
                    while (i < length && IsDigit(source[i]))
                        i++;
                }
                return i;
            }

            while (i < length && IsDigit(source[i]))
                i++;
            if (i < length && source[i] == '.' && !(i + 1 < length && source[i + 1] == '.'))
            {
                i++;
                while (i < length && IsDigit(source[i]))
                    i++;
            }
            if (i < length && (source[i] == 'e' || source[i] == 'E'))
            {
                i++;
                if (i < length && (source[i] == '+' || source[i] == '-'))
                    i++;
                while (i < length && IsDigit(source[i]))
                    i++;
            }
            return i;
        }

        private static int SkipComment(string source, int i)
        {
            int level = LongBracketLevel(source, i);
            if (level >= 0)
                return SkipLongBracket(source, i, level);

            int length = source.Length;
            while (i < length && source[i] != '\n' && source[i] != '\r')
                i++;
            return i;
        }

        private static int LongBracketLevel(string source, int i)
        {
            if (i >= source.Length || source[i] != '[')
                return -1;
            int j = i + 1;
            int level = 0;
            while (j < source.Length && source[j] == '=')
            {
                level++;
                j++;
            }
            return j < source.Length && source[j] == '[' ? level : -1;
        }

        private static int SkipLongBracket(string source, int i, int level)
        {
            int length = source.Length;
            i += level + 2;
            while (i < length)
            {
                if (source[i] == ']')
                {
                    int j = i + 1;
                    int count = 0;
                    while (j < length && source[j] == '=')
                    {
                        count++;
                        j++;
                    }
                    if (count == level && j < length && source[j] == ']')
                        return j + 1;
                }
                i++;
            }
            return length;
        }

        private static int SkipShortString(string source, int start)
        {
            char quote = source[start];
            int i = start + 1;
            int length = source.Length;
            while (i < length)
            {
                char c = source[i];
                if (c == '\\')
                {
                    i += 2;
                    continue;
                }
                if (c == quote)
                    return i + 1;
                if (c == '\n' || c == '\r')
                    return i;
                i++;
            }
            return length;
        }

        private static bool IsNameStart(char c) => c == '_' || char.IsLetter(c);

        private static bool IsNamePart(char c) => c == '_' || char.IsLetterOrDigit(c);

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        private static bool IsHexDigit(char c) => IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    }
}
