using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using LuaScript.Engine;
using Vortice;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Project.Items;

namespace LuaScript
{
    internal sealed class LuaScriptEffectProcessor(IGraphicsDevicesAndContext devices, LuaScriptEffect item) : VideoEffectProcessorBase(devices)
    {
        private readonly record struct RenderKey(
            int Frame,
            double Time,
            int Length,
            int Fps,
            double Track0,
            double Track1,
            double Track2,
            double Track3,
            string Script,
            TimelineSourceUsage Usage,
            Guid SceneId,
            int TimelineFrame,
            double TimelineTime,
            int SceneWidth,
            int SceneHeight,
            int Layer,
            int InputIndex,
            int InputCount,
            int GroupIndex,
            int GroupCount,
            int TimelineTotalFrame,
            double TimelineTotalTime,
            DrawDescription InputDesc
        );

        private const int NativeTimeoutMilliseconds = 5000;

        private static string NativeDirectory =>
            Path.Combine(Path.GetDirectoryName(typeof(LuaScriptEffectProcessor).Assembly.Location) ?? AppContext.BaseDirectory, "native");

        private readonly LuaScriptEngine _engine = new();
        private readonly SemaphoreSlim _pixelLoaderSemaphore = new(1, 1);
        private AviUtlScriptContext _context = new();

        private LuaJitWorker? _nativeWorker;
        private double[]? _nativeFields;
        private bool _nativeWarned;

        private EffectDescription? _frameDesc;
        private SceneObjectResolver? _frameResolver;
        private bool _frameResolverBuilt;

        private Guid _lastSceneGuid;
        private string _lastSceneId = string.Empty;
        private bool _sceneIdCached;

        private GraphicsDevicesAndContext? _ownCtx;

        private ID2D1Bitmap1? _renderTarget;
        private ID2D1Bitmap1? _stagingBitmap;
        private ID2D1Bitmap1? _outputBitmap;
        private AffineTransform2D? _transformEffect;
        private byte[]? _pixelBuffer;
        private int _bitmapWidth;
        private int _bitmapHeight;
        private RawRectF _cachedBounds;

        private bool _isFirst = true;
        private ID2D1Image? _cachedInput;
        private RenderKey _cachedKey;
        private SceneObjectQuery[] _cachedQueries = [];
        private DrawDescription? _cachedOutputDesc;
        private bool _cachedPixelsModified;

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            _ownCtx = new GraphicsDevicesAndContext(devices);
            disposer.Collect(_ownCtx);
            _context.ResolverProvider = GetFrameResolver;
            return null;
        }

        private SceneObjectResolver GetFrameResolver()
        {
            if (!_frameResolverBuilt)
            {
                _frameResolver = BuildSceneObjectResolver(_frameDesc!);
                _frameResolverBuilt = true;
            }
            return _frameResolver!;
        }

        private string ResolveSceneId(Guid sceneId)
        {
            if (!_sceneIdCached || sceneId != _lastSceneGuid)
            {
                _lastSceneId = sceneId.ToString();
                _lastSceneGuid = sceneId;
                _sceneIdCached = true;
            }
            return _lastSceneId;
        }

        protected override void setInput(ID2D1Image? input)
        {
            if (!ReferenceEquals(_cachedInput, input))
                _isFirst = true;
            _cachedInput = input;
        }

        protected override void ClearEffectChain()
        {
            effectOutput = null;
        }

        public override DrawDescription Update(EffectDescription desc)
        {
            if (input is null || _ownCtx is null)
                return desc.DrawDescription;

            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;
            var time = desc.ItemPosition.Time.TotalSeconds;
            var script = item.Script ?? string.Empty;

            var t0 = item.Track0.GetValue(frame, length, fps);
            var t1 = item.Track1.GetValue(frame, length, fps);
            var t2 = item.Track2.GetValue(frame, length, fps);
            var t3 = item.Track3.GetValue(frame, length, fps);

            var inDesc = desc.DrawDescription;

            var key = new RenderKey(
                frame, time, length, fps,
                t0, t1, t2, t3,
                script, desc.Usage, desc.SceneId,
                desc.TimelinePosition.Frame,
                desc.TimelinePosition.Time.TotalSeconds,
                desc.ScreenSize.Width, desc.ScreenSize.Height,
                desc.Layer, desc.InputIndex, desc.InputCount,
                desc.GroupIndex, desc.GroupCount,
                desc.TimelineDuration.Frame,
                desc.TimelineDuration.Time.TotalSeconds,
                inDesc);

            _frameDesc = desc;
            _frameResolver = null;
            _frameResolverBuilt = false;

            if (!_isFirst && key == _cachedKey &&
                (_cachedQueries.Length == 0 || QueriesMatch(_cachedQueries, GetFrameResolver())))
            {
                effectOutput = _cachedPixelsModified ? _transformEffect?.Output : null;
                return _cachedOutputDesc ?? inDesc;
            }

            var bounds = _ownCtx.DeviceContext.GetImageLocalBounds(input);
            int imgW = Math.Max(1, (int)Math.Ceiling(bounds.Right - bounds.Left));
            int imgH = Math.Max(1, (int)Math.Ceiling(bounds.Bottom - bounds.Top));

            var ctx = _context;
            PopulateContext(ctx, in key, imgW, imgH);
            ctx.SetPixelLoader(() => LoadInputPixels(bounds, imgW, imgH));

            DrawDescription outDesc = inDesc;
            bool pixelsModified = false;

            try
            {
                bool wantNative = ScriptDirective.ResolveAuto(script) == ScriptEngineKind.Native;
                bool nativeReady = wantNative && LuaJitWorker.IsAvailable(NativeDirectory);
                if (wantNative && !nativeReady)
                    WarnNativeUnavailableOnce();

                if (nativeReady)
                {
                    pixelsModified = ExecuteNative(script, ctx, bounds, imgW, imgH);
                }
                else
                {
                    _engine.Execute(script, ctx);

                    if (ctx.IsPixelsDirty)
                    {
                        EnsureBitmaps(imgW, imgH);
                        WritePixelsToOutput(ctx.GetPixelBuffer()!, imgW);
                        UpdateTransformEffect(bounds);
                        effectOutput = _transformEffect!.Output;
                        pixelsModified = true;
                    }
                    else
                    {
                        effectOutput = null;
                    }
                }

                outDesc = BuildOutputDesc(inDesc, ctx);
            }
            catch (LuaScriptTimeoutException ex)
            {
                effectOutput = null;
                _context = new AviUtlScriptContext { ResolverProvider = GetFrameResolver };
                _isFirst = true;
                Log.Default.Write(ex.Message, ex);
                return inDesc;
            }
            catch (LuaScriptException ex)
            {
                effectOutput = null;
                outDesc = inDesc;
                Log.Default.Write(ex.Message, ex);
            }
            finally
            {
                _pixelLoaderSemaphore.Wait();
                _pixelLoaderSemaphore.Release();
            }

            _isFirst = false;
            _cachedKey = key;
            _cachedQueries = ctx.ObjectQueries.Count == 0 ? [] : [.. ctx.ObjectQueries];
            _cachedOutputDesc = outDesc;
            _cachedPixelsModified = pixelsModified;

            return outDesc;
        }

        private static SceneObjectResolver BuildSceneObjectResolver(EffectDescription desc)
        {
            var scenes = desc.Scenes;
            if (scenes is null)
                return new SceneObjectResolver([], desc.FPS);

            Scene? scene = null;
            foreach (var info in scenes)
            {
                if (info is Scene candidate && candidate.ID == desc.SceneId)
                {
                    scene = candidate;
                    break;
                }
            }
            if (scene is null)
                return new SceneObjectResolver([], desc.FPS);

            var items = scene.Timeline.Items;
            var entries = new List<SceneObjectResolver.Entry>(items.Count);

            foreach (var item in items)
            {
                if (item is not VisualItem visual)
                    continue;
                var tag = item.Remark;
                if (string.IsNullOrEmpty(tag))
                    continue;

                entries.Add(new SceneObjectResolver.Entry(tag, item.Frame, item.Length, item.Layer, visual));
            }

            return new SceneObjectResolver([.. entries], desc.FPS);
        }

        private static bool QueriesMatch(SceneObjectQuery[] queries, SceneObjectResolver resolver)
        {
            for (int i = 0; i < queries.Length; i++)
            {
                var query = queries[i];
                SceneObjectInfo? current = resolver.TryResolve(query.Tag, query.Frame, out var info) ? info : null;
                if (!Nullable.Equals(current, query.Result))
                    return false;
            }
            return true;
        }

        private void PopulateContext(AviUtlScriptContext ctx, in RenderKey key, int imgW, int imgH)
        {
            var zoom = key.InputDesc.Zoom;
            double zoomAvg = (zoom.X + zoom.Y) / 2d;
            double aspect = zoomAvg > 0d
                ? (zoom.X - zoom.Y) / (zoom.X + zoom.Y)
                : 0d;

            ctx.ClearQueries();

            ctx.ImageWidth = imgW;
            ctx.ImageHeight = imgH;
            ctx.X = key.InputDesc.Draw.X;
            ctx.Y = key.InputDesc.Draw.Y;
            ctx.Z = key.InputDesc.Draw.Z;
            ctx.Ox = key.InputDesc.CenterPoint.X;
            ctx.Oy = key.InputDesc.CenterPoint.Y;
            ctx.Oz = 0d;
            ctx.Sx = zoom.X;
            ctx.Sy = zoom.Y;
            ctx.Zoom = zoomAvg;
            ctx.Aspect = aspect;
            ctx.Alpha = key.InputDesc.Opacity * 255d;
            ctx.Rx = key.InputDesc.Rotation.X;
            ctx.Ry = key.InputDesc.Rotation.Y;
            ctx.Rz = key.InputDesc.Rotation.Z;
            ctx.Track0 = key.Track0;
            ctx.Track1 = key.Track1;
            ctx.Track2 = key.Track2;
            ctx.Track3 = key.Track3;
            ctx.Time = key.Time;
            ctx.Frame = key.Frame;
            ctx.TotalFrame = key.Length;
            ctx.TotalTime = key.Fps > 0 ? key.Length / (double)key.Fps : 0d;
            ctx.Framerate = key.Fps;
            ctx.TimelineFrame = key.TimelineFrame;
            ctx.TimelineTime = key.TimelineTime;
            ctx.SceneWidth = key.SceneWidth;
            ctx.SceneHeight = key.SceneHeight;
            ctx.Layer = key.Layer;
            ctx.Index = key.InputIndex;
            ctx.Num = key.InputCount;
            ctx.GroupIndex = key.GroupIndex;
            ctx.GroupCount = key.GroupCount;
            ctx.TimelineTotalFrame = key.TimelineTotalFrame;
            ctx.TimelineTotalTime = key.TimelineTotalTime;
            ctx.IsSaving = key.Usage == TimelineSourceUsage.Exporting;
            ctx.IsPlaying = key.Usage == TimelineSourceUsage.Playing;
            ctx.IsPaused = key.Usage == TimelineSourceUsage.Paused;
            ctx.SceneId = ResolveSceneId(key.SceneId);
            ctx.TimeRatio = key.Length > 0 ? key.Frame / (double)key.Length : 0d;
        }

        private static DrawDescription BuildOutputDesc(DrawDescription inDesc, AviUtlScriptContext ctx)
        {
            return inDesc with
            {
                Draw = new Vector3((float)ctx.X, (float)ctx.Y, (float)ctx.Z),
                CenterPoint = new Vector2((float)ctx.Ox, (float)ctx.Oy),
                Zoom = new Vector2((float)ctx.Sx, (float)ctx.Sy),
                Opacity = Math.Clamp(ctx.Alpha / 255d, 0d, 1d),
                Rotation = new Vector3((float)ctx.Rx, (float)ctx.Ry, (float)ctx.Rz),
            };
        }

        private void WarnNativeUnavailableOnce()
        {
            if (_nativeWarned) return;
            _nativeWarned = true;
            Log.Default.Write($"LuaScript: native runtime not found at '{NativeDirectory}'. Falling back to MoonSharp (slow). Deploy the 'native' folder next to the plugin.");
        }

        private bool ExecuteNative(string script, AviUtlScriptContext ctx, RawRectF bounds, int imgW, int imgH)
        {
            _nativeWorker ??= new LuaJitWorker(NativeDirectory);
            _nativeFields ??= new double[NativeProtocol.FieldCount];

            NativeFieldMap.ToFields(ctx, _nativeFields);
            byte[] buffer = LoadInputPixels(bounds, imgW, imgH);

            bool ok = _nativeWorker.Execute(
                script, _nativeFields, buffer, imgW, imgH, NativeTimeoutMilliseconds,
                (tag, frame) => ctx.ResolveObject(tag, frame, out var info) ? info : null,
                out bool dirty, out string? error);

            if (!ok)
            {
                if (error is not null && error.Contains("timed out", StringComparison.Ordinal))
                    throw new LuaScriptTimeoutException(error);
                throw new LuaScriptRuntimeException(error ?? "Native script execution failed.");
            }

            NativeFieldMap.FromFields(_nativeFields, ctx);

            if (dirty)
            {
                EnsureBitmaps(imgW, imgH);
                WritePixelsToOutput(buffer, imgW);
                UpdateTransformEffect(bounds);
                effectOutput = _transformEffect!.Output;
                return true;
            }

            effectOutput = null;
            return false;
        }

        private byte[] LoadInputPixels(RawRectF bounds, int width, int height)
        {
            _pixelLoaderSemaphore.Wait();
            try
            {
                EnsureBitmaps(width, height);

                var dc = _ownCtx!.DeviceContext;
                var savedTarget = dc.Target;

                dc.Target = _renderTarget;
                dc.BeginDraw();
                dc.Clear(null);
                dc.DrawImage(input!, new Vector2(-bounds.Left, -bounds.Top));
                dc.EndDraw();
                dc.Target = savedTarget;

                _stagingBitmap!.CopyFromBitmap(_renderTarget!);

                var mapped = _stagingBitmap.Map(MapOptions.Read);
                try
                {
                    if (mapped.Pitch == width * 4)
                    {
                        Marshal.Copy(mapped.Bits, _pixelBuffer!, 0, width * height * 4);
                    }
                    else
                    {
                        for (int row = 0; row < height; row++)
                            Marshal.Copy(
                                mapped.Bits + mapped.Pitch * row,
                                _pixelBuffer!,
                                row * width * 4,
                                width * 4);
                    }
                }
                finally
                {
                    _stagingBitmap.Unmap();
                }

                return _pixelBuffer!;
            }
            finally
            {
                _pixelLoaderSemaphore.Release();
            }
        }

        private unsafe void WritePixelsToOutput(byte[] pixels, int width)
        {
            fixed (byte* ptr = pixels)
                _outputBitmap!.CopyFromMemory(new nint(ptr), width * 4);
        }

        private void UpdateTransformEffect(RawRectF bounds)
        {
            if (_transformEffect is null)
            {
                _transformEffect = new AffineTransform2D(_ownCtx!.DeviceContext);
                _transformEffect.SetInput(0, _outputBitmap, true);
                _cachedBounds = default;
            }

            if (_cachedBounds.Left != bounds.Left || _cachedBounds.Top != bounds.Top)
            {
                _transformEffect.TransformMatrix = Matrix3x2.CreateTranslation(bounds.Left, bounds.Top);
                _cachedBounds = bounds;
            }
        }

        private void EnsureBitmaps(int width, int height)
        {
            if (_bitmapWidth == width && _bitmapHeight == height) return;

            _renderTarget?.Dispose();
            _stagingBitmap?.Dispose();
            _outputBitmap?.Dispose();
            _transformEffect?.Dispose();
            _renderTarget = null;
            _stagingBitmap = null;
            _outputBitmap = null;
            _transformEffect = null;
            _bitmapWidth = 0;
            _bitmapHeight = 0;

            var dc = _ownCtx!.DeviceContext;

            _renderTarget = dc.CreateEmptyBitmap(width, height, BitmapOptions.Target);

            var stagingProps = new BitmapProperties1(
                new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                96f, 96f,
                BitmapOptions.CpuRead | BitmapOptions.CannotDraw);
            _stagingBitmap = dc.CreateBitmap(
                new SizeI(width, height),
                nint.Zero,
                width * 4,
                stagingProps);

            _outputBitmap = dc.CreateEmptyBitmap(width, height, BitmapOptions.Target);
            _pixelBuffer = new byte[width * height * 4];

            _bitmapWidth = width;
            _bitmapHeight = height;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _engine.Dispose();
                _nativeWorker?.Dispose();
                _pixelLoaderSemaphore.Dispose();
                _renderTarget?.Dispose();
                _stagingBitmap?.Dispose();
                _outputBitmap?.Dispose();
                _transformEffect?.Dispose();
                _renderTarget = null;
                _stagingBitmap = null;
                _outputBitmap = null;
                _transformEffect = null;
            }
            base.Dispose(disposing);
        }
    }
}
