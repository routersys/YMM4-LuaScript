using LuaScript;

namespace LuaScript.Tests
{
    public sealed class LuaScriptEngineLifecycleTests
    {
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

        private static LuaScriptEngine CreateEngine() => new(static () => new StubMediaLoader());

        private static AviUtlScriptContext NewContext()
        {
            var ctx = new AviUtlScriptContext
            {
                ImageWidth = 100,
                ImageHeight = 80,
                Frame = 7,
                TotalFrame = 30,
                Time = 2.5,
                TotalTime = 10d,
                Framerate = 60,
                Layer = 3,
                SceneWidth = 200,
                SceneHeight = 160,
                SceneId = "scene-1",
            };
            return ctx;
        }

        [Fact]
        public void PerFrameVariablesProjectedIntoObjAndGlobals()
        {
            using var engine = CreateEngine();
            var ctx = NewContext();

            engine.Execute("obj.x = obj.w; obj.y = time; obj.z = frame; obj.ox = scene.width; obj.oy = anim.lerp(0, 10, 0.5)", ctx);

            Assert.Equal(100d, ctx.X);
            Assert.Equal(2.5d, ctx.Y);
            Assert.Equal(7d, ctx.Z);
            Assert.Equal(200d, ctx.Ox);
            Assert.Equal(5d, ctx.Oy);
        }

        [Fact]
        public void Ymm4TableExposesContextValues()
        {
            using var engine = CreateEngine();
            var ctx = NewContext();

            engine.Execute("obj.x = (ymm4.scene_id == 'scene-1') and 1 or 0", ctx);

            Assert.Equal(1d, ctx.X);
        }

        [Fact]
        public void ObjectWritesFlowBackToContext()
        {
            using var engine = CreateEngine();
            var ctx = NewContext();

            engine.Execute("obj.x = 5; obj.y = 6; obj.alpha = 0.25", ctx);

            Assert.Equal(5d, ctx.X);
            Assert.Equal(6d, ctx.Y);
            Assert.Equal(0.25d, ctx.Alpha);
        }

        [Fact]
        public void UserGlobalsResetBetweenFrames()
        {
            using var engine = CreateEngine();

            var first = NewContext();
            engine.Execute("myUserGlobal = 42", first);

            var second = NewContext();
            engine.Execute("obj.x = (myUserGlobal == nil) and 1 or 0", second);

            Assert.Equal(1d, second.X);
        }

        [Fact]
        public void UserObjectKeysResetBetweenFrames()
        {
            using var engine = CreateEngine();

            var first = NewContext();
            engine.Execute("obj.customKey = 99", first);

            var second = NewContext();
            engine.Execute("obj.x = (obj.customKey == nil) and 1 or 0", second);

            Assert.Equal(1d, second.X);
        }

        [Fact]
        public void ReplacedObjectTableRecoversNextFrame()
        {
            using var engine = CreateEngine();

            var first = NewContext();
            engine.Execute("obj = {}", first);

            var second = NewContext();
            second.ImageWidth = 123;
            engine.Execute("obj.x = obj.w", second);

            Assert.Equal(123d, second.X);
        }

        [Fact]
        public void CompiledChunkSurvivesAcrossFrames()
        {
            using var engine = CreateEngine();

            var first = NewContext();
            first.Frame = 1;
            engine.Execute("obj.x = frame", first);
            Assert.Equal(1d, first.X);

            var second = NewContext();
            second.Frame = 2;
            engine.Execute("obj.x = frame", second);
            Assert.Equal(2d, second.X);
        }
    }
}
