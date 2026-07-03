namespace LuaScript.Compat
{
    internal static class AviUtlGlobalFunctions
    {
        public static int BitOr(double a, double b) => ToBitOperand(a) | ToBitOperand(b);

        public static int BitAnd(double a, double b) => ToBitOperand(a) & ToBitOperand(b);

        public static int BitXor(double a, double b) => ToBitOperand(a) ^ ToBitOperand(b);

        public static int BitShift(double value, double shift)
        {
            int v = ToBitOperand(value);
            int n = ToBitOperand(shift);
            return n >= 0 ? v << n : v >> -n;
        }

        public static int ToBitOperand(double value)
        {
            if (double.IsNaN(value))
                return 0;
            if (value <= int.MinValue)
                return int.MinValue;
            if (value >= int.MaxValue)
                return int.MaxValue;
            return (int)Math.Ceiling(value - 0.5);
        }

        public static int RgbCompose(double r, double g, double b) =>
            (ToColorChannel(r) << 16) | (ToColorChannel(g) << 8) | ToColorChannel(b);

        public static void RgbComponents(double color, out int r, out int g, out int b)
        {
            int c = ToColor(color);
            r = (c >> 16) & 0xFF;
            g = (c >> 8) & 0xFF;
            b = c & 0xFF;
        }

        public static int RgbInterpolate(double r1, double g1, double b1, double r2, double g2, double b2, double t) =>
            RgbCompose(r1 + (r2 - r1) * t, g1 + (g2 - g1) * t, b1 + (b2 - b1) * t);

        public static int HsvCompose(double h, double s, double v)
        {
            h = WrapHue(h);
            s = ClampRatio(s);
            v = ClampRatio(v);

            double c = v * s;
            double x = c * (1d - Math.Abs(h / 60d % 2d - 1d));
            double m = v - c;
            int sector = (int)Math.Floor(h / 60d);

            double r, g, b;
            if (sector == 0) { r = c; g = x; b = 0d; }
            else if (sector == 1) { r = x; g = c; b = 0d; }
            else if (sector == 2) { r = 0d; g = c; b = x; }
            else if (sector == 3) { r = 0d; g = x; b = c; }
            else if (sector == 4) { r = x; g = 0d; b = c; }
            else { r = c; g = 0d; b = x; }

            return (ToColorChannel(RoundHalfUp((r + m) * 255d)) << 16) |
                   (ToColorChannel(RoundHalfUp((g + m) * 255d)) << 8) |
                   ToColorChannel(RoundHalfUp((b + m) * 255d));
        }

        public static void HsvComponents(double color, out int h, out int s, out int v)
        {
            RgbComponents(color, out int r, out int g, out int b);
            double rf = r / 255d;
            double gf = g / 255d;
            double bf = b / 255d;

            double max = Math.Max(rf, Math.Max(gf, bf));
            double min = Math.Min(rf, Math.Min(gf, bf));
            double delta = max - min;

            double hue = 0d;
            if (delta > 0d)
            {
                if (max == rf)
                    hue = 60d * (((gf - bf) / delta % 6d + 6d) % 6d);
                else if (max == gf)
                    hue = 60d * ((bf - rf) / delta + 2d);
                else
                    hue = 60d * ((rf - gf) / delta + 4d);
            }

            h = (int)RoundHalfUp(hue);
            s = (int)RoundHalfUp(max > 0d ? delta / max * 100d : 0d);
            v = (int)RoundHalfUp(max * 100d);
        }

        public static int HsvInterpolate(double h1, double s1, double v1, double h2, double s2, double v2, double t) =>
            HsvCompose(h1 + (h2 - h1) * t, s1 + (s2 - s1) * t, v1 + (v2 - v1) * t);

        private static int ToColorChannel(double value)
        {
            if (double.IsNaN(value) || value <= 0d)
                return 0;
            if (value >= 255d)
                return 255;
            return (int)Math.Floor(value);
        }

        private static int ToColor(double value)
        {
            if (double.IsNaN(value))
                return 0;
            if (value <= int.MinValue)
                value = int.MinValue;
            else if (value >= int.MaxValue)
                value = int.MaxValue;
            long floored = (long)Math.Floor(value);
            return (int)(floored & 0xFFFFFF);
        }

        private static double WrapHue(double h)
        {
            if (double.IsNaN(h) || double.IsInfinity(h))
                return 0d;
            double wrapped = h - Math.Floor(h / 360d) * 360d;
            return wrapped >= 360d ? 0d : wrapped;
        }

        private static double ClampRatio(double value)
        {
            if (double.IsNaN(value) || value <= 0d)
                return 0d;
            if (value >= 100d)
                return 1d;
            return value / 100d;
        }

        private static double RoundHalfUp(double value) => Math.Floor(value + 0.5);
    }
}
