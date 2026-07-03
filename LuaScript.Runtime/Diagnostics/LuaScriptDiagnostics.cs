using System;
using System.Collections.Generic;

namespace LuaScript.Diagnostics
{
    internal interface ILuaScriptDiagnosticsListener
    {
        void OnDiagnosticsChanged();
    }

    internal sealed class LuaScriptDiagnostics
    {
        public static LuaScriptDiagnostics Instance { get; } = new();

        private const int MaxTrackedScripts = 64;

        private readonly object _gate = new();
        private readonly Dictionary<string, IReadOnlyList<LuaScriptDiagnostic>> _entries = new(StringComparer.Ordinal);
        private readonly Queue<string> _insertionOrder = new();
        private readonly List<WeakReference<ILuaScriptDiagnosticsListener>> _listeners = [];

        private LuaScriptDiagnostics()
        {
        }

        public void Subscribe(ILuaScriptDiagnosticsListener listener)
        {
            lock (_gate)
            {
                PruneDeadListeners();
                _listeners.Add(new WeakReference<ILuaScriptDiagnosticsListener>(listener));
            }
        }

        public IReadOnlyList<LuaScriptDiagnostic> Get(string? script)
        {
            if (script is null)
                return [];

            lock (_gate)
                return _entries.TryGetValue(script, out var diagnostics) ? diagnostics : [];
        }

        public void Report(string? script, IReadOnlyList<LuaScriptDiagnostic> diagnostics)
        {
            if (script is null)
                return;

            diagnostics ??= [];

            ILuaScriptDiagnosticsListener[] targets;
            lock (_gate)
            {
                if (_entries.TryGetValue(script, out var existing) && AreEqual(existing, diagnostics))
                    return;

                if (!_entries.ContainsKey(script))
                    _insertionOrder.Enqueue(script);

                _entries[script] = diagnostics;

                while (_entries.Count > MaxTrackedScripts && _insertionOrder.Count > 0)
                {
                    var oldest = _insertionOrder.Dequeue();
                    if (!string.Equals(oldest, script, StringComparison.Ordinal))
                        _entries.Remove(oldest);
                }

                targets = CollectLiveListeners();
            }

            foreach (var target in targets)
                target.OnDiagnosticsChanged();
        }

        private ILuaScriptDiagnosticsListener[] CollectLiveListeners()
        {
            var live = new List<ILuaScriptDiagnosticsListener>(_listeners.Count);
            for (int i = _listeners.Count - 1; i >= 0; i--)
            {
                if (_listeners[i].TryGetTarget(out var listener))
                    live.Add(listener);
                else
                    _listeners.RemoveAt(i);
            }
            return [.. live];
        }

        private void PruneDeadListeners()
        {
            for (int i = _listeners.Count - 1; i >= 0; i--)
            {
                if (!_listeners[i].TryGetTarget(out _))
                    _listeners.RemoveAt(i);
            }
        }

        private static bool AreEqual(IReadOnlyList<LuaScriptDiagnostic> a, IReadOnlyList<LuaScriptDiagnostic> b)
        {
            if (a.Count != b.Count)
                return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (!a[i].Equals(b[i]))
                    return false;
            }
            return true;
        }
    }
}
