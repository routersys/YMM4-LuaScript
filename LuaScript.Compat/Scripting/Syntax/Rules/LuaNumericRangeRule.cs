namespace LuaScript.Compat.Syntax.Rules
{
    [LuaSyntaxRule(110)]
    internal sealed class LuaNumericRangeRule : ILuaSyntaxRule
    {
        public bool TryApply(LuaRewriteContext context)
        {
            if (!context.Current.IsName("in"))
                return false;

            int open = context.NextSignificant(context.Index + 1);
            if (open < 0 || !context.Input[open].IsOperator("<"))
                return false;
            if (!LuaRangeSyntax.TryParse(context.Input, open, out var parts, out int end))
                return false;

            string stopExpression = parts.Exclusive ? $"({parts.Stop}) - 1" : parts.Stop;
            string replacement = parts.Step is null
                ? $"= {parts.Start}, {stopExpression}"
                : $"= {parts.Start}, {stopExpression}, {parts.Step}";
            context.EmitRaw(replacement);
            context.Index = end;
            return true;
        }
    }
}
