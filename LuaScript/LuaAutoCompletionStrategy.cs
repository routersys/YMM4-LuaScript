using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LuaScript.Api;
using YukkuriMovieMaker.Controls.AvalonEdit.AutoCompletionStrategy;

namespace LuaScript
{
    internal sealed partial class LuaAutoCompletionStrategy : IAutoCompletionStrategy
    {
        [GeneratedRegex(@"[a-zA-Z_][a-zA-Z_0-9]*$", RegexOptions.None)]
        private static partial Regex IdentifierPattern();

        [GeneratedRegex(@"(?<!local\s)\bfunction\s+([a-zA-Z_][a-zA-Z_0-9]*(?:\.[a-zA-Z_][a-zA-Z_0-9]*)*)\s*\(", RegexOptions.None)]
        private static partial Regex FunctionPattern();

        [GeneratedRegex(@"\blocal\s+function\s+([a-zA-Z_][a-zA-Z_0-9]*)\s*\(", RegexOptions.None)]
        private static partial Regex LocalFunctionPattern();

        public IEnumerable<string> GetCompletionItems(string input, string line, string sourceCode)
        {
            if (string.IsNullOrEmpty(input))
                return [];

            var lastDot = line.LastIndexOf('.');
            if (lastDot >= 0)
            {
                var beforeDot = line[..lastDot];
                var nsMatch = IdentifierPattern().Match(beforeDot);
                if (!nsMatch.Success)
                    return [];

                return LuaCompletionSource.TryGetTableMembers(nsMatch.Value, out var members)
                    ? members
                    : [];
            }

            if (input[0] is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_')
            {
                var userFunctions = GetUserDefinedFunctions(sourceCode);
                return LuaCompletionSource.StaticCandidates
                    .Concat(userFunctions)
                    .Distinct()
                    .OrderBy(x => x.Length)
                    .ThenBy(x => x, StringComparer.OrdinalIgnoreCase);
            }

            return [];
        }

        private static IEnumerable<string> GetUserDefinedFunctions(string sourceCode)
        {
            foreach (Match m in FunctionPattern().Matches(sourceCode))
                yield return m.Groups[1].Value;

            foreach (Match m in LocalFunctionPattern().Matches(sourceCode))
                yield return m.Groups[1].Value;
        }
    }
}
