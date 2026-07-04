using LuaScript.Compat;

namespace LuaScript.Tests
{
    public sealed class AviUtlScriptShaderTests
    {
        private const string ShaderBlock = "--[[pixelshader@ps:\nTexture2D src : register(t0);\nfloat4 ps(float4 pos : SV_Position) : SV_Target\n{\n    float4 c = src[uint2(floor(pos.xy))];\n    return (c.a != 0.0 && c.r > 0.5) ? c : float4(0, 0, 0, 0);\n}\n]]";

        [Fact]
        public void Transform_PreservesShaderBlockVerbatim()
        {
            var source = ShaderBlock + "\nobj.pixelshader(\"ps\", \"object\", \"object\")";
            var transformed = AviUtlScript.Transform(source);

            Assert.Contains(ShaderBlock, transformed.Replace("\r\n", "\n"));
            var library = AviUtlPixelShaderLibrary.Parse(source);
            Assert.True(library.TryGet("ps", out var hlsl));
            Assert.Contains("c.a != 0.0 && c.r > 0.5", hlsl);
        }

        [Fact]
        public void Transform_SectionedScript_ShaderStaysInSourceLibrary()
        {
            var source = "@main\n--track@a:A,0,100,0\n" + ShaderBlock + "\nobj.pixelshader(\"ps\", \"object\", \"object\")\n@sub\nobj.x = 1";
            var transformed = AviUtlScript.Transform(source);

            Assert.Contains("obj.pixelshader", transformed);
            Assert.True(AviUtlPixelShaderLibrary.Parse(source).TryGet("ps", out _));
        }
    }
}
