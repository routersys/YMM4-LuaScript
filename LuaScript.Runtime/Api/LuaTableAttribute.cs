namespace LuaScript.Api
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    internal sealed class LuaTableAttribute(string name) : Attribute
    {
        public string Name { get; } = name;
    }
}
