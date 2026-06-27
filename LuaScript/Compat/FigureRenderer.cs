using System;

namespace LuaScript.Compat
{
    internal static class FigureRenderer
    {
        private enum Shape
        {
            Ellipse,
            Rectangle,
            Triangle,
            Pentagon,
            Hexagon,
            Star,
        }

        public static byte[] Render(string name, int width, int height, int colorRgb, double lineWidth)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);
            var buffer = new byte[width * height * 4];

            Shape shape = Resolve(name);
            double hx = width / 2d;
            double hy = height / 2d;

            double r = (colorRgb >> 16) & 0xFF;
            double g = (colorRgb >> 8) & 0xFF;
            double b = colorRgb & 0xFF;

            (double X, double Y)[]? polygon = shape switch
            {
                Shape.Triangle => RegularPolygon(3, hx, hy),
                Shape.Pentagon => RegularPolygon(5, hx, hy),
                Shape.Hexagon => RegularPolygon(6, hx, hy),
                Shape.Star => StarPolygon(hx, hy),
                _ => null,
            };

            bool filled = lineWidth <= 0d;

            for (int y = 0; y < height; y++)
            {
                double py = y + 0.5 - hy;
                int row = y * width * 4;
                for (int x = 0; x < width; x++)
                {
                    double px = x + 0.5 - hx;
                    double d = shape switch
                    {
                        Shape.Ellipse => EllipseSdf(px, py, hx, hy),
                        Shape.Rectangle => BoxSdf(px, py, hx, hy),
                        _ => PolygonSdf(px, py, polygon!),
                    };
                    if (!filled)
                        d = Math.Max(d, -d - lineWidth);

                    double coverage = 0.5 - d;
                    if (coverage <= 0d)
                        continue;
                    if (coverage > 1d)
                        coverage = 1d;

                    int i = row + x * 4;
                    buffer[i] = ToByte(b * coverage);
                    buffer[i + 1] = ToByte(g * coverage);
                    buffer[i + 2] = ToByte(r * coverage);
                    buffer[i + 3] = ToByte(255d * coverage);
                }
            }

            return buffer;
        }

        private static Shape Resolve(string name)
        {
            switch (name.Trim())
            {
                case "円":
                case "楕円":
                case "circle":
                case "ellipse":
                    return Shape.Ellipse;
                case "三角形":
                case "triangle":
                    return Shape.Triangle;
                case "五角形":
                case "pentagon":
                    return Shape.Pentagon;
                case "六角形":
                case "hexagon":
                    return Shape.Hexagon;
                case "星形":
                case "star":
                    return Shape.Star;
                default:
                    return Shape.Rectangle;
            }
        }

        private static (double X, double Y)[] RegularPolygon(int sides, double hx, double hy)
        {
            var vertices = new (double X, double Y)[sides];
            for (int k = 0; k < sides; k++)
            {
                double angle = -Math.PI / 2d + 2d * Math.PI * k / sides;
                vertices[k] = (Math.Cos(angle) * hx, Math.Sin(angle) * hy);
            }
            return vertices;
        }

        private static (double X, double Y)[] StarPolygon(double hx, double hy)
        {
            const double innerRatio = 0.38196601125010515d;
            var vertices = new (double X, double Y)[10];
            for (int k = 0; k < 10; k++)
            {
                double angle = -Math.PI / 2d + Math.PI * k / 5d;
                double radius = (k & 1) == 0 ? 1d : innerRatio;
                vertices[k] = (Math.Cos(angle) * hx * radius, Math.Sin(angle) * hy * radius);
            }
            return vertices;
        }

        private static double EllipseSdf(double px, double py, double ax, double ay)
        {
            if (ax <= 0d || ay <= 0d)
                return double.MaxValue;
            double kx = px / ax;
            double ky = py / ay;
            double k1 = Math.Sqrt(kx * kx + ky * ky);
            if (k1 == 0d)
                return -Math.Min(ax, ay);
            double nx = kx / ax;
            double ny = ky / ay;
            double k2 = Math.Sqrt(nx * nx + ny * ny);
            return k1 * (k1 - 1d) / k2;
        }

        private static double BoxSdf(double px, double py, double hx, double hy)
        {
            double qx = Math.Abs(px) - hx;
            double qy = Math.Abs(py) - hy;
            double ox = Math.Max(qx, 0d);
            double oy = Math.Max(qy, 0d);
            return Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(qx, qy), 0d);
        }

        private static double PolygonSdf(double px, double py, (double X, double Y)[] v)
        {
            int n = v.Length;
            double dx = px - v[0].X;
            double dy = py - v[0].Y;
            double d = dx * dx + dy * dy;
            double s = 1d;

            for (int i = 0, j = n - 1; i < n; j = i, i++)
            {
                double ex = v[j].X - v[i].X;
                double ey = v[j].Y - v[i].Y;
                double wx = px - v[i].X;
                double wy = py - v[i].Y;
                double t = Clamp((wx * ex + wy * ey) / (ex * ex + ey * ey), 0d, 1d);
                double bx = wx - ex * t;
                double by = wy - ey * t;
                d = Math.Min(d, bx * bx + by * by);

                bool c1 = py >= v[i].Y;
                bool c2 = py < v[j].Y;
                bool c3 = ex * wy > ey * wx;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3))
                    s = -s;
            }

            return s * Math.Sqrt(d);
        }

        private static double Clamp(double v, double lo, double hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }

        private static byte ToByte(double v)
        {
            int i = (int)(v + 0.5d);
            if (i < 0) return 0;
            if (i > 255) return 255;
            return (byte)i;
        }
    }
}
