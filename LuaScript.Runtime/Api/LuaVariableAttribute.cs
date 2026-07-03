namespace LuaScript.Api
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly | AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    internal sealed class LuaVariableAttribute(string name) : Attribute
    {
        public string Name { get; } = name;
        public string Table { get; init; } = string.Empty;
        public bool InCatalog { get; init; } = true;
    }
}
