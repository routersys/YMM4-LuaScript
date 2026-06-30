using System.Collections.Generic;

namespace LuaScript.Compat
{
    internal readonly record struct AviUtlParameterMapping(string Source, string Property, double Scale, double Offset)
    {
        public double Transform(double value) => value * Scale + Offset;
    }

    internal sealed class AviUtlEffectMapping
    {
        private readonly Dictionary<string, AviUtlParameterMapping> _parameters;

        public AviUtlEngine Engine { get; }

        public string TargetType { get; }

        public AviUtlEffectMapping(AviUtlEngine engine, string targetType, IEnumerable<AviUtlParameterMapping> parameters)
        {
            Engine = engine;
            TargetType = targetType;
            _parameters = new Dictionary<string, AviUtlParameterMapping>(StringComparer.OrdinalIgnoreCase);
            foreach (var parameter in parameters)
                _parameters[parameter.Source] = parameter;
        }

        public bool TryGetParameter(string source, out AviUtlParameterMapping mapping) =>
            _parameters.TryGetValue(source, out mapping);
    }
}
