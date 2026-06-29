namespace LuaScript.Engine.Kernel
{
    internal sealed class KernelUnsupportedException(string reason) : Exception(reason);
}
