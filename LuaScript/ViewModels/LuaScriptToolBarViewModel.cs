using System;
using System.Collections.Generic;

namespace LuaScript
{
    internal sealed class LuaScriptToolBarViewModel
    {
        private IReadOnlyList<IScriptProvider> _providers = [];

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public void SetProviders(IReadOnlyList<IScriptProvider> providers) => _providers = providers;

        public bool HasProviders => _providers.Count > 0;

        public string? FirstScript => _providers.Count == 0 ? null : _providers[0].Script;

        public void ApplyScript(string script) => Edit(provider => provider.Script = script);

        public void ResetToDefault() => Edit(provider => provider.Script = provider.DefaultScript);

        private void Edit(Action<IScriptProvider> apply)
        {
            if (_providers.Count == 0)
                return;

            BeginEdit?.Invoke(this, EventArgs.Empty);
            foreach (var provider in _providers)
                apply(provider);
            EndEdit?.Invoke(this, EventArgs.Empty);
        }
    }
}
