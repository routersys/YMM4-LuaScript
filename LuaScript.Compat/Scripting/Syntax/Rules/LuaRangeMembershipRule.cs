namespace LuaScript.Compat.Syntax.Rules
{
    [LuaSyntaxRule(108)]
    internal sealed class LuaRangeMembershipRule : ILuaSyntaxRule
    {
        public bool TryApply(LuaRewriteContext context)
        {
            if (!context.Current.IsName("in"))
                return false;
            if (IsForHeader(context))
                return false;

            int open = context.NextSignificant(context.Index + 1);
            if (open < 0 || !context.Input[open].IsOperator("<"))
                return false;
            if (!LuaRangeSyntax.TryParse(context.Input, open, out var parts, out int end))
                return false;
            if (parts.Step is not null)
                return false;

            if (!context.TryPopOrOperand(out string left))
                return false;

            string temporary = context.NextTemporary();
            string upper = parts.Exclusive ? "<" : "<=";
            context.EmitRaw($"(function() local {temporary} = ({left}) return {temporary} >= ({LuaSyntaxExtensions.Rewrite(parts.Start)}) and {temporary} {upper} ({LuaSyntaxExtensions.Rewrite(parts.Stop)}) end)()");
            context.Index = end;
            return true;
        }

        private static bool IsForHeader(LuaRewriteContext context)
        {
            var output = context.Output;
            for (int i = output.Count - 1; i >= 0; i--)
            {
                var token = output[i];
                if (token.Kind == LuaSyntaxTokenKind.Newline)
                    return false;
                if (token.Kind != LuaSyntaxTokenKind.Name)
                    continue;
                if (token.Text is "do" or "then")
                    return false;
                if (token.Text == "for")
                    return true;
            }
            return false;
        }
    }
}
