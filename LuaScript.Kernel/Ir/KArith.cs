namespace LuaScript.Engine.Kernel
{
    internal sealed record KArith(KArithOp Op, KExpr Left, KExpr Right) : KExpr;
}
