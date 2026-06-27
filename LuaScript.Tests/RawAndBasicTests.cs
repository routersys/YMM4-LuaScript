using MoonSharp.Interpreter;

namespace LuaScript.Tests
{
    public class RawAndBasicTests
    {
        private readonly MoonSharpLane _lane = new();

        [Fact]
        public void RawLen_Table() => Assert.Equal(3d, _lane.Number("rawlen({10,20,30})"));

        [Fact]
        public void RawLen_String() => Assert.Equal(5d, _lane.Number("rawlen('hello')"));

        [Fact]
        public void RawEqual_SameReference()
        {
            Assert.True(_lane.Boolean("(function() local t={} return rawequal(t,t) end)()"));
            Assert.False(_lane.Boolean("rawequal({},{})"));
        }

        [Fact]
        public void RawGetSet_BypassMetatable()
        {
            Assert.Equal(7d, _lane.Number(
                "(function() local t=setmetatable({},{__index=function() return 99 end}) rawset(t,'k',7) return rawget(t,'k') end)()"));
            Assert.Equal(99d, _lane.Number(
                "(function() local t=setmetatable({},{__index=function() return 99 end}) return t.missing end)()"));
        }

        [Fact]
        public void Select_Count() => Assert.Equal(3d, _lane.Number("select('#', 'a', 'b', 'c')"));

        [Fact]
        public void Select_Index() => Assert.Equal(20d, _lane.Number("(select(2, 10, 20, 30))"));

        [Theory]
        [InlineData("type(1)", "number")]
        [InlineData("type('x')", "string")]
        [InlineData("type({})", "table")]
        [InlineData("type(nil)", "nil")]
        [InlineData("type(true)", "boolean")]
        [InlineData("type(print)", "function")]
        public void Type_Names(string expression, string expected) =>
            Assert.Equal(expected, _lane.Text(expression));

        [Theory]
        [InlineData("tonumber('42')", 42d)]
        [InlineData("tonumber('10', 2)", 2d)]
        [InlineData("tonumber('ff', 16)", 255d)]
        public void ToNumber_Parsing(string expression, double expected) =>
            Assert.Equal(expected, _lane.Number(expression));

        [Fact]
        public void ToNumber_InvalidIsNil() =>
            Assert.Equal(DataType.Nil, _lane.Type("tonumber('not a number')"));

        [Fact]
        public void ToNumber_HexStringPrefix_IsNil_MoonSharpDivergence() =>
            Assert.Equal(DataType.Nil, _lane.Type("tonumber('0x1F')"));

        [Fact]
        public void Unpack_Global() =>
            Assert.Equal(6d, _lane.Number("(function() local a,b,c = unpack({1,2,3}) return a+b+c end)()"));

        [Fact]
        public void Pcall_CapturesError() =>
            Assert.False(_lane.Boolean("(function() local ok = pcall(function() error('boom') end) return ok end)()"));

        [Fact]
        public void Pcall_SuccessReturnsTrue() =>
            Assert.True(_lane.Boolean("(function() local ok = pcall(function() return 1 end) return ok end)()"));

        [Fact]
        public void StringFormat_Basic() =>
            Assert.Equal("a=5 b=2.50", _lane.Text("string.format('a=%d b=%.2f', 5, 2.5)"));

        [Fact]
        public void MathFloorCeil()
        {
            Assert.Equal(3d, _lane.Number("math.floor(3.7)"));
            Assert.Equal(4d, _lane.Number("math.ceil(3.2)"));
        }
    }
}
