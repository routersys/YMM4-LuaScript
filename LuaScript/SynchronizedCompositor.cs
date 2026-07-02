using System;
using System.Threading;

namespace LuaScript
{
    internal sealed class SynchronizedCompositor(IBufferCompositor inner, SemaphoreSlim gate) : IBufferCompositor, IDisposable
    {
        public bool TryCompose(byte[] dst, int dstW, int dstH, byte[] src, int srcW, int srcH, in DrawCommand command)
        {
            gate.Wait();
            try
            {
                return inner.TryCompose(dst, dstW, dstH, src, srcW, srcH, command);
            }
            finally
            {
                gate.Release();
            }
        }

        public void Dispose() => (inner as IDisposable)?.Dispose();
    }
}
