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
        private static readonly Action<string, int, bool, int, double[]> NoSetAnchor = (_, _, _, _, _) => { };
        private static readonly System.Collections.Generic.IReadOnlyDictionary<string, string> NoStringParams = new System.Collections.Generic.Dictionary<string, string>();

        private readonly LuaJitWorker _worker = new(NativeDir);

        private Func<string, SceneValue> _sceneGet = _ => SceneValue.Nil;
        private Action<string, SceneValue> _sceneSet = (_, _) => { };

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

        private bool RunWorker(
            string script,
            double[] fields,
            System.Collections.Generic.IReadOnlyDictionary<string, string> stringParameters,
            Func<byte[]> loadPixels,
            int width,
            int height,
            int timeoutMs,
            Func<string, int, SceneObjectInfo?> resolveObject,
            Func<string, int, double, double, double, (byte[] buffer, int w, int h)> loadFigure,
            Func<string, string, double, bool, bool, int, (byte[] buffer, int w, int h)> loadText,
            Func<string, (byte[] buffer, int w, int h)> loadImage,
            Func<string, double, (byte[] buffer, int w, int h)> loadMovie,
            Action<string, System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, object>>> addEffect,
            Action<DrawCommand> addDraw,
            Action<string, int, bool, int, double[]> setAnchor,
            out bool pixelsDirty,
            out bool bufferReplaced,
            out byte[]? resultPixels,
            out int resultWidth,
            out int resultHeight,
            out string? error)
        {
            byte[]? uploaded = null;
            bool ok = _worker.Execute(
                script, fields, stringParameters,
                (address, capacity) =>
                {
                    uploaded = loadPixels();
                    System.Runtime.InteropServices.Marshal.Copy(uploaded, 0, address, uploaded.Length);
                },
                width, height, timeoutMs,
                resolveObject, loadFigure, loadText, loadImage, loadMovie, addEffect, addDraw, setAnchor,
                _sceneGet, _sceneSet,
                out pixelsDirty, out bufferReplaced, out resultWidth, out resultHeight, out error);

            resultPixels = null;
            if (ok && pixelsDirty)
            {
                int pixelSize = resultWidth * resultHeight * 4;
                byte[] destination = !bufferReplaced && uploaded is not null && uploaded.Length >= pixelSize
                    ? uploaded
                    : new byte[pixelSize];
                _worker.ReadPixels((address, capacity) =>
                    System.Runtime.InteropServices.Marshal.Copy(address, destination, 0, pixelSize));
                resultPixels = destination;
            }
            return ok;
        }

        [Fact]
        public void PixelUpload_ProvidesRegionCoveringFrame()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            const int w = 3, h = 2;
            var pixels = new byte[w * h * 4];
            long observedCapacity = -1;

            bool ok = _worker.Execute(
                "local r, g, b, a = obj.getpixel(0, 0) obj.x = a",
                Fields(w, h, 0d), NoStringParams,
                (address, capacity) =>
                {
                    observedCapacity = capacity;
                    System.Runtime.InteropServices.Marshal.Copy(pixels, 0, address, pixels.Length);
                },
                w, h, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor,
                _sceneGet, _sceneSet,
                out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.True(observedCapacity >= (long)w * h * 4);
        }

        [Fact]
        public void TransformScript_WritesBackObjFields()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var fields = Fields(4, 4, 2d);
            var pixels = new byte[4 * 4 * 4];

            bool ok = RunWorker(
                "obj.rz = obj.time * 90\nobj.alpha = 128",
                fields, NoStringParams, () => pixels, 4, 4, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out bool dirty, out _, out _, out _, out _, out string? error);

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

            bool ok = RunWorker(
                "obj.x = string.len(obj.text) obj.alpha = string.len(obj.file_image)",
                fields, stringParameters, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.Equal(5d, fields[NativeProtocol.X]);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount("C:/サンプル/画像.png"), fields[NativeProtocol.Alpha]);
        }

        [Fact]
        public void StringParameters_GrowAndShrinkAroundCapacity()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            string big = new string('x', 100 * 1024);

            var large = new System.Collections.Generic.Dictionary<string, string> { ["text"] = big };
            var fieldsLarge = Fields(2, 2, 0d);
            bool ok = RunWorker(
                "obj.x = string.len(obj.text)",
                fieldsLarge, large, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? error);
            Assert.True(ok, error);
            Assert.Equal(big.Length, fieldsLarge[NativeProtocol.X]);

            var small = new System.Collections.Generic.Dictionary<string, string> { ["text"] = "ok" };
            var fieldsSmall = Fields(2, 2, 0d);
            ok = RunWorker(
                "obj.x = string.len(obj.text)",
                fieldsSmall, small, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out error);
            Assert.True(ok, error);
            Assert.Equal(2d, fieldsSmall[NativeProtocol.X]);
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

            bool ok = RunWorker(script, fields, NoStringParams, () => pixels, w, h, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out bool dirty, out _, out _, out _, out _, out string? error);

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

            bool ok = RunWorker(script, fields, NoStringParams, () => pixels, w, h, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out bool dirty, out _, out _, out _, out _, out string? error);

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
        public void PixelData_RebuildsAfterSetPixel()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            var fields = Fields(2, 2, 0d);
            const string script =
                "obj.setpixel(0,0,100,150,200,255) " +
                "local pd = obj.getpixeldata() " +
                "obj.x = pd:get(1) obj.y = pd:get(2) obj.z = pd:get(3) obj.alpha = pd:get(4)";

            bool ok = RunWorker(script, fields, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.Equal(100d, fields[NativeProtocol.X]);
            Assert.Equal(150d, fields[NativeProtocol.Y]);
            Assert.Equal(200d, fields[NativeProtocol.Z]);
            Assert.Equal(255d, fields[NativeProtocol.Alpha]);
        }

        [Fact]
        public void PixelData_WritesAreVisibleToGetPixel()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            pixels[3] = 255;
            var fields = Fields(2, 2, 0d);
            const string script =
                "local pd = obj.getpixeldata() " +
                "pd:set(1,120) pd:set(2,130) pd:set(3,140) " +
                "local r,g,b,a = obj.getpixel(0,0) " +
                "obj.x = r obj.y = g obj.z = b obj.alpha = a";

            bool ok = RunWorker(script, fields, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out bool dirty, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.True(dirty);
            Assert.Equal(120d, fields[NativeProtocol.X]);
            Assert.Equal(130d, fields[NativeProtocol.Y]);
            Assert.Equal(140d, fields[NativeProtocol.Z]);
            Assert.Equal(255d, fields[NativeProtocol.Alpha]);
        }

        [Fact]
        public void PixelData_StaleWritesDoNotResurrect_AfterLoad()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            const int iw = 2, ih = 2;
            var decoded = new byte[iw * ih * 4];
            for (int i = 0; i < decoded.Length; i++)
                decoded[i] = (byte)((i * 19 + 7) & 0xFF);
            Func<string, (byte[], int, int)> loadImage = _ => (decoded, iw, ih);

            var pixels = new byte[4 * 4 * 4];
            for (int i = 3; i < pixels.Length; i += 4)
                pixels[i] = 255;

            const string script =
                "local pd = obj.getpixeldata() " +
                "pd:set(1, 11) pd:set(2, 22) pd:set(3, 33) " +
                "obj.load('image', 'p')";

            bool ok = RunWorker(script, Fields(4, 4, 0d), NoStringParams, () => pixels, 4, 4, 5000, NoResolver, NoLoadFigure, NoLoadText, loadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor,
                out bool dirty, out bool bufferReplaced, out byte[]? result, out int rw, out int rh, out string? error);

            Assert.True(ok, error);
            Assert.True(dirty);
            Assert.True(bufferReplaced);
            Assert.Equal(iw, rw);
            Assert.Equal(ih, rh);
            Assert.NotNull(result);
            Assert.Equal(decoded, result!.AsSpan(0, decoded.Length).ToArray());
        }

        [Fact]
        public void PixelData_StaleWritesDoNotResurrect_AfterCopyBuffer()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            const int w = 2, h = 2;
            var pixels = new byte[w * h * 4];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = (byte)((i * 29 + 3) & 0xFF);
            pixels[3] = 255; pixels[7] = 255; pixels[11] = 255; pixels[15] = 255;
            var original = (byte[])pixels.Clone();

            const string script =
                "obj.copybuffer('tmp','obj') " +
                "local pd = obj.getpixeldata() " +
                "pd:set(1, 99) pd:set(2, 98) pd:set(3, 97) " +
                "obj.copybuffer('obj','tmp')";

            bool ok = RunWorker(script, Fields(w, h, 0d), NoStringParams, () => pixels, w, h, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor,
                out bool dirty, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.True(dirty);
            Assert.Equal(original, pixels);
        }

        [Fact]
        public void PersistentWorker_HandlesMultipleCalls()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            for (int k = 1; k <= 5; k++)
            {
                var fields = Fields(2, 2, k);
                bool ok = RunWorker("obj.x = obj.time * 10", fields, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? error);
                Assert.True(ok, error);
                Assert.Equal(k * 10d, fields[NativeProtocol.X]);
            }
        }

        [Fact]
        public void AlternatingScripts_RecompileWhenVersionChanges()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            const string scriptA = "obj.x = obj.time * 2";
            const string scriptB = "obj.x = obj.time * 3";

            for (int k = 1; k <= 4; k++)
            {
                var fa = Fields(2, 2, k);
                bool okA = RunWorker(scriptA, fa, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? errorA);
                Assert.True(okA, errorA);
                Assert.Equal(k * 2d, fa[NativeProtocol.X]);

                var fb = Fields(2, 2, k);
                bool okB = RunWorker(scriptB, fb, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? errorB);
                Assert.True(okB, errorB);
                Assert.Equal(k * 3d, fb[NativeProtocol.X]);
            }
        }

        [Fact]
        public void CompileError_IsReportedEveryFrame_AndRecovers()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            const string broken = "obj.x = (";

            for (int k = 0; k < 2; k++)
            {
                bool ok = RunWorker(broken, Fields(2, 2, 0d), NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? error);
                Assert.False(ok);
                Assert.False(string.IsNullOrEmpty(error));
                Assert.DoesNotContain("timed out", error);
            }

            var fields = Fields(2, 2, 9d);
            bool recovered = RunWorker("obj.x = obj.time", fields, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? recoverError);
            Assert.True(recovered, recoverError);
            Assert.Equal(9d, fields[NativeProtocol.X]);
        }

        [Fact]
        public void Timeout_KillsWorker_AndRecovers()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            var fields = Fields(2, 2, 0d);

            bool timedOut = RunWorker(
                "local x=0 while true do x=x+1 end", fields, NoStringParams, () => pixels, 2, 2, 1500, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? error);
            Assert.False(timedOut);
            Assert.Contains("timed out", error);

            var fields2 = Fields(2, 2, 3d);
            bool ok = RunWorker("obj.x = obj.time", fields2, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out error);
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

            bool ok = RunWorker(script, fields, NoStringParams, () => pixels, 2, 2, 5000, resolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? error);

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

            bool ok = RunWorker(script, Fields(2, 2, 0d), NoStringParams, () => pixels, 2, 2, 5000, resolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? error);
            Assert.True(ok, error);
            Assert.Equal(1, calls);
            var fields = Fields(2, 2, 0d);

            x = 20d;
            ok = RunWorker(script, fields, NoStringParams, () => pixels, 2, 2, 5000, resolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out error);
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

            bool ok = RunWorker(script, fields, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? error);

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

            bool ok = RunWorker(
                "obj.draw() obj.draw(10, 20, 30, 2, 0.5, 0.25)",
                fields, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, addDraw, NoSetAnchor,
                out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.Equal(2, commands.Count);
            Assert.Equal(new DrawCommand(0d, 0d, 0d, 1d, 1d, 0d), commands[0]);
            Assert.Equal(new DrawCommand(10d, 20d, 30d, 2d, 0.5d, 0.25d), commands[1]);
        }

        [Fact]
        public void Draw_BatchesInOrderAcrossRingOverflow()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            var commands = new System.Collections.Generic.List<DrawCommand>();
            Action<DrawCommand> addDraw = commands.Add;

            int total = NativeProtocol.DrawRingCapacity + 100;
            string script = "for i=1," + total + " do obj.draw(i,0,0,1,1,0) end";

            bool ok = RunWorker(
                script, Fields(2, 2, 0d), NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, addDraw, NoSetAnchor,
                out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.Equal(total, commands.Count);
            for (int i = 0; i < total; i++)
                Assert.Equal(i + 1, commands[i].Ox);
        }

        [Fact]
        public void DrawPoly_RecordsDefaultUvAndAlpha()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[4 * 4 * 4];
            var fields = Fields(4, 4, 0d);
            var commands = new System.Collections.Generic.List<DrawCommand>();
            Action<DrawCommand> addDraw = commands.Add;

            bool ok = RunWorker(
                "obj.drawpoly(0,0,0, 10,0,0, 10,10,0, 0,10,0)",
                fields, NoStringParams, () => pixels, 4, 4, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, addDraw, NoSetAnchor,
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

            bool ok = RunWorker(
                "obj.drawpoly(0,0,0, 8,0,0, 8,8,0, 0,8,0, 1,1, 3,1, 3,3, 1,3, 0.5)",
                fields, NoStringParams, () => pixels, 4, 4, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, addDraw, NoSetAnchor,
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

            bool ok = RunWorker(
                "obj.setoption('antialias', 0) obj.x = obj.getoption('antialias') obj.y = obj.getoption('unset')",
                fields, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor,
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

            bool ok = RunWorker(
                "obj.draw() obj.setoption('antialias', 0) obj.draw()",
                Fields(2, 2, 0d), NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, addDraw, NoSetAnchor,
                out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.Equal(2, commands.Count);
            Assert.Equal(1d, commands[0].Antialias);
            Assert.Equal(0d, commands[1].Antialias);
        }

        [Fact]
        public void Draw_CarriesBlendFromOption()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            var commands = new System.Collections.Generic.List<DrawCommand>();
            Action<DrawCommand> addDraw = commands.Add;

            bool ok = RunWorker(
                "obj.draw() obj.setoption('blend', 3) obj.draw()",
                Fields(2, 2, 0d), NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, addDraw, NoSetAnchor,
                out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.Equal(2, commands.Count);
            Assert.Equal(0d, commands[0].Blend);
            Assert.Equal(3d, commands[1].Blend);
        }

        [Fact]
        public void DrawPoly_CarriesBlendFromOption()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[4 * 4 * 4];
            var commands = new System.Collections.Generic.List<DrawCommand>();
            Action<DrawCommand> addDraw = commands.Add;

            bool ok = RunWorker(
                "obj.setoption('blend', 11) obj.drawpoly(0,0,0, 10,0,0, 10,10,0, 0,10,0)",
                Fields(4, 4, 0d), NoStringParams, () => pixels, 4, 4, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, addDraw, NoSetAnchor,
                out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.Single(commands);
            Assert.Equal(11d, commands[0].Blend);
        }

        [Fact]
        public void SetOption_DrawState_WritesBackFlag()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];

            var fTrue = Fields(2, 2, 0d);
            bool okTrue = RunWorker(
                "obj.setoption('draw_state', true)",
                fTrue, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor,
                out _, out _, out _, out _, out _, out string? errorTrue);
            Assert.True(okTrue, errorTrue);
            Assert.Equal(1d, fTrue[NativeProtocol.DrawState]);

            var fFalse = Fields(2, 2, 0d);
            bool okFalse = RunWorker(
                "obj.setoption('draw_state', false)",
                fFalse, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor,
                out _, out _, out _, out _, out _, out string? errorFalse);
            Assert.True(okFalse, errorFalse);
            Assert.Equal(2d, fFalse[NativeProtocol.DrawState]);

            var fUnset = Fields(2, 2, 0d);
            bool okUnset = RunWorker(
                "obj.x = 1",
                fUnset, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor,
                out _, out _, out _, out _, out _, out string? errorUnset);
            Assert.True(okUnset, errorUnset);
            Assert.Equal(0d, fUnset[NativeProtocol.DrawState]);
        }

        [Fact]
        public void SetAnchor_FillsLuaVariableFromResolver()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            var fields = Fields(2, 2, 0d);

            string? capturedGroup = null;
            int capturedCount = -1;
            Action<string, int, bool, int, double[]> setAnchor = (group, count, is3D, connection, positions) =>
            {
                capturedGroup = group;
                capturedCount = count;
                int stride = is3D ? 3 : 2;
                for (int i = 0; i < count; i++)
                {
                    positions[i * stride + 0] = i * 10;
                    positions[i * stride + 1] = i * 10 + 5;
                    if (is3D)
                        positions[i * stride + 2] = 0;
                }
            };

            string script =
                "local n = obj.setanchor('pos', 2, 'loop')\n" +
                "obj.x = n\n" +
                "obj.y = pos[1]\n" +
                "obj.z = pos[3]";

            bool ok = RunWorker(script, fields, NoStringParams, () => pixels, 2, 2, 5000,
                NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, setAnchor,
                out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.Equal("pos", capturedGroup);
            Assert.Equal(2, capturedCount);
            Assert.Equal(2d, fields[NativeProtocol.X]);
            Assert.Equal(0d, fields[NativeProtocol.Y]);
            Assert.Equal(10d, fields[NativeProtocol.Z]);
        }

        [Fact]
        public void DrawTarget_TempBuffer_CompositesAndCopiesBack()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[2 * 2 * 4];
            var fields = Fields(2, 2, 0d);

            string script =
                "obj.setpixel(0,0,255,0,0,255)\n" +
                "obj.setpixel(1,0,0,255,0,255)\n" +
                "obj.setpixel(0,1,0,0,255,255)\n" +
                "obj.setpixel(1,1,255,255,0,255)\n" +
                "obj.setoption('drawtarget','tempbuffer',2,2)\n" +
                "obj.draw(1,1,0,1,1,0)\n" +
                "obj.setoption('drawtarget','framebuffer')\n" +
                "obj.copybuffer('obj','tmp')";

            bool ok = RunWorker(
                script, fields, NoStringParams, () => pixels, 2, 2, 5000,
                NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor,
                out bool dirty, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.True(dirty);

            Assert.Equal(0, pixels[0]);
            Assert.Equal(0, pixels[1]);
            Assert.Equal(255, pixels[2]);
            Assert.Equal(255, pixels[3]);

            int i11 = (1 * 2 + 1) * 4;
            Assert.Equal(0, pixels[i11]);
            Assert.Equal(255, pixels[i11 + 1]);
            Assert.Equal(255, pixels[i11 + 2]);
            Assert.Equal(255, pixels[i11 + 3]);
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

            bool ok = RunWorker(
                "obj.setfont('Meiryo', 40, 3, 0x112233) obj.load('text', 'Hi')",
                fields, NoStringParams, () => pixels, 4, 4, 5000, NoResolver, NoLoadFigure, loadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor,
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

            bool ok = RunWorker(
                "obj.load('image', 'C:/sample.png')",
                fields, NoStringParams, () => pixels, 4, 4, 5000, NoResolver, NoLoadFigure, NoLoadText, loadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor,
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

            bool ok = RunWorker(
                "obj.load('movie', 'C:/clip.mp4', 1.5)",
                fields, NoStringParams, () => pixels, 4, 4, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, loadMovie, NoAddEffect, NoAddDraw, NoSetAnchor,
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

            bool ok = RunWorker(
                "obj.x = obj.getvalue('track0') obj.y = obj.getvalue('alpha') obj.z = obj.getvalue('unknown')",
                fields, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor,
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

            bool ok = RunWorker(script, fields, NoStringParams, () => pixels, w, h, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out bool dirty, out _, out _, out _, out _, out string? error);

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
            bool ok = RunWorker("obj.copybuffer('cache:foo','obj')", Fields(w, h, 0d), NoStringParams, () => save, w, h, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? error);
            Assert.True(ok, error);

            var blank = new byte[w * h * 4];
            ok = RunWorker("obj.copybuffer('obj','cache:foo')", Fields(w, h, 0d), NoStringParams, () => blank, w, h, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out bool dirty, out _, out byte[]? restored, out _, out _, out error);
            Assert.True(ok, error);
            Assert.True(dirty);
            Assert.NotNull(restored);
            Assert.Equal(stored, restored!.AsSpan(0, stored.Length).ToArray());
        }

        [Fact]
        public void TransformScript_NeverRequestsPixels()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            int loads = 0;
            Func<byte[]> loadPixels = () => { loads++; return pixels; };

            var fields = Fields(2, 2, 1d);
            bool ok = RunWorker(
                "obj.x = obj.time * 10 obj.draw()",
                fields, NoStringParams, loadPixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor,
                out bool dirty, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.False(dirty);
            Assert.Equal(0, loads);
            Assert.Equal(10d, fields[NativeProtocol.X]);
        }

        [Fact]
        public void PixelScript_RequestsPixelsExactlyOnce()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            pixels[3] = 255; pixels[7] = 255; pixels[11] = 255; pixels[15] = 255;
            int loads = 0;
            Func<byte[]> loadPixels = () => { loads++; return pixels; };

            bool ok = RunWorker(
                "for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a = obj.getpixel(x,y) obj.setpixel(x,y,r,g,b,a) end end",
                Fields(2, 2, 0d), NoStringParams, loadPixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor,
                out bool dirty, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.True(dirty);
            Assert.Equal(1, loads);
        }

        [Fact]
        public void Worker_SurvivesSizeChanges_PreservingCache()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var stored = new byte[2 * 2 * 4];
            for (int i = 0; i < stored.Length; i++)
                stored[i] = (byte)((i * 41 + 13) & 0xFF);
            stored[3] = 255; stored[7] = 255; stored[11] = 255; stored[15] = 255;

            bool ok = RunWorker("obj.copybuffer('cache:sz','obj')", Fields(2, 2, 0d), NoStringParams, () => stored, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? error);
            Assert.True(ok, error);

            var bigger = new byte[4 * 4 * 4];
            ok = RunWorker("obj.copybuffer('obj','cache:sz')", Fields(4, 4, 0d), NoStringParams, () => bigger, 4, 4, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out bool dirty, out bool bufferReplaced, out byte[]? restored, out int rw, out int rh, out error);
            Assert.True(ok, error);
            Assert.True(dirty);
            Assert.True(bufferReplaced);
            Assert.Equal(2, rw);
            Assert.Equal(2, rh);
            Assert.NotNull(restored);
            Assert.Equal(stored, restored!.AsSpan(0, stored.Length).ToArray());
        }

        [Fact]
        public void BareGlobals_MirrorObjFields()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            var fields = Fields(2, 2, 2d);
            fields[NativeProtocol.Frame] = 10d;
            fields[NativeProtocol.TotalFrame] = 20d;
            fields[NativeProtocol.Framerate] = 30d;
            fields[NativeProtocol.Layer] = 3d;
            fields[NativeProtocol.TimelineFrame] = 100d;
            fields[NativeProtocol.TimelineTime] = 4d;

            const string script =
                "obj.x = time * 10 + frame " +
                "obj.y = totalframe + framerate + layer " +
                "obj.z = timelineframe + timelinetime";

            bool ok = RunWorker(script, fields, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.Equal(30d, fields[NativeProtocol.X]);
            Assert.Equal(53d, fields[NativeProtocol.Y]);
            Assert.Equal(104d, fields[NativeProtocol.Z]);
        }

        [Fact]
        public void UserGlobals_AreResetBetweenRuns()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];

            bool ok = RunWorker(
                "leak = 42 obj.leaked = 7 scene.custom = 1 ymm4.custom = 2",
                Fields(2, 2, 0d), NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? error);
            Assert.True(ok, error);

            var fields = Fields(2, 2, 0d);
            ok = RunWorker(
                "obj.x = (leak == nil and 1 or 0) + (obj.leaked == nil and 10 or 0) + (scene.custom == nil and 100 or 0) + (ymm4.custom == nil and 1000 or 0)",
                fields, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out error);
            Assert.True(ok, error);
            Assert.Equal(1111d, fields[NativeProtocol.X]);
        }

        private Dictionary<string, SceneValue> BindSceneValues()
        {
            var values = new Dictionary<string, SceneValue>(StringComparer.Ordinal);
            _sceneGet = name => values.TryGetValue(name, out var value) ? value : SceneValue.Nil;
            _sceneSet = (name, value) =>
            {
                if (value.Kind == SceneValueKind.Nil)
                    values.Remove(name);
                else
                    values[name] = value;
            };
            return values;
        }

        [Fact]
        public void SceneSet_RoundTripsAllValueKinds()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var values = BindSceneValues();

            var pixels = new byte[16];
            const string script =
                "scene.set('num', 12.5) " +
                "scene.set('text', 'こんにちは') " +
                "scene.set('flag', true) " +
                "scene.set('gone', 1) scene.set('gone', nil) " +
                "scene.set(1, 'ignored') " +
                "scene.set('table', {})";

            bool ok = RunWorker(script, Fields(2, 2, 0d), NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.Equal(SceneValue.FromNumber(12.5), values["num"]);
            Assert.Equal(SceneValue.FromString("こんにちは"), values["text"]);
            Assert.Equal(SceneValue.FromBoolean(true), values["flag"]);
            Assert.False(values.ContainsKey("gone"));
            Assert.False(values.ContainsKey("table"));
        }

        [Fact]
        public void SceneGet_ReturnsAllValueKinds()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var values = BindSceneValues();
            values["num"] = SceneValue.FromNumber(-3.25);
            values["text"] = SceneValue.FromString("値テキスト");
            values["flag"] = SceneValue.FromBoolean(true);

            var pixels = new byte[16];
            var fields = Fields(2, 2, 0d);
            const string script =
                "obj.x = scene.get('num') " +
                "obj.y = string.len(scene.get('text')) " +
                "obj.z = scene.get('flag') == true and 1 or 0 " +
                "obj.alpha = (scene.get('missing') == nil and 1 or 0) + (scene.get(7) == nil and 10 or 0)";

            bool ok = RunWorker(script, fields, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.Equal(-3.25d, fields[NativeProtocol.X]);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount("値テキスト"), fields[NativeProtocol.Y]);
            Assert.Equal(1d, fields[NativeProtocol.Z]);
            Assert.Equal(11d, fields[NativeProtocol.Alpha]);
        }

        [Fact]
        public void SceneValues_RoundTripAcrossWorkerRuns()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            BindSceneValues();

            var pixels = new byte[16];
            var fields = Fields(2, 2, 0d);
            const string script =
                "scene.set('counter', (scene.get('counter') or 0) + 1) " +
                "obj.x = scene.get('counter')";

            for (int i = 1; i <= 3; i++)
            {
                bool ok = RunWorker(script, fields, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? error);
                Assert.True(ok, error);
                Assert.Equal(i, fields[NativeProtocol.X]);
            }
        }

        [Fact]
        public void SceneGet_CachesRepeatedReadsWithinRun_AndResetsPerRun()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            int hostReads = 0;
            _sceneGet = _ => { hostReads++; return SceneValue.FromNumber(42d); };

            var pixels = new byte[16];
            const string script =
                "local total = 0 " +
                "for i = 1, 100 do total = total + scene.get('key') end " +
                "obj.x = total";

            for (int run = 1; run <= 2; run++)
            {
                var fields = Fields(2, 2, 0d);
                bool ok = RunWorker(script, fields, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? error);
                Assert.True(ok, error);
                Assert.Equal(4200d, fields[NativeProtocol.X]);
                Assert.Equal(run, hostReads);
            }
        }

        [Fact]
        public void SceneSet_IsReadBackWithoutHostRead()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            int hostReads = 0;
            _sceneGet = _ => { hostReads++; return SceneValue.Nil; };

            var pixels = new byte[16];
            var fields = Fields(2, 2, 0d);
            const string script =
                "scene.set('key', 7) " +
                "obj.x = scene.get('key') " +
                "scene.set('key', nil) " +
                "obj.y = scene.get('key') == nil and 1 or 0";

            bool ok = RunWorker(script, fields, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.Equal(0, hostReads);
            Assert.Equal(7d, fields[NativeProtocol.X]);
            Assert.Equal(1d, fields[NativeProtocol.Y]);
        }

        [Fact]
        public void SceneStrings_CarryMaximumLengthNameAndValue()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var values = BindSceneValues();
            string longName = new string('n', SceneSharedValues.MaxNameBytes);
            string longValue = new string('v', SceneSharedValues.MaxTextBytes);
            values[longName] = SceneValue.FromString(longValue);

            var pixels = new byte[16];
            var fields = Fields(2, 2, 0d);
            string script =
                "local name = string.rep('n', " + SceneSharedValues.MaxNameBytes + ") " +
                "obj.x = string.len(scene.get(name)) " +
                "scene.set(name, string.rep('w', " + (SceneSharedValues.MaxTextBytes + 500) + ")) " +
                "obj.y = string.len(scene.get(name))";

            bool ok = RunWorker(script, fields, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.Equal(SceneSharedValues.MaxTextBytes, fields[NativeProtocol.X]);
            Assert.Equal(SceneSharedValues.MaxTextBytes, fields[NativeProtocol.Y]);
            Assert.Equal(SceneSharedValues.MaxTextBytes, values[longName].Text!.Length);
            Assert.True(values[longName].Text!.All(c => c == 'w'));
        }

        [Fact]
        public void SceneStrings_TruncateAtUtf8Boundary()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var values = BindSceneValues();

            var pixels = new byte[16];
            var fields = Fields(2, 2, 0d);
            string script =
                "scene.set('key', string.rep('あ', 2000)) " +
                "obj.x = string.len(scene.get('key'))";

            bool ok = RunWorker(script, fields, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            int expectedBytes = SceneSharedValues.MaxTextBytes / 3 * 3;
            Assert.Equal(expectedBytes, fields[NativeProtocol.X]);
            var stored = values["key"];
            Assert.Equal(SceneValueKind.String, stored.Kind);
            Assert.Equal(expectedBytes / 3, stored.Text!.Length);
            Assert.True(stored.Text!.All(c => c == 'あ'));
        }

        [Fact]
        public void GlobalFunctions_MatchOracleInsideWorker()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            var fields = Fields(2, 2, 0d);
            const string script =
                "obj.x = OR(8, 3) + AND(12, 6) + XOR(25, 11) " +
                "obj.y = SHIFT(5, 3) + SHIFT(91, -2) " +
                "obj.z = RGB(64, 192, 255) " +
                "obj.alpha = HSV(60, 100, 100)";

            bool ok = RunWorker(script, fields, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.Equal(11d + 4d + 18d, fields[NativeProtocol.X]);
            Assert.Equal(40d + 22d, fields[NativeProtocol.Y]);
            Assert.Equal(0x40C0FF, fields[NativeProtocol.Z]);
            Assert.Equal(0xFFFF00, fields[NativeProtocol.Alpha]);
        }

        [Fact]
        public void RgbInterpolation_UsesObjectTimeRatio()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            var fields = Fields(2, 2, 1d);
            fields[NativeProtocol.TotalTime] = 4d;

            bool ok = RunWorker(
                "obj.x = RGB(0, 0, 0, 100, 200, 40)",
                fields, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.Equal(Compat.AviUtlGlobalFunctions.RgbInterpolate(0, 0, 0, 100, 200, 40, 0.25), fields[NativeProtocol.X]);
        }

        [Fact]
        public void RuntimeError_IsReported_AndWorkerSurvives()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            var fields = Fields(2, 2, 0d);

            bool ok = RunWorker("obj.x = missing.value", fields, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out string? error);
            Assert.False(ok);
            Assert.False(string.IsNullOrEmpty(error));

            var fields2 = Fields(2, 2, 7d);
            bool ok2 = RunWorker("obj.x = obj.time", fields2, NoStringParams, () => pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoLoadText, NoLoadImage, NoLoadMovie, NoAddEffect, NoAddDraw, NoSetAnchor, out _, out _, out _, out _, out _, out error);
            Assert.True(ok2, error);
            Assert.Equal(7d, fields2[NativeProtocol.X]);
        }
    }
}
