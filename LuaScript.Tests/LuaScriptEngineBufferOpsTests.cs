using LuaScript;

namespace LuaScript.Tests
{
    public sealed class LuaScriptEngineBufferOpsTests
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

        private static AviUtlScriptContext NewContext(byte[] buffer, int width, int height)
        {
            var ctx = new AviUtlScriptContext { Framerate = 30 };
            ctx.SetResolvedBuffer(buffer, width, height);
            return ctx;
        }

        private static byte[] Bgra(params (byte B, byte G, byte R, byte A)[] pixels)
        {
            var buffer = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                buffer[i * 4 + 0] = pixels[i].B;
                buffer[i * 4 + 1] = pixels[i].G;
                buffer[i * 4 + 2] = pixels[i].R;
                buffer[i * 4 + 3] = pixels[i].A;
            }
            return buffer;
        }

        [Fact]
        public void Fill_WholeBuffer_WritesPremultipliedColor()
        {
            using var engine = CreateEngine();
            var ctx = NewContext(new byte[2 * 2 * 4], 2, 2);

            engine.Execute("obj.fill(200, 100, 50, 255)", ctx);

            var buffer = ctx.GetPixelBuffer()!;
            for (int i = 0; i < 4; i++)
            {
                Assert.Equal(50, buffer[i * 4 + 0]);
                Assert.Equal(100, buffer[i * 4 + 1]);
                Assert.Equal(200, buffer[i * 4 + 2]);
                Assert.Equal(255, buffer[i * 4 + 3]);
            }
        }

        [Fact]
        public void Fill_Rectangle_TouchesOnlyRequestedPixels()
        {
            using var engine = CreateEngine();
            var ctx = NewContext(new byte[3 * 1 * 4], 3, 1);

            engine.Execute("obj.fill(255, 255, 255, 255, 1, 0, 1, 1)", ctx);

            var buffer = ctx.GetPixelBuffer()!;
            Assert.Equal(new byte[] { 0, 0, 0, 0 }, buffer.AsSpan(0, 4).ToArray());
            Assert.Equal(new byte[] { 255, 255, 255, 255 }, buffer.AsSpan(4, 4).ToArray());
            Assert.Equal(new byte[] { 0, 0, 0, 0 }, buffer.AsSpan(8, 4).ToArray());
        }

        [Fact]
        public void GetPixelRegion_ReturnsStraightRgba()
        {
            using var engine = CreateEngine();
            var ctx = NewContext(Bgra((50, 100, 200, 255)), 1, 1);

            engine.Execute("local t = obj.getpixelregion(0, 0, 1, 1) obj.x = t[1] obj.y = t[2] obj.z = t[3] obj.alpha = t[4]", ctx);

            Assert.Equal(200d, ctx.X);
            Assert.Equal(100d, ctx.Y);
            Assert.Equal(50d, ctx.Z);
            Assert.Equal(255d, ctx.Alpha);
        }

        [Fact]
        public void GetPixelRegion_OutOfBounds_YieldsTransparent()
        {
            using var engine = CreateEngine();
            var ctx = NewContext(Bgra((10, 20, 30, 255)), 1, 1);

            engine.Execute("local t = obj.getpixelregion(1, 0, 1, 1) obj.x = t[1] + t[2] + t[3] + t[4]", ctx);

            Assert.Equal(0d, ctx.X);
        }

        [Fact]
        public void PutPixelRegion_WritesPremultipliedColor()
        {
            using var engine = CreateEngine();
            var ctx = NewContext(new byte[2 * 1 * 4], 2, 1);

            engine.Execute("obj.putpixelregion(0, 0, 2, 1, {200, 100, 50, 255, 10, 20, 30, 255})", ctx);

            var buffer = ctx.GetPixelBuffer()!;
            Assert.Equal(new byte[] { 50, 100, 200, 255 }, buffer.AsSpan(0, 4).ToArray());
            Assert.Equal(new byte[] { 30, 20, 10, 255 }, buffer.AsSpan(4, 4).ToArray());
        }

        [Fact]
        public void RegionRoundTrip_PreservesOpaquePixels()
        {
            using var engine = CreateEngine();
            var original = Bgra((10, 20, 30, 255), (40, 50, 60, 255), (70, 80, 90, 255), (100, 110, 120, 255));
            var ctx = NewContext((byte[])original.Clone(), 2, 2);

            engine.Execute(
                "local t = obj.getpixelregion(0, 0, obj.w, obj.h) obj.fill(0, 0, 0, 0) obj.putpixelregion(0, 0, obj.w, obj.h, t)",
                ctx);

            Assert.Equal(original, ctx.GetPixelBuffer());
        }

        [Fact]
        public void Convolve_Identity_LeavesOpaqueBufferUnchanged()
        {
            using var engine = CreateEngine();
            var original = Bgra((10, 20, 30, 255), (40, 50, 60, 255), (70, 80, 90, 255), (100, 110, 120, 255));
            var ctx = NewContext((byte[])original.Clone(), 2, 2);

            engine.Execute("obj.convolve({0, 0, 0, 0, 1, 0, 0, 0, 0}, 3)", ctx);

            Assert.Equal(original, ctx.GetPixelBuffer());
        }

        [Fact]
        public void Convolve_BoxBlur_AveragesHorizontalStep()
        {
            using var engine = CreateEngine();
            var ctx = NewContext(Bgra((0, 0, 0, 255), (255, 255, 255, 255)), 2, 1);

            engine.Execute("obj.convolve({1, 1, 1, 1, 1, 1, 1, 1, 1}, 3)", ctx);

            var buffer = ctx.GetPixelBuffer()!;
            Assert.Equal(85, buffer[2]);
            Assert.Equal(85, buffer[1]);
            Assert.Equal(85, buffer[0]);
            Assert.Equal(170, buffer[6]);
            Assert.Equal(255, buffer[3]);
            Assert.Equal(255, buffer[7]);
        }

        [Fact]
        public void Resize_Nearest_ReplicatesBlocks()
        {
            using var engine = CreateEngine();
            var ctx = NewContext(Bgra((11, 12, 13, 255), (21, 22, 23, 255), (31, 32, 33, 255), (41, 42, 43, 255)), 2, 2);

            engine.Execute("obj.resize(4, 4, 'nearest')", ctx);

            Assert.Equal(4, ctx.ImageWidth);
            Assert.Equal(4, ctx.ImageHeight);
            Assert.True(ctx.BufferReplaced);

            var buffer = ctx.GetPixelBuffer()!;
            byte SampleR(int x, int y) => buffer[(y * 4 + x) * 4 + 2];
            Assert.Equal(13, SampleR(0, 0));
            Assert.Equal(13, SampleR(1, 1));
            Assert.Equal(23, SampleR(2, 0));
            Assert.Equal(33, SampleR(0, 2));
            Assert.Equal(43, SampleR(3, 3));
        }

        [Fact]
        public void Resize_UpdatesObjDimensions()
        {
            using var engine = CreateEngine();
            var ctx = NewContext(new byte[4 * 4 * 4], 4, 4);

            engine.Execute("obj.resize(8, 2) obj.x = obj.w obj.y = obj.h obj.z = obj.diagonal", ctx);

            Assert.Equal(8, ctx.ImageWidth);
            Assert.Equal(2, ctx.ImageHeight);
            Assert.Equal(8d, ctx.X);
            Assert.Equal(2d, ctx.Y);
            Assert.Equal(Math.Sqrt(8d * 8d + 2d * 2d), ctx.Z);
        }
    }
}
