namespace LuaScript.Generator
{
    internal sealed record LuaFunctionModel(
        string LuaName,
        string MethodName,
        LuaReturnKind ReturnKind,
        EquatableArray<LuaParameterModel> Parameters);
}
