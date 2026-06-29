using System.Threading.Tasks;

namespace LuaScript.Engine.Kernel
{
    internal sealed class CpuKernel(CpuKernel.RowDelegate row)
    {
        public delegate void RowDelegate(byte[] buffer, int width, int rowBase, double y, double[] uniforms);

        private const int ParallelThreshold = 64;

        private readonly RowDelegate _row = row;

        public void Execute(byte[] buffer, int width, int height, double[] uniforms)
        {
            var row = _row;
            int stride = width * 4;

            if (height < ParallelThreshold)
            {
                for (int y = 0; y < height; y++)
                    row(buffer, width, y * stride, y, uniforms);
                return;
            }

            Parallel.For(0, height, y => row(buffer, width, y * stride, y, uniforms));
        }
    }
}
