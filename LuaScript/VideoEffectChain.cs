using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Windows.Media;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Effects;
using LuaScript.Compat;

namespace LuaScript
{
    internal sealed class VideoEffectChain : IDisposable
    {
        private sealed class Node
        {
            public required IVideoEffect Model;
            public required IVideoEffectProcessor Processor;
            public required AviUtlEffectMapping? Mapping;
            public required int RequestIndex;
        }

        private static readonly AviUtlParameterMapping Identity = new(string.Empty, string.Empty, 1d, 0d);

        private static Dictionary<string, Type>? _typeRegistry;
        private static Dictionary<string, Type>? _nameRegistry;
        private static readonly Dictionary<Type, Dictionary<string, PropertyInfo>> _propertyCache = [];

        private readonly IGraphicsDevicesAndContext _devices;
        private readonly List<Node> _nodes = [];
        private string[] _signature = [];
        private AviUtlEngine _signatureTarget = AviUtlEngine.Both;

        public VideoEffectChain(IGraphicsDevicesAndContext devices)
        {
            _devices = devices;
        }

        public ID2D1Image Apply(ID2D1Image source, IReadOnlyList<AviUtlEffectRequest> requests, EffectDescription baseDesc, AviUtlEngine target, ref DrawDescription drawDescription)
        {
            try
            {
                SyncNodes(requests, target);
            }
            catch
            {
                return source;
            }

            var current = source;
            var desc = drawDescription;

            foreach (var node in _nodes)
            {
                try
                {
                    ApplyParameters(node.Model, node.Mapping, requests[node.RequestIndex].Arguments);
                    node.Processor.SetInput(current);
                    var effectDescription = new EffectDescription(baseDesc, desc, baseDesc.InputIndex, baseDesc.InputCount, baseDesc.GroupIndex, baseDesc.GroupCount);
                    desc = node.Processor.Update(effectDescription);
                    current = node.Processor.Output;
                }
                catch
                {
                }
            }

            drawDescription = desc;
            return current;
        }

        private void SyncNodes(IReadOnlyList<AviUtlEffectRequest> requests, AviUtlEngine target)
        {
            if (SignatureMatches(requests, target))
                return;

            DisposeNodes();
            _signatureTarget = target;
            _signature = new string[requests.Count];
            for (int i = 0; i < requests.Count; i++)
                _signature[i] = requests[i].Name;

            for (int i = 0; i < requests.Count; i++)
            {
                Type? type;
                AviUtlEffectMapping? mapping;
                if (AviUtlCompatMap.Default.TryResolve(requests[i].Name, target, out var resolved) &&
                    ResolveType(resolved.TargetType) is { } mappedType)
                {
                    mapping = resolved;
                    type = mappedType;
                }
                else
                {
                    mapping = null;
                    type = ResolveName(requests[i].Name);
                }
                if (type is null)
                    continue;

                IVideoEffect? model = null;
                try
                {
                    model = Activator.CreateInstance(type) as IVideoEffect;
                }
                catch
                {
                }
                if (model is null)
                    continue;

                IVideoEffectProcessor processor;
                try
                {
                    processor = model.CreateVideoEffect(_devices);
                }
                catch
                {
                    continue;
                }

                _nodes.Add(new Node { Model = model, Processor = processor, Mapping = mapping, RequestIndex = i });
            }
        }

        private bool SignatureMatches(IReadOnlyList<AviUtlEffectRequest> requests, AviUtlEngine target)
        {
            if (_signatureTarget != target || _signature.Length != requests.Count)
                return false;
            for (int i = 0; i < requests.Count; i++)
            {
                if (!string.Equals(_signature[i], requests[i].Name, StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private static Type? ResolveType(string targetType)
        {
            EnsureRegistries();
            return _typeRegistry!.TryGetValue(Normalize(targetType), out var type) ? type : null;
        }

        private static Type? ResolveName(string name)
        {
            EnsureRegistries();
            return _nameRegistry!.TryGetValue(Normalize(name), out var type) ? type : null;
        }

        private static void EnsureRegistries()
        {
            if (_typeRegistry is not null && _nameRegistry is not null)
                return;

            var byType = new Dictionary<string, Type>(StringComparer.Ordinal);
            var byName = new Dictionary<string, Type>(StringComparer.Ordinal);
            try
            {
                foreach (var factory in EffectFactories.VideoEffectFactories)
                {
                    try
                    {
                        var type = factory.EffectType;
                        if (type is null)
                            continue;
                        byType.TryAdd(Normalize(type.Name), type);
                        Register(byName, factory.Name, type);
                        foreach (var keyword in factory.Keywords)
                            Register(byName, keyword, type);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
            _typeRegistry = byType;
            _nameRegistry = byName;
        }

        private static void Register(Dictionary<string, Type> map, string? key, Type type)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;
            map.TryAdd(Normalize(key), type);
        }

        private static string Normalize(string value) => value.Trim().ToLowerInvariant();

        private static void ApplyParameters(IVideoEffect model, AviUtlEffectMapping? mapping, IReadOnlyList<KeyValuePair<string, object>> arguments)
        {
            if (arguments.Count == 0)
                return;

            var properties = GetProperties(model.GetType());
            foreach (var (key, value) in arguments)
            {
                PropertyInfo? property;
                AviUtlParameterMapping parameter;
                if (mapping is not null)
                {
                    if (!mapping.TryGetParameter(key, out parameter) ||
                        !properties.TryGetValue(Normalize(parameter.Property), out property))
                        continue;
                }
                else
                {
                    if (!properties.TryGetValue(Normalize(key), out property))
                        continue;
                    parameter = Identity;
                }

                try
                {
                    SetValue(model, property, value, parameter);
                }
                catch
                {
                }
            }
        }

        private static Dictionary<string, PropertyInfo> GetProperties(Type type)
        {
            if (_propertyCache.TryGetValue(type, out var cached))
                return cached;

            var map = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length != 0)
                    continue;
                bool usable = property.PropertyType == typeof(Animation) || property.GetSetMethod() is not null;
                if (!usable)
                    continue;

                map.TryAdd(Normalize(property.Name), property);
                try
                {
                    var display = property.GetCustomAttribute<DisplayAttribute>()?.GetName();
                    if (!string.IsNullOrWhiteSpace(display))
                        map.TryAdd(Normalize(display), property);
                }
                catch
                {
                }
            }

            _propertyCache[type] = map;
            return map;
        }

        private static void SetValue(IVideoEffect model, PropertyInfo property, object value, AviUtlParameterMapping parameter)
        {
            var type = property.PropertyType;

            if (type == typeof(Animation))
            {
                if (property.GetValue(model) is Animation animation)
                    animation.CopyFrom(new Animation(parameter.Transform(ToDouble(value))));
                return;
            }
            if (type == typeof(bool))
            {
                property.SetValue(model, ToBool(value));
                return;
            }
            if (type.IsEnum)
            {
                property.SetValue(model, ToEnum(type, value));
                return;
            }
            if (type == typeof(Color))
            {
                property.SetValue(model, ToColor(value));
                return;
            }
            if (type == typeof(double) || type == typeof(float) || type == typeof(int) || type == typeof(long) || type == typeof(short))
            {
                property.SetValue(model, Convert.ChangeType(parameter.Transform(ToDouble(value)), type, CultureInfo.InvariantCulture));
                return;
            }
            if (type == typeof(string))
                property.SetValue(model, value?.ToString());
        }

        private static double ToDouble(object value) => value switch
        {
            double d => d,
            bool b => b ? 1d : 0d,
            string s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : 0d,
            _ => 0d,
        };

        private static bool ToBool(object value) => value switch
        {
            bool b => b,
            double d => d != 0d,
            string s => s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1",
            _ => false,
        };

        private static object ToEnum(Type type, object value)
        {
            if (value is string s && Enum.TryParse(type, s, true, out var parsed))
                return parsed;
            return Enum.ToObject(type, (int)ToDouble(value));
        }

        private static Color ToColor(object value)
        {
            int rgb = (int)ToDouble(value);
            return Color.FromRgb((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
        }

        private void DisposeNodes()
        {
            foreach (var node in _nodes)
            {
                try
                {
                    node.Processor.ClearInput();
                }
                catch
                {
                }
                node.Processor.Dispose();
            }
            _nodes.Clear();
        }

        public void Dispose() => DisposeNodes();
    }
}
