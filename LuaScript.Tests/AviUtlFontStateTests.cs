using MoonSharp.Interpreter;

namespace LuaScript.Tests
{
    public class AviUtlFontStateTests
    {
        private static (string Family, double Size, bool Bold, bool Italic, int Color) Snapshot(AviUtlFontState state) =>
            (state.Family, state.Size, state.Bold, state.Italic, state.Color);

        private static DynValue NewTable() => DynValue.NewTable(new Script());

        private static DynValue NewFunction() => DynValue.NewCallback((_, _) => DynValue.Void);

        [Fact]
        public void Defaults_MatchWorkerDefaults()
        {
            var state = new AviUtlFontState();
            Assert.Equal((string.Empty, 34d, false, false, 0xFFFFFF), Snapshot(state));
        }

        [Fact]
        public void Reset_RestoresDefaults()
        {
            var state = new AviUtlFontState();
            state.Apply(DynValue.NewString("Meiryo"), DynValue.NewNumber(40), DynValue.NewNumber(3), DynValue.NewNumber(0x112233));
            state.Reset();
            Assert.Equal((string.Empty, 34d, false, false, 0xFFFFFF), Snapshot(state));
        }

        [Fact]
        public void Apply_CoercesArgumentsLikeNativeWorker()
        {
            var state = new AviUtlFontState();

            state.Apply(DynValue.NewString("Meiryo"), DynValue.NewNumber(40), DynValue.NewNumber(3), DynValue.NewNumber(0x112233));
            Assert.Equal(("Meiryo", 40d, true, true, 0x112233), Snapshot(state));

            state.Apply(DynValue.NewNumber(123), DynValue.NewString("48"), DynValue.Nil, DynValue.Nil);
            Assert.Equal(("123", 48d, true, true, 0x112233), Snapshot(state));

            state.Apply(DynValue.True, DynValue.False, NewTable(), NewFunction());
            Assert.Equal(("123", 48d, true, true, 0x112233), Snapshot(state));

            state.Apply(DynValue.Nil, DynValue.Nil, DynValue.NewNumber(-1.5), DynValue.Nil);
            Assert.Equal(("123", 48d, false, true, 0x112233), Snapshot(state));

            state.Apply(DynValue.NewString("X"), DynValue.NewNumber(20), DynValue.NewNumber(0), DynValue.NewNumber(0));
            Assert.Equal(("X", 20d, false, false, 0), Snapshot(state));
        }

        [Fact]
        public void Apply_NegativeStyles_UseFloorSemantics()
        {
            var state = new AviUtlFontState();

            state.Apply(DynValue.Nil, DynValue.Nil, DynValue.NewNumber(-1d), DynValue.Nil);
            Assert.True(state.Bold);
            Assert.True(state.Italic);

            state.Apply(DynValue.Nil, DynValue.Nil, DynValue.NewNumber(-2d), DynValue.Nil);
            Assert.False(state.Bold);
            Assert.True(state.Italic);

            state.Apply(DynValue.Nil, DynValue.Nil, DynValue.NewNumber(-3d), DynValue.Nil);
            Assert.True(state.Bold);
            Assert.False(state.Italic);
        }

        [Fact]
        public void Apply_NonFiniteAndHugeStyles_ClearFlagsWithoutError()
        {
            var state = new AviUtlFontState();
            state.Apply(DynValue.Nil, DynValue.Nil, DynValue.NewNumber(3d), DynValue.Nil);

            state.Apply(DynValue.Nil, DynValue.Nil, DynValue.NewNumber(double.NaN), DynValue.Nil);
            Assert.False(state.Bold);
            Assert.False(state.Italic);

            state.Apply(DynValue.Nil, DynValue.Nil, DynValue.NewNumber(3d), DynValue.Nil);
            state.Apply(DynValue.Nil, DynValue.Nil, DynValue.NewNumber(1e300), DynValue.Nil);
            Assert.False(state.Bold);
            Assert.False(state.Italic);

            state.Apply(DynValue.Nil, DynValue.Nil, DynValue.NewNumber(3d), DynValue.Nil);
            state.Apply(DynValue.Nil, DynValue.Nil, DynValue.NewNumber(double.PositiveInfinity), DynValue.Nil);
            Assert.False(state.Bold);
            Assert.False(state.Italic);
        }
    }
}
