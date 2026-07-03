using LuaScript.Compat;
using MoonSharp.Interpreter;

namespace LuaScript.Tests
{
    public class LuaSyntaxExtensionsTests
    {
        private static string Rewrite(string source) => LuaSyntaxExtensions.Rewrite(source);

        [Fact]
        public void IsNot_RewritesToNotEqual()
        {
            Assert.Equal("a ~= b", Rewrite("a is not b"));
        }

        [Fact]
        public void IsNot_RewritesInCondition()
        {
            Assert.Equal("if x ~= nil then end", Rewrite("if x is not nil then end"));
        }

        [Theory]
        [InlineData("a is  not b", "a ~= b")]
        [InlineData("a is\tnot b", "a ~= b")]
        [InlineData("a is\t  not b", "a ~= b")]
        public void IsNot_AllowsHorizontalWhitespace(string source, string expected)
        {
            Assert.Equal(expected, Rewrite(source));
        }

        [Fact]
        public void IsNot_RewritesMultipleOccurrences()
        {
            Assert.Equal("a ~= b and c ~= d", Rewrite("a is not b and c is not d"));
        }

        [Fact]
        public void Newline_BetweenIsAndNot_IsNotAnOperator()
        {
            const string source = "a is\nnot b";
            Assert.Equal(source, Rewrite(source));
        }

        [Theory]
        [InlineData("island")]
        [InlineData("basis not x")]
        [InlineData("a isnot b")]
        [InlineData("a is nothing")]
        [InlineData("local is = 1\nx = is + 1")]
        public void WordBoundaries_AreRespected(string source)
        {
            Assert.Equal(source, Rewrite(source));
        }

        [Theory]
        [InlineData("a.is not b")]
        [InlineData("a:is not b")]
        public void MemberAccess_IsNotRewritten(string source)
        {
            Assert.Equal(source, Rewrite(source));
        }

        [Fact]
        public void ShortString_ContentIsPreserved()
        {
            const string source = "x = \"a is not b\"";
            Assert.Equal(source, Rewrite(source));
        }

        [Fact]
        public void ShortComment_ContentIsPreserved()
        {
            const string source = "-- a is not b\nx = 1";
            Assert.Equal(source, Rewrite(source));
        }

        [Fact]
        public void LongComment_ContentIsPreserved()
        {
            const string source = "--[[ a is not b ]]\nx = 1";
            Assert.Equal(source, Rewrite(source));
        }

        [Fact]
        public void LongString_ContentIsPreserved()
        {
            const string source = "x = [==[ a is not b ]==]";
            Assert.Equal(source, Rewrite(source));
        }

        [Fact]
        public void RewrittenCode_PreservesLineCount()
        {
            const string source = "a = 1\nb = a is not 2\nc = 3";
            string rewritten = Rewrite(source);
            Assert.Equal(source.Split('\n').Length, rewritten.Split('\n').Length);
            Assert.Contains("b = a ~= 2", rewritten);
        }

        [Fact]
        public void NoIsToken_ReturnsSameReference()
        {
            const string source = "obj.x = 1\nobj.y = 2";
            Assert.Same(source, Rewrite(source));
        }

        [Fact]
        public void IsNot_EvaluatesTrueWhenDifferent_ThroughTransform()
        {
            var script = new Script(CoreModules.Preset_SoftSandbox);
            script.DoString(AviUtlScript.Transform("result = 1 is not 2"));
            Assert.True(script.Globals.Get("result").Boolean);
        }

        [Fact]
        public void IsNot_EvaluatesFalseWhenEqual_ThroughTransform()
        {
            var script = new Script(CoreModules.Preset_SoftSandbox);
            script.DoString(AviUtlScript.Transform("result = 2 is not 2"));
            Assert.False(script.Globals.Get("result").Boolean);
        }
    }
}
