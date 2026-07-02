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

            public bool TryCompose(byte[] dst, int dstW, int dstH, byte[] src, int srcW, int srcH, in DrawCommand command)
            {
                Calls++;
                LastDst = dst;
                LastSrc = src;
                LastCommand = command;
                return true;
            }
        }

        private sealed class RejectingCompositor : IBufferCompositor
        {
            public int Calls;

            public bool TryCompose(byte[] dst, int dstW, int dstH, byte[] src, int srcW, int srcH, in DrawCommand command)
            {
                Calls++;
                return false;
            }
        }

        private sealed class ThrowingCompositor : IBufferCompositor
        {
            public int Calls;

            public bool TryCompose(byte[] dst, int dstW, int dstH, byte[] src, int srcW, int srcH, in DrawCommand command)
            {
                Calls++;
                throw new InvalidOperationException("device lost");
            }
        }

        private sealed class DisposableCompositor : IBufferCompositor, IDisposable
        {
            public bool Disposed;

            public bool TryCompose(byte[] dst, int dstW, int dstH, byte[] src, int srcW, int srcH, in DrawCommand command) => true;

            public void Dispose() => Disposed = true;
        }

        private static readonly DrawCommand Command = new(1d, 2d, 0d, 1d, 1d, 0d);

        [Fact]
        public void TryCompose_PrimarySucceeds_FallbackIsNotUsed()
        {
            var primary = new RecordingCompositor();
            var fallback = new RecordingCompositor();
            var compositor = new FallbackCompositor(primary, fallback);

            Assert.True(compositor.TryCompose(new byte[4], 1, 1, new byte[4], 1, 1, Command));

            Assert.Equal(1, primary.Calls);
            Assert.Equal(0, fallback.Calls);
            Assert.False(compositor.IsDegraded);
        }

        [Fact]
        public void TryCompose_PrimaryThrows_FallbackReceivesSameArguments()
        {
            var primary = new ThrowingCompositor();
            var fallback = new RecordingCompositor();
            Exception? reported = null;
            var compositor = new FallbackCompositor(primary, fallback, ex => reported = ex);
            var dst = new byte[4];
            var src = new byte[4];

            Assert.True(compositor.TryCompose(dst, 1, 1, src, 1, 1, Command));

            Assert.Equal(1, fallback.Calls);
            Assert.Same(dst, fallback.LastDst);
            Assert.Same(src, fallback.LastSrc);
            Assert.Equal(Command, fallback.LastCommand);
            Assert.IsType<InvalidOperationException>(reported);
            Assert.True(compositor.IsDegraded);
        }

        [Fact]
        public void TryCompose_AfterFailure_PrimaryIsSkippedAndDegradationReportedOnce()
        {
            var primary = new ThrowingCompositor();
            var fallback = new RecordingCompositor();
            int reports = 0;
            var compositor = new FallbackCompositor(primary, fallback, _ => reports++);

            compositor.TryCompose(new byte[4], 1, 1, new byte[4], 1, 1, Command);
            compositor.TryCompose(new byte[4], 1, 1, new byte[4], 1, 1, Command);

            Assert.Equal(1, primary.Calls);
            Assert.Equal(2, fallback.Calls);
            Assert.Equal(1, reports);
        }

        [Fact]
        public void TryCompose_PrimaryRejects_FallbackIsUsedWithoutDegradation()
        {
            var primary = new RejectingCompositor();
            var fallback = new RecordingCompositor();
            int reports = 0;
            var compositor = new FallbackCompositor(primary, fallback, _ => reports++);

            Assert.True(compositor.TryCompose(new byte[4], 1, 1, new byte[4], 1, 1, Command));
            Assert.True(compositor.TryCompose(new byte[4], 1, 1, new byte[4], 1, 1, Command));

            Assert.Equal(2, primary.Calls);
            Assert.Equal(2, fallback.Calls);
            Assert.Equal(0, reports);
            Assert.False(compositor.IsDegraded);
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
