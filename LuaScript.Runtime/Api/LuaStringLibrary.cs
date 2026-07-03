namespace LuaScript.Api
{
    [LuaTable("string")]
    [LuaBuiltin("format", "formatstring", "...")]
    [LuaBuiltin("len", "s")]
    [LuaBuiltin("sub", "s", "i", "j")]
    [LuaBuiltin("upper", "s")]
    [LuaBuiltin("lower", "s")]
    [LuaBuiltin("rep", "s", "n", "sep")]
    [LuaBuiltin("reverse", "s")]
    [LuaBuiltin("find", "s", "pattern", "init", "plain")]
    [LuaBuiltin("match", "s", "pattern", "init")]
    [LuaBuiltin("gmatch", "s", "pattern")]
    [LuaBuiltin("gsub", "s", "pattern", "repl", "n")]
    [LuaBuiltin("byte", "s", "i", "j")]
    [LuaBuiltin("char", "...")]
    [LuaBuiltin("dump", "function")]
    [LuaBuiltin("pack", "fmt", "...")]
    [LuaBuiltin("unpack", "fmt", "s", "pos")]
    [LuaBuiltin("packsize", "fmt")]
    internal static class LuaStringLibrary;
}
