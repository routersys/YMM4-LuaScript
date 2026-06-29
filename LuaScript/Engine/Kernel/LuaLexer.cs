using System;
using System.Collections.Generic;
using System.Globalization;

namespace LuaScript.Engine.Kernel
{
    internal static class LuaLexer
    {
        private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
        {
            "and", "break", "do", "else", "elseif", "end", "false", "for", "function",
            "if", "in", "local", "nil", "not", "or", "repeat", "return", "then",
            "true", "until", "while",
        };

        public static List<LuaToken> Tokenize(string source)
        {
            var tokens = new List<LuaToken>();
            int i = 0;
            int length = source.Length;

            while (i < length)
            {
                char c = source[i];

                if (c is ' ' or '\t' or '\r' or '\n' or '\f' or '\v')
                {
                    i++;
                    continue;
                }

                if (c == '-' && i + 1 < length && source[i + 1] == '-')
                {
                    i = SkipComment(source, i + 2);
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    i = ReadShortString(source, i, tokens);
                    continue;
                }

                if (c == '[' && i + 1 < length && (source[i + 1] == '[' || source[i + 1] == '='))
                {
                    int level = LongBracketLevel(source, i);
                    if (level >= 0)
                    {
                        i = ReadLongString(source, i, level, tokens);
                        continue;
                    }
                }

                if (char.IsDigit(c) || (c == '.' && i + 1 < length && char.IsDigit(source[i + 1])))
                {
                    i = ReadNumber(source, i, tokens);
                    continue;
                }

                if (IsNameStart(c))
                {
                    i = ReadName(source, i, tokens);
                    continue;
                }

                i = ReadSymbol(source, i, tokens);
            }

            tokens.Add(new LuaToken(LuaTokenKind.Eof, string.Empty, 0d));
            return tokens;
        }

        private static int SkipComment(string source, int i)
        {
            int level = LongBracketLevel(source, i);
            if (level >= 0)
                return SkipLongBracket(source, i, level);

            int length = source.Length;
            while (i < length && source[i] != '\n')
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
            throw new KernelUnsupportedException("Unterminated long bracket.");
        }

        private static int ReadLongString(string source, int start, int level, List<LuaToken> tokens)
        {
            int end = SkipLongBracket(source, start, level);
            int contentStart = start + level + 2;
            int contentEnd = end - level - 2;
            string text = source.Substring(contentStart, Math.Max(0, contentEnd - contentStart));
            tokens.Add(new LuaToken(LuaTokenKind.String, text, 0d));
            return end;
        }

        private static int ReadShortString(string source, int start, List<LuaToken> tokens)
        {
            char quote = source[start];
            int i = start + 1;
            int length = source.Length;
            var value = new System.Text.StringBuilder();

            while (i < length)
            {
                char c = source[i];
                if (c == quote)
                {
                    tokens.Add(new LuaToken(LuaTokenKind.String, value.ToString(), 0d));
                    return i + 1;
                }
                if (c == '\n')
                    throw new KernelUnsupportedException("Unterminated string.");
                if (c == '\\')
                {
                    i = ReadEscape(source, i + 1, value);
                    continue;
                }
                value.Append(c);
                i++;
            }
            throw new KernelUnsupportedException("Unterminated string.");
        }

        private static int ReadEscape(string source, int i, System.Text.StringBuilder value)
        {
            if (i >= source.Length)
                throw new KernelUnsupportedException("Unterminated string escape.");

            char c = source[i];
            switch (c)
            {
                case 'n': value.Append('\n'); return i + 1;
                case 't': value.Append('\t'); return i + 1;
                case 'r': value.Append('\r'); return i + 1;
                case 'a': value.Append('\a'); return i + 1;
                case 'b': value.Append('\b'); return i + 1;
                case 'f': value.Append('\f'); return i + 1;
                case 'v': value.Append('\v'); return i + 1;
                case '\\': value.Append('\\'); return i + 1;
                case '"': value.Append('"'); return i + 1;
                case '\'': value.Append('\''); return i + 1;
                case '\n': value.Append('\n'); return i + 1;
                default:
                    if (char.IsDigit(c))
                    {
                        int n = 0;
                        int count = 0;
                        while (i < source.Length && count < 3 && char.IsDigit(source[i]))
                        {
                            n = n * 10 + (source[i] - '0');
                            i++;
                            count++;
                        }
                        value.Append((char)n);
                        return i;
                    }
                    value.Append(c);
                    return i + 1;
            }
        }

        private static int ReadNumber(string source, int start, List<LuaToken> tokens)
        {
            int length = source.Length;
            int i = start;

            if (source[i] == '0' && i + 1 < length && (source[i + 1] == 'x' || source[i + 1] == 'X'))
            {
                i += 2;
                int digitsStart = i;
                while (i < length && Uri.IsHexDigit(source[i]))
                    i++;
                if (i == digitsStart)
                    throw new KernelUnsupportedException("Malformed hexadecimal literal.");
                string hex = source.Substring(digitsStart, i - digitsStart);
                double hexValue = (double)ulong.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                tokens.Add(new LuaToken(LuaTokenKind.Number, source.Substring(start, i - start), hexValue));
                return i;
            }

            while (i < length && char.IsDigit(source[i]))
                i++;
            if (i < length && source[i] == '.')
            {
                i++;
                while (i < length && char.IsDigit(source[i]))
                    i++;
            }
            if (i < length && (source[i] == 'e' || source[i] == 'E'))
            {
                i++;
                if (i < length && (source[i] == '+' || source[i] == '-'))
                    i++;
                while (i < length && char.IsDigit(source[i]))
                    i++;
            }

            string text = source.Substring(start, i - start);
            double value = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            tokens.Add(new LuaToken(LuaTokenKind.Number, text, value));
            return i;
        }

        private static int ReadName(string source, int start, List<LuaToken> tokens)
        {
            int i = start;
            int length = source.Length;
            while (i < length && IsNamePart(source[i]))
                i++;
            string text = source.Substring(start, i - start);
            tokens.Add(new LuaToken(Keywords.Contains(text) ? LuaTokenKind.Keyword : LuaTokenKind.Name, text, 0d));
            return i;
        }

        private static int ReadSymbol(string source, int start, List<LuaToken> tokens)
        {
            int length = source.Length;
            char c = source[start];

            if (c == '.' && start + 2 < length && source[start + 1] == '.' && source[start + 2] == '.')
            {
                tokens.Add(new LuaToken(LuaTokenKind.Symbol, "...", 0d));
                return start + 3;
            }

            if (start + 1 < length)
            {
                string two = source.Substring(start, 2);
                switch (two)
                {
                    case "==":
                    case "~=":
                    case "<=":
                    case ">=":
                    case "..":
                        tokens.Add(new LuaToken(LuaTokenKind.Symbol, two, 0d));
                        return start + 2;
                }
            }

            switch (c)
            {
                case '+':
                case '-':
                case '*':
                case '/':
                case '%':
                case '^':
                case '#':
                case '<':
                case '>':
                case '=':
                case '(':
                case ')':
                case '{':
                case '}':
                case '[':
                case ']':
                case ';':
                case ':':
                case ',':
                case '.':
                    tokens.Add(new LuaToken(LuaTokenKind.Symbol, c.ToString(), 0d));
                    return start + 1;
                default:
                    throw new KernelUnsupportedException($"Unexpected character '{c}'.");
            }
        }

        private static bool IsNameStart(char c) => c == '_' || char.IsLetter(c);

        private static bool IsNamePart(char c) => c == '_' || char.IsLetterOrDigit(c);
    }
}
