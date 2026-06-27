using System;

namespace LuaScript.Compat
{
    internal static class MoonSharpBit32
    {
        private const double Modulo = 4294967296.0;

        public static uint ToUInt32(double value) => unchecked((uint)Math.IEEERemainder(value, Modulo));

        public static int ToInt32(double value) => unchecked((int)Math.IEEERemainder(value, Modulo));

        public static uint Band(params double[] values)
        {
            uint accum = ToUInt32(values[0]);
            for (int i = 1; i < values.Length; i++)
                accum &= ToUInt32(values[i]);
            return accum;
        }

        public static uint Bor(params double[] values)
        {
            uint accum = ToUInt32(values[0]);
            for (int i = 1; i < values.Length; i++)
                accum |= ToUInt32(values[i]);
            return accum;
        }

        public static uint Bxor(params double[] values)
        {
            uint accum = ToUInt32(values[0]);
            for (int i = 1; i < values.Length; i++)
                accum ^= ToUInt32(values[i]);
            return accum;
        }

        public static bool Btest(params double[] values) => Band(values) != 0u;

        public static uint Bnot(double value) => ~ToUInt32(value);

        public static uint Lshift(double value, double shift)
        {
            uint v = ToUInt32(value);
            int a = (int)shift;
            return a < 0 ? v >> -a : v << a;
        }

        public static uint Rshift(double value, double shift)
        {
            uint v = ToUInt32(value);
            int a = (int)shift;
            return a < 0 ? v << -a : v >> a;
        }

        public static int Arshift(double value, double shift)
        {
            int v = ToInt32(value);
            int a = (int)shift;
            return a < 0 ? v << -a : v >> a;
        }

        public static uint Lrotate(double value, double shift)
        {
            uint v = ToUInt32(value);
            int a = ((int)shift) % 32;
            return a < 0 ? (v >> -a) | (v << (32 + a)) : (v << a) | (v >> (32 - a));
        }

        public static uint Rrotate(double value, double shift)
        {
            uint v = ToUInt32(value);
            int a = ((int)shift) % 32;
            return a < 0 ? (v << -a) | (v >> (32 + a)) : (v >> a) | (v << (32 - a));
        }

        public static uint Extract(double value, double pos, double? width)
        {
            uint v = ToUInt32(value);
            int p = (int)pos;
            int w = width is null ? 1 : (int)width.Value;
            ValidatePosWidth(p, w);
            return (v >> p) & NBitMask(w);
        }

        public static uint Replace(double value, double replacement, double pos, double? width)
        {
            uint v = ToUInt32(value);
            uint u = ToUInt32(replacement);
            int p = (int)pos;
            int w = width is null ? 1 : (int)width.Value;
            ValidatePosWidth(p, w);
            uint mask = NBitMask(w) << p;
            return (v & ~mask) | (u & mask);
        }

        private static void ValidatePosWidth(int pos, int width)
        {
            if (pos > 31 || pos + width > 31)
                throw new ArgumentException("trying to access non-existent bits");
        }

        private static uint NBitMask(int bits)
        {
            if (bits <= 0)
                return 0u;
            if (bits >= 32)
                return 0xFFFFFFFFu;
            return (1u << bits) - 1u;
        }
    }
}
