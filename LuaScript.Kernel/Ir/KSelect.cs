namespace LuaScript.Engine.Kernel
{
    internal sealed record KSelect(KBool Condition, KExpr WhenTrue, KExpr WhenFalse) : KExpr;
}
