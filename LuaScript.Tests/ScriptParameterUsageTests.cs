using LuaScript.Compat;

namespace LuaScript.Tests
{
    public sealed class ScriptParameterUsageTests
    {
        [Theory]
        [InlineData("if obj.check0 then obj.alpha = 0 end", 0)]
        [InlineData("local v = obj.check3 and 255 or 0", 3)]
        [InlineData("obj . check1 = true", 1)]
        public void DetectsReferencedCheck(string script, int index)
        {
            var usage = ScriptParameterUsage.Detect(script);
            for (int i = 0; i < 4; i++)
                Assert.Equal(i == index, usage.Check(i));
            Assert.False(usage.Color);
        }

        [Fact]
        public void DetectsReferencedColor()
        {
            var usage = ScriptParameterUsage.Detect("obj.rz = color / 1000");
            Assert.True(usage.Color);
        }

        [Fact]
        public void DetectsMultipleChecks()
        {
            var usage = ScriptParameterUsage.Detect("local a = obj.check0\nlocal b = obj.check2");
            Assert.True(usage.Check0);
            Assert.False(usage.Check1);
            Assert.True(usage.Check2);
            Assert.False(usage.Check3);
        }

        [Theory]
        [InlineData("obj.setoption(\"color\", 1)")]
        [InlineData("-- color and obj.check0 in a comment")]
        [InlineData("local colorful = 1")]
        [InlineData("scene.color = 0")]
        public void IgnoresStringsCommentsAndUnrelatedNames(string script)
        {
            var usage = ScriptParameterUsage.Detect(script);
            Assert.False(usage.Color);
            for (int i = 0; i < 4; i++)
                Assert.False(usage.Check(i));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("local s = \"unterminated")]
        public void ReturnsNoneForEmptyOrInvalid(string? script)
        {
            var usage = ScriptParameterUsage.Detect(script);
            Assert.False(usage.Color);
            Assert.False(usage.Check0);
        }

        [Fact]
        public void DoesNotDetectOutOfRangeCheck()
        {
            var usage = ScriptParameterUsage.Detect("local a = obj.check4");
            for (int i = 0; i < 4; i++)
                Assert.False(usage.Check(i));
        }
    }
}
