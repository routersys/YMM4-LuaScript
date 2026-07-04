using LuaScript.Compat;

namespace LuaScript.Tests
{
    public sealed class PixelShaderMoonSharpTests
    {
        private const string ShaderSource = "--[[pixelshader@ps:\nfloat4 ps(float4 pos : SV_Position) : SV_Target { return 0; }\n]]";

        private sealed class StubMediaLoader : IMediaSourceLoader
        {
            public byte[] RenderText(string text, string fontFamily, double fontSize, bool bold, bool italic, int colorRgb, out int width, out int height)
            {
                width = 1;
                height = 1;
                return new byte[4];
            }

            public byte[] DecodeImage(string path, out int width, out int height)
            {
                width = 1;
                height = 1;
                return new byte[4];
            }

            public byte[] DecodeMovie(string path, double time, out int width, out int height)
            {
                width = 1;
                height = 1;
                return new byte[4];
            }

            public void Dispose() { }
        }

        private sealed class RecordingRunner : IPixelShaderRunner
        {
            public PixelShaderRunStatus Result = PixelShaderRunStatus.Success;
            public string? Error;
            public int CallCount;
            public string? Hlsl;
            public string? EntryPoint;
            public PixelShaderInput[] Resources = [];
            public float[] Constants = [];
            public PixelShaderBlend Blend;
            public PixelShaderSampler Sampler;
            public byte[]? Target;
            public int TargetWidth;
            public int TargetHeight;

            public PixelShaderRunStatus TryRun(
                string hlsl,
                string entryPoint,
                ReadOnlySpan<PixelShaderInput> resources,
                ReadOnlySpan<float> constants,
                PixelShaderBlend blend,
                PixelShaderSampler sampler,
                byte[] target,
                int targetWidth,
                int targetHeight,
                out string? error)
            {
                CallCount++;
                Hlsl = hlsl;
                EntryPoint = entryPoint;
                Resources = resources.ToArray();
                Constants = constants.ToArray();
                Blend = blend;
                Sampler = sampler;
                Target = target;
                TargetWidth = targetWidth;
                TargetHeight = targetHeight;
                error = Error;
                return Result;
            }
        }

        private static LuaScriptEngine CreateEngine() => new(static () => new StubMediaLoader());

        private static (AviUtlScriptContext Context, RecordingRunner Runner) NewContext(string script)
        {
            var runner = new RecordingRunner();
            var ctx = new AviUtlScriptContext
            {
                ImageWidth = 4,
                ImageHeight = 3,
                ShaderRunner = runner,
                PixelShaders = AviUtlPixelShaderLibrary.Parse(script),
            };
            ctx.SetPixelLoader(static () => new byte[4 * 3 * 4]);
            return (ctx, runner);
        }

        [Fact]
        public void PixelShader_PassesParsedArgumentsToRunner()
        {
            using var engine = CreateEngine();
            var (ctx, runner) = NewContext(ShaderSource);

            engine.Execute(ShaderSource + "\nobj.pixelshader(\"ps\", \"object\", {\"object\"}, {1, 2.5}, \"draw\", \"clamp\")", ctx);

            Assert.Equal(1, runner.CallCount);
            Assert.Contains("float4 ps", runner.Hlsl);
            Assert.Equal("ps", runner.EntryPoint);
            Assert.Single(runner.Resources);
            Assert.Equal(4, runner.Resources[0].Width);
            Assert.Equal(3, runner.Resources[0].Height);
            Assert.Equal([1f, 2.5f], runner.Constants);
            Assert.Equal(PixelShaderBlend.Draw, runner.Blend);
            Assert.Equal(PixelShaderSampler.Clamp, runner.Sampler);
            Assert.Equal(4, runner.TargetWidth);
            Assert.Equal(3, runner.TargetHeight);
            Assert.True(ctx.IsPixelsDirty);
        }

        [Fact]
        public void PixelShader_SingleStringResourceForm()
        {
            using var engine = CreateEngine();
            var (ctx, runner) = NewContext(ShaderSource);

            engine.Execute(ShaderSource + "\nobj.pixelshader(\"ps\", \"object\", \"object\")", ctx);

            Assert.Single(runner.Resources);
            Assert.False(runner.Resources[0].IsRandom);
        }

        [Fact]
        public void PixelShader_DefaultsAreCopyAndNoSampler()
        {
            using var engine = CreateEngine();
            var (ctx, runner) = NewContext(ShaderSource);

            engine.Execute(ShaderSource + "\nobj.pixelshader(\"ps\", \"object\", \"object\")", ctx);

            Assert.Equal(PixelShaderBlend.Copy, runner.Blend);
            Assert.Equal(PixelShaderSampler.None, runner.Sampler);
            Assert.Empty(runner.Constants);
        }

        [Fact]
        public void PixelShader_RandomResource()
        {
            using var engine = CreateEngine();
            var (ctx, runner) = NewContext(ShaderSource);

            engine.Execute(ShaderSource + "\nobj.pixelshader(\"ps\", \"object\", {\"object\", \"random\"})", ctx);

            Assert.Equal(2, runner.Resources.Length);
            Assert.True(runner.Resources[1].IsRandom);
            Assert.Equal(PixelShaderInput.RandomSize, runner.Resources[1].Width);
        }

        [Fact]
        public void PixelShader_MissingResourceBuffer_BindsTransparentPixel()
        {
            using var engine = CreateEngine();
            var (ctx, runner) = NewContext(ShaderSource);

            engine.Execute(ShaderSource + "\nobj.pixelshader(\"ps\", \"object\", {\"cache:none\", \"framebuffer\"})", ctx);

            Assert.Equal(2, runner.Resources.Length);
            Assert.All(runner.Resources, static r =>
            {
                Assert.False(r.IsRandom);
                Assert.Equal(1, r.Width);
                Assert.Equal(1, r.Height);
            });
        }

        [Fact]
        public void PixelShader_TempBufferTarget_CreatedAtObjectSize()
        {
            using var engine = CreateEngine();
            var (ctx, runner) = NewContext(ShaderSource);

            engine.Execute(ShaderSource + "\nobj.pixelshader(\"ps\", \"tempbuffer\", \"object\")", ctx);

            Assert.Equal(4, runner.TargetWidth);
            Assert.Equal(3, runner.TargetHeight);
            Assert.False(ctx.IsPixelsDirty);
            Assert.True(ctx.TryGetShaderResource("tempbuffer", out var data, out int w, out int h));
            Assert.Same(runner.Target, data);
            Assert.Equal(4, w);
            Assert.Equal(3, h);
        }

        [Fact]
        public void PixelShader_CacheTarget_CreatedAndReused()
        {
            using var engine = CreateEngine();
            var (ctx, runner) = NewContext(ShaderSource);

            engine.Execute(ShaderSource + "\nobj.pixelshader(\"ps\", \"cache:tmp\", \"object\")\nobj.pixelshader(\"ps\", \"object\", \"cache:tmp\")", ctx);

            Assert.Equal(2, runner.CallCount);
            Assert.Single(runner.Resources);
            Assert.Equal(4, runner.Resources[0].Width);
        }

        [Fact]
        public void PixelShader_FramebufferTarget_IsIgnored()
        {
            using var engine = CreateEngine();
            var (ctx, runner) = NewContext(ShaderSource);

            engine.Execute(ShaderSource + "\nobj.pixelshader(\"ps\", \"framebuffer\", \"object\")", ctx);

            Assert.Equal(0, runner.CallCount);
            Assert.False(ctx.IsPixelsDirty);
        }

        [Fact]
        public void PixelShader_UnknownShaderName_Throws()
        {
            using var engine = CreateEngine();
            var (ctx, _) = NewContext(ShaderSource);

            var ex = Assert.Throws<LuaScriptRuntimeException>(() =>
                engine.Execute(ShaderSource + "\nobj.pixelshader(\"missing\", \"object\", \"object\")", ctx));
            Assert.Contains("missing", ex.Message);
        }

        [Fact]
        public void PixelShader_CompileError_ThrowsWithCompilerMessage()
        {
            using var engine = CreateEngine();
            var (ctx, runner) = NewContext(ShaderSource);
            runner.Result = PixelShaderRunStatus.CompileError;
            runner.Error = "error X3000: syntax error";

            var ex = Assert.Throws<LuaScriptRuntimeException>(() =>
                engine.Execute(ShaderSource + "\nobj.pixelshader(\"ps\", \"object\", \"object\")", ctx));
            Assert.Contains("X3000", ex.Message);
            Assert.False(ctx.IsPixelsDirty);
        }

        [Fact]
        public void PixelShader_Unavailable_IsSilentlySkipped()
        {
            using var engine = CreateEngine();
            var (ctx, runner) = NewContext(ShaderSource);
            runner.Result = PixelShaderRunStatus.Unavailable;

            engine.Execute(ShaderSource + "\nobj.pixelshader(\"ps\", \"object\", \"object\")", ctx);

            Assert.Equal(1, runner.CallCount);
            Assert.False(ctx.IsPixelsDirty);
        }

        [Fact]
        public void PixelShader_NoRunner_IsSilentlySkipped()
        {
            using var engine = CreateEngine();
            var (ctx, _) = NewContext(ShaderSource);
            ctx.ShaderRunner = null;

            engine.Execute(ShaderSource + "\nobj.pixelshader(\"ps\", \"object\", \"object\")", ctx);

            Assert.False(ctx.IsPixelsDirty);
        }

        [Fact]
        public void PixelShader_UnknownBlend_Throws()
        {
            using var engine = CreateEngine();
            var (ctx, _) = NewContext(ShaderSource);

            Assert.Throws<LuaScriptRuntimeException>(() =>
                engine.Execute(ShaderSource + "\nobj.pixelshader(\"ps\", \"object\", \"object\", {}, \"multiply\")", ctx));
        }

        [Fact]
        public void PixelShader_UnknownSampler_Throws()
        {
            using var engine = CreateEngine();
            var (ctx, _) = NewContext(ShaderSource);

            Assert.Throws<LuaScriptRuntimeException>(() =>
                engine.Execute(ShaderSource + "\nobj.pixelshader(\"ps\", \"object\", \"object\", {}, \"copy\", \"point\")", ctx));
        }

        [Fact]
        public void PixelShader_TooManyConstants_Throws()
        {
            using var engine = CreateEngine();
            var (ctx, _) = NewContext(ShaderSource);

            var ex = Assert.Throws<LuaScriptRuntimeException>(() =>
                engine.Execute(ShaderSource + "\nlocal c = {}\nfor i = 1, 1025 do c[i] = 0 end\nobj.pixelshader(\"ps\", \"object\", \"object\", c)", ctx));
            Assert.Contains("constants", ex.Message);
        }

        [Fact]
        public void PixelShader_TooManyResources_Throws()
        {
            using var engine = CreateEngine();
            var (ctx, _) = NewContext(ShaderSource);

            var ex = Assert.Throws<LuaScriptRuntimeException>(() =>
                engine.Execute(ShaderSource + "\nlocal r = {}\nfor i = 1, 9 do r[i] = \"object\" end\nobj.pixelshader(\"ps\", \"object\", r)", ctx));
            Assert.Contains("resources", ex.Message);
        }

        [Fact]
        public void PixelShader_CrossScriptName_UsesLocalEntryPoint()
        {
            using var engine = CreateEngine();
            var (ctx, runner) = NewContext(ShaderSource);

            engine.Execute(ShaderSource + "\nobj.pixelshader(\"ps@Other\", \"object\", \"object\")", ctx);

            Assert.Equal("ps", runner.EntryPoint);
        }

        [Fact]
        public void ResolveAuto_PixelShaderScript_RoutesToMoonSharp()
        {
            Assert.Equal(Engine.ScriptEngineKind.MoonSharp, Engine.ScriptDirective.ResolveAuto("obj.pixelshader(\"ps\", \"object\", \"object\")"));
            Assert.Equal(Engine.ScriptEngineKind.Native, Engine.ScriptDirective.ResolveAuto("obj.setpixel(0, 0, 1, 2, 3)"));
            Assert.Equal(Engine.ScriptEngineKind.Native, Engine.ScriptDirective.ResolveAuto("--!native\nobj.pixelshader(\"ps\", \"object\", \"object\")"));
        }
    }
}
