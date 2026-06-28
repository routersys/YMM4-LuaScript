using System;

namespace LuaScript
{
    internal sealed class LuaScriptRuntimeException(string message, Exception? inner = null) : LuaScriptException(message, inner);
}
