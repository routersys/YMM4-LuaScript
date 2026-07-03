using System.Diagnostics;
using System.Runtime.Intrinsics;
using System.Threading.Tasks;

namespace LuaScript.Engine.Kernel
{
    internal sealed class CpuKernel(CpuKernel.RowDelegate row, CpuKernel.RowDelegate? block)
    {
        public delegate void RowDelegate(byte[] buffer, int rowBase, int xStart, int xEnd, double y, double[] uniforms);

        private const int ParallelThreshold = 64;
        private const int WarmupRunsPerPath = 1;
        private const int TimedRunsPerPath = 3;

        private readonly RowDelegate _row = row;
        private readonly RowDelegate? _block = block;
        private long _rowTicks;
        private long _blockTicks;
        private int _rowRuns;
        private int _blockRuns;
        private bool _nextIsBlock = true;
        private bool _decided;
        private bool _useBlock;

        public bool? BlockAdopted => _decided ? _useBlock : null;

        public void Execute(byte[] buffer, int width, int height, double[] uniforms)
        {
            if (_block is null || !Vector256.IsHardwareAccelerated)
            {
                Run(buffer, width, height, uniforms, false);
                return;
            }

            if (_decided)
            {
                Run(buffer, width, height, uniforms, _useBlock);
                return;
            }

            bool useBlock = _nextIsBlock;
            _nextIsBlock = !_nextIsBlock;

            long start = Stopwatch.GetTimestamp();
            Run(buffer, width, height, uniforms, useBlock);
            long elapsed = Stopwatch.GetTimestamp() - start;

            if (useBlock)
            {
                if (_blockRuns >= WarmupRunsPerPath)
                    _blockTicks += elapsed;
                _blockRuns++;
            }
            else
            {
                if (_rowRuns >= WarmupRunsPerPath)
                    _rowTicks += elapsed;
                _rowRuns++;
            }

            if (_rowRuns >= WarmupRunsPerPath + TimedRunsPerPath &&
                _blockRuns >= WarmupRunsPerPath + TimedRunsPerPath)
            {
                _useBlock = _blockTicks < _rowTicks;
                _decided = true;
            }
        }

        private void Run(byte[] buffer, int width, int height, double[] uniforms, bool useBlock)
        {
            var row = _row;
            var block = useBlock ? _block : null;
            int stride = width * 4;
            int blockWidth = block is null ? 0 : width & ~3;

            void RunRow(int y)
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
                    RunRow(y);
                return;
            }

            Parallel.For(0, height, RunRow);
        }
    }
}
