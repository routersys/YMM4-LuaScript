using System;

namespace LuaScript
{
    internal sealed class LuaScriptTimeoutException(string message, Exception? inner = null) : LuaScriptException(message, inner);
}
