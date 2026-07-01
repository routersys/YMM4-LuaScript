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

        public double Slider0 { get; set; }
        public double Slider1 { get; set; }
        public double Slider2 { get; set; }
        public double Slider3 { get; set; }

        public bool Check0 { get; set; }
        public bool Check1 { get; set; }
        public bool Check2 { get; set; }
        public bool Check3 { get; set; }

        public bool HasColor { get; set; }
        public double ColorValue { get; set; }

        public bool? DrawStateOverride { get; set; }

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

        private readonly Dictionary<string, string> _stringParameters = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, string> StringParameters => _stringParameters;

        internal void SetStringParameter(string name, string? value) => _stringParameters[name] = value ?? string.Empty;

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

        private bool _drawTargetTemp;

        internal void ResetDrawTarget() => _drawTargetTemp = false;

        internal void SetDrawTarget(bool temp, int width, int height, bool hasSize)
        {
            _drawTargetTemp = temp;
            if (temp && hasSize)
            {
                int w = Math.Max(1, width);
                int h = Math.Max(1, height);
                _buffers["t"] = (new byte[w * h * 4], w, h);
            }
        }

        internal void SubmitDraw(DrawCommand command)
        {
            if (!_drawTargetTemp)
            {
                _drawCommands.Add(command);
                return;
            }
            CompositeToTemp(command);
        }

        private void CompositeToTemp(DrawCommand command)
        {
            EnsurePixelBuffer();
            var src = _pixelBuffer;
            if (src is null)
                return;

            if (!_buffers.TryGetValue("t", out var temp))
            {
                int w = Math.Max(1, ImageWidth);
                int h = Math.Max(1, ImageHeight);
                temp = (new byte[w * h * 4], w, h);
                _buffers["t"] = temp;
            }

            bool linear = command.Antialias != 0d;
            if (command.Poly is { } poly)
                SoftwareCompositor.DrawPolyInto(temp.Data, temp.Width, temp.Height, src, ImageWidth, ImageHeight, poly, command.Alpha, linear);
            else
                SoftwareCompositor.DrawInto(temp.Data, temp.Width, temp.Height, src, ImageWidth, ImageHeight, command.Ox, command.Oy, command.Zoom, command.Aspect, command.Alpha, linear);
        }

        private readonly Dictionary<string, (byte[] Data, int Width, int Height)> _buffers = new(StringComparer.Ordinal);

        internal bool CopyBuffer(string dst, string src)
        {
            if (!TryReadBuffer(src, out var data, out int w, out int h))
                return false;
            return TryWriteBuffer(dst, data, w, h);
        }

        private bool TryReadBuffer(string id, out byte[] data, out int width, out int height)
        {
            data = [];
            width = 0;
            height = 0;
            switch (BufferKind(id, out string key))
            {
                case 'o':
                    EnsurePixelBuffer();
                    if (_pixelBuffer is null)
                        return false;
                    data = _pixelBuffer;
                    width = ImageWidth;
                    height = ImageHeight;
                    return true;
                case 't':
                case 'c':
                    if (_buffers.TryGetValue(key, out var stored))
                    {
                        data = stored.Data;
                        width = stored.Width;
                        height = stored.Height;
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        private bool TryWriteBuffer(string id, byte[] data, int width, int height)
        {
            switch (BufferKind(id, out string key))
            {
                case 'o':
                    ReplaceBuffer((byte[])data.Clone(), width, height);
                    return true;
                case 't':
                case 'c':
                    _buffers[key] = ((byte[])data.Clone(), width, height);
                    return false;
                default:
                    return false;
            }
        }

        private static char BufferKind(string id, out string key)
        {
            key = string.Empty;
            int i = 0;
            while (i < id.Length && (id[i] == ' ' || id[i] == '\t'))
                i++;
            if (i >= id.Length)
                return '\0';
            char kind = char.ToLowerInvariant(id[i]);
            if (kind == 't')
                key = "t";
            else if (kind == 'c')
            {
                int colon = id.IndexOf(':', i);
                key = colon >= 0 ? "c:" + id[(colon + 1)..].Trim() : "c";
            }
            return kind;
        }

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
