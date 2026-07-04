namespace LuaScript.Compat.Syntax.Rules
{
    [LuaSyntaxRule(35)]
    internal sealed class LuaLambdaRule : ILuaSyntaxRule
    {
        public bool TryApply(LuaRewriteContext context)
        {
            if (!context.Current.IsOperator("=>"))
                return false;
            if (!context.TryReadTrailingLvalue(out int start, out string target))
                return false;
            if (!TryNormalizeParameters(target, out string parameters))
                return false;

            int body = context.NextSignificant(context.Index + 1);
            if (body < 0 || !LuaSyntaxFacts.IsValueStart(context.Input[body]))
                return false;

            context.RemoveOutputFrom(start);
            context.Advance();
            string expression = context.ReadForwardOrOperand();
            if (expression.Length == 0)
                return false;

            context.EmitRaw($"(function({parameters}) return {LuaSyntaxExtensions.Rewrite(expression)} end)");
            return true;
        }

        private static bool TryNormalizeParameters(string text, out string parameters)
        {
            parameters = string.Empty;
            text = text.Trim();
            bool wrapped = text.StartsWith("(") && text.EndsWith(")");
            if (wrapped)
                text = text.Substring(1, text.Length - 2).Trim();

            if (text.Length == 0)
                return wrapped;

            var names = text.Split(',');
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i].Trim();
                if (name == "...")
                {
                    if (i != names.Length - 1)
                        return false;
                    continue;
                }
                if (!IsName(name) || LuaKeywords.IsReserved(name))
                    return false;
                if (!wrapped && names.Length > 1)
                    return false;
            }

            parameters = string.Join(", ", System.Array.ConvertAll(names, static n => n.Trim()));
            return true;
        }

        private static bool IsName(string text)
        {
            if (text.Length == 0)
                return false;
            if (text[0] != '_' && !char.IsLetter(text[0]))
                return false;
            for (int i = 1; i < text.Length; i++)
            {
                if (text[i] != '_' && !char.IsLetterOrDigit(text[i]))
                    return false;
            }
            return true;
        }
    }
}
