using System.Collections.Generic;
using LuaScript.Compat;

namespace LuaScript.Tests
{
    public class Bit32CleanTests
    {
        [Fact]
        public void Correct_Lua52_Semantics()
        {
            Assert.Equal(0xFFFFFFFFu, Bit32.Band(4294967295d, 4294967295d));
            Assert.Equal(0xFFFFFFFFu, Bit32.Bnot(0d));
            Assert.Equal(2147483648u, Bit32.Lshift(1d, 31d));
            Assert.Equal(0u, Bit32.Lshift(1d, 32d));
            Assert.Equal(0u, Bit32.Lshift(1d, -1d));
            Assert.Equal(1u, Bit32.Rshift(2147483648d, 31d));
            Assert.Equal(3221225472u, Bit32.Arshift(2147483648d, 1d));
            Assert.Equal(0xFFFFFFFFu, Bit32.Arshift(4294967295d, 40d));
            Assert.Equal(2147483648u, Bit32.Lrotate(1d, -1d));
            Assert.Equal(1u, Bit32.Lrotate(2147483648d, 1d));
        }

        [Fact]
        public void Correct_Extract_Replace_AllowsFullRange()
        {
            Assert.Equal(1u, Bit32.Extract(4294967295d, 31, 1));
            Assert.Equal(0xFFFFFFFFu, Bit32.Extract(4294967295d, 0, 32));
            Assert.Equal(255u, Bit32.Extract(255d, 0, 8));
            Assert.Equal(2147483648u, Bit32.Replace(0d, 1d, 31, 1));
        }

        [Theory]
        [InlineData(32, 1)]
        [InlineData(0, 33)]
        [InlineData(-1, 1)]
        [InlineData(0, 0)]
        public void Extract_OutOfRange_Throws(int field, int width) =>
            Assert.Throws<System.ArgumentException>(() => Bit32.Extract(1d, field, width));

        public static readonly double[] SafeValues =
        [
            0d, 1d, 2d, 255d, 256d, 65535d, 65536d, 16777215d,
            1073741824d, 2147483647d,
        ];

        public static readonly int[] SafeShifts = [0, 1, 4, 8, 15, 16, 24, 31];

        public static IEnumerable<object[]> SafeBinary()
        {
            foreach (var a in SafeValues)
                foreach (var b in SafeValues)
                    yield return [a, b];
        }

        public static IEnumerable<object[]> SafeValueAndShift()
        {
            foreach (var a in SafeValues)
                foreach (var n in SafeShifts)
                    yield return [a, n];
        }

        [Theory]
        [MemberData(nameof(SafeBinary))]
        public void Bitwise_MatchesMoonSharp_OnSafeDomain(double a, double b)
        {
            Assert.Equal(MoonSharpBit32.Band(a, b), Bit32.Band(a, b));
            Assert.Equal(MoonSharpBit32.Bor(a, b), Bit32.Bor(a, b));
            Assert.Equal(MoonSharpBit32.Bxor(a, b), Bit32.Bxor(a, b));
            Assert.Equal(MoonSharpBit32.Btest(a, b), Bit32.Btest(a, b));
        }

        [Theory]
        [MemberData(nameof(SafeValueAndShift))]
        public void Shifts_MatchMoonSharp_OnSafeDomain(double a, int n)
        {
            Assert.Equal(MoonSharpBit32.Lshift(a, n), Bit32.Lshift(a, n));
            Assert.Equal(MoonSharpBit32.Rshift(a, n), Bit32.Rshift(a, n));
            Assert.Equal((double)MoonSharpBit32.Arshift(a, n), (double)Bit32.Arshift(a, n));
            Assert.Equal(MoonSharpBit32.Lrotate(a, n), Bit32.Lrotate(a, n));
            Assert.Equal(MoonSharpBit32.Rrotate(a, n), Bit32.Rrotate(a, n));
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(0, 8)]
        [InlineData(4, 4)]
        [InlineData(8, 8)]
        [InlineData(0, 16)]
        [InlineData(15, 16)]
        [InlineData(30, 1)]
        public void Extract_MatchesMoonSharp_OnSafeDomain(int field, int width)
        {
            foreach (var a in SafeValues)
                Assert.Equal(MoonSharpBit32.Extract(a, field, width), Bit32.Extract(a, field, width));
        }

        [Fact]
        public void Replace_IsCorrectLua52_ShiftsReplacementIntoField()
        {
            Assert.Equal(0xF0u, Bit32.Replace(0d, 15d, 4, 4));
            Assert.Equal(0x80000000u, Bit32.Replace(0d, 1d, 31, 1));
            Assert.Equal(0xABCD0000u, Bit32.Replace(0d, 43981d, 16, 16));
        }

        [Fact]
        public void Replace_DivergesFromMoonSharpBug_ForNonZeroField()
        {
            Assert.NotEqual((double)MoonSharpBit32.Replace(0d, 15d, 4, 4), (double)Bit32.Replace(0d, 15d, 4, 4));
            Assert.Equal(0u, MoonSharpBit32.Replace(0d, 15d, 4, 4));
            Assert.Equal(0xF0u, Bit32.Replace(0d, 15d, 4, 4));
        }
    }
}
