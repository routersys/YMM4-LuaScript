namespace LuaScript.Generator
{
    internal sealed record LuaCatalogEntry(
        string Table,
        string Name,
        bool IsFunction,
        EquatableArray<string> Parameters);
}
