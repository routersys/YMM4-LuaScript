using System;
using System.Collections.Generic;
using System.Globalization;
using LuaScript.Compat;

namespace LuaScript.Tests
{
    public class AviUtlRandomEquivalenceTests
    {
        [Fact]
        public void Rand_StaysWithinRequestedRange()
        {
            for (int seed = 0; seed < 64; seed++)
            {
                double value = AviUtlRandom.Next(-3, 7, seed, seed * 3 + 1);
                Assert.InRange(value, -3d, 7d);
                Assert.Equal(Math.Floor(value), value);
            }
        }

        [Fact]
        public void Rand_IsDeterministicForSameInputs()
        {
            Assert.Equal(
                AviUtlRandom.Next(0, 100, 5, 12),
                AviUtlRandom.Next(0, 100, 5, 12));
        }

        [Fact]
        public void Rand_MatchesBetweenMoonSharpAndLuaJit()
        {
            var lane = new LuaJitLane();
            Assert.True(lane.Available, "native/luajit.exe must be present");

            var cases = new List<(double A, double B, double Seed, double Frame)>();
            for (int seed = 0; seed < 16; seed++)
                for (int frame = 0; frame < 4; frame++)
                {
                    cases.Add((0d, 100d, seed, frame));
                    cases.Add((-50d, 50d, seed + 1, frame * 7));
                    cases.Add((1d, 6d, seed * 13, frame));
                    cases.Add((-3.5d, 3.5d, seed, frame + 1000));
                }

            var exprs = new List<string>(cases.Count);
            foreach (var c in cases)
                exprs.Add($"aviutl_rand({L(c.A)},{L(c.B)},{L(c.Seed)},{L(c.Frame)})");

            string[] lines = lane.Eval(exprs);
            Assert.Equal(exprs.Count, lines.Length);

            var failures = new List<string>();
            for (int i = 0; i < cases.Count; i++)
            {
                double expected = AviUtlRandom.Next(cases[i].A, cases[i].B, cases[i].Seed, cases[i].Frame);
                if (!double.TryParse(lines[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var got))
                {
                    failures.Add($"{exprs[i]} => luajit '{lines[i]}' (not a number), expected {expected}");
                    continue;
                }
                if (got != expected)
                    failures.Add($"{exprs[i]} => luajit {got}, moonsharp {expected}");
            }

            Assert.True(failures.Count == 0,
                $"{failures.Count}/{cases.Count} mismatches:\n" + string.Join("\n", failures.GetRange(0, Math.Min(25, failures.Count))));
        }

        private static string L(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
