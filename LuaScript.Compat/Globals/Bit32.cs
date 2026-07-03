using System;

namespace LuaScript.Compat
{
    internal static class Bit32
    {
        public static uint ToUInt32(double value) => unchecked((uint)(long)value);

        public static uint Band(params double[] values)
        {
            uint accum = 0xFFFFFFFFu;
            foreach (var value in values)
                accum &= ToUInt32(value);
            return accum;
        }

        public static uint Bor(params double[] values)
        {
            uint accum = 0u;
            foreach (var value in values)
                accum |= ToUInt32(value);
            return accum;
        }

        public static uint Bxor(params double[] values)
        {
            uint accum = 0u;
            foreach (var value in values)
                accum ^= ToUInt32(value);
            return accum;
        }

        public static bool Btest(params double[] values) => Band(values) != 0u;

        public static uint Bnot(double value) => ~ToUInt32(value);

        public static uint Lshift(double value, double shift)
        {
            uint v = ToUInt32(value);
            int a = (int)shift;
            if (a <= -32 || a >= 32)
                return 0u;
            return a >= 0 ? v << a : v >> -a;
        }

        public static uint Rshift(double value, double shift) => Lshift(value, -shift);

        public static uint Arshift(double value, double shift)
        {
            uint v = ToUInt32(value);
            int a = (int)shift;
            if (a < 0)
                return Lshift(value, -shift);
            bool negative = (v & 0x80000000u) != 0u;
            if (a >= 32)
                return negative ? 0xFFFFFFFFu : 0u;
            if (a == 0)
                return v;
            uint result = v >> a;
            if (negative)
                result |= ~0u << (32 - a);
            return result;
        }

        public static uint Lrotate(double value, double shift)
        {
            uint v = ToUInt32(value);
            int a = ((int)shift % 32 + 32) % 32;
            return a == 0 ? v : (v << a) | (v >> (32 - a));
        }

        public static uint Rrotate(double value, double shift) => Lrotate(value, -shift);

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
            return (v & ~mask) | ((u & NBitMask(w)) << p);
        }

        private static void ValidatePosWidth(int pos, int width)
        {
            if (pos < 0 || width < 1 || pos + width > 32)
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
