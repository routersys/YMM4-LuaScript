using System;
using System.Collections.Generic;
using System.Text;

namespace LuaScript.Compat
{
    internal static class AviUtlScript
    {
        private enum DirectiveKind
        {
            None,
            Dialog,
            Color,
            Check,
            Param,
        }

        public static string Transform(string source)
        {
            if (string.IsNullOrEmpty(source))
                return source;

            var lines = source.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            int sectionStart = 0;
            int sectionEnd = lines.Length;
            bool hasSection = false;

            for (int i = 0; i < lines.Length; i++)
            {
                if (!IsSectionHeader(lines[i]))
                    continue;

                hasSection = true;
                sectionStart = i + 1;
                sectionEnd = lines.Length;
                for (int j = sectionStart; j < lines.Length; j++)
                {
                    if (IsSectionHeader(lines[j]))
                    {
                        sectionEnd = j;
                        break;
                    }
                }
                break;
            }

            var prelude = new List<string>();
            for (int i = sectionStart; i < sectionEnd; i++)
                AppendDeclarations(lines[i], prelude);

            if (!hasSection && prelude.Count == 0)
                return source;

            var builder = new StringBuilder(source.Length + 64);
            foreach (var declaration in prelude)
                builder.Append(declaration).Append('\n');
            for (int i = sectionStart; i < sectionEnd; i++)
                builder.Append(lines[i]).Append('\n');

            return builder.ToString();
        }

        private static bool IsSectionHeader(string line)
        {
            int i = SkipSpaces(line, 0);
            return i < line.Length && line[i] == '@';
        }

        private static void AppendDeclarations(string line, List<string> prelude)
        {
            int i = SkipSpaces(line, 0);
            if (i + 1 >= line.Length || line[i] != '-' || line[i + 1] != '-')
                return;
            i += 2;
            if (i < line.Length && line[i] == '!')
                return;

            int nameStart = i;
            while (i < line.Length && IsNameChar(line[i]))
                i++;
            if (i >= line.Length || line[i] != ':')
                return;

            string name = line.Substring(nameStart, i - nameStart);
            string content = line.Substring(i + 1);

            switch (Classify(name))
            {
                case DirectiveKind.Dialog:
                    AppendDialog(content, prelude);
                    break;
                case DirectiveKind.Color:
                    AppendColor(content, prelude);
                    break;
                case DirectiveKind.Check:
                    AppendCheck(name, content, prelude);
                    break;
                case DirectiveKind.Param:
                    AppendParam(content, prelude);
                    break;
            }
        }

        private static DirectiveKind Classify(string name)
        {
            if (name.Equals("dialog", StringComparison.OrdinalIgnoreCase))
                return DirectiveKind.Dialog;
            if (name.Equals("color", StringComparison.OrdinalIgnoreCase))
                return DirectiveKind.Color;
            if (name.Equals("param", StringComparison.OrdinalIgnoreCase))
                return DirectiveKind.Param;
            if (name.Length > 5 && name.StartsWith("check", StringComparison.OrdinalIgnoreCase) && AllDigits(name, 5))
                return DirectiveKind.Check;
            return DirectiveKind.None;
        }

        private static void AppendDialog(string content, List<string> prelude)
        {
            foreach (var segment in content.Split(';'))
            {
                string trimmed = segment.Trim();
                if (trimmed.Length == 0)
                    continue;
                int comma = trimmed.IndexOf(',');
                if (comma < 0)
                    continue;
                string declaration = trimmed.Substring(comma + 1).Trim();
                if (declaration.Length != 0)
                    prelude.Add(declaration);
            }
        }

        private static void AppendColor(string content, List<string> prelude)
        {
            string token = FirstToken(content);
            if (IsColorLiteral(token))
                prelude.Add("local color = " + token);
        }

        private static void AppendCheck(string name, string content, List<string> prelude)
        {
            string index = name.Substring(5);
            var parts = content.Split(',');
            string def = parts[parts.Length - 1].Trim();
            if (IsIntegerLiteral(def))
                prelude.Add("obj.check" + index + " = " + def);
        }

        private static void AppendParam(string content, List<string> prelude)
        {
            string trimmed = content.Trim();
            if (trimmed.Length != 0)
                prelude.Add(trimmed);
        }

        private static string FirstToken(string content)
        {
            int start = SkipSpaces(content, 0);
            int i = start;
            while (i < content.Length && content[i] != ',' && content[i] != ' ' && content[i] != '\t')
                i++;
            return content.Substring(start, i - start);
        }

        private static bool IsColorLiteral(string token)
        {
            if (token.Length == 0)
                return false;
            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (token.Length <= 2)
                    return false;
                for (int k = 2; k < token.Length; k++)
                    if (!IsHexDigit(token[k]))
                        return false;
                return true;
            }
            foreach (char c in token)
                if (c < '0' || c > '9')
                    return false;
            return true;
        }

        private static bool IsIntegerLiteral(string token)
        {
            if (token.Length == 0)
                return false;
            int start = token[0] == '-' || token[0] == '+' ? 1 : 0;
            if (start >= token.Length)
                return false;
            for (int i = start; i < token.Length; i++)
                if (token[i] < '0' || token[i] > '9')
                    return false;
            return true;
        }

        private static bool AllDigits(string text, int start)
        {
            for (int i = start; i < text.Length; i++)
                if (text[i] < '0' || text[i] > '9')
                    return false;
            return true;
        }

        private static int SkipSpaces(string text, int index)
        {
            while (index < text.Length && (text[index] == ' ' || text[index] == '\t'))
                index++;
            return index;
        }

        private static bool IsNameChar(char c) =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');

        private static bool IsHexDigit(char c) =>
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    }
}
