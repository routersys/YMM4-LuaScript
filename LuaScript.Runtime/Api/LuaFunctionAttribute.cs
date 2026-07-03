namespace LuaScript.Api
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, Inherited = false, AllowMultiple = true)]
    internal sealed class LuaFunctionAttribute(string name, params string[] parameters) : Attribute
    {
        public string Name { get; } = name;
        public string[] Parameters { get; } = parameters;
        public string Table { get; init; } = string.Empty;
    }
}
