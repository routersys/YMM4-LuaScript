namespace LuaScript.Compat.Syntax
{
    internal interface ILuaSyntaxRule
    {
        bool TryApply(LuaRewriteContext context);
    }
}
