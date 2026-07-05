namespace LuaScript.Api
{
    internal static class LuaCompletionSource
    {
        internal static readonly string[] Keywords =
        [
            "and", "break", "do", "else", "elseif", "end", "false", "for",
            "function", "goto", "if", "in", "local", "nil", "not", "or",
            "repeat", "return", "then", "true", "until", "while"
        ];

        private sealed record Snapshot(
            int Version,
            string[] StaticCandidates,
            IReadOnlyDictionary<string, string[]> TableMembers);

        private static Snapshot? s_snapshot;

        internal static string[] StaticCandidates => Current().StaticCandidates;

        internal static bool TryGetTableMembers(string table, out string[] members)
        {
            if (Current().TableMembers.TryGetValue(table, out var found))
            {
                members = found;
                return true;
            }
            members = [];
            return false;
        }

        private static Snapshot Current()
        {
            var snapshot = s_snapshot;
            int version = LuaApiCatalog.Version;
            if (snapshot is null || snapshot.Version != version)
                s_snapshot = snapshot = Build(version);
            return snapshot;
        }

        private static Snapshot Build(int version)
        {
            var members = LuaApiCatalog.Members;

            var tableMembers = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var globals = new List<string>();
            foreach (var member in members)
            {
                if (member.Table.Length == 0)
                {
                    globals.Add(member.Name);
                    continue;
                }
                if (!tableMembers.TryGetValue(member.Table, out var list))
                {
                    list = [];
                    tableMembers[member.Table] = list;
                }
                list.Add(member.QualifiedName);
            }

            var candidates = Keywords
                .Concat(LuaScript.Compat.Syntax.LuaSyntaxRuleRegistry.CatalogKeywords)
                .Concat(globals)
                .Concat(tableMembers.Keys)
                .Concat(tableMembers.Values.SelectMany(x => x))
                .Distinct()
                .OrderBy(x => x.Length)
                .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var frozen = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var (table, list) in tableMembers)
                frozen[table] = [.. list.Distinct()];

            return new Snapshot(version, candidates, frozen);
        }
    }
}
