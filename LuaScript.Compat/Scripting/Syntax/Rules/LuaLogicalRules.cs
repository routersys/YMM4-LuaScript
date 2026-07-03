namespace LuaScript.Compat.Syntax.Rules
{
    [LuaSyntaxRule(40)]
    internal sealed class LogicalNotRule : ILuaSyntaxRule
    {
        public bool TryApply(LuaRewriteContext context)
        {
            if (!context.Current.IsOperator("!"))
                return false;

            context.Advance();
            context.EmitWord("not");
            return true;
        }
    }

    [LuaSyntaxRule(50)]
    internal sealed class AndAliasRule : ILuaSyntaxRule
    {
        public bool TryApply(LuaRewriteContext context)
        {
            if (!context.Current.IsOperator("&&"))
                return false;

            context.Advance();
            context.EmitWord("and");
            return true;
        }
    }

    [LuaSyntaxRule(60)]
    internal sealed class OrAliasRule : ILuaSyntaxRule
    {
        public bool TryApply(LuaRewriteContext context)
        {
            if (!context.Current.IsOperator("||"))
                return false;

            context.Advance();
            context.EmitWord("or");
            return true;
        }
    }
}
