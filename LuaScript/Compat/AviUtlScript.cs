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
                if (!ScriptParserHelper.IsSectionHeader(lines[i]))
                    continue;

                hasSection = true;
                sectionStart = i + 1;
                sectionEnd = lines.Length;
                for (int j = sectionStart; j < lines.Length; j++)
                {
                    if (ScriptParserHelper.IsSectionHeader(lines[j]))
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

        private static void AppendDeclarations(string line, List<string> prelude)
        {
            int i = ScriptParserHelper.SkipSpaces(line, 0);
            if (i + 1 >= line.Length || line[i] != '-' || line[i + 1] != '-')
                return;
            i += 2;
            if (i < line.Length && line[i] == '!')
                return;

            int nameStart = i;
            while (i < line.Length && ScriptParserHelper.IsNameChar(line[i]))
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
                case DirectiveKind.Param:
                    AppendParam(content, prelude);
                    break;
            }
        }

        private static DirectiveKind Classify(string name)
        {
            if (name.Equals("dialog", StringComparison.OrdinalIgnoreCase))
                return DirectiveKind.Dialog;
            if (name.Equals("param", StringComparison.OrdinalIgnoreCase))
                return DirectiveKind.Param;
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

        private static void AppendParam(string content, List<string> prelude)
        {
            string trimmed = content.Trim();
            if (trimmed.Length != 0)
                prelude.Add(trimmed);
        }
    }
}
