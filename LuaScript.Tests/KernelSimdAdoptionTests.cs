using System;
using System.Runtime.Intrinsics;
using System.Threading;
using LuaScript.Engine.Kernel;

namespace LuaScript.Tests
{
    public class KernelSimdAdoptionTests
    {
        private const int CalibrationExecutes = 8;

        private static CpuKernel.RowDelegate Path(Action onCall, int delayMs) =>
            (buffer, rowBase, xStart, xEnd, y, uniforms) =>
            {
                onCall();
                if (delayMs > 0)
                    Thread.Sleep(delayMs);
            };

        [Fact]
        public void SlowerBlock_SettlesOnScalar()
        {
            if (!Vector256.IsHardwareAccelerated)
                return;

            int rowCalls = 0, blockCalls = 0;
            var kernel = new CpuKernel(Path(() => rowCalls++, 0), Path(() => blockCalls++, 5));
            var buffer = new byte[8 * 4];
            var uniforms = new double[1];

            for (int i = 0; i < CalibrationExecutes - 1; i++)
                kernel.Execute(buffer, 8, 1, uniforms);
            Assert.Null(kernel.BlockAdopted);

            kernel.Execute(buffer, 8, 1, uniforms);
            Assert.True(kernel.BlockAdopted.HasValue);
            Assert.False(kernel.BlockAdopted.Value);

            int blockCallsAtDecision = blockCalls;
            int rowCallsAtDecision = rowCalls;
            for (int i = 0; i < 5; i++)
                kernel.Execute(buffer, 8, 1, uniforms);

            Assert.Equal(blockCallsAtDecision, blockCalls);
            Assert.Equal(rowCallsAtDecision + 5, rowCalls);
        }

        [Fact]
        public void FasterBlock_SettlesOnBlock()
        {
            if (!Vector256.IsHardwareAccelerated)
                return;

            int rowCalls = 0, blockCalls = 0;
            var kernel = new CpuKernel(Path(() => rowCalls++, 5), Path(() => blockCalls++, 0));
            var buffer = new byte[8 * 4];
            var uniforms = new double[1];

            for (int i = 0; i < CalibrationExecutes; i++)
                kernel.Execute(buffer, 8, 1, uniforms);
            Assert.True(kernel.BlockAdopted.HasValue);
            Assert.True(kernel.BlockAdopted.Value);

            int rowCallsAtDecision = rowCalls;
            int blockCallsAtDecision = blockCalls;
            for (int i = 0; i < 5; i++)
                kernel.Execute(buffer, 8, 1, uniforms);

            Assert.Equal(rowCallsAtDecision, rowCalls);
            Assert.Equal(blockCallsAtDecision + 5, blockCalls);
        }

        [Fact]
        public void CalibrationAlternatesBothPaths()
        {
            if (!Vector256.IsHardwareAccelerated)
                return;

            int rowCalls = 0, blockCalls = 0;
            var kernel = new CpuKernel(Path(() => rowCalls++, 0), Path(() => blockCalls++, 0));
            var buffer = new byte[8 * 4];
            var uniforms = new double[1];

            for (int i = 0; i < CalibrationExecutes; i++)
                kernel.Execute(buffer, 8, 1, uniforms);

            Assert.Equal(CalibrationExecutes / 2, rowCalls);
            Assert.Equal(CalibrationExecutes / 2, blockCalls);
        }

        [Fact]
        public void WithoutBlock_RunsScalarWithoutDecision()
        {
            int rowCalls = 0;
            var kernel = new CpuKernel(Path(() => rowCalls++, 0), null);
            var buffer = new byte[8 * 4];
            var uniforms = new double[1];

            for (int i = 0; i < CalibrationExecutes + 2; i++)
                kernel.Execute(buffer, 8, 1, uniforms);

            Assert.Null(kernel.BlockAdopted);
            Assert.Equal(CalibrationExecutes + 2, rowCalls);
        }

        [Fact]
        public void BlockPath_UsesScalarRowForRemainder()
        {
            if (!Vector256.IsHardwareAccelerated)
                return;

            int rowCalls = 0, blockCalls = 0;
            var kernel = new CpuKernel(Path(() => rowCalls++, 0), Path(() => blockCalls++, 0));
            var buffer = new byte[10 * 4];
            var uniforms = new double[1];

            kernel.Execute(buffer, 10, 1, uniforms);
            Assert.Equal(1, blockCalls);
            Assert.Equal(1, rowCalls);
        }
    }
}
