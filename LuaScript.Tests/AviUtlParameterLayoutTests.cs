using LuaScript.Compat;

namespace LuaScript.Tests
{
    public class AviUtlParameterLayoutTests
    {
        [Fact]
        public void NoDirectives_HasAnyIsFalse()
        {
            var layout = AviUtlParameterLayout.Parse("obj.x = 1\nobj.y = 2");
            Assert.False(layout.HasAny);
            Assert.False(layout.HasTrack(0));
            Assert.False(layout.HasColor);
        }

        [Fact]
        public void Track_ParsesNameRangeDefaultStep()
        {
            var layout = AviUtlParameterLayout.Parse("--track0:Amp,0,100,10,0.5\nobj.ox = obj.track0");
            Assert.True(layout.HasAny);
            var t = layout.GetTrack(0);
            Assert.NotNull(t);
            Assert.Equal("Amp", t!.Value.Name);
            Assert.Equal(0d, t.Value.Min);
            Assert.Equal(100d, t.Value.Max);
            Assert.Equal(10d, t.Value.Default);
            Assert.Equal(0.5d, t.Value.Step);
        }

        [Fact]
        public void Track_DefaultIsClampedIntoRange()
        {
            var layout = AviUtlParameterLayout.Parse("--track1:V,0,10,999");
            var t = layout.GetTrack(1);
            Assert.Equal(10d, t!.Value.Default);
        }

        [Fact]
        public void Track_SwapsInvertedMinMax()
        {
            var layout = AviUtlParameterLayout.Parse("--track0:V,100,0,50");
            var t = layout.GetTrack(0);
            Assert.Equal(0d, t!.Value.Min);
            Assert.Equal(100d, t.Value.Max);
        }

        [Fact]
        public void Track_NegativeRangeParses()
        {
            var layout = AviUtlParameterLayout.Parse("--track2:Off,-100,100,-25");
            var t = layout.GetTrack(2);
            Assert.Equal(-100d, t!.Value.Min);
            Assert.Equal(-25d, t.Value.Default);
        }

        [Fact]
        public void Track_IndexBeyondThreeIsIgnored()
        {
            var layout = AviUtlParameterLayout.Parse("--track4:V,0,10,5");
            Assert.False(layout.HasAny);
        }

        [Fact]
        public void Check_ParsesDefaultAsBool()
        {
            var layout = AviUtlParameterLayout.Parse("--check0:On,1\n--check1:Off,0");
            Assert.True(layout.GetCheck(0)!.Value.Default);
            Assert.False(layout.GetCheck(1)!.Value.Default);
            Assert.Equal("On", layout.GetCheck(0)!.Value.Name);
        }

        [Fact]
        public void Color_ParsesHexLiteral()
        {
            var layout = AviUtlParameterLayout.Parse("--color:0xff8800");
            Assert.True(layout.HasColor);
            Assert.Equal(0xff8800, layout.Color!.Value.Default);
        }

        [Fact]
        public void Color_ParsesDecimalLiteral()
        {
            var layout = AviUtlParameterLayout.Parse("--color:255");
            Assert.Equal(255, layout.Color!.Value.Default);
        }

        [Fact]
        public void Combined_TrackCheckColorAllDetected()
        {
            const string src =
                "--track0:Amp,0,100,10\n" +
                "--track1:Spd,0,50,5\n" +
                "--check0:Enable,1\n" +
                "--color:0x112233\n" +
                "obj.ox = obj.track0";
            var layout = AviUtlParameterLayout.Parse(src);
            Assert.True(layout.HasTrack(0));
            Assert.True(layout.HasTrack(1));
            Assert.False(layout.HasTrack(2));
            Assert.True(layout.HasCheck(0));
            Assert.True(layout.HasColor);
        }

        [Fact]
        public void Section_OnlyFirstSectionHeaderIsScanned()
        {
            const string src = "@First\n--track0:A,0,10,3\n@Second\n--track1:B,0,10,4";
            var layout = AviUtlParameterLayout.Parse(src);
            Assert.True(layout.HasTrack(0));
            Assert.False(layout.HasTrack(1));
        }

        [Fact]
        public void EngineDirective_IsNotMistakenForParameter()
        {
            var layout = AviUtlParameterLayout.Parse("--!native\nobj.x = 1");
            Assert.False(layout.HasAny);
        }
    }
}
