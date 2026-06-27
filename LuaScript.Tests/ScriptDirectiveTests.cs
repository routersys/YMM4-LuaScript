using LuaScript.Engine;

namespace LuaScript.Tests
{
    public class ScriptDirectiveTests
    {
        [Theory]
        [InlineData(null, "MoonSharp")]
        [InlineData("", "MoonSharp")]
        [InlineData("obj.rz = time * 90", "MoonSharp")]
        [InlineData("--!native", "Native")]
        [InlineData("--!gpu", "Gpu")]
        [InlineData("--!cpu", "Cpu")]
        [InlineData("--!moonsharp", "MoonSharp")]
        [InlineData("--! native ", "Native")]
        [InlineData("--!NATIVE", "Native")]
        [InlineData("\r\n  --!gpu\r\nfunction pixel() end", "Gpu")]
        [InlineData("-- normal comment\n--!native\nobj.x = 0", "Native")]
        [InlineData("--!unknown", "MoonSharp")]
        [InlineData("--!native extra", "MoonSharp")]
        [InlineData("obj.x=0 --!native", "MoonSharp")]
        public void Resolve_ReturnsExpectedEngine(string? script, string expected)
        {
            Assert.Equal(expected, ScriptDirective.Resolve(script).ToString());
        }

        [Fact]
        public void Resolve_FirstDirectiveWins()
        {
            Assert.Equal("Native", ScriptDirective.Resolve("--!native\n--!gpu").ToString());
        }
    }
}
