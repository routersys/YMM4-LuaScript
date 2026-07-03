namespace LuaScript.Api
{
    [LuaTable("bit32")]
    [LuaBuiltin("band", "...")]
    [LuaBuiltin("bor", "...")]
    [LuaBuiltin("bxor", "...")]
    [LuaBuiltin("bnot", "x")]
    [LuaBuiltin("lshift", "x", "disp")]
    [LuaBuiltin("rshift", "x", "disp")]
    [LuaBuiltin("arshift", "x", "disp")]
    [LuaBuiltin("extract", "n", "field", "width")]
    [LuaBuiltin("replace", "n", "v", "field", "width")]
    [LuaBuiltin("btest", "...")]
    [LuaBuiltin("countlz", "x")]
    [LuaBuiltin("countrz", "x")]
    internal static class LuaBit32Library;
}
