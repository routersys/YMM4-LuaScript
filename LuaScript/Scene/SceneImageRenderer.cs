using Vortice;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace LuaScript
{
    internal sealed class SceneImageRenderer(IGraphicsDevicesAndContext devices) : IDisposable
    {
        [ThreadStatic]
        private static bool t_rendering;

        private readonly Dictionary<Guid, ITimelineSource> _sources = [];
        private GraphicsDevicesAndContext? _ctx;
        private PixelBufferManager? _pixels;

        public bool TryRender(EffectDescription desc, string name, double time, out byte[] pixels, out int width, out int height)
        {
            pixels = [];
            width = 0;
            height = 0;

            if (t_rendering)
                return false;

            var info = FindScene(desc, name);
            if (info is null)
                return false;

            _ctx ??= new GraphicsDevicesAndContext(devices);
            if (!_sources.TryGetValue(info.ID, out var source))
            {
                if (!info.TryCreateVideoSource(_ctx, out source))
                    return false;
                _sources[info.ID] = source;
            }

            int w = Math.Max(1, info.Width);
            int h = Math.Max(1, info.Height);

            t_rendering = true;
            try
            {
                source.Update(TimeSpan.FromSeconds(time), desc.Usage);
                _pixels ??= new PixelBufferManager(_ctx);
                pixels = _pixels.LoadInputPixels(source.Output, new RawRectF(-w / 2f, -h / 2f, w / 2f, h / 2f), w, h);
            }
            catch
            {
                _sources.Remove(info.ID);
                source.Dispose();
                pixels = [];
                return false;
            }
            finally
            {
                t_rendering = false;
            }

            width = w;
            height = h;
            return true;
        }

        private static ISceneInfo? FindScene(EffectDescription desc, string name)
        {
            foreach (var info in desc.Scenes)
            {
                if (info.ID != desc.SceneId && string.Equals(info.Name, name, StringComparison.Ordinal))
                    return info;
            }
            return null;
        }

        public void Dispose()
        {
            foreach (var source in _sources.Values)
                source.Dispose();
            _sources.Clear();
            _pixels?.Dispose();
            _ctx?.Dispose();
        }
    }
}
