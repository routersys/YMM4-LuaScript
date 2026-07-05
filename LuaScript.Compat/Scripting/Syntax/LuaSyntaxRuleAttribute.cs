using System;

namespace LuaScript.Compat.Syntax
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    internal sealed class LuaSyntaxRuleAttribute(int order) : Attribute
    {
        public int Order { get; } = order;

        public bool IsCatalog { get; set; }

        public string Keyword { get; set; } = "";
    }
}
