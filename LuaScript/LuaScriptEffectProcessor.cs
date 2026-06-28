using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using LuaScript.Compat;
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
            bool Check0,
            bool Check1,
            bool Check2,
            bool Check3,
            bool HasColor,
            int Color,
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
        private bool _effectChainWarned;

        private EffectDescription? _frameDesc;
        private SceneObjectResolver? _frameResolver;
        private bool _frameResolverBuilt;

        private Guid _lastSceneGuid;
        private string _lastSceneId = string.Empty;
        private bool _sceneIdCached;

        private GraphicsDevicesAndContext? _ownCtx;

        private PixelBufferManager? _pixelManager;

        private bool _isFirst = true;
        private string _sourceScript = string.Empty;
        private string _runnableScript = string.Empty;
        private bool _runnableCached;
        private ID2D1Image? _cachedInput;
        private RenderKey _cachedKey;
        private SceneObjectQuery[] _cachedQueries = [];
        private DrawDescription? _cachedOutputDesc;
        private ID2D1Image? _cachedEffectOutput;

        private VideoEffectChain? _effectChain;

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            _ownCtx = new GraphicsDevicesAndContext(devices);
            disposer.Collect(_ownCtx);
            _pixelManager = new PixelBufferManager(_ownCtx);
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

        private string GetRunnableScript(string source)
        {
            if (_runnableCached && string.Equals(source, _sourceScript, StringComparison.Ordinal))
                return _runnableScript;

            _runnableScript = AviUtlScript.Transform(source);
            _sourceScript = source;
            _runnableCached = true;
            return _runnableScript;
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

            var layout = item.Layout;
            var t0 = ClampTrack(item.Track0.GetValue(frame, length, fps), layout, 0);
            var t1 = ClampTrack(item.Track1.GetValue(frame, length, fps), layout, 1);
            var t2 = ClampTrack(item.Track2.GetValue(frame, length, fps), layout, 2);
            var t3 = ClampTrack(item.Track3.GetValue(frame, length, fps), layout, 3);

            bool c0 = item.Check0, c1 = item.Check1, c2 = item.Check2, c3 = item.Check3;
            bool hasColor = layout.HasColor;
            int colorRgb = hasColor ? (item.Color.R << 16) | (item.Color.G << 8) | item.Color.B : 0;

            var inDesc = desc.DrawDescription;

            var key = new RenderKey(
                frame, time, length, fps,
                t0, t1, t2, t3,
                c0, c1, c2, c3, hasColor, colorRgb,
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
                effectOutput = _cachedEffectOutput;
                return _cachedOutputDesc ?? inDesc;
            }

            var bounds = _ownCtx.DeviceContext.GetImageLocalBounds(input);
            int imgW = Math.Max(1, (int)Math.Ceiling(bounds.Right - bounds.Left));
            int imgH = Math.Max(1, (int)Math.Ceiling(bounds.Bottom - bounds.Top));

            var ctx = _context;
            PopulateContext(ctx, in key, imgW, imgH);
            ctx.SetPixelLoader(() => LoadInputPixels(bounds, imgW, imgH));

            DrawDescription outDesc = inDesc;

            try
            {
                string runnable = GetRunnableScript(script);
                bool wantNative = ScriptDirective.ResolveAuto(runnable) == ScriptEngineKind.Native;
                bool nativeReady = wantNative && LuaJitWorker.IsAvailable(NativeDirectory);
                if (wantNative && !nativeReady)
                    WarnNativeUnavailableOnce();

                if (nativeReady)
                {
                    ExecuteNative(runnable, ctx, bounds, imgW, imgH);
                }
                else
                {
                    _engine.Execute(runnable, ctx);

                    if (ctx.IsPixelsDirty)
                    {
                        int bufW = ctx.ImageWidth;
                        int bufH = ctx.ImageHeight;
                        _pixelManager!.WritePixelsToOutput(ctx.GetPixelBuffer()!, bufW, bufH);
                        if (ctx.BufferReplaced)
                            effectOutput = _pixelManager.GetTransformOutput(-bufW / 2f, -bufH / 2f);
                        else
                            effectOutput = _pixelManager.GetTransformOutput(bounds.Left, bounds.Top);
                    }
                    else
                    {
                        effectOutput = null;
                    }
                }

                outDesc = BuildOutputDesc(inDesc, ctx);

                if (ctx.EffectRequests.Count > 0)
                {
                    var source = effectOutput ?? input;
                    try
                    {
                        effectOutput = ApplyEffectChain(source, ctx.EffectRequests, desc, ref outDesc);
                    }
                    catch (Exception ex)
                    {
                        effectOutput = source;
                        WarnEffectChainFailureOnce(ex);
                    }
                }
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
            _cachedEffectOutput = effectOutput;

            return outDesc;
        }

        private ID2D1Image ApplyEffectChain(ID2D1Image source, IReadOnlyList<AviUtlEffectRequest> requests, EffectDescription desc, ref DrawDescription drawDescription)
        {
            _effectChain ??= new VideoEffectChain(_ownCtx!);
            return _effectChain.Apply(source, requests, desc, ref drawDescription);
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
            ctx.ClearEffects();

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
            ctx.Check0 = key.Check0;
            ctx.Check1 = key.Check1;
            ctx.Check2 = key.Check2;
            ctx.Check3 = key.Check3;
            ctx.HasColor = key.HasColor;
            ctx.ColorValue = key.Color;
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

        private static double ClampTrack(double value, AviUtlParameterLayout layout, int index)
        {
            return layout.GetTrack(index) is { } p ? Math.Clamp(value, p.Min, p.Max) : value;
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

        private void WarnEffectChainFailureOnce(Exception ex)
        {
            if (_effectChainWarned) return;
            _effectChainWarned = true;
            Log.Default.Write("LuaScript: obj.effect chain failed and was skipped for this frame. The source image is passed through unchanged.", ex);
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
                NativeLoadFigure,
                (name, args) => ctx.AddEffect(new AviUtlEffectRequest(name, args)),
                out bool dirty, out bool bufferReplaced, out byte[]? newPixels,
                out int resultW, out int resultH, out string? error);

            if (!ok)
            {
                if (error is not null && error.Contains("timed out", StringComparison.Ordinal))
                    throw new LuaScriptTimeoutException(error);
                throw new LuaScriptRuntimeException(error ?? "Native script execution failed.");
            }

            NativeFieldMap.FromFields(_nativeFields, ctx);

            if (dirty)
            {
                int outW = bufferReplaced ? resultW : imgW;
                int outH = bufferReplaced ? resultH : imgH;
                byte[] outPixels = buffer;

                if (bufferReplaced && newPixels != null)
                {
                    outPixels = newPixels;
                    ctx.ReplaceBuffer(outPixels, outW, outH);
                }

                _pixelManager!.WritePixelsToOutput(outPixels, outW, outH);
                if (bufferReplaced)
                    effectOutput = _pixelManager.GetTransformOutput(-outW / 2f, -outH / 2f);
                else
                    effectOutput = _pixelManager.GetTransformOutput(bounds.Left, bounds.Top);
                return true;
            }

            effectOutput = null;
            return false;
        }

        private static (byte[] buffer, int w, int h) NativeLoadFigure(string name, int color, double size, double lineWidth, double aspect)
        {
            aspect = Math.Clamp(aspect, -1d, 1d);
            int boundingSize = Math.Max(1, (int)Math.Round(size));
            double w = aspect >= 0d ? boundingSize * (1d - aspect) : boundingSize;
            double h = aspect <= 0d ? boundingSize * (1d + aspect) : boundingSize;
            int fw = Math.Max(1, (int)Math.Round(w));
            int fh = Math.Max(1, (int)Math.Round(h));
            var buffer = FigureRenderer.Render(name, fw, fh, color, lineWidth);
            return (buffer, fw, fh);
        }

        private byte[] LoadInputPixels(RawRectF bounds, int width, int height)
        {
            _pixelLoaderSemaphore.Wait();
            try
            {
                return _pixelManager!.LoadInputPixels(input!, bounds, width, height);
            }
            finally
            {
                _pixelLoaderSemaphore.Release();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _engine.Dispose();
                _nativeWorker?.Dispose();
                _effectChain?.Dispose();
                _pixelLoaderSemaphore.Dispose();
                _pixelManager?.Dispose();
                _pixelManager = null;
            }
            base.Dispose(disposing);
        }
    }
}
