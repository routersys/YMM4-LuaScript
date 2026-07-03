namespace LuaScript.Engine.Kernel
{
    internal sealed record KLogical(bool IsAnd, KBool Left, KBool Right) : KBool;
}
