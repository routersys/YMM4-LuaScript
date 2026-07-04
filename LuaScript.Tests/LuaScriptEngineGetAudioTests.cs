using LuaScript;

namespace LuaScript.Tests
{
    public sealed class LuaScriptEngineGetAudioTests
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
                ImageWidth = 2,
                ImageHeight = 2,
                Framerate = 30,
            };
        }

        [Fact]
        public void GetAudio_FillsBufferTableFromLoader()
        {
            using var engine = CreateEngine();
            var ctx = NewContext();

            string? capturedFile = null;
            string? capturedType = null;
            int capturedSize = -1;
            ctx.AudioLoader = (file, type, size) =>
            {
                capturedFile = file;
                capturedType = type;
                capturedSize = size;
                return (3, 44100, [1.5d, -2.5d, 3d]);
            };

            engine.Execute(
                "local buf = {}\n" +
                "local n, rate = obj.getaudio(buf, 'audiobuffer', 'pcm', 3)\n" +
                "obj.x = n\n" +
                "obj.y = rate\n" +
                "obj.z = buf[1]\n" +
                "obj.ox = buf[2]\n" +
                "obj.oy = buf[3]",
                ctx);

            Assert.Equal("audiobuffer", capturedFile);
            Assert.Equal("pcm", capturedType);
            Assert.Equal(3, capturedSize);
            Assert.Equal(3d, ctx.X);
            Assert.Equal(44100d, ctx.Y);
            Assert.Equal(1.5d, ctx.Z);
            Assert.Equal(-2.5d, ctx.Ox);
            Assert.Equal(3d, ctx.Oy);
        }

        [Fact]
        public void GetAudio_NilBuffer_ReturnsTableAsThirdValue()
        {
            using var engine = CreateEngine();
            var ctx = NewContext();

            ctx.AudioLoader = (_, type, _) => (2, 48000, [7d, 8d]);

            engine.Execute(
                "local n, rate, buf = obj.getaudio(nil, 'C:/test.wav', 'spectrum.r', 2)\n" +
                "obj.x = n\n" +
                "obj.y = rate\n" +
                "obj.z = buf[1]\n" +
                "obj.ox = buf[2]",
                ctx);

            Assert.Equal(2d, ctx.X);
            Assert.Equal(48000d, ctx.Y);
            Assert.Equal(7d, ctx.Z);
            Assert.Equal(8d, ctx.Ox);
        }

        [Fact]
        public void GetAudio_DefaultsTypeToPcm()
        {
            using var engine = CreateEngine();
            var ctx = NewContext();

            string? capturedType = null;
            int capturedSize = -1;
            ctx.AudioLoader = (_, type, size) =>
            {
                capturedType = type;
                capturedSize = size;
                return (0, 48000, []);
            };

            engine.Execute("obj.x = obj.getaudio({}, 'audiobuffer')", ctx);

            Assert.Equal("pcm", capturedType);
            Assert.Equal(0, capturedSize);
            Assert.Equal(0d, ctx.X);
        }

        [Fact]
        public void GetAudio_WithoutLoader_ReturnsZero()
        {
            using var engine = CreateEngine();
            var ctx = NewContext();

            engine.Execute(
                "local n, rate, buf = obj.getaudio(nil, 'audiobuffer', 'pcm', 8)\n" +
                "obj.x = n\n" +
                "obj.y = rate\n" +
                "obj.z = #buf\n" +
                "obj.ox = obj.getaudio({}, 7)",
                ctx);

            Assert.Equal(0d, ctx.X);
            Assert.Equal(0d, ctx.Y);
            Assert.Equal(0d, ctx.Z);
            Assert.Equal(0d, ctx.Ox);
        }

        [Fact]
        public void GetAudio_ClampsCountToDataLength()
        {
            using var engine = CreateEngine();
            var ctx = NewContext();

            ctx.AudioLoader = (_, _, _) => (10, 48000, [5d]);

            engine.Execute(
                "local n, _, buf = obj.getaudio(nil, 'audiobuffer', 'pcm', 10)\n" +
                "obj.x = n\n" +
                "obj.y = #buf",
                ctx);

            Assert.Equal(1d, ctx.X);
            Assert.Equal(1d, ctx.Y);
        }
    }
}
