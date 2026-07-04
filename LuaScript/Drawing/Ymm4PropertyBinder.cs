using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Windows.Media;
using LuaScript.Compat;
using YukkuriMovieMaker.Commons;

namespace LuaScript
{
    internal static class Ymm4PropertyBinder
    {
        public static readonly AviUtlParameterMapping Identity = new(string.Empty, string.Empty, 1d, 0d);

        private static readonly Dictionary<Type, Dictionary<string, PropertyInfo>> s_propertyCache = [];

        public static string Normalize(string value) => value.Trim().ToLowerInvariant();

        public static Dictionary<string, PropertyInfo> GetProperties(Type type)
        {
            lock (s_propertyCache)
            {
                if (s_propertyCache.TryGetValue(type, out var cached))
                    return cached;
            }

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

            lock (s_propertyCache)
            {
                s_propertyCache[type] = map;
            }
            return map;
        }

        public static void ApplyArguments(object model, IReadOnlyList<KeyValuePair<string, object>> arguments)
        {
            if (arguments.Count == 0)
                return;

            var properties = GetProperties(model.GetType());
            foreach (var (key, value) in arguments)
            {
                if (!properties.TryGetValue(Normalize(key), out var property))
                    continue;
                try
                {
                    SetValue(model, property, value, Identity);
                }
                catch
                {
                }
            }
        }

        public static void SetValue(object model, PropertyInfo property, object value, AviUtlParameterMapping parameter)
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
    }
}
