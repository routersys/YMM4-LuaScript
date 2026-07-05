namespace LuaScript.Compat.Syntax.Rules
{
    [LuaSyntaxRule(2)]
    internal sealed class LuaFastRule : ILuaSyntaxRule
    {
        private const string Message = "[fast] は obj.fill / obj.convolve / obj.resize にのみ適用できます。";

        public bool TryApply(LuaRewriteContext context)
        {
            if (!context.Current.IsOperator("["))
                return false;

            if (context.TryPreviousOutputSignificantSameLine(out var previous) && LuaSyntaxFacts.IsValueEnd(previous))
                return false;

            int nameIndex = context.NextSignificant(context.Index + 1);
            if (nameIndex < 0 || !context.Input[nameIndex].IsName("fast"))
                return false;

            int closeIndex = context.NextSignificant(nameIndex + 1);
            if (closeIndex < 0 || !context.Input[closeIndex].IsOperator("]"))
                return false;

            int targetIndex = context.NextSignificant(closeIndex + 1);
            if (targetIndex < 0 || context.Input[targetIndex].Kind != LuaSyntaxTokenKind.Name)
                return false;

            EmitBetween(context, closeIndex + 1, targetIndex);

            if (TryResolveTarget(context, targetIndex, out int funcIndex, out string function))
            {
                context.Index = funcIndex + 1;
                context.EmitRaw("__fast_" + function);
                return true;
            }

            context.AddDiagnostic(Message);
            context.Index = targetIndex;
            return true;
        }

        private static bool TryResolveTarget(LuaRewriteContext context, int objIndex, out int funcIndex, out string function)
        {
            funcIndex = -1;
            function = string.Empty;
            if (!context.Input[objIndex].IsName("obj"))
                return false;

            int dot = context.NextSignificant(objIndex + 1);
            if (dot < 0 || !context.Input[dot].IsOperator("."))
                return false;

            int name = context.NextSignificant(dot + 1);
            if (name < 0)
                return false;
            string text = context.Input[name].Text;
            if (text is not ("fill" or "convolve" or "resize"))
                return false;

            int paren = context.NextSignificant(name + 1);
            if (paren < 0 || !context.Input[paren].IsOperator("("))
                return false;

            funcIndex = name;
            function = text;
            return true;
        }

        private static void EmitBetween(LuaRewriteContext context, int from, int to)
        {
            for (int i = from; i < to; i++)
                context.Emit(context.Input[i]);
        }
    }
}
