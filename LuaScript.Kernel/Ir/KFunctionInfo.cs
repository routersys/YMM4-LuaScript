namespace LuaScript.Engine.Kernel
{
    internal readonly record struct KFunctionInfo(KFunc Func, string Name, int MinArgs, int MaxArgs);
}
