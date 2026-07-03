using LuaScript.Compat.Syntax;

namespace LuaScript.Compat
{
    internal static class LuaSyntaxExtensions
    {
        public static string Rewrite(string source)
        {
            if (string.IsNullOrEmpty(source))
                return source;

            var tokens = LuaSyntaxLexer.Tokenize(source);
            var context = new LuaRewriteContext(tokens);
            var rules = LuaSyntaxRuleRegistry.Rules;

            while (!context.AtEnd)
            {
                var current = context.Current;
                if (current.IsTrivia || current.Kind == LuaSyntaxTokenKind.String)
                {
                    context.Emit(current);
                    context.Advance();
                    continue;
                }

                bool matched = false;
                foreach (var rule in rules)
                {
                    if (rule.TryApply(context))
                    {
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    context.Emit(current);
                    context.Advance();
                }
            }

            return context.Changed ? context.Serialize() : source;
        }
    }
}
