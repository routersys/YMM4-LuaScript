using System;
using System.Text;

namespace LuaScript.Compat
{
    internal static class LuaSyntaxExtensions
    {
        public static string Rewrite(string source)
        {
            if (string.IsNullOrEmpty(source) || source.IndexOf("is", StringComparison.Ordinal) < 0)
                return source;

            var builder = new StringBuilder(source.Length);
            int i = 0;
            int length = source.Length;

            while (i < length)
            {
                char c = source[i];

                if (c == '-' && i + 1 < length && source[i + 1] == '-')
                {
                    int start = i;
                    i = SkipComment(source, i + 2);
                    builder.Append(source, start, i - start);
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    int start = i;
                    i = SkipShortString(source, i);
                    builder.Append(source, start, i - start);
                    continue;
                }

                if (c == '[')
                {
                    int level = LongBracketLevel(source, i);
                    if (level >= 0)
                    {
                        int start = i;
                        i = SkipLongBracket(source, i, level);
                        builder.Append(source, start, i - start);
                        continue;
                    }
                }

                if (IsNameStart(c))
                {
                    int start = i;
                    i = SkipName(source, i);
                    if (i - start == 2 && source[start] == 'i' && source[start + 1] == 's'
                        && !IsMemberAccess(source, start)
                        && TryMatchNot(source, i, out int afterNot))
                    {
                        builder.Append("~=");
                        i = afterNot;
                    }
                    else
                    {
                        builder.Append(source, start, i - start);
                    }
                    continue;
                }

                builder.Append(c);
                i++;
            }

            return builder.ToString();
        }

        private static bool TryMatchNot(string source, int index, out int afterNot)
        {
            afterNot = index;
            int i = index;
            int length = source.Length;
            int wsStart = i;
            while (i < length && (source[i] == ' ' || source[i] == '\t'))
                i++;
            if (i == wsStart || i + 3 > length)
                return false;
            if (source[i] != 'n' || source[i + 1] != 'o' || source[i + 2] != 't')
                return false;
            int after = i + 3;
            if (after < length && IsNamePart(source[after]))
                return false;
            afterNot = after;
            return true;
        }

        private static bool IsMemberAccess(string source, int nameStart)
        {
            for (int j = nameStart - 1; j >= 0; j--)
            {
                char c = source[j];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '\f' || c == '\v')
                    continue;
                return c == '.' || c == ':';
            }
            return false;
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
                if (c == '\n')
                    return i;
                i++;
            }
            return length;
        }

        private static int SkipName(string source, int i)
        {
            int length = source.Length;
            while (i < length && IsNamePart(source[i]))
                i++;
            return i;
        }

        private static bool IsNameStart(char c) => c == '_' || char.IsLetter(c);

        private static bool IsNamePart(char c) => c == '_' || char.IsLetterOrDigit(c);
    }
}
