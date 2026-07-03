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
    }
}
