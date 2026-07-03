namespace LuaScript.Compat.Syntax.Rules
{
    [LuaSyntaxRule(70)]
    internal sealed class AugmentedAssignmentRule : ILuaSyntaxRule
    {
        public bool TryApply(LuaRewriteContext context)
        {
            if (context.Current.Kind != LuaSyntaxTokenKind.Operator)
                return false;

            string? connective = Connective(context.Current.Text);
            if (connective is null)
                return false;
            if (!context.TryReadTrailingLvalue(out _, out string target))
                return false;

            context.Advance();
            string value = context.ReadLineExpression();
            string separator = context.LastOutputIsTrivia() ? string.Empty : " ";
            context.EmitRaw($"{separator}= {target} {connective} ({value})");
            return true;
        }

        private static string? Connective(string op) => op switch
        {
            "+=" => "+",
            "-=" => "-",
            "*=" => "*",
            "/=" => "/",
            "%=" => "%",
            "^=" => "^",
            "..=" => "..",
            "||=" => "or",
            "&&=" => "and",
            _ => null,
        };
    }

    [LuaSyntaxRule(80)]
    internal sealed class NilCoalesceAssignRule : ILuaSyntaxRule
    {
        public bool TryApply(LuaRewriteContext context)
        {
            if (!context.Current.IsOperator("??="))
                return false;
            if (!context.TryReadTrailingLvalue(out int start, out string target))
                return false;

            context.RemoveOutputFrom(start);
            context.Advance();
            string value = context.ReadLineExpression();
            context.EmitRaw($"if {target} == nil then {target} = ({value}) end");
            return true;
        }
    }

    [LuaSyntaxRule(90)]
    internal sealed class IncrementRule : ILuaSyntaxRule
    {
        public bool TryApply(LuaRewriteContext context)
        {
            if (!context.Current.IsOperator("++"))
                return false;
            if (!context.TryReadTrailingLvalue(out _, out string target))
                return false;

            context.Advance();
            string separator = context.LastOutputIsTrivia() ? string.Empty : " ";
            context.EmitRaw($"{separator}= {target} + 1");
            return true;
        }
    }
}
