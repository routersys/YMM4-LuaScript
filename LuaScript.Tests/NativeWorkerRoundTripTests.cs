using System;
using System.IO;
using LuaScript.Engine;

namespace LuaScript.Tests
{
    public class NativeWorkerRoundTripTests : IDisposable
    {
        private static string NativeDir => Path.Combine(AppContext.BaseDirectory, "native");

        private static readonly Func<string, int, SceneObjectInfo?> NoResolver = (_, _) => null;
        private static readonly Func<string, int, double, double, double, (byte[] buffer, int w, int h)> NoLoadFigure = (_, _, _, _, _) => ([], 1, 1);
        private static readonly Func<string, string, double, bool, bool, int, (byte[] buffer, int w, int h)> NoLoadText = (_, _, _, _, _, _) => ([], 1, 1);
        private static readonly Func<string, (byte[] buffer, int w, int h)> NoLoadImage = _ => ([], 1, 1);
        private static readonly Func<string, double, (byte[] buffer, int w, int h)> NoLoadMovie = (_, _) => ([], 1, 1);
        private static readonly Action<string, System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, object>>> NoAddEffect = (_, _) => { };
        private static readonly Action<DrawCommand> NoAddDraw = _ => { };
        private static readonly System.Collections.Generic.IReadOnlyDictionary<string, string> NoStringParams = new System.Collections.Generic.Dictionary<string, string>();

        private readonly LuaJitWorker _worker = new(NativeDir);

        public void Dispose() => _worker.Dispose();

        private static double[] Fields(int w, int h, double time)
        {
            var f = new double[NativeProtocol.FieldCount];
            f[NativeProtocol.W] = w;
            f[NativeProtocol.H] = h;
            f[NativeProtocol.Time] = time;
            f[NativeProtocol.Alpha] = 255d;
            return f;
        }

        [Fact]
        public void TransformScript_WritesBackObjFields()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var fields = Fields(4, 4, 2d);
            var pixels = new byte[4 * 4 * 4];

            bool ok = _worker.Execute(
                "obj.rz = obj.time * 90\nobj.alpha = 128",
                fields, NoStringParams, pixels, 4, 4, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, out bool dirty, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.False(dirty);
            Assert.Equal(180d, fields[NativeProtocol.Rz]);
            Assert.Equal(128d, fields[NativeProtocol.Alpha]);
        }

        [Fact]
        public void StringParameters_AreExposedOnObj()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var fields = Fields(2, 2, 0d);
            var pixels = new byte[16];
            var stringParameters = new System.Collections.Generic.Dictionary<string, string>
            {
                ["text"] = "hello",
                ["file_image"] = "C:/サンプル/画像.png",
            };

            bool ok = _worker.Execute(
                "obj.x = string.len(obj.text) obj.alpha = string.len(obj.file_image)",
                fields, stringParameters, pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.Equal(5d, fields[NativeProtocol.X]);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount("C:/サンプル/画像.png"), fields[NativeProtocol.Alpha]);
        }

        [Fact]
        public void PixelScript_MatchesExpectedGrayscale()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            const int w = 2, h = 2;
            var pixels = new byte[w * h * 4];
            void SetBgra(int i, byte b, byte g, byte r, byte a)
            {
                pixels[i * 4 + 0] = b; pixels[i * 4 + 1] = g; pixels[i * 4 + 2] = r; pixels[i * 4 + 3] = a;
            }
            SetBgra(0, 10, 20, 30, 255);
            SetBgra(1, 0, 0, 0, 0);
            SetBgra(2, 50, 100, 200, 128);
            SetBgra(3, 255, 255, 255, 255);

            var expected = (byte[])pixels.Clone();
            for (int i = 0; i < w * h; i++)
                Grayscale(expected, i);

            var fields = Fields(w, h, 0d);
            const string script =
                "for y=0,obj.h-1 do for x=0,obj.w-1 do " +
                "local r,g,b,a = obj.getpixel(x,y) " +
                "local gray = r*0.299 + g*0.587 + b*0.114 " +
                "obj.setpixel(x,y,gray,gray,gray,a) end end";

            bool ok = _worker.Execute(script, fields, NoStringParams, pixels, w, h, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, out bool dirty, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.True(dirty);
            Assert.Equal(expected, pixels);
        }

        [Fact]
        public void PixelDataProxy_MatchesExpectedGrayscale()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            const int w = 2, h = 2;
            var pixels = new byte[w * h * 4];
            void SetBgra(int i, byte b, byte g, byte r, byte a)
            {
                pixels[i * 4 + 0] = b; pixels[i * 4 + 1] = g; pixels[i * 4 + 2] = r; pixels[i * 4 + 3] = a;
            }
            SetBgra(0, 10, 20, 30, 255);
            SetBgra(1, 0, 0, 0, 0);
            SetBgra(2, 50, 100, 200, 128);
            SetBgra(3, 255, 255, 255, 255);

            var expected = (byte[])pixels.Clone();
            for (int i = 0; i < w * h; i++)
                Grayscale(expected, i);

            var fields = Fields(w, h, 0d);
            const string script =
                "local pd = obj.getpixeldata() local w,h = pd.width, pd.height " +
                "for y=0,h-1 do for x=0,w-1 do local base=(y*w+x)*4 " +
                "local r=pd:get(base+1) local g=pd:get(base+2) local b=pd:get(base+3) " +
                "local gray=r*0.299+g*0.587+b*0.114 " +
                "pd:set(base+1,gray) pd:set(base+2,gray) pd:set(base+3,gray) end end";

            bool ok = _worker.Execute(script, fields, NoStringParams, pixels, w, h, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, out bool dirty, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.True(dirty);
            Assert.Equal(expected, pixels);
        }

        private static void Grayscale(byte[] px, int i)
        {
            double b = px[i * 4 + 0], g = px[i * 4 + 1], r = px[i * 4 + 2], a = px[i * 4 + 3];
            double sr, sg, sb;
            if (a <= 0d) { sr = sg = sb = 0d; }
            else
            {
                double s = 255d / a;
                sr = Math.Clamp(r * s, 0d, 255d);
                sg = Math.Clamp(g * s, 0d, 255d);
                sb = Math.Clamp(b * s, 0d, 255d);
            }
            double gray = sr * 0.299 + sg * 0.587 + sb * 0.114;
            double aK = Math.Clamp(a, 0d, 255d) / 255d;
            px[i * 4 + 0] = (byte)Math.Floor(Math.Clamp(gray * aK, 0d, 255d));
            px[i * 4 + 1] = (byte)Math.Floor(Math.Clamp(gray * aK, 0d, 255d));
            px[i * 4 + 2] = (byte)Math.Floor(Math.Clamp(gray * aK, 0d, 255d));
            px[i * 4 + 3] = (byte)Math.Floor(Math.Clamp(a, 0d, 255d));
        }

        [Fact]
        public void PersistentWorker_HandlesMultipleCalls()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            for (int k = 1; k <= 5; k++)
            {
                var fields = Fields(2, 2, k);
                bool ok = _worker.Execute("obj.x = obj.time * 10", fields, NoStringParams, pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, out _, out _, out _, out _, out _, out string? error);
                Assert.True(ok, error);
                Assert.Equal(k * 10d, fields[NativeProtocol.X]);
            }
        }

        [Fact]
        public void Timeout_KillsWorker_AndRecovers()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            var fields = Fields(2, 2, 0d);

            bool timedOut = _worker.Execute(
                "local x=0 while true do x=x+1 end", fields, NoStringParams, pixels, 2, 2, 1500, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, out _, out _, out _, out _, out _, out string? error);
            Assert.False(timedOut);
            Assert.Contains("timed out", error);

            var fields2 = Fields(2, 2, 3d);
            bool ok = _worker.Execute("obj.x = obj.time", fields2, NoStringParams, pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, out _, out _, out _, out _, out _, out error);
            Assert.True(ok, error);
            Assert.Equal(3d, fields2[NativeProtocol.X]);
        }

        [Fact]
        public void GetObject_ResolvesThroughCallback()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            var fields = Fields(2, 2, 0d);
            var resolved = new SceneObjectInfo("a", true, 12d, 34d, 56d, 2d, 90d, 200d, 7);
            Func<string, int, SceneObjectInfo?> resolver = (tag, frame) =>
                tag == "a" && frame == 5 ? resolved : null;

            const string script =
                "local o = obj.getobject(\"a\", 5) " +
                "obj.x = o.x obj.y = o.y obj.z = o.z " +
                "obj.zoom = o.zoom obj.rz = o.rz obj.alpha = o.alpha " +
                "obj.sx = o.sy obj.rzr = o.rzr";

            bool ok = _worker.Execute(script, fields, NoStringParams, pixels, 2, 2, 5000, resolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.Equal(12d, fields[NativeProtocol.X]);
            Assert.Equal(34d, fields[NativeProtocol.Y]);
            Assert.Equal(56d, fields[NativeProtocol.Z]);
            Assert.Equal(2d, fields[NativeProtocol.Zoom]);
            Assert.Equal(90d, fields[NativeProtocol.Rz]);
            Assert.Equal(200d, fields[NativeProtocol.Alpha]);
            Assert.Equal(2d, fields[NativeProtocol.Sx]);
            Assert.Equal(90d * Math.PI / 180d, fields[NativeProtocol.Rzr]);
        }

        [Fact]
        public void GetObject_CachesRepeatedQueryAndResetsEachRun()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            int calls = 0;
            double x = 10d;
            Func<string, int, SceneObjectInfo?> resolver = (tag, frame) =>
            {
                calls++;
                return tag == "a" ? new SceneObjectInfo("a", true, x, 0, 0, 1, 0, 255, 0) : null;
            };

            const string script =
                "local a = obj.getobject(\"a\") " +
                "local b = obj.getobject(\"a\") " +
                "obj.x = a.x + b.x";

            bool ok = _worker.Execute(script, Fields(2, 2, 0d), NoStringParams, pixels, 2, 2, 5000, resolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, out _, out _, out _, out _, out _, out string? error);
            Assert.True(ok, error);
            Assert.Equal(1, calls);
            var fields = Fields(2, 2, 0d);

            x = 20d;
            ok = _worker.Execute(script, fields, NoStringParams, pixels, 2, 2, 5000, resolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, out _, out _, out _, out _, out _, out error);
            Assert.True(ok, error);
            Assert.Equal(2, calls);
            Assert.Equal(40d, fields[NativeProtocol.X]);
        }

        [Fact]
        public void GetObject_ReturnsNil_WhenUnresolved()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            var fields = Fields(2, 2, 0d);

            const string script =
                "local o = obj.getobject(\"missing\") obj.x = o == nil and 1 or 0";

            bool ok = _worker.Execute(script, fields, NoStringParams, pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.Equal(1d, fields[NativeProtocol.X]);
        }

        [Fact]
        public void Draw_RecordsCommandsThroughCallback()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            var fields = Fields(2, 2, 0d);
            var commands = new System.Collections.Generic.List<DrawCommand>();
            Action<DrawCommand> addDraw = commands.Add;

            bool ok = _worker.Execute(
                "obj.draw() obj.draw(10, 20, 30, 2, 0.5, 0.25)",
                fields, NoStringParams, pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, addDraw,
                out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.Equal(2, commands.Count);
            Assert.Equal(new DrawCommand(0d, 0d, 0d, 1d, 1d, 0d), commands[0]);
            Assert.Equal(new DrawCommand(10d, 20d, 30d, 2d, 0.5d, 0.25d), commands[1]);
        }

        [Fact]
        public void DrawPoly_RecordsDefaultUvAndAlpha()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[4 * 4 * 4];
            var fields = Fields(4, 4, 0d);
            var commands = new System.Collections.Generic.List<DrawCommand>();
            Action<DrawCommand> addDraw = commands.Add;

            bool ok = _worker.Execute(
                "obj.drawpoly(0,0,0, 10,0,0, 10,10,0, 0,10,0)",
                fields, NoStringParams, pixels, 4, 4, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, addDraw,
                out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.Single(commands);
            var poly = commands[0].Poly;
            Assert.NotNull(poly);
            Assert.Equal(new double[] { 0, 0, 0, 10, 0, 0, 10, 10, 0, 0, 10, 0, 0, 0, 4, 0, 4, 4, 0, 4, 1 }, poly);
        }

        [Fact]
        public void DrawPoly_RecordsExplicitUvAndAlpha()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[4 * 4 * 4];
            var fields = Fields(4, 4, 0d);
            var commands = new System.Collections.Generic.List<DrawCommand>();
            Action<DrawCommand> addDraw = commands.Add;

            bool ok = _worker.Execute(
                "obj.drawpoly(0,0,0, 8,0,0, 8,8,0, 0,8,0, 1,1, 3,1, 3,3, 1,3, 0.5)",
                fields, NoStringParams, pixels, 4, 4, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, addDraw,
                out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.Single(commands);
            var poly = commands[0].Poly;
            Assert.NotNull(poly);
            Assert.Equal(new double[] { 0, 0, 0, 8, 0, 0, 8, 8, 0, 0, 8, 0, 1, 1, 3, 1, 3, 3, 1, 3, 0.5 }, poly);
            Assert.Equal(0.5d, commands[0].Alpha);
        }

        [Fact]
        public void SetOption_GetOption_RoundTrips()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            var fields = Fields(2, 2, 0d);

            bool ok = _worker.Execute(
                "obj.setoption('antialias', 0) obj.x = obj.getoption('antialias') obj.y = obj.getoption('unset')",
                fields, NoStringParams, pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw,
                out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.Equal(0d, fields[NativeProtocol.X]);
            Assert.Equal(0d, fields[NativeProtocol.Y]);
        }

        [Fact]
        public void Draw_CarriesAntialiasFromOption()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            var commands = new System.Collections.Generic.List<DrawCommand>();
            Action<DrawCommand> addDraw = commands.Add;

            bool ok = _worker.Execute(
                "obj.draw() obj.setoption('antialias', 0) obj.draw()",
                Fields(2, 2, 0d), NoStringParams, pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, addDraw,
                out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.Equal(2, commands.Count);
            Assert.Equal(1d, commands[0].Antialias);
            Assert.Equal(0d, commands[1].Antialias);
        }

        [Fact]
        public void LoadText_RoundTripsThroughCallback()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            const int tw = 3, th = 2;
            var rendered = new byte[tw * th * 4];
            for (int i = 0; i < rendered.Length; i++)
                rendered[i] = (byte)((i * 17 + 5) & 0xFF);

            string? capturedFamily = null;
            string? capturedText = null;
            double capturedSize = 0;
            bool capturedBold = false, capturedItalic = false;
            int capturedColor = 0;
            Func<string, string, double, bool, bool, int, (byte[], int, int)> loadText =
                (family, text, size, bold, italic, color) =>
                {
                    capturedFamily = family; capturedText = text; capturedSize = size;
                    capturedBold = bold; capturedItalic = italic; capturedColor = color;
                    return (rendered, tw, th);
                };

            var pixels = new byte[4 * 4 * 4];
            var fields = Fields(4, 4, 0d);

            bool ok = _worker.Execute(
                "obj.setfont('Meiryo', 40, 3, 0x112233) obj.load('text', 'Hi')",
                fields, NoStringParams, pixels, 4, 4, 5000, NoResolver, NoLoadFigure, loadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw,
                out bool dirty, out bool bufferReplaced, out byte[]? newPixels, out int rw, out int rh, out string? error);

            Assert.True(ok, error);
            Assert.Equal("Meiryo", capturedFamily);
            Assert.Equal("Hi", capturedText);
            Assert.Equal(40d, capturedSize);
            Assert.True(capturedBold);
            Assert.True(capturedItalic);
            Assert.Equal(0x112233, capturedColor);
            Assert.True(dirty);
            Assert.True(bufferReplaced);
            Assert.Equal(tw, rw);
            Assert.Equal(th, rh);
            Assert.NotNull(newPixels);
            Assert.Equal(rendered, newPixels!.AsSpan(0, rendered.Length).ToArray());
        }

        [Fact]
        public void LoadImage_RoundTripsThroughCallback()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            const int iw = 2, ih = 2;
            var decoded = new byte[iw * ih * 4];
            for (int i = 0; i < decoded.Length; i++)
                decoded[i] = (byte)((i * 23 + 9) & 0xFF);

            string? capturedPath = null;
            Func<string, (byte[], int, int)> loadImage = path =>
            {
                capturedPath = path;
                return (decoded, iw, ih);
            };

            var pixels = new byte[4 * 4 * 4];
            var fields = Fields(4, 4, 0d);

            bool ok = _worker.Execute(
                "obj.load('image', 'C:/sample.png')",
                fields, NoStringParams, pixels, 4, 4, 5000, NoResolver, NoLoadFigure, NoLoadText, loadImage, NoLoadMovie, NoAddEffect, NoAddDraw,
                out bool dirty, out bool bufferReplaced, out byte[]? newPixels, out int rw, out int rh, out string? error);

            Assert.True(ok, error);
            Assert.Equal("C:/sample.png", capturedPath);
            Assert.True(dirty);
            Assert.True(bufferReplaced);
            Assert.Equal(iw, rw);
            Assert.Equal(ih, rh);
            Assert.NotNull(newPixels);
            Assert.Equal(decoded, newPixels!.AsSpan(0, decoded.Length).ToArray());
        }

        [Fact]
        public void LoadMovie_RoundTripsPathAndTime()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            const int mw = 2, mh = 2;
            var frame = new byte[mw * mh * 4];
            for (int i = 0; i < frame.Length; i++)
                frame[i] = (byte)((i * 31 + 3) & 0xFF);

            string? capturedPath = null;
            double capturedTime = -1;
            Func<string, double, (byte[], int, int)> loadMovie = (path, time) =>
            {
                capturedPath = path;
                capturedTime = time;
                return (frame, mw, mh);
            };

            var pixels = new byte[4 * 4 * 4];
            var fields = Fields(4, 4, 0d);

            bool ok = _worker.Execute(
                "obj.load('movie', 'C:/clip.mp4', 1.5)",
                fields, NoStringParams, pixels, 4, 4, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, loadMovie, NoAddEffect, NoAddDraw,
                out bool dirty, out bool bufferReplaced, out byte[]? newPixels, out int rw, out int rh, out string? error);

            Assert.True(ok, error);
            Assert.Equal("C:/clip.mp4", capturedPath);
            Assert.Equal(1.5d, capturedTime);
            Assert.True(dirty);
            Assert.True(bufferReplaced);
            Assert.Equal(mw, rw);
            Assert.Equal(mh, rh);
            Assert.NotNull(newPixels);
            Assert.Equal(frame, newPixels!.AsSpan(0, frame.Length).ToArray());
        }

        [Fact]
        public void GetValue_ReturnsObjFields()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            var fields = Fields(2, 2, 0d);
            fields[NativeProtocol.Track0] = 42d;

            bool ok = _worker.Execute(
                "obj.x = obj.getvalue('track0') obj.y = obj.getvalue('alpha') obj.z = obj.getvalue('unknown')",
                fields, NoStringParams, pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw,
                out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.Equal(42d, fields[NativeProtocol.X]);
            Assert.Equal(255d, fields[NativeProtocol.Y]);
            Assert.Equal(0d, fields[NativeProtocol.Z]);
        }

        [Fact]
        public void CopyBuffer_TmpSaveRestore_RoundTripsPixels()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            const int w = 2, h = 2;
            var pixels = new byte[w * h * 4];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = (byte)((i * 37 + 11) & 0xFF);
            pixels[3] = 255; pixels[7] = 255; pixels[11] = 255; pixels[15] = 255;
            var original = (byte[])pixels.Clone();

            var fields = Fields(w, h, 0d);
            const string script =
                "obj.copybuffer('tmp','obj') " +
                "for y=0,obj.h-1 do for x=0,obj.w-1 do obj.setpixel(x,y,0,0,0,0) end end " +
                "obj.copybuffer('obj','tmp')";

            bool ok = _worker.Execute(script, fields, NoStringParams, pixels, w, h, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, out bool dirty, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.True(dirty);
            Assert.Equal(original, pixels);
        }

        [Fact]
        public void CopyBuffer_CachePersistsAcrossRuns()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            const int w = 2, h = 2;
            var stored = new byte[w * h * 4];
            for (int i = 0; i < stored.Length; i++)
                stored[i] = (byte)((i * 53 + 7) & 0xFF);
            stored[3] = 255; stored[7] = 255; stored[11] = 255; stored[15] = 255;

            var save = (byte[])stored.Clone();
            bool ok = _worker.Execute("obj.copybuffer('cache:foo','obj')", Fields(w, h, 0d), NoStringParams, save, w, h, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, out _, out _, out _, out _, out _, out string? error);
            Assert.True(ok, error);

            var blank = new byte[w * h * 4];
            ok = _worker.Execute("obj.copybuffer('obj','cache:foo')", Fields(w, h, 0d), NoStringParams, blank, w, h, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, out bool dirty, out _, out _, out _, out _, out error);
            Assert.True(ok, error);
            Assert.True(dirty);
            Assert.Equal(stored, blank);
        }

        [Fact]
        public void RuntimeError_IsReported_AndWorkerSurvives()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            var fields = Fields(2, 2, 0d);

            bool ok = _worker.Execute("obj.x = missing.value", fields, NoStringParams, pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, out _, out _, out _, out _, out _, out string? error);
            Assert.False(ok);
            Assert.False(string.IsNullOrEmpty(error));

            var fields2 = Fields(2, 2, 7d);
            bool ok2 = _worker.Execute("obj.x = obj.time", fields2, NoStringParams, pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, out _, out _, out _, out _, out _, out error);
            Assert.True(ok2, error);
            Assert.Equal(7d, fields2[NativeProtocol.X]);
        }
    }
}
