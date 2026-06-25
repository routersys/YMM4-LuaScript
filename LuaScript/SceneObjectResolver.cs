using YukkuriMovieMaker.Project.Items;

namespace LuaScript
{
    internal sealed class SceneObjectResolver
    {
        internal readonly record struct Entry(string Tag, int Frame, int Length, int Layer, VisualItem Item);

        private readonly Entry[] _entries;
        private readonly int _fps;

        internal SceneObjectResolver(Entry[] entries, int fps)
        {
            _entries = entries;
            _fps = fps;
        }

        internal bool TryResolve(string tag, int timelineFrame, out SceneObjectInfo info)
        {
            int fallback = -1;
            for (int i = 0; i < _entries.Length; i++)
            {
                if (!string.Equals(_entries[i].Tag, tag, StringComparison.Ordinal))
                    continue;
                if (IsExist(_entries[i], timelineFrame))
                {
                    info = Evaluate(_entries[i], timelineFrame);
                    return true;
                }
                if (fallback < 0)
                    fallback = i;
            }

            if (fallback >= 0)
            {
                info = Evaluate(_entries[fallback], timelineFrame);
                return true;
            }

            info = default;
            return false;
        }

        private static bool IsExist(in Entry entry, int timelineFrame)
            => entry.Frame <= timelineFrame && timelineFrame < entry.Frame + entry.Length;

        private SceneObjectInfo Evaluate(in Entry entry, int timelineFrame)
        {
            var visual = entry.Item;
            int length = entry.Length;
            int local = Math.Clamp(timelineFrame - entry.Frame, 0, length);

            double x = visual.X.GetValue(local, length, _fps);
            double y = visual.Y.GetValue(local, length, _fps);
            double z = visual.Z.GetValue(local, length, _fps);
            double zoom = visual.Zoom.GetValue(local, length, _fps) / 100d;
            double rz = visual.Rotation.GetValue(local, length, _fps);

            double opacity = visual.Opacity.GetValue(local, length, _fps) / 100d;
            double timeSec = _fps > 0 ? local / (double)_fps : 0d;
            double durationSec = _fps > 0 ? length / (double)_fps : 0d;
            double fadeIn = visual.FadeIn <= 0d ? 1d : Math.Min(1d, timeSec / visual.FadeIn);
            double fadeOut = visual.FadeOut <= 0d ? 1d : Math.Min(1d, (durationSec - timeSec) / visual.FadeOut);
            double alpha = opacity * Math.Min(fadeIn, fadeOut) * 255d;

            return new SceneObjectInfo(entry.Tag, IsExist(entry, timelineFrame), x, y, z, zoom, rz, alpha, entry.Layer);
        }
    }
}
