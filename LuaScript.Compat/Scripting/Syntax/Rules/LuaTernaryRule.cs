namespace LuaScript.Compat.Syntax.Rules
{
    [LuaSyntaxRule(96)]
    internal sealed class LuaTernaryRule : ILuaSyntaxRule
    {
        public bool TryApply(LuaRewriteContext context)
        {
            if (!context.Current.IsOperator("?"))
                return false;
            if (!context.TryPreviousOutputSignificantSameLine(out var previous) || !LuaSyntaxFacts.IsValueEnd(previous))
                return false;

            int thenStart = context.NextSignificantSameLine(context.Index + 1);
            if (thenStart < 0 || !LuaSyntaxFacts.IsValueStart(context.Input[thenStart]))
                return false;

            int colon = FindColon(context, context.Index + 1);
            if (colon < 0)
                return false;

            int elseStart = context.NextSignificantSameLine(colon + 1);
            if (elseStart < 0 || !LuaSyntaxFacts.IsValueStart(context.Input[elseStart]))
                return false;

            if (!context.TryPopOrOperand(out string condition))
                return false;

            context.Index++;
            string thenExpression = ReadUntil(context, colon);
            context.Index = colon + 1;
            string elseExpression = context.ReadForwardOrOperand();

            context.EmitRaw($"(function() if {condition} then return ({LuaSyntaxExtensions.Rewrite(thenExpression)}) else return ({LuaSyntaxExtensions.Rewrite(elseExpression)}) end end)()");
            return true;
        }

        private static int FindColon(LuaRewriteContext context, int from)
        {
            int depth = 0;
            for (int i = from; i < context.Input.Count; i++)
            {
                var token = context.Input[i];
                if (token.Kind == LuaSyntaxTokenKind.Newline)
                    return -1;
                if (token.Kind == LuaSyntaxTokenKind.Name && depth == 0 && LuaKeywords.IsExpressionBoundary(token.Text))
                    return -1;
                if (token.Kind != LuaSyntaxTokenKind.Operator)
                    continue;
                if (token.Text is "(" or "[" or "{")
                {
                    depth++;
                    continue;
                }
                if (token.Text is ")" or "]" or "}")
                {
                    if (depth == 0)
                        return -1;
                    depth--;
                    continue;
                }
                if (depth > 0)
                    continue;
                if (token.Text is "," or ";" or "?")
                    return -1;
                if (token.Text == ":")
                    return i;
            }
            return -1;
        }

        private static string ReadUntil(LuaRewriteContext context, int stop)
        {
            var builder = new System.Text.StringBuilder();
            int begin = context.Index;
            int end = stop;
            while (begin < end && context.Input[begin].IsTrivia)
                begin++;
            while (end > begin && context.Input[end - 1].IsTrivia)
                end--;
            for (int i = begin; i < end; i++)
                builder.Append(context.Input[i].Text);
            return builder.ToString();
        }
    }
}
