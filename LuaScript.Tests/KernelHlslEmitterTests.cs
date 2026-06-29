using LuaScript.Engine.Kernel;

namespace LuaScript.Tests
{
    public sealed class KernelHlslEmitterTests
    {
        private const string Grayscale =
            "for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) " +
            "local l=0.298912*r+0.586611*g+0.114478*b obj.setpixel(x,y,l,l,l,a) end end";

        private const string Threshold =
            "for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) " +
            "obj.setpixel(x,y,r>128 and 255 or 0,g,b,a) end end";

        private const string WithTrack =
            "for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) " +
            "obj.setpixel(x,y,r*obj.track0/100,g,b,a) end end";

        [Fact]
        public void EmitsValidShaderSkeleton()
        {
            string hlsl = HlslKernelEmitter.Emit(Extract(Grayscale));

            Assert.Contains("Texture2D InputTexture : register(t0);", hlsl);
            Assert.Contains("cbuffer constants : register(b0)", hlsl);
            Assert.Contains("float4 main(float4 pos : SV_POSITION", hlsl);
            Assert.Contains("InputTexture.Sample(InputSampler, uv.xy)", hlsl);
            Assert.Contains("return float4(oR, oG, oB, oA);", hlsl);
            Assert.Contains("inR", hlsl);
            Assert.Contains("inG", hlsl);
            Assert.Contains("inB", hlsl);
        }

        [Fact]
        public void ThresholdEmitsTernary()
        {
            string hlsl = HlslKernelEmitter.Emit(Extract(Threshold));
            Assert.Contains(" ? (", hlsl);
            Assert.Contains(" > ", hlsl);
        }

        [Fact]
        public void UniformReferencesUsePackedConstantBuffer()
        {
            string hlsl = HlslKernelEmitter.Emit(Extract(WithTrack));
            int index = (int)KernelUniform.Track0;
            Assert.Contains($"uniforms[{index / 4}].{"xyzw"[index % 4]}", hlsl);
        }

        [Fact]
        public void EmissionIsDeterministic()
        {
            var program = Extract(Grayscale);
            Assert.Equal(HlslKernelEmitter.Emit(program), HlslKernelEmitter.Emit(program));
        }

        private static KernelProgram Extract(string script)
        {
            var program = KernelExtractor.TryExtract(script);
            Assert.NotNull(program);
            return program!;
        }
    }
}
