namespace LuaScript.Compat
{
    internal static class ScriptParserHelper
    {
        public static bool IsSectionHeader(string line)
        {
            int i = SkipSpaces(line, 0);
            return i < line.Length && line[i] == '@';
        }

        public static int SkipSpaces(string text, int index)
        {
            while (index < text.Length && (text[index] == ' ' || text[index] == '\t'))
                index++;
            return index;
        }

        public static bool IsNameChar(char c) =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');

        public static string[] SplitLines(string source) =>
            source.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

        public static bool TryFindSection(string[] lines, out int start, out int end)
        {
            start = 0;
            end = lines.Length;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!IsSectionHeader(lines[i]))
                    continue;
                start = i + 1;
                end = lines.Length;
                for (int j = start; j < lines.Length; j++)
                {
                    if (IsSectionHeader(lines[j]))
                    {
                        end = j;
                        break;
                    }
                }
                return true;
            }
            return false;
        }

        public static bool TryParseDirective(string line, out string name, out string content)
        {
            name = string.Empty;
            content = string.Empty;
            int i = SkipSpaces(line, 0);
            if (i + 1 >= line.Length || line[i] != '-' || line[i + 1] != '-')
                return false;
            i += 2;
            if (i < line.Length && line[i] == '!')
                return false;

            int nameStart = i;
            while (i < line.Length && IsNameChar(line[i]))
                i++;
            if (i >= line.Length || line[i] != ':')
                return false;

            name = line.Substring(nameStart, i - nameStart);
            content = line.Substring(i + 1);
            return true;
        }
    }
}
