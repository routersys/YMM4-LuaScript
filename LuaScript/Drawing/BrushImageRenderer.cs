using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Brush;

namespace LuaScript
{
    internal sealed class BrushImageRenderer(IGraphicsDevicesAndContext devices) : IDisposable
    {
        private const int MaxDimension = 8192;

        private readonly Dictionary<string, IBrushSource?> _sources = new(StringComparer.Ordinal);
        private GraphicsDevicesAndContext? _ctx;
        private PixelBufferManager? _pixels;

        public bool TryRender(TimelineItemSourceDescription desc, string name, int width, int height, out byte[] pixels, out int outWidth, out int outHeight)
        {
            pixels = [];
            outWidth = 0;
            outHeight = 0;

            if (width <= 0 || height <= 0 || width > MaxDimension || height > MaxDimension)
                return false;

            if (!_sources.TryGetValue(name, out var source))
            {
                source = CreateSource(name);
                _sources[name] = source;
            }
            if (source is null)
                return false;

            try
            {
                source.Update(desc);
                _pixels ??= new PixelBufferManager(_ctx!);
                pixels = _pixels.LoadBrushPixels(source.Brush, width, height);
            }
            catch
            {
                _sources.Remove(name);
                source.Dispose();
                pixels = [];
                return false;
            }

            outWidth = width;
            outHeight = height;
            return true;
        }

        private IBrushSource? CreateSource(string name)
        {
            foreach (var plugin in BrushFactory.Plugins)
            {
                if (!string.Equals(plugin.Name, name, StringComparison.Ordinal))
                    continue;
                _ctx ??= new GraphicsDevicesAndContext(devices);
                return plugin.CreateBrushParameter().CreateBrush(_ctx);
            }
            return null;
        }

        public void Dispose()
        {
            foreach (var source in _sources.Values)
                source?.Dispose();
            _sources.Clear();
            _pixels?.Dispose();
            _ctx?.Dispose();
        }
    }
}
