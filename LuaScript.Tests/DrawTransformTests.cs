using System.Numerics;

namespace LuaScript.Tests
{
    public class DrawTransformTests
    {
        [Fact]
        public void TryResolve_Affine_MapsSourceCenterToOffset()
        {
            var command = new DrawCommand(10d, 20d, 0d, 1d, 1d, 0d);

            Assert.True(DrawTransform.TryResolve(command, 4, 6, out var matrix));

            var mapped = Vector2.Transform(new Vector2(2f, 3f), matrix);
            Assert.Equal(10d, mapped.X, 3);
            Assert.Equal(20d, mapped.Y, 3);
        }

        [Fact]
        public void TryResolve_Affine_AppliesZoomAndAspect()
        {
            var command = new DrawCommand(0d, 0d, 0d, 2d, 1d, 0.5d);

            Assert.True(DrawTransform.TryResolve(command, 2, 2, out var matrix));

            var mapped = Vector2.Transform(new Vector2(2f, 2f), matrix);
            Assert.Equal(3d, mapped.X, 3);
            Assert.Equal(1d, mapped.Y, 3);
        }

        [Fact]
        public void TryResolve_Affine_ClampsAspect()
        {
            var command = new DrawCommand(0d, 0d, 0d, 2d, 1d, -3d);

            Assert.False(DrawTransform.TryResolve(command, 2, 2, out _));
        }

        [Fact]
        public void TryResolve_ZeroZoom_Fails()
        {
            var command = new DrawCommand(0d, 0d, 0d, 0d, 1d, 0d);

            Assert.False(DrawTransform.TryResolve(command, 2, 2, out _));
        }

        [Fact]
        public void TryResolve_NegativeZoom_Mirrors()
        {
            var command = new DrawCommand(0d, 0d, 0d, -1d, 1d, 0d);

            Assert.True(DrawTransform.TryResolve(command, 2, 2, out var matrix));

            var mapped = Vector2.Transform(new Vector2(2f, 2f), matrix);
            Assert.Equal(-1d, mapped.X, 3);
            Assert.Equal(-1d, mapped.Y, 3);
        }

        [Fact]
        public void TryResolve_IdentityPoly_YieldsIdentity()
        {
            double[] poly =
            [
                0, 0, 0, 2, 0, 0, 2, 2, 0, 0, 2, 0,
                0, 0, 2, 0, 2, 2, 0, 2,
                1,
            ];
            var command = new DrawCommand(0d, 0d, 0d, 1d, 1d, 0d, poly);

            Assert.True(DrawTransform.TryResolve(command, 2, 2, out var matrix));
            Assert.Equal(Matrix3x2.Identity, matrix);
        }

        [Fact]
        public void TryResolve_DegeneratePoly_Fails()
        {
            var command = new DrawCommand(0d, 0d, 0d, 1d, 1d, 0d, new double[DrawPolyMath.Length]);

            Assert.False(DrawTransform.TryResolve(command, 2, 2, out _));
        }
    }
}
