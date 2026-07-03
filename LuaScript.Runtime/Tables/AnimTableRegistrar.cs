using LuaScript.Api;
using MoonSharp.Interpreter;

namespace LuaScript
{
    [LuaTable("anim")]
    internal static partial class AnimTableRegistrar
    {
        [LuaConstant("tau")]
        private static readonly double s_tau = Math.PI * 2d;

        [LuaConstant("e")]
        private static readonly double s_e = Math.E;

        [LuaConstant("phi")]
        private static readonly double s_phi = (1d + Math.Sqrt(5d)) / 2d;

        [LuaConstant("sqrt2")]
        private static readonly double s_sqrt2 = Math.Sqrt(2d);

        internal static void RegisterFunctions(Table anim) => RegisterLuaMembers(anim);

        [LuaFunction("lerp")]
        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        [LuaFunction("smoothstep")]
        private static double Smoothstep(double edge0, double edge1 = 1d, double x = 0d)
        {
            double span = edge1 - edge0;
            double t = span == 0d ? 0d : Math.Clamp((x - edge0) / span, 0d, 1d);
            return t * t * (3d - 2d * t);
        }

        [LuaFunction("smootherstep")]
        private static double Smootherstep(double edge0, double edge1 = 1d, double x = 0d)
        {
            double span = edge1 - edge0;
            double t = span == 0d ? 0d : Math.Clamp((x - edge0) / span, 0d, 1d);
            return t * t * t * (t * (6d * t - 15d) + 10d);
        }

        [LuaFunction("clamp")]
        private static double Clamp(double v, double lo = 0d, double hi = 1d) => Math.Clamp(v, lo, hi);

        [LuaFunction("map")]
        private static double Map(double v, double a1 = 0d, double b1 = 1d, double a2 = 0d, double b2 = 1d)
        {
            double range = b1 - a1;
            if (range == 0d) return a2;
            return a2 + (b2 - a2) * (v - a1) / range;
        }

        [LuaFunction("norm")]
        private static double Norm(double v, double lo = 0d, double hi = 1d)
        {
            double span = hi - lo;
            if (span == 0d) return 0d;
            return (v - lo) / span;
        }

        [LuaFunction("wrap")]
        private static double Wrap(double v, double lo = 0d, double hi = 1d)
        {
            double range = hi - lo;
            if (range <= 0d) return lo;
            double result = (v - lo) % range;
            if (result < 0d) result += range;
            return lo + result;
        }

        [LuaFunction("pingpong")]
        private static double Pingpong(double t, double length = 1d)
        {
            if (length <= 0d) return 0d;
            double v = t % (2d * length);
            if (v < 0d) v += 2d * length;
            return v > length ? 2d * length - v : v;
        }

        [LuaFunction("sign")]
        private static double Sign(double v) => Math.Sign(v);

        [LuaFunction("oscillate")]
        private static double Oscillate(double t, double lo = 0d, double hi = 1d, double freq = 1d)
        {
            double wave = (Math.Sin(t * freq * s_tau) + 1d) / 2d;
            return lo + (hi - lo) * wave;
        }

        [LuaFunction("triangle")]
        private static double Triangle(double t, double freq = 1d)
        {
            double f = t * freq;
            f -= Math.Floor(f);
            return 1d - 2d * Math.Abs(f - 0.5d);
        }

        [LuaFunction("square")]
        private static double Square(double t, double freq = 1d)
        {
            double f = t * freq;
            f -= Math.Floor(f);
            return f >= 0.5d ? 1d : 0d;
        }

        [LuaFunction("duration")]
        private static double Duration(double t, double dur = 1d)
        {
            if (dur <= 0d) return 1d;
            return Math.Clamp(t / dur, 0d, 1d);
        }

        [LuaFunction("delay")]
        private static double Delay(double t, double d) => Math.Max(0d, t - d);

        [LuaFunction("ease_in")]
        private static double EaseIn(double t) => t * t;

        [LuaFunction("ease_out")]
        private static double EaseOut(double t)
        {
            double inv = 1d - t;
            return 1d - inv * inv;
        }

        [LuaFunction("ease_in_out")]
        private static double EaseInOut(double t)
        {
            if (t < 0.5d)
                return 2d * t * t;
            double inv = -2d * t + 2d;
            return 1d - inv * inv / 2d;
        }

        [LuaFunction("elastic")]
        private static double Elastic(double t)
        {
            t = Math.Clamp(t, 0d, 1d);
            if (t == 0d) return 0d;
            if (t == 1d) return 1d;
            return Math.Pow(2d, -10d * t) * Math.Sin((t * 10d - 0.75d) * (s_tau / 3d)) + 1d;
        }

        [LuaFunction("back")]
        private static double Back(double t)
        {
            const double c1 = 1.70158d;
            const double c3 = c1 + 1d;
            double inv = t - 1d;
            return 1d + c3 * inv * inv * inv + c1 * inv * inv;
        }

        [LuaFunction("step")]
        private static double Step(double edge, double x) => x >= edge ? 1d : 0d;

        [LuaFunction("fract")]
        private static double Fract(double v) => v - Math.Floor(v);

        [LuaFunction("bounce")]
        private static double Bounce(double t)
        {
            const double n1 = 7.5625d;
            const double d1 = 2.75d;
            t = Math.Clamp(t, 0d, 1d);
            if (t < 1d / d1)
                return n1 * t * t;
            if (t < 2d / d1)
            {
                t -= 1.5d / d1;
                return n1 * t * t + 0.75d;
            }
            if (t < 2.5d / d1)
            {
                t -= 2.25d / d1;
                return n1 * t * t + 0.9375d;
            }
            t -= 2.625d / d1;
            return n1 * t * t + 0.984375d;
        }

        [LuaFunction("hsv_to_rgb")]
        private static DynValue HsvToRgb(double h, double s, double v)
        {
            double c = v * s;
            double x = c * (1d - Math.Abs((h / 60d) % 2d - 1d));
            double m = v - c;
            int sector = ((int)Math.Floor(h / 60d) % 6 + 6) % 6;
            (double r, double g, double b) = sector switch
            {
                0 => (c, x, 0d),
                1 => (x, c, 0d),
                2 => (0d, c, x),
                3 => (0d, x, c),
                4 => (x, 0d, c),
                _ => (c, 0d, x),
            };
            return DynValue.NewTuple(
                DynValue.NewNumber((r + m) * 255d),
                DynValue.NewNumber((g + m) * 255d),
                DynValue.NewNumber((b + m) * 255d));
        }

        [LuaFunction("rgb_to_hsv")]
        private static DynValue RgbToHsv(double r, double g, double b)
        {
            r /= 255d;
            g /= 255d;
            b /= 255d;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;
            double h = 0d;
            if (delta > 0d)
            {
                if (max == r) h = 60d * ((((g - b) / delta) % 6d + 6d) % 6d);
                else if (max == g) h = 60d * ((b - r) / delta + 2d);
                else h = 60d * ((r - g) / delta + 4d);
            }
            return DynValue.NewTuple(
                DynValue.NewNumber(h),
                DynValue.NewNumber(max > 0d ? delta / max : 0d),
                DynValue.NewNumber(max));
        }

        [LuaFunction("len")]
        private static double Len(double x, double y) => Math.Sqrt(x * x + y * y);

        [LuaFunction("dist")]
        private static double Dist(double x1, double y1, double x2, double y2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        [LuaFunction("dot")]
        private static double Dot(double x1, double y1, double x2, double y2) => x1 * x2 + y1 * y2;

        [LuaFunction("normalize")]
        private static DynValue Normalize(double x, double y)
        {
            double magnitude = Math.Sqrt(x * x + y * y);
            if (magnitude == 0d)
                return DynValue.NewTuple(DynValue.NewNumber(0d), DynValue.NewNumber(0d));
            return DynValue.NewTuple(
                DynValue.NewNumber(x / magnitude),
                DynValue.NewNumber(y / magnitude));
        }

        [LuaFunction("noise")]
        private static double Noise(double x, double y, double z)
        {
            int xi = (int)Math.Floor(x);
            int yi = (int)Math.Floor(y);
            int zi = (int)Math.Floor(z);

            double xf = x - xi;
            double yf = y - yi;
            double zf = z - zi;

            double u = xf * xf * (3d - 2d * xf);
            double v = yf * yf * (3d - 2d * yf);
            double w = zf * zf * (3d - 2d * zf);

            double c000 = GetNoiseHash(xi, yi, zi);
            double c100 = GetNoiseHash(xi + 1, yi, zi);
            double c010 = GetNoiseHash(xi, yi + 1, zi);
            double c110 = GetNoiseHash(xi + 1, yi + 1, zi);
            double c001 = GetNoiseHash(xi, yi, zi + 1);
            double c101 = GetNoiseHash(xi + 1, yi, zi + 1);
            double c011 = GetNoiseHash(xi, yi + 1, zi + 1);
            double c111 = GetNoiseHash(xi + 1, yi + 1, zi + 1);

            double x00 = c000 + u * (c100 - c000);
            double x10 = c010 + u * (c110 - c010);
            double x01 = c001 + u * (c101 - c001);
            double x11 = c011 + u * (c111 - c011);

            double y0 = x00 + v * (x10 - x00);
            double y1 = x01 + v * (x11 - x01);

            return y0 + w * (y1 - y0);
        }

        [LuaFunction("rand", "min", "max", "seed")]
        private static DynValue Rand(CallbackArguments args)
        {
            if (args.Count == 0) return DynValue.NewNumber(0d);
            double seed, min = 0d, max = 1d;
            if (args.Count == 1)
            {
                seed = args[0].CastToNumber() ?? 0d;
            }
            else if (args.Count == 2)
            {
                max = args[0].CastToNumber() ?? 1d;
                seed = args[1].CastToNumber() ?? 0d;
            }
            else
            {
                min = args[0].CastToNumber() ?? 0d;
                max = args[1].CastToNumber() ?? 1d;
                seed = args[2].CastToNumber() ?? 0d;
            }
            long bits = BitConverter.DoubleToInt64Bits(seed);
            uint h = (uint)((bits ^ (bits >> 32)) * 374761393);
            h = (h ^ (h >> 13)) * 1274126177;
            double r = (double)(h ^ (h >> 16)) / 4294967295.0;
            return DynValue.NewNumber(min + r * (max - min));
        }

        [LuaFunction("polar")]
        private static DynValue Polar(double r, double a)
        {
            double rad = a * Math.PI / 180d;
            return DynValue.NewTuple(
                DynValue.NewNumber(r * Math.Cos(rad)),
                DynValue.NewNumber(r * Math.Sin(rad))
            );
        }

        [LuaFunction("rotate")]
        private static DynValue Rotate(double x, double y, double a)
        {
            double rad = a * Math.PI / 180d;
            double c = Math.Cos(rad);
            double s = Math.Sin(rad);
            return DynValue.NewTuple(
                DynValue.NewNumber(x * c - y * s),
                DynValue.NewNumber(x * s + y * c)
            );
        }

        [LuaFunction("bezier")]
        private static double Bezier(double t, double p0 = 0d, double p1 = 0d, double p2 = 1d, double p3 = 1d)
        {
            t = Math.Clamp(t, 0d, 1d);
            double inv = 1d - t;
            double b0 = inv * inv * inv;
            double b1 = 3d * inv * inv * t;
            double b2 = 3d * inv * t * t;
            double b3 = t * t * t;
            return b0 * p0 + b1 * p1 + b2 * p2 + b3 * p3;
        }

        private static double GetNoiseHash(int x, int y, int z)
        {
            uint n = (uint)(x * 374761393 + y * 668265263 + z * 1013904223);
            n = (n ^ (n >> 13)) * 1274126177;
            return (double)(n ^ (n >> 16)) / 4294967295.0;
        }
    }
}
