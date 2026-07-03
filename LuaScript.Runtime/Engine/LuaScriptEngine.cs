using LuaScript.Anchor;
using LuaScript.Api;
using LuaScript.Compat;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Debugging;
using MoonSharp.Interpreter.Loaders;

namespace LuaScript
{
    internal sealed partial class LuaScriptEngine : IDisposable
    {
        private readonly record struct ExecutionResult(
            LuaScriptException? Exception,
            bool TimedOut);

        [LuaTable("obj")]
        private sealed partial class ExecutionThread : IDisposable
        {
            private sealed class CancellationDebugger : IDebugger
            {
                private static readonly DebuggerAction s_runAction = new() { Action = DebuggerAction.ActionType.Run };

                private CancellationToken _token;

                internal void UpdateToken(CancellationToken token) => _token = token;

                public DebuggerCaps GetDebuggerCaps() => 0;
                public void SetDebugService(DebugService debugService) { }
                public void SetSourceCode(SourceCode sourceCode) { }
                public void SetByteCode(string[] byteCode) { }
                public bool SignalRuntimeException(ScriptRuntimeException ex) => false;
                public void SignalExecutionEnded() { }
                public void Update(WatchType watchType, IEnumerable<WatchItem> items) { }
                public List<DynamicExpression> GetWatchItems() => [];
                public void RefreshBreakpoints(IEnumerable<SourceRef> refs) { }

                public bool IsPauseRequested() => _token.IsCancellationRequested;

                public DebuggerAction GetAction(int ip, SourceRef sourceref)
                {
                    _token.ThrowIfCancellationRequested();
                    return s_runAction;
                }
            }

            private readonly SemaphoreSlim _workSignal = new(0);
            private readonly ManualResetEventSlim _doneSignal = new(false);
            private readonly CancellationTokenSource _cts = new();
            private readonly Thread _thread;
            private readonly CancellationDebugger _debugger = new();

            private string _jobCode = string.Empty;
            private AviUtlScriptContext? _jobContext;
            private LuaScriptException? _jobException;
            private volatile bool _disposeRequested;

            private readonly List<string> _removalBuffer = [];

            private readonly Dictionary<string, DynValue> _options = new(StringComparer.Ordinal);
            private readonly Dictionary<string, DynValue> _pixelOptions = new(StringComparer.Ordinal);

            private readonly Func<IMediaSourceLoader> _mediaLoaderFactory;
            private IMediaSourceLoader? _mediaLoader;
            private readonly AviUtlFontState _fontState = new();

            private Script? _script;
            private DynValue? _compiledChunk;
            private string _lastCompiledCode = string.Empty;
            private Table? _objTable;
            private Table? _sceneTable;
            private Table? _animTable;
            private Table? _ymm4Table;
            private HashSet<string>? _builtinGlobalSnapshot;
            private HashSet<string>? _objTableSnapshot;
            private HashSet<string>? _sceneTableSnapshot;
            private HashSet<string>? _animTableSnapshot;
            private HashSet<string>? _ymm4TableSnapshot;
            private CancellationToken _activeCancellation;
            private AviUtlScriptContext? _activeContext;

            private AviUtlGlobalRegistrar? _globalRegistrar;
            private SceneTableRegistrar? _sceneRegistrar;

            internal ExecutionThread(Func<IMediaSourceLoader> mediaLoaderFactory)
            {
                _mediaLoaderFactory = mediaLoaderFactory;
                _activeCancellation = _cts.Token;
                _debugger.UpdateToken(_cts.Token);
                _thread = new Thread(WorkerLoop)
                {
                    IsBackground = true,
                    Name = "LuaScriptWorker"
                };
                _thread.Start();
            }

            internal ExecutionResult TryExecute(string code, AviUtlScriptContext ctx, int timeoutMs)
            {
                _jobCode = code;
                _jobContext = ctx;
                _jobException = null;
                _doneSignal.Reset();
                _workSignal.Release();

                if (_doneSignal.Wait(timeoutMs))
                    return new ExecutionResult(_jobException, false);

                _cts.Cancel();
                return new ExecutionResult(null, TimedOut: true);
            }

            private void WorkerLoop()
            {
                while (true)
                {
                    _workSignal.Wait();
                    if (_disposeRequested)
                        return;
                    ProcessJob();
                    _doneSignal.Set();
                }
            }

            private void ProcessJob()
            {
                var context = _jobContext!;
                _activeContext = context;

                try
                {
                    EnsureScript();
                    EnsureScriptIntegrity();
                    EnsureCompiled(_jobCode);
                    SetupGlobals(context);
                    _script!.Call(_compiledChunk!);
                    ReadBackGlobals(context);
                    _jobException = null;
                }
                catch (OperationCanceledException) when (_cts.IsCancellationRequested)
                {
                    _jobException = null;
                }
                catch (LuaScriptException ex)
                {
                    _jobException = ex;
                }
                catch (ScriptRuntimeException ex)
                {
                    _jobException = new LuaScriptRuntimeException(ex.DecoratedMessage ?? ex.Message, ex);
                }
                catch (Exception ex)
                {
                    _jobException = new LuaScriptRuntimeException(ex.Message, ex);
                }
                finally
                {
                    _activeContext = null;
                }
            }

            private void EnsureScript()
            {
                if (_script is not null) return;
                var script = new Script(
                    CoreModules.Basic |
                    CoreModules.Math |
                    CoreModules.String |
                    CoreModules.Table |
                    CoreModules.Bit32 |
                    CoreModules.TableIterators |
                    CoreModules.Metatables |
                    CoreModules.ErrorHandling);
                script.Options.ScriptLoader = new DisabledFileScriptLoader();
                script.AttachDebugger(_debugger);
                _globalRegistrar = new AviUtlGlobalRegistrar(CurrentTimeRatio);
                _globalRegistrar.RegisterLuaMembers(script.Globals);
                _script = script;
            }

            private void EnsureScriptIntegrity()
            {
                if (_script is null) return;
                if (_script.Globals.Get("math").Type == DataType.Table) return;
                _script = null;
                _compiledChunk = null;
                _lastCompiledCode = string.Empty;
                _objTable = null;
                _sceneTable = null;
                _animTable = null;
                _ymm4Table = null;
                _builtinGlobalSnapshot = null;
                _objTableSnapshot = null;
                _sceneTableSnapshot = null;
                _animTableSnapshot = null;
                _ymm4TableSnapshot = null;
                EnsureScript();
            }

            private void EnsureCompiled(string code)
            {
                if (_compiledChunk is not null && code == _lastCompiledCode) return;

                DynValue chunk;
                try
                {
                    chunk = _script!.LoadString(code, null, "LuaScript");
                }
                catch (SyntaxErrorException ex)
                {
                    throw new LuaScriptCompilationException(ex.DecoratedMessage ?? ex.Message, ex);
                }

                _compiledChunk = chunk;
                _lastCompiledCode = code;
            }

            private void SetupGlobals(AviUtlScriptContext ctx)
            {
                var script = _script!;
                bool isFirstSetup = _objTable is null;

                _options.Clear();
                _pixelOptions.Clear();
                _fontState.Reset();

                if (isFirstSetup)
                {
                    var sceneTable = new Table(script);
                    RegisterSceneCallbacks(sceneTable);
                    script.Globals["scene"] = sceneTable;

                    var objTable = new Table(script);
                    RegisterLuaMembers(objTable);
                    script.Globals["obj"] = objTable;

                    var animTable = new Table(script);
                    AnimTableRegistrar.RegisterFunctions(animTable);
                    script.Globals["anim"] = animTable;

                    var ymm4Table = new Table(script);
                    script.Globals["ymm4"] = ymm4Table;

                    _sceneTable = sceneTable;
                    _animTable = animTable;
                    _ymm4Table = ymm4Table;
                    _objTable = objTable;
                }
                else
                {
                    ResetUserGlobals(script);

                    if (!ReferenceEquals(script.Globals.Get("scene").Table, _sceneTable))
                    {
                        var sceneTable = new Table(script);
                        RegisterSceneCallbacks(sceneTable);
                        script.Globals["scene"] = sceneTable;
                        _sceneTable = sceneTable;
                        _sceneTableSnapshot = null;
                    }

                    if (!ReferenceEquals(script.Globals.Get("obj").Table, _objTable))
                    {
                        var objTable = new Table(script);
                        RegisterLuaMembers(objTable);
                        script.Globals["obj"] = objTable;
                        _objTable = objTable;
                        _objTableSnapshot = null;
                    }

                    if (!ReferenceEquals(script.Globals.Get("anim").Table, _animTable))
                    {
                        var animTable = new Table(script);
                        AnimTableRegistrar.RegisterFunctions(animTable);
                        script.Globals["anim"] = animTable;
                        _animTable = animTable;
                        _animTableSnapshot = null;
                    }

                    if (!ReferenceEquals(script.Globals.Get("ymm4").Table, _ymm4Table))
                    {
                        var ymm4Table = new Table(script);
                        script.Globals["ymm4"] = ymm4Table;
                        _ymm4Table = ymm4Table;
                        _ymm4TableSnapshot = null;
                    }

                    ResetUserTableKeys(_objTable!, _objTableSnapshot);
                    ResetUserTableKeys(_sceneTable!, _sceneTableSnapshot);
                    ResetUserTableKeys(_animTable!, _animTableSnapshot);
                    ResetUserTableKeys(_ymm4Table!, _ymm4TableSnapshot);
                }

                _globalRegistrar!.UpdateLuaMembers(script.Globals, ctx);
                _sceneRegistrar!.UpdateLuaMembers(_sceneTable!, ctx);
                Ymm4TableRegistrar.UpdateLuaMembers(_ymm4Table!, ctx);
                UpdateLuaMembers(_objTable!, ctx);

                if (isFirstSetup)
                {
                    _builtinGlobalSnapshot = CaptureGlobalKeys(script);
                    _objTableSnapshot = CaptureTableKeys(_objTable!);
                    _sceneTableSnapshot = CaptureTableKeys(_sceneTable!);
                    _animTableSnapshot = CaptureTableKeys(_animTable!);
                    _ymm4TableSnapshot = CaptureTableKeys(_ymm4Table!);
                }

                script.Call(
                    script.Globals.Get("math").Table.Get("randomseed"),
                    DynValue.NewNumber(ctx.Frame));
            }

            private static HashSet<string> CaptureGlobalKeys(Script script)
            {
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var key in script.Globals.Keys)
                {
                    if (key.Type == DataType.String)
                        keys.Add(key.String);
                }
                return keys;
            }

            private static HashSet<string> CaptureTableKeys(Table table)
            {
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var key in table.Keys)
                {
                    if (key.Type == DataType.String)
                        keys.Add(key.String);
                }
                return keys;
            }

            private void ResetUserTableKeys(Table table, HashSet<string>? snapshot)
            {
                if (snapshot is null) return;
                var buffer = _removalBuffer;
                buffer.Clear();
                foreach (var key in table.Keys)
                {
                    if (key.Type == DataType.String &&
                        !snapshot.Contains(key.String))
                    {
                        buffer.Add(key.String);
                    }
                }
                foreach (var key in buffer)
                    table[key] = DynValue.Nil;
            }

            private void ResetUserGlobals(Script script)
            {
                var buffer = _removalBuffer;
                buffer.Clear();
                foreach (var key in script.Globals.Keys)
                {
                    if (key.Type == DataType.String &&
                        !_builtinGlobalSnapshot!.Contains(key.String))
                    {
                        buffer.Add(key.String);
                    }
                }
                foreach (var key in buffer)
                    script.Globals[key] = DynValue.Nil;
            }

            private double CurrentTimeRatio() =>
                _activeContext is { TotalTime: > 0d } context ? context.Time / context.TotalTime : 0d;

            private void RegisterSceneCallbacks(Table scene)
            {
                _sceneRegistrar = new SceneTableRegistrar(GetSceneValue, SetSceneValue);
                _sceneRegistrar.RegisterLuaMembers(scene);
            }

            private SceneValue GetSceneValue(string name)
            {
                _activeCancellation.ThrowIfCancellationRequested();
                return _activeContext?.GetSceneValue(name) ?? SceneValue.Nil;
            }

            private void SetSceneValue(string name, SceneValue value)
            {
                _activeCancellation.ThrowIfCancellationRequested();
                _activeContext?.SetSceneValue(name, value);
            }

            [LuaVariable("w")]
            private int W(AviUtlScriptContext ctx) => ctx.ImageWidth;

            [LuaVariable("h")]
            private int H(AviUtlScriptContext ctx) => ctx.ImageHeight;

            [LuaVariable("hw")]
            private double Hw(AviUtlScriptContext ctx) => ctx.ImageWidth / 2d;

            [LuaVariable("hh")]
            private double Hh(AviUtlScriptContext ctx) => ctx.ImageHeight / 2d;

            [LuaVariable("diagonal")]
            private double Diagonal(AviUtlScriptContext ctx) => Math.Sqrt((double)ctx.ImageWidth * ctx.ImageWidth + (double)ctx.ImageHeight * ctx.ImageHeight);

            [LuaVariable("cx")]
            private double Cx(AviUtlScriptContext ctx) => ctx.ImageWidth / 2d;

            [LuaVariable("cy")]
            private double Cy(AviUtlScriptContext ctx) => ctx.ImageHeight / 2d;

            [LuaVariable("cz")]
            private double Cz(AviUtlScriptContext ctx) => 0d;

            [LuaVariable("x")]
            private double X(AviUtlScriptContext ctx) => ctx.X;

            [LuaVariable("y")]
            private double Y(AviUtlScriptContext ctx) => ctx.Y;

            [LuaVariable("z")]
            private double Z(AviUtlScriptContext ctx) => ctx.Z;

            [LuaVariable("ox")]
            private double Ox(AviUtlScriptContext ctx) => ctx.Ox;

            [LuaVariable("oy")]
            private double Oy(AviUtlScriptContext ctx) => ctx.Oy;

            [LuaVariable("oz")]
            private double Oz(AviUtlScriptContext ctx) => ctx.Oz;

            [LuaVariable("sx")]
            private double Sx(AviUtlScriptContext ctx) => ctx.Sx;

            [LuaVariable("sy")]
            private double Sy(AviUtlScriptContext ctx) => ctx.Sy;

            [LuaVariable("sz", InCatalog = false)]
            private double Sz(AviUtlScriptContext ctx) => 1d;

            [LuaVariable("zoom")]
            private double Zoom(AviUtlScriptContext ctx) => ctx.Zoom;

            [LuaVariable("alpha")]
            private double Alpha(AviUtlScriptContext ctx) => ctx.Alpha;

            [LuaVariable("aspect")]
            private double Aspect(AviUtlScriptContext ctx) => ctx.Aspect;

            [LuaVariable("rx")]
            private double Rx(AviUtlScriptContext ctx) => ctx.Rx;

            [LuaVariable("ry")]
            private double Ry(AviUtlScriptContext ctx) => ctx.Ry;

            [LuaVariable("rz")]
            private double Rz(AviUtlScriptContext ctx) => ctx.Rz;

            [LuaVariable("rxr")]
            private double Rxr(AviUtlScriptContext ctx) => ctx.RxRad;

            [LuaVariable("ryr")]
            private double Ryr(AviUtlScriptContext ctx) => ctx.RyRad;

            [LuaVariable("rzr")]
            private double Rzr(AviUtlScriptContext ctx) => ctx.RzRad;

            [LuaVariable("track0")]
            private double Track0(AviUtlScriptContext ctx) => ctx.Track0;

            [LuaVariable("track1")]
            private double Track1(AviUtlScriptContext ctx) => ctx.Track1;

            [LuaVariable("track2")]
            private double Track2(AviUtlScriptContext ctx) => ctx.Track2;

            [LuaVariable("track3")]
            private double Track3(AviUtlScriptContext ctx) => ctx.Track3;

            [LuaVariable("slider0")]
            private double Slider0(AviUtlScriptContext ctx) => ctx.Slider0;

            [LuaVariable("slider1")]
            private double Slider1(AviUtlScriptContext ctx) => ctx.Slider1;

            [LuaVariable("slider2")]
            private double Slider2(AviUtlScriptContext ctx) => ctx.Slider2;

            [LuaVariable("slider3")]
            private double Slider3(AviUtlScriptContext ctx) => ctx.Slider3;

            [LuaVariable("check0")]
            private bool Check0(AviUtlScriptContext ctx) => ctx.Check0;

            [LuaVariable("check1")]
            private bool Check1(AviUtlScriptContext ctx) => ctx.Check1;

            [LuaVariable("check2")]
            private bool Check2(AviUtlScriptContext ctx) => ctx.Check2;

            [LuaVariable("check3")]
            private bool Check3(AviUtlScriptContext ctx) => ctx.Check3;

            [LuaVariable("text")]
            private string? Text(AviUtlScriptContext ctx) => ctx.StringParameters.TryGetValue("text", out var value) ? value : null;

            [LuaVariable("font")]
            private string? Font(AviUtlScriptContext ctx) => ctx.StringParameters.TryGetValue("font", out var value) ? value : null;

            [LuaVariable("dir")]
            private string? Dir(AviUtlScriptContext ctx) => ctx.StringParameters.TryGetValue("dir", out var value) ? value : null;

            [LuaVariable("file_video")]
            private string? FileVideo(AviUtlScriptContext ctx) => ctx.StringParameters.TryGetValue("file_video", out var value) ? value : null;

            [LuaVariable("file_audio")]
            private string? FileAudio(AviUtlScriptContext ctx) => ctx.StringParameters.TryGetValue("file_audio", out var value) ? value : null;

            [LuaVariable("file_image")]
            private string? FileImage(AviUtlScriptContext ctx) => ctx.StringParameters.TryGetValue("file_image", out var value) ? value : null;

            [LuaVariable("file_project")]
            private string? FileProject(AviUtlScriptContext ctx) => ctx.StringParameters.TryGetValue("file_project", out var value) ? value : null;

            [LuaVariable("file_mp4")]
            private string? FileMp4(AviUtlScriptContext ctx) => ctx.StringParameters.TryGetValue("file_mp4", out var value) ? value : null;

            [LuaVariable("file_exo")]
            private string? FileExo(AviUtlScriptContext ctx) => ctx.StringParameters.TryGetValue("file_exo", out var value) ? value : null;

            [LuaVariable("file_subtitle")]
            private string? FileSubtitle(AviUtlScriptContext ctx) => ctx.StringParameters.TryGetValue("file_subtitle", out var value) ? value : null;

            [LuaVariable("file_shader")]
            private string? FileShader(AviUtlScriptContext ctx) => ctx.StringParameters.TryGetValue("file_shader", out var value) ? value : null;

            [LuaVariable("time")]
            private double Time(AviUtlScriptContext ctx) => ctx.Time;

            [LuaVariable("totaltime")]
            private double Totaltime(AviUtlScriptContext ctx) => ctx.TotalTime;

            [LuaVariable("t")]
            private double T(AviUtlScriptContext ctx) => ctx.TotalFrame > 0 ? ctx.Frame / (double)ctx.TotalFrame : 0d;

            [LuaVariable("frame")]
            private int Frame(AviUtlScriptContext ctx) => ctx.Frame;

            [LuaVariable("totalframe")]
            private int Totalframe(AviUtlScriptContext ctx) => ctx.TotalFrame;

            [LuaVariable("framerate")]
            private int Framerate(AviUtlScriptContext ctx) => ctx.Framerate;

            [LuaVariable("layer")]
            private int Layer(AviUtlScriptContext ctx) => ctx.Layer;

            [LuaVariable("index")]
            private int Index(AviUtlScriptContext ctx) => ctx.Index;

            [LuaVariable("num")]
            private int Num(AviUtlScriptContext ctx) => ctx.Num;
            [LuaFunction("getobject")]
            private DynValue GetObject(ScriptExecutionContext execCtx, CallbackArguments args)
            {
                _activeCancellation.ThrowIfCancellationRequested();
                if (_activeContext is null || args.Count == 0)
                    return DynValue.Nil;
                var tag = args[0];
                if (tag.Type != DataType.String)
                    return DynValue.Nil;
                int frame = _activeContext.TimelineFrame;
                if (args.Count >= 2)
                {
                    var frameArg = args[1];
                    if (frameArg.Type != DataType.Number)
                        return DynValue.Nil;
                    frame = (int)frameArg.Number;
                }
                if (!_activeContext.ResolveObject(tag.String, frame, out var info))
                    return DynValue.Nil;
                return BuildObjectTable(execCtx.GetScript(), info);
            }

            [LuaFunction("getpixel")]
            private DynValue GetPixel(CallbackArguments args)
            {
                _activeCancellation.ThrowIfCancellationRequested();
                if (_activeContext is null) return DynValue.Nil;
                int x = (int)(args[0].CastToNumber() ?? 0d);
                int y = (int)(args[1].CastToNumber() ?? 0d);
                var (r, g, b, a) = _activeContext.GetPixel(x, y);
                return DynValue.NewTuple(
                    DynValue.NewNumber(r),
                    DynValue.NewNumber(g),
                    DynValue.NewNumber(b),
                    DynValue.NewNumber(a));
            }

            [LuaFunction("setpixel")]
            private DynValue SetPixel(CallbackArguments args)
            {
                _activeCancellation.ThrowIfCancellationRequested();
                if (_activeContext is null) return DynValue.Void;
                int x = (int)(args[0].CastToNumber() ?? 0d);
                int y = (int)(args[1].CastToNumber() ?? 0d);
                double r = args[2].CastToNumber() ?? 0d;
                double g = args[3].CastToNumber() ?? 0d;
                double b = args[4].CastToNumber() ?? 0d;
                double a = args.Count > 5 ? args[5].CastToNumber() ?? 255d : 255d;
                _activeContext.SetPixel(x, y, r, g, b, a);
                return DynValue.Void;
            }

            [LuaFunction("getpixeldata")]
            private DynValue GetPixelData()
            {
                _activeCancellation.ThrowIfCancellationRequested();
                if (_activeContext is null) return DynValue.Nil;
                _activeContext.EnsurePixelBuffer();
                return UserData.Create(new PixelDataProxy(_activeContext));
            }

            [LuaFunction("putpixeldata")]
            private DynValue PutPixelData() => DynValue.Void;

            [LuaFunction("rand")]
            private DynValue Rand(CallbackArguments args)
            {
                _activeCancellation.ThrowIfCancellationRequested();
                double frameDefault = _activeContext?.Frame ?? 0d;
                double a = args.Count > 0 ? args[0].CastToNumber() ?? 0d : 0d;
                double b = args.Count > 1 ? args[1].CastToNumber() ?? 0d : 0d;
                double seed = args.Count > 2 ? args[2].CastToNumber() ?? 0d : 0d;
                double frame = args.Count > 3 ? args[3].CastToNumber() ?? frameDefault : frameDefault;
                return DynValue.NewNumber(AviUtlRandom.Next(a, b, seed, frame));
            }

            [LuaFunction("load")]
            private DynValue Load(CallbackArguments args)
            {
                _activeCancellation.ThrowIfCancellationRequested();
                if (_activeContext is null || args.Count == 0 || args[0].Type != DataType.String)
                    return DynValue.Void;

                switch (args[0].String)
                {
                    case "figure" when args.Count >= 4:
                        LoadFigure(
                            args[1].Type == DataType.String ? args[1].String : string.Empty,
                            (int)(args[2].CastToNumber() ?? 0d),
                            args[3].CastToNumber() ?? 0d,
                            args.Count > 4 ? args[4].CastToNumber() ?? 0d : 0d,
                            args.Count > 5 ? Math.Clamp(args[5].CastToNumber() ?? 0d, -1d, 1d) : 0d);
                        break;
                    case "text":
                        LoadText(args.Count > 1 && args[1].Type == DataType.String ? args[1].String : string.Empty);
                        break;
                    case "image" when args.Count > 1 && args[1].Type == DataType.String:
                        LoadImage(args[1].String);
                        break;
                    case "movie" when args.Count > 1 && args[1].Type == DataType.String:
                        LoadMovie(
                            args[1].String,
                            args.Count > 2 ? args[2].CastToNumber() ?? _activeContext.Time : _activeContext.Time);
                        break;
                }
                return DynValue.Void;
            }

            [LuaFunction("setfont")]
            private DynValue SetFont(CallbackArguments args)
            {
                _activeCancellation.ThrowIfCancellationRequested();
                _fontState.Apply(
                    args.Count > 0 ? args[0] : DynValue.Nil,
                    args.Count > 1 ? args[1] : DynValue.Nil,
                    args.Count > 2 ? args[2] : DynValue.Nil,
                    args.Count > 3 ? args[3] : DynValue.Nil);
                return DynValue.Void;
            }

            [LuaFunction("draw")]
            private DynValue Draw(CallbackArguments args)
            {
                _activeCancellation.ThrowIfCancellationRequested();
                if (_activeContext is null)
                    return DynValue.Void;

                double ox = args.Count > 0 ? args[0].CastToNumber() ?? 0d : 0d;
                double oy = args.Count > 1 ? args[1].CastToNumber() ?? 0d : 0d;
                double oz = args.Count > 2 ? args[2].CastToNumber() ?? 0d : 0d;
                double zoom = args.Count > 3 ? args[3].CastToNumber() ?? 1d : 1d;
                double alpha = args.Count > 4 ? args[4].CastToNumber() ?? 1d : 1d;
                double aspect = args.Count > 5 ? args[5].CastToNumber() ?? 0d : 0d;

                _activeContext.SubmitDraw(new DrawCommand(ox, oy, oz, zoom, alpha, aspect, null, CurrentAntialias(), CurrentBlend()));
                return DynValue.Void;
            }

            [LuaFunction("drawpoly")]
            private DynValue DrawPoly(CallbackArguments args)
            {
                _activeCancellation.ThrowIfCancellationRequested();
                if (_activeContext is null || args.Count < 12)
                    return DynValue.Void;

                var poly = new double[DrawPolyMath.Length];
                for (int i = 0; i < 12; i++)
                    poly[i] = args[i].CastToNumber() ?? 0d;

                if (args.Count >= 20)
                {
                    for (int i = 0; i < 8; i++)
                        poly[12 + i] = args[12 + i].CastToNumber() ?? 0d;
                }
                else
                {
                    double w = _activeContext.ImageWidth;
                    double h = _activeContext.ImageHeight;
                    poly[12] = 0d; poly[13] = 0d;
                    poly[14] = w; poly[15] = 0d;
                    poly[16] = w; poly[17] = h;
                    poly[18] = 0d; poly[19] = h;
                }

                poly[20] = args.Count switch
                {
                    13 => args[12].CastToNumber() ?? 1d,
                    >= 21 => args[20].CastToNumber() ?? 1d,
                    _ => 1d,
                };

                _activeContext.SubmitDraw(new DrawCommand(0d, 0d, 0d, 1d, poly[20], 0d, poly, CurrentAntialias(), CurrentBlend()));
                return DynValue.Void;
            }

            [LuaFunction("copybuffer")]
            private DynValue CopyBuffer(CallbackArguments args)
            {
                _activeCancellation.ThrowIfCancellationRequested();
                if (_activeContext is null || args.Count < 2 ||
                    args[0].Type != DataType.String || args[1].Type != DataType.String)
                    return DynValue.Void;

                if (_activeContext.CopyBuffer(args[0].String, args[1].String))
                    RefreshObjDimensions();
                return DynValue.Void;
            }

            [LuaFunction("getvalue")]
            private DynValue GetValue(CallbackArguments args)
            {
                _activeCancellation.ThrowIfCancellationRequested();
                if (args.Count == 0 || args[0].Type != DataType.String)
                    return DynValue.NewNumber(0d);
                var value = _objTable!.Get(args[0].String);
                return value.Type == DataType.Number ? value : DynValue.NewNumber(0d);
            }

            [LuaFunction("setoption")]
            private DynValue SetOption(CallbackArguments args)
            {
                _activeCancellation.ThrowIfCancellationRequested();
                if (args.Count == 0 || args[0].Type != DataType.String)
                    return DynValue.Void;
                var name = args[0].String;
                var value = args.Count > 1 ? args[1] : DynValue.True;
                _options[name] = value;
                if (name == "draw_state" && _activeContext is not null)
                    _activeContext.DrawStateOverride = value.Type == DataType.Boolean
                        ? value.Boolean
                        : (value.CastToNumber() ?? 0d) != 0d;
                if (name == "drawtarget" && _activeContext is not null)
                {
                    if (value.Type == DataType.String && value.String == "tempbuffer")
                    {
                        bool hasSize = args.Count > 3;
                        int w = hasSize ? (int)(args[2].CastToNumber() ?? 0d) : 0;
                        int h = hasSize ? (int)(args[3].CastToNumber() ?? 0d) : 0;
                        _activeContext.SetDrawTarget(true, w, h, hasSize);
                    }
                    else
                    {
                        _activeContext.SetDrawTarget(false, 0, 0, false);
                    }
                }
                return DynValue.Void;
            }

            [LuaFunction("getoption")]
            private DynValue GetOption(CallbackArguments args)
            {
                _activeCancellation.ThrowIfCancellationRequested();
                if (args.Count == 0 || args[0].Type != DataType.String)
                    return DynValue.NewNumber(0d);
                return _options.TryGetValue(args[0].String, out var value) ? value : DynValue.NewNumber(0d);
            }

            [LuaFunction("pixeloption")]
            private DynValue PixelOption(CallbackArguments args)
            {
                _activeCancellation.ThrowIfCancellationRequested();
                if (args.Count == 0 || args[0].Type != DataType.String)
                    return DynValue.Void;
                _pixelOptions[args[0].String] = args.Count > 1 ? args[1] : DynValue.True;
                return DynValue.Void;
            }

            [LuaFunction("setanchor")]
            private DynValue SetAnchor(CallbackArguments args)
            {
                _activeCancellation.ThrowIfCancellationRequested();
                if (_activeContext is null || _script is null || args.Count < 2 || args[0].Type != DataType.String)
                    return DynValue.NewNumber(0d);

                var anchorName = args[0].String;
                int count = AnchorSupport.ClampCount((int)(args[1].CastToNumber() ?? 0d));
                if (string.Equals(anchorName, AnchorSupport.TrackGroup, StringComparison.Ordinal))
                    return DynValue.NewNumber(0d);

                var connection = AnchorConnection.None;
                bool is3D = false;
                for (int i = 2; i < args.Count; i++)
                {
                    if (args[i].Type == DataType.String)
                        AnchorSupport.ApplyOption(args[i].String, ref connection, ref is3D);
                }

                int stride = is3D ? 3 : 2;
                var table = new Table(_script);
                var source = _activeContext.AnchorSource;
                for (int i = 0; i < count; i++)
                {
                    AnchorSupport.ResolvePosition(source, anchorName, i, out double x, out double y, out double z);
                    table.Set(i * stride + 1, DynValue.NewNumber(x));
                    table.Set(i * stride + 2, DynValue.NewNumber(y));
                    if (is3D)
                        table.Set(i * stride + 3, DynValue.NewNumber(z));
                }
                _script.Globals.Set(anchorName, DynValue.NewTable(table));

                _activeContext.AddAnchorRequest(new AnchorRequestData(anchorName, count, connection, is3D));
                return DynValue.NewNumber(count);
            }

            [LuaFunction("effect")]
            private DynValue Effect(CallbackArguments args)
            {
                _activeCancellation.ThrowIfCancellationRequested();
                if (_activeContext is null || args.Count == 0 || args[0].Type != DataType.String)
                    return DynValue.Void;

                string name = args[0].String;
                var arguments = new List<KeyValuePair<string, object>>();
                for (int i = 1; i + 1 < args.Count; i += 2)
                {
                    if (args[i].Type != DataType.String)
                        continue;
                    var value = args[i + 1];
                    object boxed = value.Type switch
                    {
                        DataType.Number => value.Number,
                        DataType.Boolean => value.Boolean,
                        DataType.String => value.String,
                        _ => value.CastToNumber() ?? 0d,
                    };
                    arguments.Add(new KeyValuePair<string, object>(args[i].String, boxed));
                }

                _activeContext.AddEffect(new AviUtlEffectRequest(name, arguments));
                return DynValue.Void;
            }

            private double CurrentAntialias() =>
                _options.TryGetValue("antialias", out var value) && value.Type == DataType.Number
                    ? value.Number
                    : 1d;

            private double CurrentBlend() =>
                _options.TryGetValue("blend", out var value) && value.Type == DataType.Number
                    ? value.Number
                    : 0d;

            private void LoadFigure(string name, int color, double size, double line, double aspect)
            {
                int boundingSize = Math.Max(1, (int)Math.Round(size));
                double width = aspect >= 0d ? boundingSize * (1d - aspect) : boundingSize;
                double height = aspect <= 0d ? boundingSize * (1d + aspect) : boundingSize;
                int w = Math.Max(1, (int)Math.Round(width));
                int h = Math.Max(1, (int)Math.Round(height));

                var buffer = FigureRenderer.Render(name, w, h, color, line);
                _activeContext!.ReplaceBuffer(buffer, w, h);

                RefreshObjDimensions();
            }

            private void LoadText(string text)
            {
                _mediaLoader ??= _mediaLoaderFactory();
                var buffer = _mediaLoader.RenderText(
                    text, _fontState.Family, _fontState.Size, _fontState.Bold, _fontState.Italic, _fontState.Color,
                    out int w, out int h);
                _activeContext!.ReplaceBuffer(buffer, w, h);
                RefreshObjDimensions();
            }

            private void LoadImage(string path)
            {
                _mediaLoader ??= _mediaLoaderFactory();
                var buffer = _mediaLoader.DecodeImage(path, out int w, out int h);
                _activeContext!.ReplaceBuffer(buffer, w, h);
                RefreshObjDimensions();
            }

            private void LoadMovie(string path, double time)
            {
                _mediaLoader ??= _mediaLoaderFactory();
                var buffer = _mediaLoader.DecodeMovie(path, time, out int w, out int h);
                _activeContext!.ReplaceBuffer(buffer, w, h);
                RefreshObjDimensions();
            }

            private void RefreshObjDimensions()
            {
                int w = _activeContext!.ImageWidth;
                int h = _activeContext.ImageHeight;
                var obj = _objTable!;
                obj["w"] = w;
                obj["h"] = h;
                obj["hw"] = w / 2d;
                obj["hh"] = h / 2d;
                obj["cx"] = w / 2d;
                obj["cy"] = h / 2d;
                obj["cz"] = 0d;
                obj["diagonal"] = Math.Sqrt((double)w * w + (double)h * h);
            }

            private static DynValue BuildObjectTable(Script script, SceneObjectInfo info)
            {
                var table = new Table(script)
                {
                    ["exist"] = info.Exist,
                    ["x"] = info.X,
                    ["y"] = info.Y,
                    ["z"] = info.Z,
                    ["zoom"] = info.Zoom,
                    ["sx"] = info.Zoom,
                    ["sy"] = info.Zoom,
                    ["rx"] = 0d,
                    ["ry"] = 0d,
                    ["rz"] = info.Rz,
                    ["rxr"] = 0d,
                    ["ryr"] = 0d,
                    ["rzr"] = info.Rz * Math.PI / 180d,
                    ["alpha"] = info.Alpha,
                    ["layer"] = info.Layer,
                };
                return DynValue.NewTable(table);
            }

            private void ReadBackGlobals(AviUtlScriptContext ctx)
            {
                if (_objTable is null) return;

                ctx.X = _objTable.Get("x").CastToNumber() ?? ctx.X;
                ctx.Y = _objTable.Get("y").CastToNumber() ?? ctx.Y;
                ctx.Z = _objTable.Get("z").CastToNumber() ?? ctx.Z;
                ctx.Ox = _objTable.Get("ox").CastToNumber() ?? ctx.Ox;
                ctx.Oy = _objTable.Get("oy").CastToNumber() ?? ctx.Oy;
                ctx.Oz = _objTable.Get("oz").CastToNumber() ?? ctx.Oz;
                ctx.Alpha = _objTable.Get("alpha").CastToNumber() ?? ctx.Alpha;

                ctx.ApplyWriteBack(
                    _objTable.Get("sx").CastToNumber() ?? ctx.Sx,
                    _objTable.Get("sy").CastToNumber() ?? ctx.Sy,
                    _objTable.Get("zoom").CastToNumber() ?? ctx.Zoom,
                    _objTable.Get("aspect").CastToNumber() ?? ctx.Aspect,
                    _objTable.Get("rx").CastToNumber() ?? ctx.Rx,
                    _objTable.Get("ry").CastToNumber() ?? ctx.Ry,
                    _objTable.Get("rz").CastToNumber() ?? ctx.Rz,
                    _objTable.Get("rxr").CastToNumber() ?? ctx.RxRad,
                    _objTable.Get("ryr").CastToNumber() ?? ctx.RyRad,
                    _objTable.Get("rzr").CastToNumber() ?? ctx.RzRad);
            }

            public void Dispose()
            {
                _disposeRequested = true;
                _workSignal.Release();
                _thread.Join();
                _mediaLoader?.Dispose();
                _cts.Cancel();
                _cts.Dispose();
                _workSignal.Dispose();
                _doneSignal.Dispose();
            }

            internal void AbandonAsync()
            {
                _disposeRequested = true;
                _workSignal.Release();
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    _thread.Join();
                    _mediaLoader?.Dispose();
                    _cts.Dispose();
                    _workSignal.Dispose();
                    _doneSignal.Dispose();
                });
            }

            private sealed class DisabledFileScriptLoader : ScriptLoaderBase
            {
                public override object LoadFile(string file, Table globalContext)
                    => throw new NotSupportedException(
                        "File access is not allowed in Lua script effects.");

                public override bool ScriptFileExists(string name) => false;
            }
        }

        private const int ExecutionTimeoutMilliseconds = 5000;

        static LuaScriptEngine()
        {
            UserData.RegisterType<PixelDataProxy>();
        }

        private readonly Func<IMediaSourceLoader> _mediaLoaderFactory;
        private ExecutionThread _executionThread;
        private bool _disposed;

        internal LuaScriptEngine(Func<IMediaSourceLoader> mediaLoaderFactory)
        {
            _mediaLoaderFactory = mediaLoaderFactory;
            _executionThread = new ExecutionThread(mediaLoaderFactory);
        }

        public void Execute(string code, AviUtlScriptContext ctx)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var result = _executionThread.TryExecute(code, ctx, ExecutionTimeoutMilliseconds);

            if (result.TimedOut)
            {
                var stale = _executionThread;
                _executionThread = new ExecutionThread(_mediaLoaderFactory);
                stale.AbandonAsync();
                throw new LuaScriptTimeoutException(
                    $"Script execution timed out after {ExecutionTimeoutMilliseconds} ms.");
            }

            if (result.Exception is not null)
                throw result.Exception;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _executionThread.Dispose();
        }
    }
}
