using System;
using System.Collections.Generic;
using System.Globalization;

namespace LuaScript.Tests
{
    public class NativeAnimEquivalenceTests
    {
        private readonly MoonSharpLane _oracle = new();

        [Fact]
        public void Anim_MatchesBetweenMoonSharpAndLuaJit()
        {
            var lane = new LuaJitLane();
            Assert.True(lane.Available, "native/luajit.exe must be present");

            var exprs = new List<string>();
            void Add(string e) => exprs.Add(e);
            void Tuple(string call, int n)
            {
                for (int k = 1; k <= n; k++)
                    exprs.Add($"({{{call}}})[{k}]");
            }

            Add("anim.tau"); Add("anim.e"); Add("anim.phi"); Add("anim.sqrt2");

            foreach (var (a, b, t) in new[] { (0d, 10d, 0.5d), (10d, 20d, 0.25d), (-5d, 5d, 0.75d), (100d, -100d, 0.1d) })
                Add($"anim.lerp({L(a)},{L(b)},{L(t)})");

            foreach (var (e0, e1, x) in new[] { (0d, 1d, 0.5d), (0d, 1d, 0.3d), (2d, 4d, 3d), (0d, 1d, -1d), (0d, 1d, 2d) })
            {
                Add($"anim.smoothstep({L(e0)},{L(e1)},{L(x)})");
                Add($"anim.smootherstep({L(e0)},{L(e1)},{L(x)})");
            }

            foreach (var (v, lo, hi) in new[] { (5d, 0d, 1d), (-1d, 0d, 1d), (0.5d, 0d, 1d), (7d, 2d, 9d) })
            {
                Add($"anim.clamp({L(v)},{L(lo)},{L(hi)})");
                Add($"anim.norm({L(v)},{L(lo)},{L(hi)})");
            }

            Add("anim.map(5,0,10,0,100)"); Add("anim.map(0.5,0,1,-1,1)");

            foreach (var (v, lo, hi) in new[] { (7d, 0d, 5d), (-1d, 0d, 5d), (12d, 2d, 8d), (-7d, -3d, 3d) })
                Add($"anim.wrap({L(v)},{L(lo)},{L(hi)})");

            foreach (var (t, len) in new[] { (7d, 5d), (3d, 5d), (0d, 5d), (13d, 5d), (-2d, 5d) })
                Add($"anim.pingpong({L(t)},{L(len)})");

            foreach (var v in new[] { -5d, 0d, 3d, 0.1d, -0.1d })
                Add($"anim.sign({L(v)})");

            Add("anim.oscillate(0,0,1,1)"); Add("anim.oscillate(0.25,0,1,1)"); Add("anim.oscillate(1,0,10,2)");

            foreach (var (t, f) in new[] { (0d, 1d), (0.25d, 1d), (0.5d, 1d), (0.75d, 1d), (1.3d, 2d) })
            {
                Add($"anim.triangle({L(t)},{L(f)})");
                Add($"anim.square({L(t)},{L(f)})");
            }

            foreach (var (t, d) in new[] { (0.5d, 1d), (2d, 1d), (0d, 1d), (0.3d, 2d) })
                Add($"anim.duration({L(t)},{L(d)})");
            foreach (var (t, d) in new[] { (2d, 1d), (0.5d, 1d), (3d, 0.5d) })
                Add($"anim.delay({L(t)},{L(d)})");

            foreach (var t in new[] { 0d, 0.25d, 0.5d, 0.75d, 1d, 0.1d })
            {
                Add($"anim.ease_in({L(t)})");
                Add($"anim.ease_out({L(t)})");
                Add($"anim.ease_in_out({L(t)})");
                Add($"anim.elastic({L(t)})");
                Add($"anim.back({L(t)})");
                Add($"anim.bounce({L(t)})");
                Add($"anim.fract({L(t + 3d)})");
            }

            Add("anim.step(0.5,0.6)"); Add("anim.step(0.5,0.4)"); Add("anim.step(0.5,0.5)");
            Add("anim.bezier(0,0,1,2,3)"); Add("anim.bezier(1,0,1,2,3)"); Add("anim.bezier(0.5,0,0.3,0.7,1)");
            Add("anim.len(3,4)"); Add("anim.len(1,1)");
            Add("anim.dist(0,0,3,4)"); Add("anim.dist(1,1,4,5)");
            Add("anim.dot(1,2,3,4)"); Add("anim.dot(0,0,1,1)");

            foreach (var (h, s, v) in new[] { (0d, 1d, 1d), (120d, 1d, 1d), (240d, 0.5d, 0.8d), (60d, 1d, 1d), (300d, 0.7d, 0.9d) })
                Tuple($"anim.hsv_to_rgb({L(h)},{L(s)},{L(v)})", 3);
            foreach (var (r, g, b) in new[] { (255d, 0d, 0d), (0d, 255d, 0d), (0d, 0d, 255d), (128d, 128d, 128d), (200d, 100d, 50d) })
                Tuple($"anim.rgb_to_hsv({L(r)},{L(g)},{L(b)})", 3);
            foreach (var (x, y) in new[] { (3d, 4d), (-1d, 1d), (5d, 0d) })
                Tuple($"anim.normalize({L(x)},{L(y)})", 2);
            foreach (var (r, a) in new[] { (1d, 0d), (1d, 90d), (2d, 45d) })
                Tuple($"anim.polar({L(r)},{L(a)})", 2);
            foreach (var (x, y, a) in new[] { (1d, 0d, 90d), (1d, 0d, 45d), (0d, 1d, 180d) })
                Tuple($"anim.rotate({L(x)},{L(y)},{L(a)})", 2);

            for (int seed = 0; seed < 12; seed++)
            {
                Add($"anim.rand({seed})");
                Add($"anim.rand(10,20,{seed})");
                Add($"anim.rand(-3,3,{seed * 7 + 1})");
            }
            foreach (var x in new[] { 0.3d, 1.5d, 2.7d, 5.1d, -1.2d })
            {
                Add($"anim.noise({L(x)})");
                Add($"anim.noise({L(x)},{L(x + 0.5d)})");
                Add($"anim.noise({L(x)},{L(x + 0.5d)},{L(x - 0.3d)})");
            }

            var expected = new double[exprs.Count];
            for (int i = 0; i < exprs.Count; i++)
                expected[i] = _oracle.Number(exprs[i]);

            string[] lines = lane.Eval(exprs);
            Assert.Equal(exprs.Count, lines.Length);

            var failures = new List<string>();
            for (int i = 0; i < exprs.Count; i++)
            {
                if (!double.TryParse(lines[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var got))
                {
                    failures.Add($"{exprs[i]} => luajit '{lines[i]}' (not a number), expected {expected[i]}");
                    continue;
                }
                double tol = 1e-9 * Math.Max(1d, Math.Abs(expected[i]));
                if (Math.Abs(got - expected[i]) > tol)
                    failures.Add($"{exprs[i]} => luajit {got}, moonsharp {expected[i]}");
            }

            Assert.True(failures.Count == 0,
                $"{failures.Count}/{exprs.Count} mismatches:\n" + string.Join("\n", failures.GetRange(0, Math.Min(25, failures.Count))));
        }

        private static string L(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
