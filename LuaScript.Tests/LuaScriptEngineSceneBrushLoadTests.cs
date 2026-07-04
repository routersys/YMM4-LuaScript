using LuaScript;

namespace LuaScript.Tests
{
    public sealed class LuaScriptEngineSceneBrushLoadTests
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
            return new AviUtlScriptContext
            {
                ImageWidth = 6,
                ImageHeight = 4,
                Time = 1.5,
                Framerate = 30,
            };
        }

        [Fact]
        public void LoadScene_ReplacesBufferAndUpdatesObjDimensions()
        {
            using var engine = CreateEngine();
            var ctx = NewContext();

            string? capturedName = null;
            double capturedTime = -1;
            var rendered = new byte[2 * 3 * 4];
            ctx.SceneImageLoader = (name, time) =>
            {
                capturedName = name;
                capturedTime = time;
                return (rendered, 2, 3);
            };

            engine.Execute("obj.load('scene', 'サブ', 4.25); obj.x = obj.w; obj.y = obj.h", ctx);

            Assert.Equal("サブ", capturedName);
            Assert.Equal(4.25d, capturedTime);
            Assert.True(ctx.BufferReplaced);
            Assert.Same(rendered, ctx.GetPixelBuffer());
            Assert.Equal(2, ctx.ImageWidth);
            Assert.Equal(3, ctx.ImageHeight);
            Assert.Equal(2d, ctx.X);
            Assert.Equal(3d, ctx.Y);
        }

        [Fact]
        public void LoadScene_DefaultsTimeToObjTime()
        {
            using var engine = CreateEngine();
            var ctx = NewContext();

            double capturedTime = -1;
            ctx.SceneImageLoader = (_, time) =>
            {
                capturedTime = time;
                return ([], 0, 0);
            };

            engine.Execute("obj.load('scene', 'サブ')", ctx);

            Assert.Equal(1.5d, capturedTime);
            Assert.False(ctx.BufferReplaced);
        }

        [Fact]
        public void LoadScene_WithoutLoader_IsNoOp()
        {
            using var engine = CreateEngine();
            var ctx = NewContext();

            engine.Execute("obj.load('scene', 'サブ'); obj.x = obj.w", ctx);

            Assert.False(ctx.BufferReplaced);
            Assert.Equal(6d, ctx.X);
        }

        [Fact]
        public void LoadBrush_ReplacesBufferWithRequestedSize()
        {
            using var engine = CreateEngine();
            var ctx = NewContext();

            string? capturedName = null;
            double capturedW = -1, capturedH = -1;
            var rendered = new byte[8 * 5 * 4];
            ctx.BrushImageLoader = (name, w, h, _) =>
            {
                capturedName = name;
                capturedW = w;
                capturedH = h;
                return (rendered, 8, 5);
            };

            engine.Execute("obj.load('brush', '縞模様', 8, 5); obj.x = obj.w; obj.y = obj.h", ctx);

            Assert.Equal("縞模様", capturedName);
            Assert.Equal(8d, capturedW);
            Assert.Equal(5d, capturedH);
            Assert.True(ctx.BufferReplaced);
            Assert.Same(rendered, ctx.GetPixelBuffer());
            Assert.Equal(8d, ctx.X);
            Assert.Equal(5d, ctx.Y);
        }

        [Fact]
        public void LoadBrush_DefaultsSizeToObjectDimensions()
        {
            using var engine = CreateEngine();
            var ctx = NewContext();

            double capturedW = -1, capturedH = -1;
            ctx.BrushImageLoader = (_, w, h, _) =>
            {
                capturedW = w;
                capturedH = h;
                return ([], 0, 0);
            };

            engine.Execute("obj.load('brush', '市松模様')", ctx);

            Assert.Equal(6d, capturedW);
            Assert.Equal(4d, capturedH);
            Assert.False(ctx.BufferReplaced);
        }

        [Fact]
        public void Brush_PassesParameterPairs()
        {
            using var engine = CreateEngine();
            var ctx = NewContext();

            string? capturedName = null;
            double capturedW = -1, capturedH = -1;
            List<KeyValuePair<string, object>>? captured = null;
            var rendered = new byte[6 * 4 * 4];
            ctx.BrushImageLoader = (name, w, h, arguments) =>
            {
                capturedName = name;
                capturedW = w;
                capturedH = h;
                captured = [.. arguments];
                return (rendered, 6, 4);
            };

            engine.Execute("obj.brush('縞模様', '色1', 0xff0000, 'ズーム', 250, '有効', true, 'ラベル', 'abc')", ctx);

            Assert.Equal("縞模様", capturedName);
            Assert.Equal(6d, capturedW);
            Assert.Equal(4d, capturedH);
            Assert.NotNull(captured);
            Assert.Equal(4, captured!.Count);
            Assert.Equal(new KeyValuePair<string, object>("色1", (double)0xff0000), captured[0]);
            Assert.Equal(new KeyValuePair<string, object>("ズーム", 250d), captured[1]);
            Assert.Equal(new KeyValuePair<string, object>("有効", true), captured[2]);
            Assert.Equal(new KeyValuePair<string, object>("ラベル", "abc"), captured[3]);
            Assert.True(ctx.BufferReplaced);
        }

        [Fact]
        public void Brush_AcceptsLeadingSizeBeforePairs()
        {
            using var engine = CreateEngine();
            var ctx = NewContext();

            double capturedW = -1, capturedH = -1;
            List<KeyValuePair<string, object>>? captured = null;
            ctx.BrushImageLoader = (_, w, h, arguments) =>
            {
                capturedW = w;
                capturedH = h;
                captured = [.. arguments];
                return ([], 0, 0);
            };

            engine.Execute("obj.brush('市松模様', 320, 240, 'ズーム', 50)", ctx);

            Assert.Equal(320d, capturedW);
            Assert.Equal(240d, capturedH);
            Assert.NotNull(captured);
            Assert.Equal(new KeyValuePair<string, object>("ズーム", 50d), Assert.Single(captured!));
        }

        [Fact]
        public void Brush_WithoutPairs_UsesObjectDimensions()
        {
            using var engine = CreateEngine();
            var ctx = NewContext();

            double capturedW = -1, capturedH = -1;
            int capturedCount = -1;
            ctx.BrushImageLoader = (_, w, h, arguments) =>
            {
                capturedW = w;
                capturedH = h;
                capturedCount = arguments.Count;
                return ([], 0, 0);
            };

            engine.Execute("obj.brush('市松模様')", ctx);

            Assert.Equal(6d, capturedW);
            Assert.Equal(4d, capturedH);
            Assert.Equal(0, capturedCount);
        }

        [Fact]
        public void LoadBrush_FailureKeepsCurrentImage()
        {
            using var engine = CreateEngine();
            var ctx = NewContext();

            ctx.BrushImageLoader = (_, _, _, _) => ([], 0, 0);

            engine.Execute("obj.load('brush', '不明'); obj.x = obj.w; obj.y = obj.h", ctx);

            Assert.False(ctx.BufferReplaced);
            Assert.Equal(6d, ctx.X);
            Assert.Equal(4d, ctx.Y);
        }
    }
}
