namespace LuaScript.Generator
{
    internal sealed record LuaUpdateModel(string LuaName, string MethodName, LuaUpdateKind Kind, LuaValueKind ValueKind);
}
