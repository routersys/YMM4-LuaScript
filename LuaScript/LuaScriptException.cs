using System;

namespace LuaScript
{
    internal abstract class LuaScriptException(string message, Exception? inner = null) : Exception(message, inner);
}
