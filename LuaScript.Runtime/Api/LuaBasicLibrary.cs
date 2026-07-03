namespace LuaScript.Api
{
    [LuaTable("")]
    [LuaBuiltin("type", "v")]
    [LuaBuiltin("tostring", "v")]
    [LuaBuiltin("tonumber", "e", "base")]
    [LuaBuiltin("select", "n", "...")]
    [LuaBuiltin("error", "message", "level")]
    [LuaBuiltin("assert", "v", "message")]
    [LuaBuiltin("print", "...")]
    [LuaBuiltin("ipairs", "t")]
    [LuaBuiltin("pairs", "t")]
    [LuaBuiltin("next", "table", "index")]
    [LuaBuiltin("unpack", "list", "i", "j")]
    [LuaBuiltin("setmetatable", "table", "metatable")]
    [LuaBuiltin("getmetatable", "object")]
    [LuaBuiltin("rawget", "table", "index")]
    [LuaBuiltin("rawset", "table", "index", "value")]
    [LuaBuiltin("rawequal", "v1", "v2")]
    [LuaBuiltin("rawlen", "v")]
    [LuaBuiltin("pcall", "f", "...")]
    [LuaBuiltin("xpcall", "f", "msgh", "...")]
    internal static class LuaBasicLibrary;
}
