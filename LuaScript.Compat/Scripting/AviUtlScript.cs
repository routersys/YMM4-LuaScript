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

        public static string Transform(string source) => TransformWithMap(source).Code;

        public static (string Code, int[] LineMap) TransformWithMap(string source)
        {
            if (string.IsNullOrEmpty(source))
                return (source, []);

            var lines = ScriptParserHelper.SplitLines(source);
            bool hasSection = ScriptParserHelper.TryFindSection(lines, out int sectionStart, out int sectionEnd);

            var prelude = new List<string>();
            for (int i = sectionStart; i < sectionEnd; i++)
                AppendDeclarations(lines[i], prelude);

            if (!hasSection && prelude.Count == 0)
                return (LuaSyntaxExtensions.Rewrite(source), []);

            var builder = new StringBuilder(source.Length + 64);
            var lineMap = new int[prelude.Count + (sectionEnd - sectionStart)];
            int row = 0;

            foreach (var declaration in prelude)
            {
                builder.Append(declaration).Append('\n');
                lineMap[row++] = -1;
            }
            for (int i = sectionStart; i < sectionEnd; i++)
            {
                builder.Append(lines[i]).Append('\n');
                lineMap[row++] = i;
            }

            return (LuaSyntaxExtensions.Rewrite(builder.ToString()), lineMap);
        }

        private static void AppendDeclarations(string line, List<string> prelude)
        {
            if (!ScriptParserHelper.TryParseDirective(line, out string name, out string content))
                return;

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
