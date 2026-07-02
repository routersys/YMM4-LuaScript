using LuaScript;

namespace LuaScript.Tests
{
    public class SoftwareCompositorTests
    {
        private static byte[] Solid(int w, int h, byte b, byte g, byte r, byte a)
        {
            var buffer = new byte[w * h * 4];
            for (int i = 0; i < w * h; i++)
            {
                buffer[i * 4] = b;
                buffer[i * 4 + 1] = g;
                buffer[i * 4 + 2] = r;
                buffer[i * 4 + 3] = a;
            }
            return buffer;
        }

        [Fact]
        public void DrawInto_NearestOpaque_BlitsSinglePixel()
        {
            var dst = new byte[3 * 3 * 4];
            var src = Solid(1, 1, 255, 255, 255, 255);

            SoftwareCompositor.DrawInto(dst, 3, 3, src, 1, 1, 1.5, 1.5, 1d, 0d, 1d, linear: false);

            int center = (1 * 3 + 1) * 4;
            Assert.Equal(255, dst[center]);
            Assert.Equal(255, dst[center + 1]);
            Assert.Equal(255, dst[center + 2]);
            Assert.Equal(255, dst[center + 3]);

            Assert.Equal(0, dst[0]);
            Assert.Equal(0, dst[(0 * 3 + 0) * 4 + 3]);
        }

        [Fact]
        public void DrawInto_HalfAlphaOverOpaque_BlendsPremultiplied()
        {
            var dst = Solid(1, 1, 0, 0, 0, 255);
            var src = Solid(1, 1, 255, 255, 255, 255);

            SoftwareCompositor.DrawInto(dst, 1, 1, src, 1, 1, 0.5, 0.5, 1d, 0d, 0.5, linear: false);

            Assert.Equal(127, dst[0]);
            Assert.Equal(127, dst[1]);
            Assert.Equal(127, dst[2]);
            Assert.Equal(255, dst[3]);
        }

        [Fact]
        public void DrawInto_OutOfBounds_LeavesDestinationUnchanged()
        {
            var dst = new byte[2 * 2 * 4];
            var src = Solid(1, 1, 255, 255, 255, 255);

            SoftwareCompositor.DrawInto(dst, 2, 2, src, 1, 1, 100d, 100d, 1d, 0d, 1d, linear: false);

            foreach (var value in dst)
                Assert.Equal(0, value);
        }

        [Fact]
        public void Compose_AffineCommand_MatchesDrawInto()
        {
            var src = Solid(1, 1, 10, 20, 30, 255);
            var actual = new byte[3 * 3 * 4];
            var expected = new byte[3 * 3 * 4];
            var command = new DrawCommand(1.5, 1.5, 0d, 1d, 1d, 0d, Antialias: 0d);

            new SoftwareCompositor().Compose(actual, 3, 3, src, 1, 1, command);
            SoftwareCompositor.DrawInto(expected, 3, 3, src, 1, 1, 1.5, 1.5, 1d, 0d, 1d, linear: false);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Compose_PolyCommand_MatchesDrawPolyInto()
        {
            var src = Solid(2, 2, 40, 50, 60, 255);
            var actual = new byte[2 * 2 * 4];
            var expected = new byte[2 * 2 * 4];
            double[] poly =
            [
                0, 0, 0, 2, 0, 0, 2, 2, 0, 0, 2, 0,
                0, 0, 2, 0, 2, 2, 0, 2,
                1,
            ];
            var command = new DrawCommand(0d, 0d, 0d, 1d, 1d, 0d, poly, Antialias: 0d);

            new SoftwareCompositor().Compose(actual, 2, 2, src, 2, 2, command);
            SoftwareCompositor.DrawPolyInto(expected, 2, 2, src, 2, 2, poly, 1d, linear: false);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Compose_AntialiasCommand_UsesLinearSampling()
        {
            var src = new byte[2 * 1 * 4];
            src[3] = 255;
            src[4] = 255;
            src[5] = 255;
            src[6] = 255;
            src[7] = 255;
            var actual = new byte[4 * 1 * 4];
            var expected = new byte[4 * 1 * 4];
            var command = new DrawCommand(2d, 0.5, 0d, 2d, 1d, 0d, Antialias: 1d);

            new SoftwareCompositor().Compose(actual, 4, 1, src, 2, 1, command);
            SoftwareCompositor.DrawInto(expected, 4, 1, src, 2, 1, 2d, 0.5, 2d, 0d, 1d, linear: true);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void DrawPolyInto_IdentityAffine_CopiesSource()
        {
            var src = new byte[2 * 2 * 4];
            for (int i = 0; i < 4; i++)
            {
                src[i * 4] = (byte)(i + 1);
                src[i * 4 + 1] = (byte)(i + 10);
                src[i * 4 + 2] = (byte)(i + 20);
                src[i * 4 + 3] = 255;
            }
            var dst = new byte[2 * 2 * 4];

            double[] poly =
            [
                0, 0, 0, 2, 0, 0, 2, 2, 0, 0, 2, 0,
                0, 0, 2, 0, 2, 2, 0, 2,
                1,
            ];

            SoftwareCompositor.DrawPolyInto(dst, 2, 2, src, 2, 2, poly, 1d, linear: false);

            Assert.Equal(src, dst);
        }
    }
}
