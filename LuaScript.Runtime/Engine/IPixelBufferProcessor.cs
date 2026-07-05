namespace LuaScript
{
    internal interface IPixelBufferProcessor
    {
        bool TryFill(byte[] target, int width, int height, double r, double g, double b, double a, int x, int y, int fillWidth, int fillHeight);

        bool TryConvolve(byte[] target, int width, int height, double[] kernel, int size, double divisor, double offset);

        bool TryResize(byte[] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, bool linear, out byte[]? target);
    }
}
