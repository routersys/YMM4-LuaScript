namespace LuaScript.Compat.Syntax.Rules
{
    [LuaSyntaxRule(100)]
    internal sealed class NilCoalesceRule : ILuaSyntaxRule
    {
        public bool TryApply(LuaRewriteContext context)
        {
            if (!context.Current.IsOperator("??"))
                return false;
            if (!context.TryPopOrOperand(out string left))
                return false;

            context.Advance();
            string right = context.ReadForwardOrOperand();
            string temporary = context.NextTemporary();
            context.EmitRaw($"(function() local {temporary} = ({left}) if {temporary} ~= nil then return {temporary} else return ({right}) end end)()");
            return true;
        }
    }
}
