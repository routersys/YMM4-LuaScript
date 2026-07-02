namespace LuaScript
{
    internal sealed class SceneValueSession
    {
        private readonly Dictionary<string, SceneValue> _writes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, SceneValue> _reads = new(StringComparer.Ordinal);
        private readonly List<SceneValueQuery> _queries = [];

        private SceneSharedValues? _store;
        private object _scope = new();
        private string _sceneId = string.Empty;
        private long _generation;
        private int _layer;

        public bool HasWrites => _writes.Count > 0;

        public IReadOnlyList<SceneValueQuery> Queries => _queries;

        public void Begin(object scope, string sceneId, long generation, int layer)
        {
            if (!ReferenceEquals(_scope, scope) || !string.Equals(_sceneId, sceneId, StringComparison.Ordinal))
            {
                _store = null;
                _scope = scope;
                _sceneId = sceneId;
            }
            _generation = generation;
            _layer = layer;
            _writes.Clear();
            _reads.Clear();
            _queries.Clear();
        }

        public SceneValue Get(string name)
        {
            name = SceneSharedValues.NormalizeName(name);
            if (_writes.TryGetValue(name, out var written))
                return written;
            if (_reads.TryGetValue(name, out var cached))
                return cached;
            var value = Store.Read(_generation, name);
            _reads[name] = value;
            _queries.Add(new SceneValueQuery(name, value));
            return value;
        }

        public void Set(string name, SceneValue value)
        {
            name = SceneSharedValues.NormalizeName(name);
            value = SceneSharedValues.NormalizeValue(value);
            if (value.Kind != SceneValueKind.Nil &&
                !_writes.ContainsKey(name) &&
                _writes.Count >= SceneSharedValues.MaxEntries)
            {
                return;
            }
            _writes[name] = value;
        }

        public void Publish()
        {
            if (_writes.Count == 0)
                return;
            Store.Publish(_generation, _layer, _writes);
        }

        private SceneSharedValues Store => _store ??= SceneSharedValues.ForScene(_scope, _sceneId);
    }
}
