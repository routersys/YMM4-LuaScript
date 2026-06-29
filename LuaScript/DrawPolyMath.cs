using System;
using System.Numerics;

namespace LuaScript
{
    internal static class DrawPolyMath
    {
        public const int Length = 21;

        public static bool TrySolveAffine(double[] poly, out Matrix3x2 matrix)
        {
            double x0 = poly[0], y0 = poly[1];
            double x1 = poly[3], y1 = poly[4];
            double x3 = poly[9], y3 = poly[10];
            double u0 = poly[12], v0 = poly[13];
            double u1 = poly[14], v1 = poly[15];
            double u3 = poly[18], v3 = poly[19];

            double det = u0 * (v1 - v3) - v0 * (u1 - u3) + (u1 * v3 - v1 * u3);
            if (Math.Abs(det) < 1e-9)
            {
                matrix = Matrix3x2.Identity;
                return false;
            }

            double inv = 1d / det;
            double a = (x0 * (v1 - v3) - v0 * (x1 - x3) + (x1 * v3 - v1 * x3)) * inv;
            double b = (u0 * (x1 - x3) - x0 * (u1 - u3) + (u1 * x3 - x1 * u3)) * inv;
            double c = (u0 * (v1 * x3 - x1 * v3) - v0 * (u1 * x3 - x1 * u3) + x0 * (u1 * v3 - v1 * u3)) * inv;
            double d = (y0 * (v1 - v3) - v0 * (y1 - y3) + (y1 * v3 - v1 * y3)) * inv;
            double e = (u0 * (y1 - y3) - y0 * (u1 - u3) + (u1 * y3 - y1 * u3)) * inv;
            double f = (u0 * (v1 * y3 - y1 * v3) - v0 * (u1 * y3 - y1 * u3) + y0 * (u1 * v3 - v1 * u3)) * inv;

            matrix = new Matrix3x2((float)a, (float)d, (float)b, (float)e, (float)c, (float)f);
            return true;
        }

        public static void Bounds(double[] poly, out double minX, out double minY, out double maxX, out double maxY)
        {
            minX = minY = double.MaxValue;
            maxX = maxY = double.MinValue;
            for (int i = 0; i < 4; i++)
            {
                double x = poly[i * 3];
                double y = poly[i * 3 + 1];
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }
    }
}
