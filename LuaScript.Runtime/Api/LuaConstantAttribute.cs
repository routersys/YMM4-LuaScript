namespace LuaScript.Api
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
    internal sealed class LuaConstantAttribute(string name) : Attribute
    {
        public string Name { get; } = name;
    }
}
