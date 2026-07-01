using System;
using System.Numerics;

namespace LuaScript
{
    internal static class SoftwareCompositor
    {
        public static void DrawInto(
            byte[] dst, int dstW, int dstH,
            byte[] src, int srcW, int srcH,
            double ox, double oy, double zoom, double aspect, double alpha, bool linear)
        {
            if (srcW <= 0 || srcH <= 0 || dstW <= 0 || dstH <= 0)
                return;

            double clampedAspect = Math.Clamp(aspect, -1d, 1d);
            double zx = zoom * (1d + clampedAspect);
            double zy = zoom * (1d - clampedAspect);
            if (zx == 0d || zy == 0d)
                return;

            double ka = Math.Clamp(alpha, 0d, 1d);
            if (ka <= 0d)
                return;

            double halfW = srcW * 0.5;
            double halfH = srcH * 0.5;
            double spanX = Math.Abs(halfW * zx);
            double spanY = Math.Abs(halfH * zy);

            int x0 = Math.Max(0, (int)Math.Floor(ox - spanX));
            int x1 = Math.Min(dstW - 1, (int)Math.Ceiling(ox + spanX));
            int y0 = Math.Max(0, (int)Math.Floor(oy - spanY));
            int y1 = Math.Min(dstH - 1, (int)Math.Ceiling(oy + spanY));

            for (int py = y0; py <= y1; py++)
            {
                for (int px = x0; px <= x1; px++)
                {
                    double u = (px + 0.5 - ox) / zx + halfW;
                    double v = (py + 0.5 - oy) / zy + halfH;
                    if (u < 0d || u >= srcW || v < 0d || v >= srcH)
                        continue;

                    Sample(src, srcW, srcH, u, v, linear, out double sb, out double sg, out double sr, out double sa);
                    CompositeOver(dst, (py * dstW + px) * 4, sb * ka, sg * ka, sr * ka, sa * ka);
                }
            }
        }

        public static void DrawPolyInto(
            byte[] dst, int dstW, int dstH,
            byte[] src, int srcW, int srcH,
            double[] poly, double alpha, bool linear)
        {
            if (srcW <= 0 || srcH <= 0 || dstW <= 0 || dstH <= 0)
                return;
            if (!DrawPolyMath.TrySolveAffine(poly, out var forward))
                return;
            if (!Matrix3x2.Invert(forward, out var inverse))
                return;

            double ka = Math.Clamp(alpha, 0d, 1d);
            if (ka <= 0d)
                return;

            DrawPolyMath.Bounds(poly, out double minX, out double minY, out double maxX, out double maxY);
            int x0 = Math.Max(0, (int)Math.Floor(minX));
            int x1 = Math.Min(dstW - 1, (int)Math.Ceiling(maxX));
            int y0 = Math.Max(0, (int)Math.Floor(minY));
            int y1 = Math.Min(dstH - 1, (int)Math.Ceiling(maxY));

            for (int py = y0; py <= y1; py++)
            {
                for (int px = x0; px <= x1; px++)
                {
                    var uv = Vector2.Transform(new Vector2(px + 0.5f, py + 0.5f), inverse);
                    double u = uv.X;
                    double v = uv.Y;
                    if (u < 0d || u >= srcW || v < 0d || v >= srcH)
                        continue;

                    Sample(src, srcW, srcH, u, v, linear, out double sb, out double sg, out double sr, out double sa);
                    CompositeOver(dst, (py * dstW + px) * 4, sb * ka, sg * ka, sr * ka, sa * ka);
                }
            }
        }

        private static void Sample(byte[] src, int srcW, int srcH, double u, double v, bool linear,
            out double b, out double g, out double r, out double a)
        {
            if (!linear)
            {
                int su = Math.Clamp((int)Math.Floor(u), 0, srcW - 1);
                int sv = Math.Clamp((int)Math.Floor(v), 0, srcH - 1);
                int si = (sv * srcW + su) * 4;
                b = src[si];
                g = src[si + 1];
                r = src[si + 2];
                a = src[si + 3];
                return;
            }

            double fx = u - 0.5;
            double fy = v - 0.5;
            int x0 = (int)Math.Floor(fx);
            int y0 = (int)Math.Floor(fy);
            double tx = fx - x0;
            double ty = fy - y0;

            b = g = r = a = 0d;
            for (int j = 0; j < 2; j++)
            {
                int sy = Math.Clamp(y0 + j, 0, srcH - 1);
                double wy = j == 0 ? 1d - ty : ty;
                for (int i = 0; i < 2; i++)
                {
                    int sx = Math.Clamp(x0 + i, 0, srcW - 1);
                    double w = wy * (i == 0 ? 1d - tx : tx);
                    int si = (sy * srcW + sx) * 4;
                    b += src[si] * w;
                    g += src[si + 1] * w;
                    r += src[si + 2] * w;
                    a += src[si + 3] * w;
                }
            }
        }

        private static void CompositeOver(byte[] dst, int di, double sb, double sg, double sr, double sa)
        {
            double inv = 1d - sa / 255d;
            dst[di] = (byte)Math.Clamp(sb + dst[di] * inv, 0d, 255d);
            dst[di + 1] = (byte)Math.Clamp(sg + dst[di + 1] * inv, 0d, 255d);
            dst[di + 2] = (byte)Math.Clamp(sr + dst[di + 2] * inv, 0d, 255d);
            dst[di + 3] = (byte)Math.Clamp(sa + dst[di + 3] * inv, 0d, 255d);
        }
    }
}
