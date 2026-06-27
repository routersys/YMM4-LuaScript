using LuaScript.Compat;

namespace LuaScript.Tests
{
    public class FigureRendererTests
    {
        private static (int B, int G, int R, int A) Pixel(byte[] buffer, int width, int x, int y)
        {
            int i = (y * width + x) * 4;
            return (buffer[i], buffer[i + 1], buffer[i + 2], buffer[i + 3]);
        }

        [Fact]
        public void Render_ProducesBufferOfRequestedSize()
        {
            var buffer = FigureRenderer.Render("円", 64, 48, 0xFFFFFF, 0d);
            Assert.Equal(64 * 48 * 4, buffer.Length);
        }

        [Fact]
        public void FilledCircle_CenterIsOpaqueAndColored()
        {
            int w = 100, h = 100;
            var buffer = FigureRenderer.Render("円", w, h, 0xFF0000, 0d);
            var (b, g, r, a) = Pixel(buffer, w, w / 2, h / 2);
            Assert.Equal(255, a);
            Assert.Equal(255, r);
            Assert.Equal(0, g);
            Assert.Equal(0, b);
        }

        [Fact]
        public void FilledCircle_CornerIsTransparent()
        {
            int w = 100, h = 100;
            var buffer = FigureRenderer.Render("円", w, h, 0xFF0000, 0d);
            var (_, _, _, a) = Pixel(buffer, w, 0, 0);
            Assert.Equal(0, a);
        }

        [Fact]
        public void Color_IsStoredPremultipliedAtHalfCoverageBoundary()
        {
            int w = 100, h = 100;
            var buffer = FigureRenderer.Render("四角形", w, h, 0x102030, 0d);
            var (b, g, r, a) = Pixel(buffer, w, w / 2, h / 2);
            Assert.Equal(255, a);
            Assert.Equal(0x10, r);
            Assert.Equal(0x20, g);
            Assert.Equal(0x30, b);
        }

        [Fact]
        public void FilledRectangle_CornerIsOpaque()
        {
            int w = 80, h = 80;
            var buffer = FigureRenderer.Render("四角形", w, h, 0xFFFFFF, 0d);
            var (_, _, _, a) = Pixel(buffer, w, 1, 1);
            Assert.Equal(255, a);
        }

        [Fact]
        public void OutlinedCircle_IsHollowAtCenterButSolidNearEdge()
        {
            int w = 100, h = 100;
            var buffer = FigureRenderer.Render("円", w, h, 0xFFFFFF, 8d);
            var (_, _, _, center) = Pixel(buffer, w, w / 2, h / 2);
            var (_, _, _, nearEdge) = Pixel(buffer, w, w / 2 + 46, h / 2);
            Assert.Equal(0, center);
            Assert.Equal(255, nearEdge);
        }

        [Fact]
        public void Triangle_CornersEmptyCentroidFilled()
        {
            int w = 100, h = 100;
            var buffer = FigureRenderer.Render("三角形", w, h, 0xFFFFFF, 0d);
            var (_, _, _, topLeft) = Pixel(buffer, w, 2, 2);
            var (_, _, _, topRight) = Pixel(buffer, w, w - 3, 2);
            var (_, _, _, centroid) = Pixel(buffer, w, w / 2, h / 2);
            Assert.Equal(0, topLeft);
            Assert.Equal(0, topRight);
            Assert.Equal(255, centroid);
        }

        [Fact]
        public void UnknownName_FallsBackToRectangle()
        {
            int w = 40, h = 40;
            var buffer = FigureRenderer.Render("unknown", w, h, 0xFFFFFF, 0d);
            var (_, _, _, corner) = Pixel(buffer, w, 1, 1);
            Assert.Equal(255, corner);
        }
    }
}
