using System;

namespace LuaScript.Tests
{
    public class FallbackCompositorTests
    {
        private sealed class RecordingCompositor : IBufferCompositor
        {
            public int Calls;
            public byte[]? LastDst;
            public byte[]? LastSrc;
            public DrawCommand LastCommand;

            public void Compose(byte[] dst, int dstW, int dstH, byte[] src, int srcW, int srcH, in DrawCommand command)
            {
                Calls++;
                LastDst = dst;
                LastSrc = src;
                LastCommand = command;
            }
        }

        private sealed class ThrowingCompositor : IBufferCompositor
        {
            public int Calls;

            public void Compose(byte[] dst, int dstW, int dstH, byte[] src, int srcW, int srcH, in DrawCommand command)
            {
                Calls++;
                throw new InvalidOperationException("device lost");
            }
        }

        private sealed class DisposableCompositor : IBufferCompositor, IDisposable
        {
            public bool Disposed;

            public void Compose(byte[] dst, int dstW, int dstH, byte[] src, int srcW, int srcH, in DrawCommand command)
            {
            }

            public void Dispose() => Disposed = true;
        }

        private static readonly DrawCommand Command = new(1d, 2d, 0d, 1d, 1d, 0d);

        [Fact]
        public void Compose_PrimarySucceeds_FallbackIsNotUsed()
        {
            var primary = new RecordingCompositor();
            var fallback = new RecordingCompositor();
            var compositor = new FallbackCompositor(primary, fallback);

            compositor.Compose(new byte[4], 1, 1, new byte[4], 1, 1, Command);

            Assert.Equal(1, primary.Calls);
            Assert.Equal(0, fallback.Calls);
            Assert.False(compositor.IsDegraded);
        }

        [Fact]
        public void Compose_PrimaryThrows_FallbackReceivesSameArguments()
        {
            var primary = new ThrowingCompositor();
            var fallback = new RecordingCompositor();
            Exception? reported = null;
            var compositor = new FallbackCompositor(primary, fallback, ex => reported = ex);
            var dst = new byte[4];
            var src = new byte[4];

            compositor.Compose(dst, 1, 1, src, 1, 1, Command);

            Assert.Equal(1, fallback.Calls);
            Assert.Same(dst, fallback.LastDst);
            Assert.Same(src, fallback.LastSrc);
            Assert.Equal(Command, fallback.LastCommand);
            Assert.IsType<InvalidOperationException>(reported);
            Assert.True(compositor.IsDegraded);
        }

        [Fact]
        public void Compose_AfterFailure_PrimaryIsSkippedAndDegradationReportedOnce()
        {
            var primary = new ThrowingCompositor();
            var fallback = new RecordingCompositor();
            int reports = 0;
            var compositor = new FallbackCompositor(primary, fallback, _ => reports++);

            compositor.Compose(new byte[4], 1, 1, new byte[4], 1, 1, Command);
            compositor.Compose(new byte[4], 1, 1, new byte[4], 1, 1, Command);

            Assert.Equal(1, primary.Calls);
            Assert.Equal(2, fallback.Calls);
            Assert.Equal(1, reports);
        }

        [Fact]
        public void Dispose_DisposesDisposableLanes()
        {
            var primary = new DisposableCompositor();
            var fallback = new DisposableCompositor();
            var compositor = new FallbackCompositor(primary, fallback);

            compositor.Dispose();

            Assert.True(primary.Disposed);
            Assert.True(fallback.Disposed);
        }
    }
}
