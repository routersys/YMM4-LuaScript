using System.Collections.Generic;

namespace LuaScript
{
    internal sealed class AviUtlEffectRequest
    {
        public string Name { get; }

        public IReadOnlyList<KeyValuePair<string, object>> Arguments { get; }

        public AviUtlEffectRequest(string name, IReadOnlyList<KeyValuePair<string, object>> arguments)
        {
            Name = name;
            Arguments = arguments;
        }
    }
}
