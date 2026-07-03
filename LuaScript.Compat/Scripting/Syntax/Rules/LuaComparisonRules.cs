namespace LuaScript.Compat.Syntax.Rules
{
    [LuaSyntaxRule(10)]
    internal sealed class IsNotRule : ILuaSyntaxRule
    {
        public bool TryApply(LuaRewriteContext context)
        {
            if (!context.Current.IsName("is"))
                return false;
            if (!context.TryPreviousOutputSignificantSameLine(out var previous) || !LuaSyntaxFacts.IsValueEnd(previous))
                return false;

            int notIndex = context.NextSignificantSameLine(context.Index + 1);
            if (notIndex < 0 || !context.Input[notIndex].IsName("not"))
                return false;

            int after = context.NextSignificant(notIndex + 1);
            if (after < 0 || !LuaSyntaxFacts.IsValueStart(context.Input[after]))
                return false;

            context.EmitOperator("~=");
            context.Index = notIndex + 1;
            return true;
        }
    }

    [LuaSyntaxRule(20)]
    internal sealed class IsRule : ILuaSyntaxRule
    {
        public bool TryApply(LuaRewriteContext context)
        {
            if (!context.Current.IsName("is"))
                return false;
            if (!context.TryPreviousOutputSignificantSameLine(out var previous) || !LuaSyntaxFacts.IsValueEnd(previous))
                return false;

            int next = context.NextSignificantSameLine(context.Index + 1);
            if (next < 0 || !LuaSyntaxFacts.IsValueStart(context.Input[next]))
                return false;

            context.EmitOperator("==");
            context.Advance();
            return true;
        }
    }

    [LuaSyntaxRule(30)]
    internal sealed class NotEqualRule : ILuaSyntaxRule
    {
        public bool TryApply(LuaRewriteContext context)
        {
            if (!context.Current.IsOperator("!="))
                return false;

            context.EmitOperator("~=");
            context.Advance();
            return true;
        }
    }
}
