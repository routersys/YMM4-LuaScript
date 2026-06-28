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
        private static readonly Action<string, System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, object>>> NoAddEffect = (_, _) => { };

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
                fields, pixels, 4, 4, 5000, NoResolver, NoLoadFigure, NoAddEffect, out bool dirty, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.False(dirty);
            Assert.Equal(180d, fields[NativeProtocol.Rz]);
            Assert.Equal(128d, fields[NativeProtocol.Alpha]);
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

            bool ok = _worker.Execute(script, fields, pixels, w, h, 5000, NoResolver, NoLoadFigure, NoAddEffect, out bool dirty, out _, out _, out _, out _, out string? error);

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

            bool ok = _worker.Execute(script, fields, pixels, w, h, 5000, NoResolver, NoLoadFigure, NoAddEffect, out bool dirty, out _, out _, out _, out _, out string? error);

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
                bool ok = _worker.Execute("obj.x = obj.time * 10", fields, pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoAddEffect, out _, out _, out _, out _, out _, out string? error);
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
                "local x=0 while true do x=x+1 end", fields, pixels, 2, 2, 1500, NoResolver, NoLoadFigure, NoAddEffect, out _, out _, out _, out _, out _, out string? error);
            Assert.False(timedOut);
            Assert.Contains("timed out", error);

            var fields2 = Fields(2, 2, 3d);
            bool ok = _worker.Execute("obj.x = obj.time", fields2, pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoAddEffect, out _, out _, out _, out _, out _, out error);
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

            bool ok = _worker.Execute(script, fields, pixels, 2, 2, 5000, resolver, NoLoadFigure, NoAddEffect, out _, out _, out _, out _, out _, out string? error);

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

            bool ok = _worker.Execute(script, Fields(2, 2, 0d), pixels, 2, 2, 5000, resolver, NoLoadFigure, NoAddEffect, out _, out _, out _, out _, out _, out string? error);
            Assert.True(ok, error);
            Assert.Equal(1, calls);
            var fields = Fields(2, 2, 0d);

            x = 20d;
            ok = _worker.Execute(script, fields, pixels, 2, 2, 5000, resolver, NoLoadFigure, NoAddEffect, out _, out _, out _, out _, out _, out error);
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

            bool ok = _worker.Execute(script, fields, pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoAddEffect, out _, out _, out _, out _, out _, out string? error);

            Assert.True(ok, error);
            Assert.Equal(1d, fields[NativeProtocol.X]);
        }

        [Fact]
        public void RuntimeError_IsReported_AndWorkerSurvives()
        {
            Assert.True(LuaJitWorker.IsAvailable(NativeDir), "native/luajit.exe must be present");

            var pixels = new byte[16];
            var fields = Fields(2, 2, 0d);

            bool ok = _worker.Execute("obj.x = missing.value", fields, pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoAddEffect, out _, out _, out _, out _, out _, out string? error);
            Assert.False(ok);
            Assert.False(string.IsNullOrEmpty(error));

            var fields2 = Fields(2, 2, 7d);
            bool ok2 = _worker.Execute("obj.x = obj.time", fields2, pixels, 2, 2, 5000, NoResolver, NoLoadFigure, NoAddEffect, out _, out _, out _, out _, out _, out error);
            Assert.True(ok2, error);
            Assert.Equal(7d, fields2[NativeProtocol.X]);
        }
    }
}
