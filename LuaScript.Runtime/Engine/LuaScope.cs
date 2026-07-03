using MoonSharp.Interpreter;

namespace LuaScript
{
    internal sealed class LuaScope
    {
        private readonly string? _globalName;
        private readonly Action<Table>? _register;
        private readonly Action<Table, AviUtlScriptContext>? _update;
        private readonly List<string> _removalBuffer = [];

        private Table? _table;
        private HashSet<string>? _snapshot;

        internal LuaScope(string? globalName, Action<Table>? register, Action<Table, AviUtlScriptContext>? update)
        {
            _globalName = globalName;
            _register = register;
            _update = update;
        }

        internal Table Table => _table!;

        internal void Create(Script script)
        {
            _snapshot = null;
            if (_globalName is null)
            {
                _table = script.Globals;
                return;
            }
            _table = Build(script);
        }

        internal void Reconcile(Script script)
        {
            if (_globalName is null)
                return;
            if (ReferenceEquals(script.Globals.Get(_globalName).Table, _table))
                return;
            _table = Build(script);
            _snapshot = null;
        }

        internal void ResetUserKeys()
        {
            if (_snapshot is null)
                return;
            var buffer = _removalBuffer;
            buffer.Clear();
            foreach (var key in _table!.Keys)
            {
                if (key.Type == DataType.String && !_snapshot.Contains(key.String))
                    buffer.Add(key.String);
            }
            foreach (var key in buffer)
                _table[key] = DynValue.Nil;
        }

        internal void Update(AviUtlScriptContext ctx) => _update?.Invoke(_table!, ctx);

        internal void CaptureSnapshot()
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in _table!.Keys)
            {
                if (key.Type == DataType.String)
                    keys.Add(key.String);
            }
            _snapshot = keys;
        }

        private Table Build(Script script)
        {
            var table = new Table(script);
            _register?.Invoke(table);
            script.Globals[_globalName!] = table;
            return table;
        }
    }
}
