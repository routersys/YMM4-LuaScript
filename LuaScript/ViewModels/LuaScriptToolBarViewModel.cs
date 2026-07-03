using System;
using System.Collections.Generic;
using YukkuriMovieMaker.Commons;

namespace LuaScript
{
    internal sealed class LuaScriptToolBarViewModel
    {
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public ItemProperty[]? ItemProperties { get; set; }

        public bool HasProviders => GetProviders().Count > 0;

        public string? FirstScript
        {
            get
            {
                var providers = GetProviders();
                return providers.Count == 0 ? null : providers[0].Script;
            }
        }

        public void ApplyScript(string script) => Edit(provider => provider.Script = script);

        public void ResetToDefault() => Edit(provider => provider.Script = provider.DefaultScript);

        private void Edit(Action<IScriptProvider> apply)
        {
            var providers = GetProviders();
            if (providers.Count == 0)
                return;

            BeginEdit?.Invoke(this, EventArgs.Empty);
            foreach (var provider in providers)
                apply(provider);
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        private List<IScriptProvider> GetProviders()
        {
            var providers = new List<IScriptProvider>();
            if (ItemProperties is null)
                return providers;

            foreach (var item in ItemProperties)
            {
                if (item.PropertyOwner is IScriptProvider provider)
                    providers.Add(provider);
            }
            return providers;
        }
    }
}
