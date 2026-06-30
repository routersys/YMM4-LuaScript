using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace LuaScript.Compat
{
    internal sealed class AviUtlCompatMap
    {
        private const string ResourceSuffix = "AviUtlCompatMap.xml";

        private readonly Dictionary<string, List<AviUtlEffectMapping>> _effects;

        public static AviUtlCompatMap Default { get; } = Load();

        private AviUtlCompatMap(Dictionary<string, List<AviUtlEffectMapping>> effects)
        {
            _effects = effects;
        }

        public bool TryResolve(string name, AviUtlEngine target, out AviUtlEffectMapping mapping)
        {
            mapping = null!;
            if (string.IsNullOrEmpty(name) || !_effects.TryGetValue(Normalize(name), out var candidates))
                return false;

            foreach (var candidate in candidates)
            {
                if (candidate.Engine.Includes(target))
                {
                    mapping = candidate;
                    return true;
                }
            }
            return false;
        }

        public static AviUtlEngine ResolveTarget(string? script)
        {
            if (string.IsNullOrEmpty(script))
                return AviUtlEngine.AviUtl;

            foreach (var raw in script.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n'))
            {
                var line = raw.Trim();
                if (!line.StartsWith("--!", System.StringComparison.Ordinal))
                    continue;
                var token = line[3..].Trim().ToLowerInvariant();
                if (token == "aviutl2")
                    return AviUtlEngine.AviUtl2;
                if (token == "aviutl")
                    return AviUtlEngine.AviUtl;
            }
            return AviUtlEngine.AviUtl;
        }

        private static AviUtlCompatMap Load()
        {
            var effects = new Dictionary<string, List<AviUtlEffectMapping>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var stream = OpenResource();
                if (stream is not null)
                    Populate(effects, stream);
            }
            catch
            {
            }
            return new AviUtlCompatMap(effects);
        }

        private static void Populate(Dictionary<string, List<AviUtlEffectMapping>> effects, Stream stream)
        {
            var root = XDocument.Load(stream).Root;
            if (root is null)
                return;

            foreach (var element in root.Elements("effect"))
            {
                var target = (string?)element.Attribute("target");
                if (string.IsNullOrWhiteSpace(target))
                    continue;

                if (!AviUtlEngineExtensions.TryParse(((string?)element.Attribute("engine"))?.Trim().ToLowerInvariant(), out var engine))
                    engine = AviUtlEngine.Both;

                var parameters = element.Elements("param").Select(ParseParameter).ToArray();
                var mapping = new AviUtlEffectMapping(engine, target.Trim(), parameters);

                foreach (var name in SplitNames((string?)element.Attribute("names")))
                {
                    if (!effects.TryGetValue(name, out var list))
                    {
                        list = [];
                        effects[name] = list;
                    }
                    list.Add(mapping);
                }
            }
        }

        private static AviUtlParameterMapping ParseParameter(XElement element)
        {
            var source = ((string?)element.Attribute("name") ?? string.Empty).Trim();
            var property = ((string?)element.Attribute("property") ?? string.Empty).Trim();
            double scale = ParseDouble((string?)element.Attribute("scale"), 1d);
            double offset = ParseDouble((string?)element.Attribute("offset"), 0d);
            return new AviUtlParameterMapping(source, property, scale, offset);
        }

        private static IEnumerable<string> SplitNames(string? names)
        {
            if (string.IsNullOrWhiteSpace(names))
                yield break;
            foreach (var part in names.Split(','))
            {
                var trimmed = part.Trim();
                if (trimmed.Length != 0)
                    yield return Normalize(trimmed);
            }
        }

        private static double ParseDouble(string? value, double fallback) =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ? result : fallback;

        private static string Normalize(string value) => value.Trim().ToLowerInvariant();

        private static Stream? OpenResource()
        {
            var assembly = typeof(AviUtlCompatMap).Assembly;
            var name = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(ResourceSuffix, System.StringComparison.Ordinal));
            return name is null ? null : assembly.GetManifestResourceStream(name);
        }
    }
}
