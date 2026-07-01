using System.Collections.Immutable;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using LuaScript.Anchor;
using LuaScript.Compat;
using LuaScript.Diagnostics;
using LuaScript.Engine;
using LuaScript.Engine.Kernel;
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
            double Slider0,
            double Slider1,
            double Slider2,
            double Slider3,
            bool Check0,
            bool Check1,
            bool Check2,
            bool Check3,
            bool HasColor,
            int Color,
            string Script,
            int StringsVersion,
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
            int AnchorVersion,
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
        private bool _gpuWarned;

        private string _kernelSource = string.Empty;
        private bool _kernelResolved;
        private KernelProgram? _kernelProgram;
        private CpuKernel? _cpuKernel;
        private GpuKernel? _gpuKernel;
        private bool _gpuKernelChecked;
        private double[]? _kernelUniforms;

        private EffectDescription? _frameDesc;
        private SceneObjectResolver? _frameResolver;
        private bool _frameResolverBuilt;

        private Guid _lastSceneGuid;
        private string _lastSceneId = string.Empty;
        private bool _sceneIdCached;

        private GraphicsDevicesAndContext? _ownCtx;

        private PixelBufferManager? _pixelManager;

        private RawRectF _pixelBounds;
        private int _pixelWidth;
        private int _pixelHeight;
        private Func<byte[]>? _pixelLoader;

        private string _directiveSource = string.Empty;
        private bool _directiveCached;
        private ScriptEngineKind _autoKind;
        private bool _hasExplicitDirective;
        private ScriptEngineKind _explicitDirectiveKind;

        private byte[]? _nativeInputPixels;
        private Func<byte[]>? _nativeLoadInput;
        private Func<string, int, SceneObjectInfo?>? _nativeResolveObject;
        private Func<string, int, double, double, double, (byte[] buffer, int w, int h)>? _nativeLoadFigure;
        private Func<string, string, double, bool, bool, int, (byte[] buffer, int w, int h)>? _nativeLoadText;
        private Func<string, (byte[] buffer, int w, int h)>? _nativeLoadImage;
        private Func<string, double, (byte[] buffer, int w, int h)>? _nativeLoadMovie;
        private Action<string, IReadOnlyList<KeyValuePair<string, object>>>? _nativeAddEffect;
        private Action<DrawCommand>? _nativeAddDraw;
        private Action<string, int, bool, int, double[]>? _nativeSetAnchor;

        private bool _isFirst = true;
        private string _sourceScript = string.Empty;
        private string _runnableScript = string.Empty;
        private int[] _runnableLineMap = [];
        private bool _runnableCached;
        private ID2D1Image? _cachedInput;
        private RenderKey _cachedKey;
        private SceneObjectQuery[] _cachedQueries = [];
        private DrawDescription? _cachedOutputDesc;
        private ID2D1Image? _cachedEffectOutput;

        private VideoEffectChain? _effectChain;
        private DrawCompositor? _drawCompositor;
        private TextRenderer? _nativeTextRenderer;
        private ImageDecoder? _nativeImageDecoder;
        private MovieDecoder? _nativeMovieDecoder;

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

            var (code, lineMap) = AviUtlScript.TransformWithMap(source);
            _runnableScript = code;
            _runnableLineMap = lineMap;
            _sourceScript = source;
            _runnableCached = true;
            return _runnableScript;
        }

        private static LuaScriptDiagnosticKind ClassifyDiagnostic(LuaScriptException exception) => exception switch
        {
            LuaScriptCompilationException => LuaScriptDiagnosticKind.Compile,
            LuaScriptTimeoutException => LuaScriptDiagnosticKind.Timeout,
            _ => LuaScriptDiagnosticKind.Runtime,
        };

        private LuaScriptDiagnostic CreateDiagnostic(LuaScriptDiagnosticKind kind, string message)
        {
            var diagnostic = LuaScriptDiagnosticParser.Parse(kind, message);
            return diagnostic.Line > 0
                ? diagnostic with { Line = MapRunnableLine(diagnostic.Line) }
                : diagnostic;
        }

        private int MapRunnableLine(int runnableLine)
        {
            int index = runnableLine - 1;
            if (index < 0 || index >= _runnableLineMap.Length)
                return runnableLine;

            int sourceLine = _runnableLineMap[index];
            return sourceLine >= 0 ? sourceLine + 1 : 0;
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
            double s0 = item.Slider0, s1 = item.Slider1, s2 = item.Slider2, s3 = item.Slider3;
            bool hasColor = item.IsColorVisible;
            int colorRgb = hasColor ? (item.Color.R << 16) | (item.Color.G << 8) | item.Color.B : 0;

            var inDesc = desc.DrawDescription;

            var key = new RenderKey(
                frame, time, length, fps,
                t0, t1, t2, t3,
                s0, s1, s2, s3,
                c0, c1, c2, c3, hasColor, colorRgb,
                script, RefreshStringParameters(),
                desc.Usage, desc.SceneId,
                desc.TimelinePosition.Frame,
                desc.TimelinePosition.Time.TotalSeconds,
                desc.ScreenSize.Width, desc.ScreenSize.Height,
                desc.Layer, desc.InputIndex, desc.InputCount,
                desc.GroupIndex, desc.GroupCount,
                desc.TimelineDuration.Frame,
                desc.TimelineDuration.Time.TotalSeconds,
                item.AnchorVersion,
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
            _pixelBounds = bounds;
            _pixelWidth = imgW;
            _pixelHeight = imgH;
            ctx.SetPixelLoader(_pixelLoader ??= LoadPendingInputPixels);

            DrawDescription outDesc = inDesc;
            IReadOnlyList<LuaScriptDiagnostic> diagnostics = [];

            try
            {
                string runnable = GetRunnableScript(script);
                EnsureKernel(runnable);
                EnsureDirective(runnable);

                if (ExecuteKernelLane(ctx, bounds, imgW, imgH))
                {
                    outDesc = inDesc;
                }
                else
                {
                    bool wantNative = _autoKind == ScriptEngineKind.Native;
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

                    if (ctx.DrawCommands.Count > 0)
                    {
                        ctx.EnsurePixelBuffer();
                        var drawBuffer = ctx.GetPixelBuffer();
                        if (drawBuffer is not null)
                        {
                            _drawCompositor ??= new DrawCompositor(_ownCtx);
                            effectOutput = _drawCompositor.Compose(drawBuffer, ctx.ImageWidth, ctx.ImageHeight, ctx.DrawCommands);
                        }
                    }

                    ApplyDrawState(ctx, bounds);

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

                    outDesc = AddAnchorControllers(outDesc, ctx);
                }
            }
            catch (LuaScriptTimeoutException ex)
            {
                effectOutput = null;
                _context = new AviUtlScriptContext { ResolverProvider = GetFrameResolver };
                _isFirst = true;
                Log.Default.Write(ex.Message, ex);
                LuaScriptDiagnostics.Instance.Report(script, [CreateDiagnostic(LuaScriptDiagnosticKind.Timeout, ex.Message)]);
                return inDesc;
            }
            catch (LuaScriptException ex)
            {
                effectOutput = null;
                outDesc = inDesc;
                Log.Default.Write(ex.Message, ex);
                diagnostics = [CreateDiagnostic(ClassifyDiagnostic(ex), ex.Message)];
            }
            finally
            {
                _pixelLoaderSemaphore.Wait();
                _pixelLoaderSemaphore.Release();
            }

            LuaScriptDiagnostics.Instance.Report(script, diagnostics);

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
            var target = AviUtlCompatMap.ResolveTarget(item.Script);
            return _effectChain.Apply(source, requests, desc, target, ref drawDescription);
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

        private static readonly string[] s_stringParameterNames =
        [
            "text", "font", "dir",
            "file_video", "file_audio", "file_image", "file_project",
            "file_mp4", "file_exo", "file_subtitle", "file_shader",
        ];

        private readonly string?[] _stringParameterValues = new string?[s_stringParameterNames.Length];
        private int _stringParametersVersion;

        private int RefreshStringParameters()
        {
            var values = _stringParameterValues;
            bool changed = false;
            changed |= UpdateStringSlot(values, 0, item.Text);
            changed |= UpdateStringSlot(values, 1, item.Font);
            changed |= UpdateStringSlot(values, 2, item.Directory);
            changed |= UpdateStringSlot(values, 3, item.FileVideo);
            changed |= UpdateStringSlot(values, 4, item.FileAudio);
            changed |= UpdateStringSlot(values, 5, item.FileImage);
            changed |= UpdateStringSlot(values, 6, item.FileProject);
            changed |= UpdateStringSlot(values, 7, item.FileMp4);
            changed |= UpdateStringSlot(values, 8, item.FileExo);
            changed |= UpdateStringSlot(values, 9, item.FileSubtitle);
            changed |= UpdateStringSlot(values, 10, item.FileShader);
            if (changed)
                _stringParametersVersion++;
            return _stringParametersVersion;
        }

        private static bool UpdateStringSlot(string?[] values, int index, string value)
        {
            if (ReferenceEquals(values[index], value))
                return false;
            values[index] = value;
            return true;
        }

        private void PopulateStringParameters(AviUtlScriptContext ctx)
        {
            var names = s_stringParameterNames;
            var values = _stringParameterValues;
            for (int i = 0; i < names.Length; i++)
                ctx.SetStringParameter(names[i], values[i]);
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
            ctx.ClearDraws();
            ctx.ClearAnchorRequests();
            ctx.AnchorSource = item.Anchors;
            ctx.DrawStateOverride = null;
            ctx.ResetDrawTarget();

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
            ctx.Slider0 = key.Slider0;
            ctx.Slider1 = key.Slider1;
            ctx.Slider2 = key.Slider2;
            ctx.Slider3 = key.Slider3;
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
            PopulateStringParameters(ctx);
        }

        private static double ClampTrack(double value, AviUtlParameterLayout layout, int index)
        {
            return layout.GetTrack(index) is { } p ? Math.Clamp(value, p.Min, p.Max) : value;
        }

        private ImmutableList<VideoEffectController> _anchorControllers = ImmutableList<VideoEffectController>.Empty;
        private AnchorRequestData[] _anchorSignature = [];
        private IReadOnlyList<LuaAnchorPoint>? _anchorSignatureSource;

        private ImmutableList<VideoEffectController> ResolveAnchorControllers(AviUtlScriptContext ctx)
        {
            var requests = ctx.AnchorRequests;
            if (requests.Count == 0)
            {
                _anchorControllers = ImmutableList<VideoEffectController>.Empty;
                _anchorSignature = [];
                _anchorSignatureSource = null;
                return _anchorControllers;
            }

            if (ReferenceEquals(_anchorSignatureSource, ctx.AnchorSource) && AnchorSignatureMatches(requests))
                return _anchorControllers;

            _anchorControllers = BuildAnchorControllers(ctx.AnchorSource, requests);
            _anchorSignature = new AnchorRequestData[requests.Count];
            for (int i = 0; i < requests.Count; i++)
                _anchorSignature[i] = requests[i];
            _anchorSignatureSource = ctx.AnchorSource;
            return _anchorControllers;
        }

        private bool AnchorSignatureMatches(IReadOnlyList<AnchorRequestData> requests)
        {
            if (_anchorSignature.Length != requests.Count)
                return false;
            for (int i = 0; i < requests.Count; i++)
                if (!_anchorSignature[i].Equals(requests[i]))
                    return false;
            return true;
        }

        private DrawDescription AddAnchorControllers(DrawDescription desc, AviUtlScriptContext ctx)
        {
            var controllers = ResolveAnchorControllers(ctx);
            return controllers.Count == 0 ? desc : desc with { Controllers = desc.Controllers.AddRange(controllers) };
        }

        private ImmutableList<VideoEffectController> BuildAnchorControllers(
            IReadOnlyList<LuaAnchorPoint>? source, IReadOnlyList<AnchorRequestData> requests)
        {
            var builder = ImmutableList.CreateBuilder<VideoEffectController>();
            foreach (var request in requests)
            {
                if (request.Count <= 0)
                    continue;

                AddConnectionControllers(builder, source, request);

                var group = request.Group;
                bool is3D = request.Is3D;
                for (int i = 0; i < request.Count; i++)
                {
                    AnchorSupport.ResolvePosition(source, group, i, out double x, out double y, out double z);
                    int index = i;
                    var point = new ControllerPoint(
                        new Vector3((float)x, (float)y, (float)(is3D ? z : 0d)),
                        arg => item.ApplyAnchorDrag(group, index, arg.Delta.X, arg.Delta.Y, is3D ? arg.Delta.Z : 0d))
                    {
                        Shape = VideoControllerPointShape.Circle,
                    };
                    builder.Add(new VideoEffectController(item, [point]));
                }
            }
            return builder.ToImmutable();
        }

        private void AddConnectionControllers(
            ImmutableList<VideoEffectController>.Builder builder, IReadOnlyList<LuaAnchorPoint>? source, in AnchorRequestData request)
        {
            switch (request.Connection)
            {
                case AnchorConnection.Line:
                    for (int i = 0; i + 1 < request.Count; i++)
                        AddEdge(builder, ConnectionPoint(source, request, i), ConnectionPoint(source, request, i + 1));
                    break;
                case AnchorConnection.Loop:
                    for (int i = 0; i + 1 < request.Count; i++)
                        AddEdge(builder, ConnectionPoint(source, request, i), ConnectionPoint(source, request, i + 1));
                    if (request.Count >= 2)
                        AddEdge(builder, ConnectionPoint(source, request, request.Count - 1), ConnectionPoint(source, request, 0));
                    break;
                case AnchorConnection.Star:
                    for (int i = 0; i < request.Count; i++)
                        AddEdge(builder, CenterPoint(), ConnectionPoint(source, request, i));
                    break;
                case AnchorConnection.Arm:
                    if (request.Count >= 1)
                        AddEdge(builder, CenterPoint(), ConnectionPoint(source, request, 0));
                    for (int i = 0; i + 1 < request.Count; i++)
                        AddEdge(builder, ConnectionPoint(source, request, i), ConnectionPoint(source, request, i + 1));
                    break;
            }
        }

        private void AddEdge(ImmutableList<VideoEffectController>.Builder builder, ControllerPoint a, ControllerPoint b) =>
            builder.Add(new VideoEffectController(item, [a, b]) { Connection = VideoControllerPointConnection.Line });

        private static ControllerPoint CenterPoint() =>
            new(Vector3.Zero) { Shape = VideoControllerPointShape.None };

        private static ControllerPoint ConnectionPoint(IReadOnlyList<LuaAnchorPoint>? source, in AnchorRequestData request, int index)
        {
            AnchorSupport.ResolvePosition(source, request.Group, index, out double x, out double y, out double z);
            return new ControllerPoint(new Vector3((float)x, (float)y, (float)(request.Is3D ? z : 0d)))
            {
                Shape = VideoControllerPointShape.None,
            };
        }

        private void ApplyDrawState(AviUtlScriptContext ctx, RawRectF bounds)
        {
            if (ctx.DrawStateOverride is not { } drawn)
                return;

            if (!drawn)
            {
                effectOutput = null;
                return;
            }

            if (effectOutput is not null)
                return;

            ctx.EnsurePixelBuffer();
            var buffer = ctx.GetPixelBuffer();
            if (buffer is null)
                return;

            _pixelManager!.WritePixelsToOutput(buffer, ctx.ImageWidth, ctx.ImageHeight);
            effectOutput = _pixelManager.GetTransformOutput(bounds.Left, bounds.Top);
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

        private void EnsureKernel(string runnable)
        {
            if (_kernelResolved && string.Equals(runnable, _kernelSource, StringComparison.Ordinal))
                return;

            _kernelSource = runnable;
            _kernelResolved = true;
            _kernelProgram = KernelExtractor.TryExtract(runnable);
            _cpuKernel = _kernelProgram is null ? null : CpuKernelCompiler.Compile(_kernelProgram);
            _gpuKernel?.Dispose();
            _gpuKernel = null;
            _gpuKernelChecked = false;
        }

        private void EnsureDirective(string runnable)
        {
            if (_directiveCached && string.Equals(runnable, _directiveSource, StringComparison.Ordinal))
                return;

            _directiveSource = runnable;
            _directiveCached = true;
            _hasExplicitDirective = ScriptDirective.TryResolveExplicit(runnable, out _explicitDirectiveKind);
            _autoKind = ScriptDirective.ResolveAuto(runnable);
        }

        private bool ExecuteKernelLane(AviUtlScriptContext ctx, RawRectF bounds, int imgW, int imgH)
        {
            if (_kernelProgram is null || _cpuKernel is null)
                return false;

            bool explicitDirective = _hasExplicitDirective;
            var kind = _explicitDirectiveKind;
            if (explicitDirective && kind is not (ScriptEngineKind.Gpu or ScriptEngineKind.Cpu))
                return false;

            _kernelUniforms ??= KernelUniformBinding.Create();
            KernelUniformBinding.Fill(ctx, _kernelUniforms);

            if (explicitDirective && kind == ScriptEngineKind.Gpu)
            {
                var gpu = GetGpuKernel();
                if (gpu is not null)
                {
                    effectOutput = gpu.Apply(input!, _kernelUniforms);
                    return true;
                }
                WarnGpuUnavailableOnce();
            }

            byte[] buffer = LoadInputPixels(bounds, imgW, imgH);
            _cpuKernel.Execute(buffer, imgW, imgH, _kernelUniforms);
            _pixelManager!.WritePixelsToOutput(buffer, imgW, imgH);
            effectOutput = _pixelManager.GetTransformOutput(bounds.Left, bounds.Top);
            return true;
        }

        private GpuKernel? GetGpuKernel()
        {
            if (_gpuKernelChecked)
                return _gpuKernel;

            _gpuKernelChecked = true;
            if (_kernelProgram is null)
                return null;

            try
            {
                var kernel = new GpuKernel(_ownCtx!, _kernelProgram);
                if (kernel.IsReady)
                    _gpuKernel = kernel;
                else
                    kernel.Dispose();
            }
            catch (Exception ex)
            {
                Log.Default.Write("LuaScript: GPU kernel initialization failed; falling back to CPU.", ex);
            }
            return _gpuKernel;
        }

        private void WarnGpuUnavailableOnce()
        {
            if (_gpuWarned) return;
            _gpuWarned = true;
            Log.Default.Write("LuaScript: GPU kernel unavailable; using the CPU kernel lane instead.");
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
            _nativeLoadInput ??= () => _nativeInputPixels ??= LoadPendingInputPixels();
            _nativeResolveObject ??= (tag, frame) => _context.ResolveObject(tag, frame, out var info) ? info : null;
            _nativeLoadFigure ??= NativeLoadFigure;
            _nativeLoadText ??= NativeLoadText;
            _nativeLoadImage ??= NativeLoadImage;
            _nativeLoadMovie ??= NativeLoadMovie;
            _nativeAddEffect ??= (name, args) => _context.AddEffect(new AviUtlEffectRequest(name, args));
            _nativeAddDraw ??= command => _context.AddDraw(command);
            _nativeSetAnchor ??= ResolveNativeAnchor;

            NativeFieldMap.ToFields(ctx, _nativeFields);
            _nativeInputPixels = null;

            bool ok = _nativeWorker.Execute(
                script, _nativeFields, ctx.StringParameters, _nativeLoadInput, imgW, imgH, NativeTimeoutMilliseconds,
                _nativeResolveObject,
                _nativeLoadFigure,
                _nativeLoadText,
                _nativeLoadImage,
                _nativeLoadMovie,
                _nativeAddEffect,
                _nativeAddDraw,
                _nativeSetAnchor,
                out bool dirty, out bool bufferReplaced, out byte[]? resultPixels,
                out int resultW, out int resultH, out string? error);

            if (!ok)
            {
                if (error is not null && error.Contains("timed out", StringComparison.Ordinal))
                    throw new LuaScriptTimeoutException(error);
                throw new LuaScriptRuntimeException(error ?? "Native script execution failed.");
            }

            NativeFieldMap.FromFields(_nativeFields, ctx);

            bool hasDraws = ctx.DrawCommands.Count > 0;

            if (dirty && resultPixels is not null)
            {
                int outW = bufferReplaced ? resultW : imgW;
                int outH = bufferReplaced ? resultH : imgH;

                if (bufferReplaced)
                    ctx.ReplaceBuffer(resultPixels, outW, outH);

                if (hasDraws)
                {
                    ctx.SetResolvedBuffer(resultPixels, outW, outH);
                    return true;
                }

                _pixelManager!.WritePixelsToOutput(resultPixels, outW, outH);
                if (bufferReplaced)
                    effectOutput = _pixelManager.GetTransformOutput(-outW / 2f, -outH / 2f);
                else
                    effectOutput = _pixelManager.GetTransformOutput(bounds.Left, bounds.Top);
                return true;
            }

            if (hasDraws)
            {
                ctx.SetResolvedBuffer(_nativeLoadInput(), imgW, imgH);
                return false;
            }

            effectOutput = null;
            return false;
        }

        private void ResolveNativeAnchor(string group, int count, bool is3D, int connection, double[] positions)
        {
            _context.AddAnchorRequest(new AnchorRequestData(group, count, (AnchorConnection)connection, is3D));

            int stride = is3D ? 3 : 2;
            var source = _context.AnchorSource;
            for (int i = 0; i < count; i++)
            {
                AnchorSupport.ResolvePosition(source, group, i, out double x, out double y, out double z);
                positions[i * stride + 0] = x;
                positions[i * stride + 1] = y;
                if (is3D)
                    positions[i * stride + 2] = z;
            }
        }

        private (byte[] buffer, int w, int h) NativeLoadText(string family, string text, double size, bool bold, bool italic, int color)
        {
            _nativeTextRenderer ??= new TextRenderer();
            var buffer = _nativeTextRenderer.Render(text, family, size, bold, italic, color, out int w, out int h);
            return (buffer, w, h);
        }

        private (byte[] buffer, int w, int h) NativeLoadImage(string path)
        {
            _nativeImageDecoder ??= new ImageDecoder();
            var buffer = _nativeImageDecoder.Decode(path, out int w, out int h);
            return (buffer, w, h);
        }

        private (byte[] buffer, int w, int h) NativeLoadMovie(string path, double time)
        {
            _nativeMovieDecoder ??= new MovieDecoder();
            var buffer = _nativeMovieDecoder.Decode(path, time, out int w, out int h);
            return (buffer, w, h);
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

        private byte[] LoadPendingInputPixels() => LoadInputPixels(_pixelBounds, _pixelWidth, _pixelHeight);

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
                _gpuKernel?.Dispose();
                _nativeWorker?.Dispose();
                _effectChain?.Dispose();
                _drawCompositor?.Dispose();
                _nativeTextRenderer?.Dispose();
                _nativeImageDecoder?.Dispose();
                _pixelLoaderSemaphore.Dispose();
                _pixelManager?.Dispose();
                _pixelManager = null;
            }
            base.Dispose(disposing);
        }
    }
}
