using System.Globalization;
using LuaScript.Compat;

namespace LuaScript.Tests
{
    public class AviUtlGlobalEquivalenceTests
    {
        private static readonly double[] s_bitValues =
        [
            0d, 1d, 5d, 8d, 3d, 12d, 6d, 25d, 11d, 91d,
            -1d, -5d, -91d, 255d, 256d, 65535d,
            0.4d, 0.5d, 0.6d, 2.5d, 2.51d, -2.5d, -2.51d,
            2147483647d, -2147483648d, 2147483648d, -2147483649d, 1e18d, -1e18d,
        ];

        private static readonly double[] s_shiftValues =
        [
            0d, 1d, 2d, 3d, 5d, 31d, 32d, 33d, 63d,
            -1d, -2d, -5d, -31d, -32d, -33d, -63d,
            0.5d, 1.5d, -0.5d,
        ];

        [Fact]
        public void BitFunctions_MatchDocumentedExamples()
        {
            var lane = new MoonSharpLane();
            Assert.Equal(11d, lane.Number("OR(8,3)"));
            Assert.Equal(4d, lane.Number("AND(12,6)"));
            Assert.Equal(18d, lane.Number("XOR(25,11)"));
            Assert.Equal(40d, lane.Number("SHIFT(5,3)"));
            Assert.Equal(22d, lane.Number("SHIFT(91,-2)"));
            Assert.Equal(-2147483648d, lane.Number("SHIFT(1,31)"));
            Assert.Equal(2d, lane.Number("OR(2.50,0)"));
            Assert.Equal(3d, lane.Number("OR(2.51,0)"));
        }

        [Fact]
        public void ColorFunctions_MatchDocumentedExamples()
        {
            var lane = new MoonSharpLane();
            Assert.Equal(0x40C0FF, lane.Number("RGB(64,192,255)"));
            Assert.Equal([0d, 128d, 255d], lane.Tuple("RGB(0x0080FF)", 3));
            Assert.Equal(0xFFFF00, lane.Number("HSV(60,100,100)"));
            Assert.Equal([210d, 100d, 100d], lane.Tuple("HSV(0x0080FF)", 3));
        }

        [Fact]
        public void Interpolation_UsesTimeRatio()
        {
            var lane = new MoonSharpLane { TimeRatio = 0.25 };
            Assert.Equal(
                AviUtlGlobalFunctions.RgbInterpolate(64, 192, 255, 128, 64, 0, 0.25),
                lane.Number("RGB(64,192,255,128,64,0)"));
            Assert.Equal(
                AviUtlGlobalFunctions.HsvInterpolate(30, 50, 100, 120, 100, 50, 0.25),
                lane.Number("HSV(30,50,100,120,100,50)"));

            lane.TimeRatio = 0d;
            Assert.Equal(lane.Number("RGB(64,192,255)"), lane.Number("RGB(64,192,255,128,64,0)"));
            lane.TimeRatio = 1d;
            Assert.Equal(lane.Number("RGB(128,64,0)"), lane.Number("RGB(64,192,255,128,64,0)"));
        }

        [Fact]
        public void MoonSharp_MatchesOracle_OverBitMatrix()
        {
            var lane = new MoonSharpLane();
            foreach (var a in s_bitValues)
            {
                foreach (var b in s_bitValues)
                {
                    string sa = Format(a);
                    string sb = Format(b);
                    Assert.Equal(AviUtlGlobalFunctions.BitOr(a, b), lane.Number($"OR({sa},{sb})"));
                    Assert.Equal(AviUtlGlobalFunctions.BitAnd(a, b), lane.Number($"AND({sa},{sb})"));
                    Assert.Equal(AviUtlGlobalFunctions.BitXor(a, b), lane.Number($"XOR({sa},{sb})"));
                }
                foreach (var shift in s_shiftValues)
                {
                    Assert.Equal(
                        AviUtlGlobalFunctions.BitShift(a, shift),
                        lane.Number($"SHIFT({Format(a)},{Format(shift)})"));
                }
            }
        }

        [Fact]
        public void MoonSharp_MatchesOracle_OverColorMatrix()
        {
            var lane = new MoonSharpLane();
            double[] channels = [0d, 1d, 63.5d, 64d, 128d, 254.9d, 255d, 256d, -1d, 1000d];
            foreach (var r in channels)
            {
                foreach (var g in channels)
                {
                    Assert.Equal(
                        AviUtlGlobalFunctions.RgbCompose(r, g, 200d),
                        lane.Number($"RGB({Format(r)},{Format(g)},200)"));
                }
            }

            double[] hues = [0d, 30d, 59.9d, 60d, 60.1d, 120d, 180d, 209.88d, 240d, 300d, 359.9d, 360d, 361d, -30d, 720.5d];
            double[] ratios = [0d, 25d, 50d, 99.5d, 100d, 101d, -5d];
            foreach (var h in hues)
            {
                foreach (var s in ratios)
                {
                    Assert.Equal(
                        AviUtlGlobalFunctions.HsvCompose(h, s, 80d),
                        lane.Number($"HSV({Format(h)},{Format(s)},80)"));
                }
            }

            int[] colors = [0x000000, 0xFFFFFF, 0x0080FF, 0x40C0FF, 0xFF0000, 0x00FF00, 0x0000FF, 0x123456, 0x808080, 0xFFFF00];
            foreach (var color in colors)
            {
                AviUtlGlobalFunctions.RgbComponents(color, out int r, out int g, out int b);
                Assert.Equal([r, g, b], lane.Tuple($"RGB({color})", 3));
                AviUtlGlobalFunctions.HsvComponents(color, out int h, out int s, out int v);
                Assert.Equal([h, s, v], lane.Tuple($"HSV({color})", 3));
            }
        }

        [Fact]
        public void LuaJitShim_MatchesOracle()
        {
            var jit = new LuaJitLane();
            Assert.True(jit.Available, "native/luajit.exe must be present");

            var expressions = new List<string>();
            var expected = new List<double>();

            foreach (var a in s_bitValues)
            {
                foreach (var b in s_bitValues)
                {
                    expressions.Add($"OR({Format(a)},{Format(b)})");
                    expected.Add(AviUtlGlobalFunctions.BitOr(a, b));
                    expressions.Add($"AND({Format(a)},{Format(b)})");
                    expected.Add(AviUtlGlobalFunctions.BitAnd(a, b));
                    expressions.Add($"XOR({Format(a)},{Format(b)})");
                    expected.Add(AviUtlGlobalFunctions.BitXor(a, b));
                }
                foreach (var shift in s_shiftValues)
                {
                    expressions.Add($"SHIFT({Format(a)},{Format(shift)})");
                    expected.Add(AviUtlGlobalFunctions.BitShift(a, shift));
                }
            }

            double[] channels = [0d, 1d, 63.5d, 64d, 128d, 254.9d, 255d, 256d, -1d, 1000d];
            foreach (var r in channels)
            {
                expressions.Add($"RGB({Format(r)},192,255)");
                expected.Add(AviUtlGlobalFunctions.RgbCompose(r, 192d, 255d));
            }

            double[] hues = [0d, 30d, 59.9d, 60d, 60.1d, 120d, 180d, 209.88d, 240d, 300d, 359.9d, 360d, 361d, -30d, 720.5d];
            foreach (var h in hues)
            {
                expressions.Add($"HSV({Format(h)},100,100)");
                expected.Add(AviUtlGlobalFunctions.HsvCompose(h, 100d, 100d));
            }

            int[] colors = [0x000000, 0xFFFFFF, 0x0080FF, 0x40C0FF, 0xFF0000, 0x00FF00, 0x0000FF, 0x123456, 0x808080, 0xFFFF00];
            foreach (var color in colors)
            {
                for (int component = 1; component <= 3; component++)
                {
                    expressions.Add($"select({component}, RGB({color}))");
                    AviUtlGlobalFunctions.RgbComponents(color, out int r, out int g, out int b);
                    expected.Add(component == 1 ? r : component == 2 ? g : b);
                    expressions.Add($"select({component}, HSV({color}))");
                    AviUtlGlobalFunctions.HsvComponents(color, out int h, out int s, out int v);
                    expected.Add(component == 1 ? h : component == 2 ? s : v);
                }
            }

            expressions.Add("(function() aviutl_set_time_ratio(0.25) return RGB(64,192,255,128,64,0) end)()");
            expected.Add(AviUtlGlobalFunctions.RgbInterpolate(64, 192, 255, 128, 64, 0, 0.25));
            expressions.Add("(function() aviutl_set_time_ratio(0.75) return HSV(30,50,100,120,100,50) end)()");
            expected.Add(AviUtlGlobalFunctions.HsvInterpolate(30, 50, 100, 120, 100, 50, 0.75));

            var results = jit.Eval(expressions);
            Assert.True(results.Length >= expressions.Count, "luajit runner returned fewer results than expressions");
            for (int i = 0; i < expressions.Count; i++)
            {
                double actual = double.Parse(results[i], CultureInfo.InvariantCulture);
                Assert.True(expected[i] == actual, $"{expressions[i]}: expected {expected[i]}, got {actual}");
            }
        }

        private static string Format(double value) =>
            value.ToString("R", CultureInfo.InvariantCulture);
    }
}
