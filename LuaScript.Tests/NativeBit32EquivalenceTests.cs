using System;
using System.Collections.Generic;
using System.Globalization;
using LuaScript.Compat;

namespace LuaScript.Tests
{
    public class NativeBit32EquivalenceTests
    {
        private static readonly double[] Values =
        [
            0d, 1d, 2d, 255d, 256d, 65535d, 65536d,
            2147483647d, 2147483648d, 4294967295d,
            4294967296d, 4294967301d,
            -1d, -2d, -256d, -2147483648d,
        ];

        private static readonly int[] Shifts =
        [
            0, 1, 4, 7, 8, 15, 16, 24, 31, 32, 33, 64,
            -1, -4, -8, -16, -31, -32, -33,
        ];

        private static readonly (int Field, int Width)[] ValidFields =
        [
            (0, 1), (0, 8), (4, 4), (8, 8), (0, 16), (15, 16), (30, 1), (0, 31), (0, 32),
        ];

        private static readonly (int Field, int Width)[] InvalidFields =
        [
            (32, 1), (0, 33), (-1, 1), (0, 0),
        ];

        private static string Lit(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        [Fact]
        public void CorrectBit32_MatchesAcrossMoonSharpAndLuaJit()
        {
            var lane = new LuaJitLane();
            Assert.True(lane.Available, "native/luajit.exe must be present");

            var exprs = new List<string>();
            var expects = new List<object>();

            void Num(string e, double v) { exprs.Add(e); expects.Add(v); }
            void Bool(string e, bool v) { exprs.Add(e); expects.Add(v); }
            void Err(string e) { exprs.Add(e); expects.Add("ERROR"); }

            foreach (var a in Values)
            {
                Num($"bit32.bnot({Lit(a)})", Bit32.Bnot(a));
                foreach (var b in Values)
                {
                    Num($"bit32.band({Lit(a)},{Lit(b)})", Bit32.Band(a, b));
                    Num($"bit32.bor({Lit(a)},{Lit(b)})", Bit32.Bor(a, b));
                    Num($"bit32.bxor({Lit(a)},{Lit(b)})", Bit32.Bxor(a, b));
                    Bool($"bit32.btest({Lit(a)},{Lit(b)})", Bit32.Btest(a, b));
                }
                foreach (var n in Shifts)
                {
                    Num($"bit32.lshift({Lit(a)},{n})", Bit32.Lshift(a, n));
                    Num($"bit32.rshift({Lit(a)},{n})", Bit32.Rshift(a, n));
                    Num($"bit32.arshift({Lit(a)},{n})", Bit32.Arshift(a, n));
                    Num($"bit32.lrotate({Lit(a)},{n})", Bit32.Lrotate(a, n));
                    Num($"bit32.rrotate({Lit(a)},{n})", Bit32.Rrotate(a, n));
                }
                foreach (var (field, width) in ValidFields)
                {
                    Num($"bit32.extract({Lit(a)},{field},{width})", Bit32.Extract(a, field, width));
                    foreach (var v in Values)
                        Num($"bit32.replace({Lit(a)},{Lit(v)},{field},{width})", Bit32.Replace(a, v, field, width));
                }
                foreach (var (field, width) in InvalidFields)
                {
                    Err($"bit32.extract({Lit(a)},{field},{width})");
                    Err($"bit32.replace({Lit(a)},1,{field},{width})");
                }
            }

            string[] lines = lane.Eval(exprs);
            Assert.Equal(exprs.Count, lines.Length);

            var failures = new List<string>();
            for (int i = 0; i < exprs.Count; i++)
            {
                string actual = lines[i];
                switch (expects[i])
                {
                    case double d:
                        if (!double.TryParse(actual, NumberStyles.Float, CultureInfo.InvariantCulture, out var got) || got != d)
                            failures.Add($"{exprs[i]} => luajit '{actual}', expected {d}");
                        break;
                    case bool b:
                        if (actual != (b ? "true" : "false"))
                            failures.Add($"{exprs[i]} => luajit '{actual}', expected {b}");
                        break;
                    case string s:
                        if (actual != s)
                            failures.Add($"{exprs[i]} => luajit '{actual}', expected {s}");
                        break;
                }
            }

            Assert.True(failures.Count == 0,
                $"{failures.Count}/{exprs.Count} mismatches:\n" + string.Join("\n", failures.GetRange(0, Math.Min(20, failures.Count))));
        }
    }
}
