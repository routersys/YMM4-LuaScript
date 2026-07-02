using System.Collections.Concurrent;
using System.Text;

namespace LuaScript
{
    internal enum SceneValueKind
    {
        Nil = 0,
        Number = 1,
        String = 2,
        Boolean = 3,
    }

    internal readonly record struct SceneValue(SceneValueKind Kind, double Number, string? Text)
    {
        public static readonly SceneValue Nil = new(SceneValueKind.Nil, 0d, null);

        public static SceneValue FromNumber(double value) => new(SceneValueKind.Number, value, null);

        public static SceneValue FromBoolean(bool value) => new(SceneValueKind.Boolean, value ? 1d : 0d, null);

        public static SceneValue FromString(string value) => new(SceneValueKind.String, 0d, value);
    }

    internal readonly record struct SceneValueQuery(string Name, SceneValue Result);

    internal sealed class SceneSharedValues
    {
        public const int MaxNameBytes = NativeTagCapacity - 1;
        public const int MaxTextBytes = NativeTagCapacity - 1;

        private const int NativeTagCapacity = 4096;

        private static readonly ConcurrentDictionary<string, SceneSharedValues> s_scenes = new(StringComparer.Ordinal);

        private readonly Dictionary<string, SceneValue> _values = new(StringComparer.Ordinal);
        private readonly object _gate = new();

        public static SceneSharedValues ForScene(string sceneId) =>
            s_scenes.GetOrAdd(sceneId, static _ => new SceneSharedValues());

        public void Set(string name, SceneValue value)
        {
            name = TruncateUtf8(name, MaxNameBytes);
            if (value.Kind == SceneValueKind.String)
                value = SceneValue.FromString(TruncateUtf8(value.Text ?? string.Empty, MaxTextBytes));

            lock (_gate)
            {
                if (value.Kind == SceneValueKind.Nil)
                    _values.Remove(name);
                else
                    _values[name] = value;
            }
        }

        public SceneValue Get(string name)
        {
            name = TruncateUtf8(name, MaxNameBytes);
            lock (_gate)
                return _values.TryGetValue(name, out var value) ? value : SceneValue.Nil;
        }

        private static string TruncateUtf8(string text, int maxBytes)
        {
            if (text.Length <= maxBytes / 3 || Encoding.UTF8.GetByteCount(text) <= maxBytes)
                return text;

            int bytes = 0;
            int length = 0;
            while (length < text.Length)
            {
                char c = text[length];
                int size;
                int step = 1;
                if (char.IsHighSurrogate(c) && length + 1 < text.Length && char.IsLowSurrogate(text[length + 1]))
                {
                    size = 4;
                    step = 2;
                }
                else if (c < 0x80)
                {
                    size = 1;
                }
                else if (c < 0x800)
                {
                    size = 2;
                }
                else
                {
                    size = 3;
                }
                if (bytes + size > maxBytes)
                    break;
                bytes += size;
                length += step;
            }
            return text[..length];
        }
    }
}
