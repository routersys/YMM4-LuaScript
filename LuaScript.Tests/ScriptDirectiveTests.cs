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

        [Theory]
        [InlineData(null, "MoonSharp")]
        [InlineData("", "MoonSharp")]
        [InlineData("obj.rz = time * 90", "Native")]
        [InlineData("local r,g,b,a = obj.getpixel(0,0)", "Native")]
        [InlineData("obj.setpixel(0,0,255,0,0)", "Native")]
        [InlineData("local pd = obj.getpixeldata()", "Native")]
        [InlineData("--!moonsharp\nobj.setpixel(0,0,1,1,1)", "MoonSharp")]
        [InlineData("--!native\nobj.rz = 1", "Native")]
        [InlineData("--!gpu\nfunction pixel() end", "Gpu")]
        [InlineData("--!cpu\nfunction pixel() end", "Cpu")]
        [InlineData("obj.load(\"figure\", \"円\", 0xff0000, 100)", "Native")]
        [InlineData("obj.draw()", "Native")]
        [InlineData("obj.setoption('blend', 1)", "Native")]
        [InlineData("obj.setanchor('pos', 2)", "Native")]
        public void ResolveAuto_DefaultsToNative(string? script, string expected)
        {
            Assert.Equal(expected, ScriptDirective.ResolveAuto(script).ToString());
        }
    }
}
