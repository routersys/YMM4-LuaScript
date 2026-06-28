namespace LuaScript
{
    internal sealed class AviUtlScriptContext
    {
        public bool IsPlaying { get; set; }
        public bool IsPaused { get; set; }
        public string SceneId { get; set; } = string.Empty;

        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }

        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Ox { get; set; }
        public double Oy { get; set; }
        public double Oz { get; set; }
        public double Sx { get; set; }
        public double Sy { get; set; }
        public double Zoom { get; set; }
        public double Aspect { get; set; }
        public double Alpha { get; set; }
        public double Rx { get; set; }
        public double Ry { get; set; }
        public double Rz { get; set; }

        public double Track0 { get; set; }
        public double Track1 { get; set; }
        public double Track2 { get; set; }
        public double Track3 { get; set; }

        public bool Check0 { get; set; }
        public bool Check1 { get; set; }
        public bool Check2 { get; set; }
        public bool Check3 { get; set; }

        public bool HasColor { get; set; }
        public double ColorValue { get; set; }

        public double Time { get; set; }
        public int Frame { get; set; }
        public int TotalFrame { get; set; }
        public double TotalTime { get; set; }
        public int Framerate { get; set; }
        public int TimelineFrame { get; set; }
        public double TimelineTime { get; set; }
        public int SceneWidth { get; set; }
        public int SceneHeight { get; set; }
        public int Layer { get; set; }
        public int Index { get; set; }
        public int Num { get; set; }

        public Func<SceneObjectResolver>? ResolverProvider { get; set; }

        private const double Epsilon = 1e-10;

        private readonly List<SceneObjectQuery> _objectQueries = [];

        public IReadOnlyList<SceneObjectQuery> ObjectQueries => _objectQueries;

        private readonly List<AviUtlEffectRequest> _effectRequests = [];

        public IReadOnlyList<AviUtlEffectRequest> EffectRequests => _effectRequests;

        internal void AddEffect(AviUtlEffectRequest request) => _effectRequests.Add(request);

        internal void ClearEffects() => _effectRequests.Clear();

        private readonly List<DrawCommand> _drawCommands = [];

        public IReadOnlyList<DrawCommand> DrawCommands => _drawCommands;

        internal void AddDraw(DrawCommand command) => _drawCommands.Add(command);

        internal void ClearDraws() => _drawCommands.Clear();

        public double RxRad => Rx * Math.PI / 180d;
        public double RyRad => Ry * Math.PI / 180d;
        public double RzRad => Rz * Math.PI / 180d;

        public int GroupIndex { get; set; }
        public int GroupCount { get; set; }
        public int TimelineTotalFrame { get; set; }
        public double TimelineTotalTime { get; set; }
        public bool IsSaving { get; set; }
        public double TimeRatio { get; set; }

        private Func<byte[]>? _pixelLoader;
        private byte[]? _pixelBuffer;
        private bool _pixelBufferLoaded;
        private bool _isPixelsDirty;
        private bool _bufferReplaced;

        public bool IsPixelsDirty => _isPixelsDirty;
        public bool BufferReplaced => _bufferReplaced;

        internal int TotalChannels => ImageWidth * ImageHeight * 4;

        internal void ClearQueries() => _objectQueries.Clear();

        internal void SetPixelLoader(Func<byte[]> loader)
        {
            _pixelLoader = loader;
            _pixelBuffer = null;
            _pixelBufferLoaded = false;
            _isPixelsDirty = false;
            _bufferReplaced = false;
        }

        internal void ReplaceBuffer(byte[] buffer, int width, int height)
        {
            _pixelBuffer = buffer;
            _pixelBufferLoaded = true;
            _isPixelsDirty = true;
            _bufferReplaced = true;
            ImageWidth = width;
            ImageHeight = height;
        }

        internal void EnsurePixelBuffer()
        {
            if (_pixelBufferLoaded) return;
            _pixelBuffer = _pixelLoader?.Invoke();
            _pixelBufferLoaded = true;
        }

        internal void SetResolvedBuffer(byte[] buffer, int width, int height)
        {
            _pixelBuffer = buffer;
            _pixelBufferLoaded = true;
            ImageWidth = width;
            ImageHeight = height;
        }

        internal byte[]? GetPixelBuffer() => _pixelBuffer;

        internal void MarkPixelsDirty() => _isPixelsDirty = true;

        public bool ResolveObject(string tag, int frame, out SceneObjectInfo info)
        {
            var resolver = ResolverProvider?.Invoke();
            if (resolver is not null && resolver.TryResolve(tag, frame, out var resolved))
            {
                _objectQueries.Add(new SceneObjectQuery(tag, frame, resolved));
                info = resolved;
                return true;
            }

            _objectQueries.Add(new SceneObjectQuery(tag, frame, null));
            info = default;
            return false;
        }

        public void ApplyWriteBack(
            double newSx, double newSy, double newZoom, double newAspect,
            double newRx, double newRy, double newRz,
            double newRxr, double newRyr, double newRzr)
        {
            double initialSx = Sx;
            double initialSy = Sy;
            double initialZoom = Zoom;
            double initialAspect = Aspect;

            Sx = newSx;
            Sy = newSy;
            Zoom = newZoom;
            Aspect = newAspect;

            bool sxsyChanged = Math.Abs(Sx - initialSx) > Epsilon || Math.Abs(Sy - initialSy) > Epsilon;
            bool zoomAspectChanged = Math.Abs(Zoom - initialZoom) > Epsilon || Math.Abs(Aspect - initialAspect) > Epsilon;

            if (zoomAspectChanged && !sxsyChanged)
            {
                Sx = Zoom * (1.0 + Aspect);
                Sy = Zoom * (1.0 - Aspect);
            }

            double initialRxr = RxRad;
            Rx = Math.Abs(newRxr - initialRxr) > Epsilon
                ? newRxr * (180d / Math.PI)
                : newRx;

            double initialRyr = RyRad;
            Ry = Math.Abs(newRyr - initialRyr) > Epsilon
                ? newRyr * (180d / Math.PI)
                : newRy;

            double initialRzr = RzRad;
            Rz = Math.Abs(newRzr - initialRzr) > Epsilon
                ? newRzr * (180d / Math.PI)
                : newRz;
        }

        public unsafe (double r, double g, double b, double a) GetPixel(int x, int y)
        {
            EnsurePixelBuffer();
            if (_pixelBuffer is null || (uint)x >= (uint)ImageWidth || (uint)y >= (uint)ImageHeight)
                return (0d, 0d, 0d, 0d);

            fixed (byte* buf = _pixelBuffer)
            {
                byte* p = buf + (y * ImageWidth + x) * 4;
                double a = p[3];
                if (a <= 0d) return (0d, 0d, 0d, 0d);

                double scale = 255d / a;
                return (
                    Math.Clamp(p[2] * scale, 0d, 255d),
                    Math.Clamp(p[1] * scale, 0d, 255d),
                    Math.Clamp(p[0] * scale, 0d, 255d),
                    a
                );
            }
        }

        public unsafe void SetPixel(int x, int y, double r, double g, double b, double a = 255d)
        {
            EnsurePixelBuffer();
            if (_pixelBuffer is null || (uint)x >= (uint)ImageWidth || (uint)y >= (uint)ImageHeight)
                return;

            _isPixelsDirty = true;
            double aK = Math.Clamp(a, 0d, 255d) / 255d;

            fixed (byte* buf = _pixelBuffer)
            {
                byte* p = buf + (y * ImageWidth + x) * 4;
                p[0] = (byte)Math.Clamp(b * aK, 0d, 255d);
                p[1] = (byte)Math.Clamp(g * aK, 0d, 255d);
                p[2] = (byte)Math.Clamp(r * aK, 0d, 255d);
                p[3] = (byte)Math.Clamp(a, 0d, 255d);
            }
        }
    }
}
