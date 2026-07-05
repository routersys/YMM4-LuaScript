using System;

namespace LuaScript
{
    internal static unsafe class PixelBufferSoftwareProcessor
    {
        public static void Fill(byte[] target, int width, int height, double r, double g, double b, double a, int x, int y, int fillWidth, int fillHeight)
        {
            double aK = Math.Clamp(a, 0d, 255d) / 255d;
            byte pb = (byte)Math.Clamp(b * aK, 0d, 255d);
            byte pg = (byte)Math.Clamp(g * aK, 0d, 255d);
            byte pr = (byte)Math.Clamp(r * aK, 0d, 255d);
            byte pa = (byte)Math.Clamp(a, 0d, 255d);
            int x1 = x + fillWidth;
            int y1 = y + fillHeight;

            fixed (byte* buf = target)
            {
                for (int py = y; py < y1; py++)
                {
                    byte* p = buf + (py * width + x) * 4;
                    for (int px = x; px < x1; px++)
                    {
                        p[0] = pb;
                        p[1] = pg;
                        p[2] = pr;
                        p[3] = pa;
                        p += 4;
                    }
                }
            }
        }

        public static void Convolve(byte[] target, int width, int height, double[] kernel, int size, double divisor, double offset, ref double[]? sourceScratch)
        {
            int count = width * height * 4;
            if (sourceScratch is null || sourceScratch.Length < count)
                sourceScratch = new double[count];
            var src = sourceScratch;

            fixed (byte* buf = target)
            {
                for (int pi = 0; pi < width * height; pi++)
                {
                    byte* p = buf + pi * 4;
                    int di = pi * 4;
                    double a = p[3];
                    src[di + 3] = a;
                    if (a <= 0d)
                    {
                        src[di] = 0d;
                        src[di + 1] = 0d;
                        src[di + 2] = 0d;
                    }
                    else
                    {
                        double s = 255d / a;
                        src[di] = Math.Clamp(p[2] * s, 0d, 255d);
                        src[di + 1] = Math.Clamp(p[1] * s, 0d, 255d);
                        src[di + 2] = Math.Clamp(p[0] * s, 0d, 255d);
                    }
                }
            }

            int half = size / 2;

            fixed (byte* buf = target)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        double sr = 0d, sg = 0d, sb = 0d, sa = 0d;
                        for (int ky = 0; ky < size; ky++)
                        {
                            int sy = Math.Clamp(y + ky - half, 0, height - 1);
                            for (int kx = 0; kx < size; kx++)
                            {
                                int sx = Math.Clamp(x + kx - half, 0, width - 1);
                                double kv = kernel[ky * size + kx];
                                int si = (sy * width + sx) * 4;
                                sr += src[si] * kv;
                                sg += src[si + 1] * kv;
                                sb += src[si + 2] * kv;
                                sa += src[si + 3] * kv;
                            }
                        }
                        double rr = Math.Clamp(sr / divisor + offset, 0d, 255d);
                        double gg = Math.Clamp(sg / divisor + offset, 0d, 255d);
                        double bb = Math.Clamp(sb / divisor + offset, 0d, 255d);
                        double aa = Math.Clamp(sa / divisor + offset, 0d, 255d);
                        double aK = aa / 255d;
                        byte* p = buf + (y * width + x) * 4;
                        p[0] = (byte)Math.Clamp(bb * aK, 0d, 255d);
                        p[1] = (byte)Math.Clamp(gg * aK, 0d, 255d);
                        p[2] = (byte)Math.Clamp(rr * aK, 0d, 255d);
                        p[3] = (byte)aa;
                    }
                }
            }
        }

        public static byte[] Resize(byte[] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, bool linear, ref byte[]? sourceScratch, ref byte[]? targetScratch)
        {
            int srcCount = sourceWidth * sourceHeight * 4;
            if (sourceScratch is null || sourceScratch.Length < srcCount)
                sourceScratch = new byte[srcCount];
            Buffer.BlockCopy(source, 0, sourceScratch, 0, srcCount);

            int dstCount = targetWidth * targetHeight * 4;
            if (targetScratch is null || targetScratch.Length != dstCount)
                targetScratch = new byte[dstCount];
            var src = sourceScratch;
            var dst = targetScratch;

            fixed (byte* s = src)
            fixed (byte* d = dst)
            {
                for (int y = 0; y < targetHeight; y++)
                {
                    for (int x = 0; x < targetWidth; x++)
                    {
                        byte* o = d + (y * targetWidth + x) * 4;
                        if (!linear)
                        {
                            int sx = Math.Clamp((int)((x + 0.5) * sourceWidth / targetWidth), 0, sourceWidth - 1);
                            int sy = Math.Clamp((int)((y + 0.5) * sourceHeight / targetHeight), 0, sourceHeight - 1);
                            byte* p = s + (sy * sourceWidth + sx) * 4;
                            o[0] = p[0];
                            o[1] = p[1];
                            o[2] = p[2];
                            o[3] = p[3];
                        }
                        else
                        {
                            double u = (x + 0.5) * sourceWidth / targetWidth - 0.5;
                            double v = (y + 0.5) * sourceHeight / targetHeight - 0.5;
                            int x0 = (int)Math.Floor(u);
                            int y0 = (int)Math.Floor(v);
                            double tx = u - x0;
                            double ty = v - y0;
                            for (int c = 0; c < 4; c++)
                            {
                                double acc = 0d;
                                for (int j = 0; j < 2; j++)
                                {
                                    int sy = Math.Clamp(y0 + j, 0, sourceHeight - 1);
                                    double wy = j == 0 ? 1d - ty : ty;
                                    for (int i = 0; i < 2; i++)
                                    {
                                        int sx = Math.Clamp(x0 + i, 0, sourceWidth - 1);
                                        double wx = i == 0 ? 1d - tx : tx;
                                        acc += s[(sy * sourceWidth + sx) * 4 + c] * wy * wx;
                                    }
                                }
                                o[c] = (byte)Math.Clamp(acc, 0d, 255d);
                            }
                        }
                    }
                }
            }

            return dst;
        }
    }
}
