using System.Runtime.Intrinsics;
using System.Threading.Tasks;

namespace LuaScript.Engine.Kernel
{
    internal sealed class CpuKernel(CpuKernel.RowDelegate row, CpuKernel.RowDelegate? block)
    {
        public delegate void RowDelegate(byte[] buffer, int rowBase, int xStart, int xEnd, double y, double[] uniforms);

        private const int ParallelThreshold = 64;

        private readonly RowDelegate _row = row;
        private readonly RowDelegate? _block = block;

        public void Execute(byte[] buffer, int width, int height, double[] uniforms)
        {
            var row = _row;
            var block = Vector256.IsHardwareAccelerated ? _block : null;
            int stride = width * 4;
            int blockWidth = block is null ? 0 : width & ~3;

            void Run(int y)
            {
                int rowBase = y * stride;
                if (blockWidth > 0)
                    block!(buffer, rowBase, 0, blockWidth, y, uniforms);
                if (blockWidth < width)
                    row(buffer, rowBase, blockWidth, width, y, uniforms);
            }

            if (height < ParallelThreshold)
            {
                for (int y = 0; y < height; y++)
                    Run(y);
                return;
            }

            Parallel.For(0, height, Run);
        }
    }
}
