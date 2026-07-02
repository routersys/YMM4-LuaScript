using System;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;

namespace LuaScript.Engine.Kernel
{
    internal sealed class GpuKernel : IDisposable
    {
        private readonly GpuKernelEffect _effect;
        private readonly float[] _packed = new float[HlslKernelEmitter.ConstantFloatCount];
        private readonly byte[] _bytes = new byte[HlslKernelEmitter.ConstantFloatCount * sizeof(float)];
        private ID2D1Image? _output;

        public GpuKernel(IGraphicsDevicesAndContext devices, KernelProgram program)
            => _effect = new GpuKernelEffect(devices, HlslKernelEmitter.Emit(program));

        public bool IsReady => _effect.IsReady;

        public ID2D1Image Apply(ID2D1Image input, double[] uniforms)
        {
            KernelUniformBinding.Pack(uniforms, _packed);
            Buffer.BlockCopy(_packed, 0, _bytes, 0, _bytes.Length);
            _effect.SetConstants(_bytes);
            _effect.SetInput(0, input, true);
            return _output ??= _effect.Output;
        }

        public void Dispose()
        {
            _output?.Dispose();
            _output = null;
            _effect.Dispose();
        }
    }
}
