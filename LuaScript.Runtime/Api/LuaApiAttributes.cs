namespace LuaScript.Api
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    internal sealed class LuaTableAttribute(string name) : Attribute
    {
        public string Name { get; } = name;
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, Inherited = false, AllowMultiple = true)]
    internal sealed class LuaFunctionAttribute(string name, params string[] parameters) : Attribute
    {
        public string Name { get; } = name;
        public string[] Parameters { get; } = parameters;
        public string Table { get; init; } = string.Empty;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
    internal sealed class LuaConstantAttribute(string name) : Attribute
    {
        public string Name { get; } = name;
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly | AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    internal sealed class LuaVariableAttribute(string name) : Attribute
    {
        public string Name { get; } = name;
        public string Table { get; init; } = string.Empty;
        public bool InCatalog { get; init; } = true;
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, Inherited = false, AllowMultiple = true)]
    internal sealed class LuaBuiltinAttribute(string name, params string[] parameters) : Attribute
    {
        public string Name { get; } = name;
        public string[] Parameters { get; } = parameters;
        public string Table { get; init; } = string.Empty;
    }
}
