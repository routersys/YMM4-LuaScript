using System.Text;

namespace LuaScript.Compat.Syntax.Rules
{
    [LuaSyntaxRule(92)]
    internal sealed class LuaSafeNavigationRule : ILuaSyntaxRule
    {
        public bool TryApply(LuaRewriteContext context)
        {
            if (!context.Current.IsOperator("?"))
                return false;

            int accessor = context.NextSignificantSameLine(context.Index + 1);
            if (accessor < 0)
                return false;

            if (context.Input[accessor].IsOperator("."))
                return TryApplyMember(context, accessor);
            if (context.Input[accessor].IsOperator("["))
                return TryApplyIndex(context, accessor);
            return false;
        }

        private static bool TryApplyMember(LuaRewriteContext context, int accessor)
        {
            int name = context.NextSignificantSameLine(accessor + 1);
            if (name < 0)
                return false;
            var token = context.Input[name];
            if (token.Kind != LuaSyntaxTokenKind.Name || LuaKeywords.IsReserved(token.Text))
                return false;

            if (!context.TryReadTrailingLvalue(out int start, out string target))
                return false;

            context.RemoveOutputFrom(start);
            context.Index = name + 1;
            string temporary = context.NextTemporary();
            context.EmitRaw($"(function() local {temporary} = ({target}) if {temporary} == nil then return nil end return {temporary}.{token.Text} end)()");
            return true;
        }

        private static bool TryApplyIndex(LuaRewriteContext context, int accessor)
        {
            int close = FindClosingBracket(context, accessor + 1);
            if (close < 0)
                return false;

            string key = Concat(context, accessor + 1, close);
            if (key.Length == 0)
                return false;

            if (!context.TryReadTrailingLvalue(out int start, out string target))
                return false;

            context.RemoveOutputFrom(start);
            context.Index = close + 1;
            string temporary = context.NextTemporary();
            context.EmitRaw($"(function() local {temporary} = ({target}) if {temporary} == nil then return nil end return {temporary}[{LuaSyntaxExtensions.Rewrite(key)}] end)()");
            return true;
        }

        private static int FindClosingBracket(LuaRewriteContext context, int from)
        {
            int depth = 0;
            for (int i = from; i < context.Input.Count; i++)
            {
                var token = context.Input[i];
                if (token.Kind != LuaSyntaxTokenKind.Operator)
                    continue;
                if (token.Text is "(" or "[" or "{")
                {
                    depth++;
                    continue;
                }
                if (token.Text is ")" or "}")
                {
                    if (depth == 0)
                        return -1;
                    depth--;
                    continue;
                }
                if (token.Text == "]")
                {
                    if (depth == 0)
                        return i;
                    depth--;
                }
            }
            return -1;
        }

        private static string Concat(LuaRewriteContext context, int begin, int end)
        {
            while (begin < end && context.Input[begin].IsTrivia)
                begin++;
            while (end > begin && context.Input[end - 1].IsTrivia)
                end--;
            var builder = new StringBuilder();
            for (int i = begin; i < end; i++)
                builder.Append(context.Input[i].Text);
            return builder.ToString();
        }
    }
}
