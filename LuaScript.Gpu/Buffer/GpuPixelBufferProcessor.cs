using LuaScript.Engine.Shader;

namespace LuaScript.Engine.Processing
{
    internal sealed class GpuPixelBufferProcessor(PixelShaderRunner runner) : IPixelBufferProcessor
    {
        private readonly object _gate = new();
        private readonly GpuFillOperation _fill = new(runner);
        private readonly GpuConvolveOperation _convolve = new(runner);
        private readonly GpuResizeOperation _resize = new(runner);

        public bool TryFill(byte[] target, int width, int height, double r, double g, double b, double a, int x, int y, int fillWidth, int fillHeight)
        {
            lock (_gate)
                return _fill.TryFill(target, width, height, r, g, b, a, x, y, fillWidth, fillHeight);
        }

        public bool TryConvolve(byte[] target, int width, int height, double[] kernel, int size, double divisor, double offset)
        {
            lock (_gate)
                return _convolve.TryConvolve(target, width, height, kernel, size, divisor, offset);
        }

        public bool TryResize(byte[] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, bool linear, out byte[]? target)
        {
            lock (_gate)
                return _resize.TryResize(source, sourceWidth, sourceHeight, targetWidth, targetHeight, linear, out target);
        }
    }
}
