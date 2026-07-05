namespace LuaScript.Generator
{
    internal sealed record LuaTableModel(
        string Namespace,
        EquatableArray<LuaTypePart> TypeChain,
        string TableName,
        string ContextType,
        EquatableArray<LuaFunctionModel> Functions,
        EquatableArray<LuaConstantModel> Constants,
        EquatableArray<LuaUpdateModel> Updates,
        EquatableArray<LuaCatalogEntry> Entries);
}
