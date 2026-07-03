using System;

namespace LuaScript.Compat
{
    internal static class AviUtlRandom
    {
        public static double Next(double a, double b, double seed, double frame)
        {
            long seedBits = BitConverter.DoubleToInt64Bits(seed);
            long frameBits = BitConverter.DoubleToInt64Bits(frame);
            uint s = (uint)(seedBits ^ (seedBits >> 32));
            uint f = (uint)(frameBits ^ (frameBits >> 32));

            uint h = s * 374761393u + f * 668265263u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;

            double r = h / 4294967295.0;

            long lo = (long)Math.Floor(Math.Min(a, b));
            long hi = (long)Math.Floor(Math.Max(a, b));
            long span = hi - lo + 1L;
            if (span <= 1L)
                return lo;

            long value = lo + (long)(r * span);
            return value > hi ? hi : value;
        }
    }
}
