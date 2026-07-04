using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Brush;

namespace LuaScript
{
    internal sealed class BrushImageRenderer(IGraphicsDevicesAndContext devices) : IDisposable
    {
        private const int MaxDimension = 8192;

        private sealed class Entry(IBrushParameter parameter, IBrushSource source)
        {
            public IBrushParameter Parameter { get; } = parameter;
            public IBrushSource Source { get; } = source;
            public string[] AppliedKeys { get; set; } = [];
        }

        private readonly Dictionary<string, Entry?> _sources = new(StringComparer.Ordinal);
        private GraphicsDevicesAndContext? _ctx;
        private PixelBufferManager? _pixels;

        public bool TryRender(
            TimelineItemSourceDescription desc,
            string name,
            int width,
            int height,
            IReadOnlyList<KeyValuePair<string, object>> arguments,
            out byte[] pixels,
            out int outWidth,
            out int outHeight)
        {
            pixels = [];
            outWidth = 0;
            outHeight = 0;

            if (width <= 0 || height <= 0 || width > MaxDimension || height > MaxDimension)
                return false;

            if (!_sources.TryGetValue(name, out var entry))
            {
                entry = CreateEntry(name);
                _sources[name] = entry;
            }
            if (entry is null)
                return false;

            if (!KeysMatch(entry.AppliedKeys, arguments))
            {
                entry.Source.Dispose();
                entry = CreateEntry(name);
                _sources[name] = entry;
                if (entry is null)
                    return false;
            }

            try
            {
                if (arguments.Count > 0)
                {
                    ParameterBinder.ApplyArguments(entry.Parameter, arguments);
                    if (entry.AppliedKeys.Length != arguments.Count)
                        entry.AppliedKeys = new string[arguments.Count];
                    for (int i = 0; i < arguments.Count; i++)
                        entry.AppliedKeys[i] = arguments[i].Key;
                }

                entry.Source.Update(desc);
                _pixels ??= new PixelBufferManager(_ctx!);
                pixels = _pixels.LoadBrushPixels(entry.Source.Brush, width, height);
            }
            catch
            {
                _sources.Remove(name);
                entry.Source.Dispose();
                pixels = [];
                return false;
            }

            outWidth = width;
            outHeight = height;
            return true;
        }

        private static bool KeysMatch(string[] appliedKeys, IReadOnlyList<KeyValuePair<string, object>> arguments)
        {
            if (appliedKeys.Length != arguments.Count)
                return false;
            for (int i = 0; i < appliedKeys.Length; i++)
            {
                if (!string.Equals(appliedKeys[i], arguments[i].Key, StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private Entry? CreateEntry(string name)
        {
            foreach (var plugin in BrushFactory.Plugins)
            {
                if (!string.Equals(plugin.Name, name, StringComparison.Ordinal))
                    continue;
                _ctx ??= new GraphicsDevicesAndContext(devices);
                var parameter = plugin.CreateBrushParameter();
                return new Entry(parameter, parameter.CreateBrush(_ctx));
            }
            return null;
        }

        public void Dispose()
        {
            foreach (var entry in _sources.Values)
                entry?.Source.Dispose();
            _sources.Clear();
            _pixels?.Dispose();
            _ctx?.Dispose();
        }
    }
}
