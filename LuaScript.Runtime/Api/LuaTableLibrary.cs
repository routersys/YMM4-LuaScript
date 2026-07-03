namespace LuaScript.Api
{
    [LuaTable("table")]
    [LuaBuiltin("insert", "list", "pos", "value")]
    [LuaBuiltin("remove", "list", "pos")]
    [LuaBuiltin("sort", "list", "comp")]
    [LuaBuiltin("concat", "list", "sep", "i", "j")]
    [LuaBuiltin("unpack", "list", "i", "j")]
    [LuaBuiltin("move", "a1", "f", "e", "t", "a2")]
    internal static class LuaTableLibrary;
}
