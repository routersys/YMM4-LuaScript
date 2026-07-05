using System;
using LuaScript.Engine.Shader;

namespace LuaScript.Engine.Processing
{
    internal abstract class GpuPixelOperation(PixelShaderRunner runner)
    {
        protected const int MaxTextureSize = 16384;

        protected bool Run(byte[] bytecode, ReadOnlySpan<PixelShaderInput> resources, ReadOnlySpan<float> constants, byte[] target, int width, int height)
        {
            return runner.TryRunHardware(
                bytecode,
                resources,
                constants,
                PixelShaderBlend.Copy,
                PixelShaderSampler.None,
                target,
                width,
                height,
                out _) == PixelShaderRunStatus.Success;
        }

        protected static bool IsTextureSupported(byte[] buffer, int width, int height)
        {
            return IsTextureSizeSupported(width, height) && (long)buffer.Length >= (long)width * height * 4;
        }

        protected static bool IsTextureSizeSupported(int width, int height)
        {
            return width > 0 && height > 0 && width <= MaxTextureSize && height <= MaxTextureSize;
        }

        protected static bool Equal(byte[] expected, byte[] actual)
        {
            return expected.AsSpan().SequenceEqual(actual);
        }

        protected static byte[] CopyProbe(byte[] source, int width, int height, int probeWidth, int probeHeight)
        {
            var data = new byte[probeWidth * probeHeight * 4];
            for (int y = 0; y < probeHeight; y++)
                Buffer.BlockCopy(source, y * width * 4, data, y * probeWidth * 4, probeWidth * 4);
            return data;
        }
    }
}
