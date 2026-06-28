using System.Numerics;
using LuaScript;

namespace LuaScript.Tests
{
    public class DrawPolyMathTests
    {
        private static double[] Quad(
            double x0, double y0, double x1, double y1,
            double x2, double y2, double x3, double y3,
            double u0, double v0, double u1, double v1,
            double u2, double v2, double u3, double v3,
            double alpha)
            => [x0, y0, 0, x1, y1, 0, x2, y2, 0, x3, y3, 0, u0, v0, u1, v1, u2, v2, u3, v3, alpha];

        [Fact]
        public void TrySolveAffine_MapsUvCornersToQuad()
        {
            var poly = Quad(
                10, 20, 18, 20, 18, 32, 10, 32,
                0, 0, 4, 0, 4, 4, 0, 4, 1);

            Assert.True(DrawPolyMath.TrySolveAffine(poly, out var m));

            AssertMaps(m, 0, 0, 10, 20);
            AssertMaps(m, 4, 0, 18, 20);
            AssertMaps(m, 4, 4, 18, 32);
            AssertMaps(m, 0, 4, 10, 32);
            AssertMaps(m, 2, 2, 14, 26);
        }

        [Fact]
        public void TrySolveAffine_HandlesRotatedQuad()
        {
            var poly = Quad(
                0, 0, 0, 10, -10, 10, -10, 0,
                0, 0, 10, 0, 10, 10, 0, 10, 1);

            Assert.True(DrawPolyMath.TrySolveAffine(poly, out var m));

            AssertMaps(m, 0, 0, 0, 0);
            AssertMaps(m, 10, 0, 0, 10);
            AssertMaps(m, 0, 10, -10, 0);
        }

        [Fact]
        public void TrySolveAffine_ReturnsFalse_OnDegenerateUv()
        {
            var poly = Quad(
                0, 0, 10, 0, 10, 10, 0, 10,
                0, 0, 0, 0, 0, 0, 0, 0, 1);

            Assert.False(DrawPolyMath.TrySolveAffine(poly, out _));
        }

        private static void AssertMaps(Matrix3x2 m, float u, float v, float x, float y)
        {
            var p = Vector2.Transform(new Vector2(u, v), m);
            Assert.Equal(x, p.X, 3);
            Assert.Equal(y, p.Y, 3);
        }
    }
}
