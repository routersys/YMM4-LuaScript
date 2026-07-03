using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
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
        public const long MaxHistoryCostBytes = 16 * 1024 * 1024;

        private const long VersionBaseCost = 64;

        private readonly record struct SceneVersion(long Frame, int Layer, SceneValue Value);

        private static readonly ConditionalWeakTable<object, ConcurrentDictionary<string, SceneSharedValues>> s_scopes = new();

        private readonly Dictionary<string, List<SceneVersion>> _names = new(StringComparer.Ordinal);
        private readonly object _gate = new();
        private long _historyCost;

        public static SceneSharedValues ForScene(object scope, string sceneId) =>
            s_scopes.GetOrCreateValue(scope).GetOrAdd(sceneId, static _ => new SceneSharedValues());

        public SceneValue Read(long generation, string name)
        {
            lock (_gate)
            {
                if (!_names.TryGetValue(name, out var versions))
                    return SceneValue.Nil;
                int index = LatestBefore(versions, generation);
                return index >= 0 ? versions[index].Value : SceneValue.Nil;
            }
        }

        public void Publish(long generation, int layer, Dictionary<string, SceneValue> writes)
        {
            lock (_gate)
            {
                foreach (var pair in writes)
                    Upsert(generation, layer, pair.Key, pair.Value);
                EnforceHistoryBudget();
            }
        }

        private void Upsert(long generation, int layer, string name, SceneValue value)
        {
            if (!_names.TryGetValue(name, out var versions))
            {
                if (_names.Count >= MaxEntries)
                    return;
                if (value == SceneValue.Nil)
                    return;
                _names[name] = [new SceneVersion(generation, layer, value)];
                return;
            }

            int index = LatestBefore(versions, generation + 1);
            if (index >= 0 && versions[index].Frame == generation)
            {
                if (layer < versions[index].Layer)
                    return;
                if (index < versions.Count - 1)
                    _historyCost += CostOf(value) - CostOf(versions[index].Value);
                versions[index] = new SceneVersion(generation, layer, value);
                return;
            }

            var previous = index >= 0 ? versions[index].Value : SceneValue.Nil;
            if (value == previous)
                return;

            versions.Insert(index + 1, new SceneVersion(generation, layer, value));
            if (index + 1 < versions.Count - 1)
                _historyCost += CostOf(value);
            else if (versions.Count > 1)
                _historyCost += CostOf(versions[^2].Value);
        }

        private void EnforceHistoryBudget()
        {
            while (_historyCost > MaxHistoryCostBytes)
            {
                List<SceneVersion>? oldest = null;
                string oldestName = string.Empty;
                foreach (var pair in _names)
                {
                    var versions = pair.Value;
                    if (versions.Count < 2)
                        continue;
                    if (oldest is null ||
                        versions[0].Frame < oldest[0].Frame ||
                        (versions[0].Frame == oldest[0].Frame && string.CompareOrdinal(pair.Key, oldestName) < 0))
                    {
                        oldest = versions;
                        oldestName = pair.Key;
                    }
                }
                if (oldest is null)
                    return;
                _historyCost -= CostOf(oldest[0].Value);
                oldest.RemoveAt(0);
            }
        }

        private static long CostOf(SceneValue value) =>
            VersionBaseCost + (value.Text is null ? 0 : value.Text.Length * 2L);

        private static int LatestBefore(List<SceneVersion> versions, long generation)
        {
            int low = 0;
            int high = versions.Count - 1;
            int result = -1;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                if (versions[mid].Frame < generation)
                {
                    result = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }
            return result;
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
