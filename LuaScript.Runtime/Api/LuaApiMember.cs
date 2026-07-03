namespace LuaScript.Api
{
    internal sealed record LuaApiMember(
        string Table,
        string Name,
        LuaApiMemberKind Kind,
        IReadOnlyList<string> Parameters)
    {
        public string QualifiedName => Table.Length == 0 ? Name : $"{Table}.{Name}";
    }
}
