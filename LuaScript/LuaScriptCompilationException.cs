using System;

namespace LuaScript
{
    internal sealed class LuaScriptCompilationException(string message, Exception? inner = null) : LuaScriptException(message, inner);
}
