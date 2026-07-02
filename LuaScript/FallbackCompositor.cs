using System;

namespace LuaScript
{
    internal sealed class FallbackCompositor(
        IBufferCompositor primary,
        IBufferCompositor fallback,
        Action<Exception>? onDegraded = null) : IBufferCompositor, IDisposable
    {
        private bool _degraded;

        public bool IsDegraded => _degraded;

        public void Compose(byte[] dst, int dstW, int dstH, byte[] src, int srcW, int srcH, in DrawCommand command)
        {
            if (!_degraded)
            {
                try
                {
                    primary.Compose(dst, dstW, dstH, src, srcW, srcH, command);
                    return;
                }
                catch (Exception ex)
                {
                    _degraded = true;
                    onDegraded?.Invoke(ex);
                }
            }
            fallback.Compose(dst, dstW, dstH, src, srcW, srcH, command);
        }

        public void Dispose()
        {
            (primary as IDisposable)?.Dispose();
            (fallback as IDisposable)?.Dispose();
        }
    }
}
