namespace LuaScript.Engine.Kernel
{
    internal sealed record KCompare(KCompareOp Op, KExpr Left, KExpr Right) : KBool;
}
