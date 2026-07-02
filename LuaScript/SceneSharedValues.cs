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
        public const int MaxNameBytes = 4095;
        public const int MaxTextBytes = 4095;
        public const int MaxEntries = 4096;

        private static readonly ConcurrentDictionary<string, SceneSharedValues> s_scenes = new(StringComparer.Ordinal);

        private readonly Dictionary<string, SceneValue> _committed = new(StringComparer.Ordinal);
        private readonly Dictionary<string, SceneValue> _pending = new(StringComparer.Ordinal);
        private readonly object _gate = new();
        private long _generation = long.MinValue;
        private bool _exporting;

        public static SceneSharedValues ForScene(string sceneId) =>
            s_scenes.GetOrAdd(sceneId, static _ => new SceneSharedValues());

        public SceneValue Read(long generation, bool exporting, string name)
        {
            lock (_gate)
            {
                Advance(generation, exporting);
                return _committed.TryGetValue(name, out var value) ? value : SceneValue.Nil;
            }
        }

        public void Publish(long generation, bool exporting, Dictionary<string, SceneValue> writes)
        {
            lock (_gate)
            {
                Advance(generation, exporting);
                foreach (var pair in writes)
                {
                    if (pair.Value.Kind == SceneValueKind.Nil ||
                        _pending.ContainsKey(pair.Key) ||
                        _pending.Count < MaxEntries)
                    {
                        _pending[pair.Key] = pair.Value;
                    }
                }
            }
        }

        private void Advance(long generation, bool exporting)
        {
            if (exporting && (!_exporting || generation < _generation))
            {
                _committed.Clear();
                _pending.Clear();
                _generation = generation;
            }
            else if (generation != _generation)
            {
                foreach (var pair in _pending)
                {
                    if (pair.Value.Kind == SceneValueKind.Nil)
                        _committed.Remove(pair.Key);
                }
                foreach (var pair in _pending)
                {
                    if (pair.Value.Kind != SceneValueKind.Nil &&
                        (_committed.ContainsKey(pair.Key) || _committed.Count < MaxEntries))
                    {
                        _committed[pair.Key] = pair.Value;
                    }
                }
                _pending.Clear();
                _generation = generation;
            }
            _exporting = exporting;
        }

        public static string NormalizeName(string name) => TruncateUtf8(name, MaxNameBytes);

        public static SceneValue NormalizeValue(SceneValue value) =>
            value.Kind == SceneValueKind.String
                ? SceneValue.FromString(TruncateUtf8(value.Text ?? string.Empty, MaxTextBytes))
                : value;

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
