using LuaScript.Compat;

namespace LuaScript.Tests
{
    public sealed class AviUtlPixelShaderLibraryTests
    {
        [Fact]
        public void Parse_EmptyOrNull_ReturnsEmpty()
        {
            Assert.Equal(0, AviUtlPixelShaderLibrary.Parse(null).Count);
            Assert.Equal(0, AviUtlPixelShaderLibrary.Parse(string.Empty).Count);
            Assert.Same(AviUtlPixelShaderLibrary.Empty, AviUtlPixelShaderLibrary.Parse(string.Empty));
        }

        [Fact]
        public void Parse_SingleShader_ExtractsBody()
        {
            var source = "--[[pixelshader@psmain:\nfloat4 psmain(float4 pos : SV_Position) : SV_Target { return 0; }\n]]\nobj.pixelshader(\"psmain\", \"object\", \"object\")";
            var library = AviUtlPixelShaderLibrary.Parse(source);

            Assert.Equal(1, library.Count);
            Assert.True(library.TryGet("psmain", out var hlsl));
            Assert.Contains("float4 psmain", hlsl);
            Assert.DoesNotContain("]]", hlsl);
        }

        [Fact]
        public void Parse_MultipleShaders_AllExtracted()
        {
            var source = "--[[pixelshader@first:\nA\n]]\ncode\n--[[pixelshader@second:\nB\n]]";
            var library = AviUtlPixelShaderLibrary.Parse(source);

            Assert.Equal(2, library.Count);
            Assert.True(library.TryGet("first", out var a));
            Assert.Equal("\nA\n", a);
            Assert.True(library.TryGet("second", out var b));
            Assert.Equal("\nB\n", b);
        }

        [Fact]
        public void Parse_NameWithUnderscoreAndDigits_Extracted()
        {
            var library = AviUtlPixelShaderLibrary.Parse("--[[pixelshader@dithering_z2:\nX\n]]");
            Assert.True(library.TryGet("dithering_z2", out _));
        }

        [Fact]
        public void Parse_LeadingWhitespace_Extracted()
        {
            var library = AviUtlPixelShaderLibrary.Parse("  \t--[[pixelshader@ps:\nX\n]]");
            Assert.True(library.TryGet("ps", out _));
        }

        [Fact]
        public void Parse_MarkerNotAtLineStart_Ignored()
        {
            var library = AviUtlPixelShaderLibrary.Parse("local x = 1 --[[pixelshader@ps:\nX\n]]");
            Assert.Equal(0, library.Count);
        }

        [Fact]
        public void Parse_BodyEndsAtFirstDoubleBracket()
        {
            var library = AviUtlPixelShaderLibrary.Parse("--[[pixelshader@ps:\nsrc[a[0]]\n]]");
            Assert.True(library.TryGet("ps", out var hlsl));
            Assert.Equal("\nsrc[a[0", hlsl);
        }

        [Fact]
        public void Parse_UnterminatedBlock_Ignored()
        {
            var library = AviUtlPixelShaderLibrary.Parse("--[[pixelshader@ps:\nfloat4 ps() : SV_Target { return 0; }");
            Assert.Equal(0, library.Count);
        }

        [Fact]
        public void Parse_MissingColon_Ignored()
        {
            var library = AviUtlPixelShaderLibrary.Parse("--[[pixelshader@ps\nX\n]]");
            Assert.Equal(0, library.Count);
        }

        [Fact]
        public void Parse_EmptyName_Ignored()
        {
            var library = AviUtlPixelShaderLibrary.Parse("--[[pixelshader@:\nX\n]]");
            Assert.Equal(0, library.Count);
        }

        [Fact]
        public void Parse_DuplicateName_FirstDefinitionWins()
        {
            var library = AviUtlPixelShaderLibrary.Parse("--[[pixelshader@ps:\nA\n]]\n--[[pixelshader@ps:\nB\n]]");
            Assert.True(library.TryGet("ps", out var hlsl));
            Assert.Equal("\nA\n", hlsl);
        }

        [Fact]
        public void Parse_CrlfSource_Extracted()
        {
            var library = AviUtlPixelShaderLibrary.Parse("--[[pixelshader@ps:\r\nX\r\n]]\r\n");
            Assert.True(library.TryGet("ps", out var hlsl));
            Assert.Equal("\r\nX\r\n", hlsl);
        }

        [Fact]
        public void Parse_InsideSectionBody_Extracted()
        {
            var source = "@通常\n--track@a:A,0,100,0\n--[[pixelshader@ps:\nX\n]]\nobj.pixelshader(\"ps\", \"object\", \"object\")";
            var library = AviUtlPixelShaderLibrary.Parse(source);
            Assert.True(library.TryGet("ps", out _));
        }

        [Fact]
        public void TryGet_CrossScriptName_ResolvesLocalPart()
        {
            var library = AviUtlPixelShaderLibrary.Parse("--[[pixelshader@ps:\nX\n]]");
            Assert.True(library.TryGet("ps@OtherScript", out var hlsl));
            Assert.Equal("\nX\n", hlsl);
        }

        [Fact]
        public void TryGet_UnknownOrEmptyName_ReturnsFalse()
        {
            var library = AviUtlPixelShaderLibrary.Parse("--[[pixelshader@ps:\nX\n]]");
            Assert.False(library.TryGet("other", out _));
            Assert.False(library.TryGet(string.Empty, out _));
        }

        [Fact]
        public void EntryPointOf_StripsScriptSuffix()
        {
            Assert.Equal("ps", AviUtlPixelShaderLibrary.EntryPointOf("ps@Script"));
            Assert.Equal("ps", AviUtlPixelShaderLibrary.EntryPointOf("ps"));
        }

        [Fact]
        public void Parse_ComputeShaderBlock_Ignored()
        {
            var library = AviUtlPixelShaderLibrary.Parse("--[[computeshader@cs:\nX\n]]");
            Assert.Equal(0, library.Count);
        }
    }
}
