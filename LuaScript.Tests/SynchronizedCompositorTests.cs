using System;
using System.Threading;

namespace LuaScript.Tests
{
    public class SynchronizedCompositorTests
    {
        private sealed class GateObservingCompositor(SemaphoreSlim gate, bool result) : IBufferCompositor
        {
            public int Calls;
            public int GateCountDuringCompose = -1;

            public bool TryCompose(byte[] dst, int dstW, int dstH, byte[] src, int srcW, int srcH, in DrawCommand command)
            {
                Calls++;
                GateCountDuringCompose = gate.CurrentCount;
                return result;
            }
        }

        private sealed class ThrowingCompositor : IBufferCompositor
        {
            public bool TryCompose(byte[] dst, int dstW, int dstH, byte[] src, int srcW, int srcH, in DrawCommand command) =>
                throw new InvalidOperationException("device lost");
        }

        private sealed class DisposableCompositor : IBufferCompositor, IDisposable
        {
            public bool Disposed;

            public bool TryCompose(byte[] dst, int dstW, int dstH, byte[] src, int srcW, int srcH, in DrawCommand command) => true;

            public void Dispose() => Disposed = true;
        }

        private static readonly DrawCommand Command = new(0d, 0d, 0d, 1d, 1d, 0d);

        [Fact]
        public void TryCompose_HoldsGateDuringComposeAndReleasesAfter()
        {
            using var gate = new SemaphoreSlim(1, 1);
            var inner = new GateObservingCompositor(gate, result: true);
            var compositor = new SynchronizedCompositor(inner, gate);

            Assert.True(compositor.TryCompose(new byte[4], 1, 1, new byte[4], 1, 1, Command));

            Assert.Equal(1, inner.Calls);
            Assert.Equal(0, inner.GateCountDuringCompose);
            Assert.Equal(1, gate.CurrentCount);
        }

        [Fact]
        public void TryCompose_PassesRejectionThrough()
        {
            using var gate = new SemaphoreSlim(1, 1);
            var inner = new GateObservingCompositor(gate, result: false);
            var compositor = new SynchronizedCompositor(inner, gate);

            Assert.False(compositor.TryCompose(new byte[4], 1, 1, new byte[4], 1, 1, Command));
            Assert.Equal(1, gate.CurrentCount);
        }

        [Fact]
        public void TryCompose_ReleasesGateWhenInnerThrows()
        {
            using var gate = new SemaphoreSlim(1, 1);
            var compositor = new SynchronizedCompositor(new ThrowingCompositor(), gate);

            Assert.Throws<InvalidOperationException>(() =>
                compositor.TryCompose(new byte[4], 1, 1, new byte[4], 1, 1, Command));
            Assert.Equal(1, gate.CurrentCount);
        }

        [Fact]
        public void Dispose_DisposesInnerButNotGate()
        {
            using var gate = new SemaphoreSlim(1, 1);
            var inner = new DisposableCompositor();
            var compositor = new SynchronizedCompositor(inner, gate);

            compositor.Dispose();

            Assert.True(inner.Disposed);
            gate.Wait();
            gate.Release();
        }
    }
}
