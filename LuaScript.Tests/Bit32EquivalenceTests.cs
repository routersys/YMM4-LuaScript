using System.Collections.Generic;
using System.Globalization;
using LuaScript.Compat;
using MoonSharp.Interpreter;

namespace LuaScript.Tests
{
    public class Bit32EquivalenceTests
    {
        private static readonly Script Oracle = new(CoreModules.Basic | CoreModules.Math | CoreModules.Bit32);

        private static double Eval(string expression) => Oracle.DoString("return " + expression).Number;

        private static bool EvalBool(string expression) => Oracle.DoString("return " + expression).Boolean;

        private static string Lit(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        public static readonly double[] RawInputs =
        [
            0d, 1d, 2d, 255d, 256d, 65535d, 65536d,
            2147483647d, 2147483648d, 4294967295d,
            4294967296d, 4294967301d,
            -1d, -2d, -256d, -2147483648d,
        ];

        public static readonly int[] Shifts =
        [
            0, 1, 4, 7, 8, 15, 16, 24, 31, 32, 33, 64,
            -1, -4, -8, -16, -31, -32, -33,
        ];

        public static IEnumerable<object[]> Unary()
        {
            foreach (var a in RawInputs)
                yield return [a];
        }

        public static IEnumerable<object[]> Binary()
        {
            foreach (var a in RawInputs)
                foreach (var b in RawInputs)
                    yield return [a, b];
        }

        public static IEnumerable<object[]> ValueAndShift()
        {
            foreach (var a in RawInputs)
                foreach (var n in Shifts)
                    yield return [a, n];
        }

        [Theory]
        [MemberData(nameof(Binary))]
        public void Band(double a, double b) =>
            Assert.Equal((double)MoonSharpBit32.Band(a, b), Eval($"bit32.band({Lit(a)},{Lit(b)})"));

        [Theory]
        [MemberData(nameof(Binary))]
        public void Bor(double a, double b) =>
            Assert.Equal((double)MoonSharpBit32.Bor(a, b), Eval($"bit32.bor({Lit(a)},{Lit(b)})"));

        [Theory]
        [MemberData(nameof(Binary))]
        public void Bxor(double a, double b) =>
            Assert.Equal((double)MoonSharpBit32.Bxor(a, b), Eval($"bit32.bxor({Lit(a)},{Lit(b)})"));

        [Theory]
        [MemberData(nameof(Binary))]
        public void Btest(double a, double b) =>
            Assert.Equal(MoonSharpBit32.Btest(a, b), EvalBool($"bit32.btest({Lit(a)},{Lit(b)})"));

        [Theory]
        [MemberData(nameof(Unary))]
        public void Bnot(double a) =>
            Assert.Equal((double)MoonSharpBit32.Bnot(a), Eval($"bit32.bnot({Lit(a)})"));

        [Theory]
        [MemberData(nameof(ValueAndShift))]
        public void Lshift(double a, int n) =>
            Assert.Equal((double)MoonSharpBit32.Lshift(a, n), Eval($"bit32.lshift({Lit(a)},{n})"));

        [Theory]
        [MemberData(nameof(ValueAndShift))]
        public void Rshift(double a, int n) =>
            Assert.Equal((double)MoonSharpBit32.Rshift(a, n), Eval($"bit32.rshift({Lit(a)},{n})"));

        [Theory]
        [MemberData(nameof(ValueAndShift))]
        public void Arshift(double a, int n) =>
            Assert.Equal((double)MoonSharpBit32.Arshift(a, n), Eval($"bit32.arshift({Lit(a)},{n})"));

        [Theory]
        [MemberData(nameof(ValueAndShift))]
        public void Lrotate(double a, int n) =>
            Assert.Equal((double)MoonSharpBit32.Lrotate(a, n), Eval($"bit32.lrotate({Lit(a)},{n})"));

        [Theory]
        [MemberData(nameof(ValueAndShift))]
        public void Rrotate(double a, int n) =>
            Assert.Equal((double)MoonSharpBit32.Rrotate(a, n), Eval($"bit32.rrotate({Lit(a)},{n})"));

        [Theory]
        [InlineData(0, 1)]
        [InlineData(0, 8)]
        [InlineData(4, 4)]
        [InlineData(8, 8)]
        [InlineData(0, 16)]
        [InlineData(8, 16)]
        [InlineData(0, 31)]
        [InlineData(30, 1)]
        [InlineData(15, 16)]
        public void Extract(int field, int width)
        {
            foreach (var a in RawInputs)
                Assert.Equal(
                    (double)MoonSharpBit32.Extract(a, field, width),
                    Eval($"bit32.extract({Lit(a)},{field},{width})"));
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(0, 8)]
        [InlineData(4, 4)]
        [InlineData(8, 8)]
        [InlineData(0, 16)]
        [InlineData(8, 16)]
        [InlineData(0, 31)]
        [InlineData(30, 1)]
        [InlineData(15, 16)]
        public void Replace(int field, int width)
        {
            foreach (var a in RawInputs)
                foreach (var v in RawInputs)
                    Assert.Equal(
                        (double)MoonSharpBit32.Replace(a, v, field, width),
                        Eval($"bit32.replace({Lit(a)},{Lit(v)},{field},{width})"));
        }

        [Theory]
        [InlineData(31, 1)]
        [InlineData(16, 16)]
        [InlineData(0, 32)]
        [InlineData(32, 0)]
        public void Extract_OutOfRange_BothReject(int field, int width)
        {
            Assert.ThrowsAny<Exception>(() => MoonSharpBit32.Extract(1d, field, width));
            Assert.ThrowsAny<ScriptRuntimeException>(() => Eval($"bit32.extract(1,{field},{width})"));
        }

        [Theory]
        [InlineData(31, 1)]
        [InlineData(16, 16)]
        [InlineData(0, 32)]
        [InlineData(32, 0)]
        public void Replace_OutOfRange_BothReject(int field, int width)
        {
            Assert.ThrowsAny<Exception>(() => MoonSharpBit32.Replace(1d, 1d, field, width));
            Assert.ThrowsAny<ScriptRuntimeException>(() => Eval($"bit32.replace(1,1,{field},{width})"));
        }
    }
}
