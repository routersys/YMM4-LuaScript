namespace LuaScript.Api
{
    internal static class LuaApiCatalog
    {
        private static readonly object s_gate = new();
        private static readonly List<LuaApiMember> s_members = [];
        private static LuaApiMember[]? s_snapshot;
        private static int s_version;

        internal static int Version
        {
            get
            {
                lock (s_gate)
                    return s_version;
            }
        }

        internal static void Register(IReadOnlyList<LuaApiMember> members)
        {
            lock (s_gate)
            {
                s_members.AddRange(members);
                s_snapshot = null;
                s_version++;
            }
        }

        internal static IReadOnlyList<LuaApiMember> Members
        {
            get
            {
                lock (s_gate)
                    return s_snapshot ??= [.. s_members];
            }
        }
    }
}
