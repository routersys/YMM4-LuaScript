namespace LuaScript.Api
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, Inherited = false, AllowMultiple = true)]
    internal sealed class LuaBuiltinAttribute(string name, params string[] parameters) : Attribute
    {
        public string Name { get; } = name;
        public string[] Parameters { get; } = parameters;
        public string Table { get; init; } = string.Empty;
    }
}
