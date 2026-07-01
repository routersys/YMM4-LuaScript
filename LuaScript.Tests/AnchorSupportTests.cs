using System.Collections.Immutable;
using LuaScript.Anchor;

namespace LuaScript.Tests
{
    public class AnchorSupportTests
    {
        [Fact]
        public void ClampCount_LimitsToRange()
        {
            Assert.Equal(0, AnchorSupport.ClampCount(-5));
            Assert.Equal(10, AnchorSupport.ClampCount(10));
            Assert.Equal(AnchorSupport.MaxAnchors, AnchorSupport.ClampCount(100));
        }

        [Fact]
        public void ApplyOption_SetsConnectionAndDimension()
        {
            var connection = AnchorConnection.None;
            bool is3D = false;

            AnchorSupport.ApplyOption("loop", ref connection, ref is3D);
            AnchorSupport.ApplyOption("xyz", ref connection, ref is3D);

            Assert.Equal(AnchorConnection.Loop, connection);
            Assert.True(is3D);

            AnchorSupport.ApplyOption("star", ref connection, ref is3D);
            Assert.Equal(AnchorConnection.Star, connection);

            AnchorSupport.ApplyOption("unknown", ref connection, ref is3D);
            Assert.Equal(AnchorConnection.Star, connection);
        }

        [Fact]
        public void DefaultPosition_IsDeterministicGrid()
        {
            Assert.Equal(AnchorSupport.DefaultPosition(0), AnchorSupport.DefaultPosition(0));
            Assert.NotEqual(AnchorSupport.DefaultPosition(0), AnchorSupport.DefaultPosition(1));

            var (x8, y8) = AnchorSupport.DefaultPosition(8);
            var (x0, y0) = AnchorSupport.DefaultPosition(0);
            Assert.Equal(x0, x8);
            Assert.True(y8 > y0);
        }

        [Fact]
        public void ResolvePosition_PrefersStoredThenDefault()
        {
            var source = ImmutableList.Create(new LuaAnchorPoint { Group = "pos", Index = 1, X = 12, Y = 34, Z = 56 });

            AnchorSupport.ResolvePosition(source, "pos", 1, out double x, out double y, out double z);
            Assert.Equal(12, x);
            Assert.Equal(34, y);
            Assert.Equal(56, z);

            AnchorSupport.ResolvePosition(source, "pos", 0, out double dx, out double dy, out double dz);
            var (ex, ey) = AnchorSupport.DefaultPosition(0);
            Assert.Equal(ex, dx);
            Assert.Equal(ey, dy);
            Assert.Equal(0, dz);
        }

        [Fact]
        public void ResolvePosition_IsGroupScoped()
        {
            var source = ImmutableList.Create(new LuaAnchorPoint { Group = "a", Index = 0, X = 5, Y = 5, Z = 0 });

            AnchorSupport.ResolvePosition(source, "b", 0, out double x, out double y, out _);
            var (ex, ey) = AnchorSupport.DefaultPosition(0);
            Assert.Equal(ex, x);
            Assert.Equal(ey, y);
        }

        [Fact]
        public void ApplyDrag_UpdatesExistingPoint()
        {
            var source = ImmutableList.Create(new LuaAnchorPoint { Group = "pos", Index = 2, X = 10, Y = 20, Z = 30 });

            var result = AnchorSupport.ApplyDrag(source, "pos", 2, 3, -4, 5);

            Assert.Single(result);
            Assert.Equal(13, result[0].X);
            Assert.Equal(16, result[0].Y);
            Assert.Equal(35, result[0].Z);
        }

        [Fact]
        public void ApplyDrag_AddsNewPointFromDefault()
        {
            var source = ImmutableList<LuaAnchorPoint>.Empty;

            var result = AnchorSupport.ApplyDrag(source, "pos", 3, 7, 8, 0);

            Assert.Single(result);
            var (bx, by) = AnchorSupport.DefaultPosition(3);
            Assert.Equal("pos", result[0].Group);
            Assert.Equal(3, result[0].Index);
            Assert.Equal(bx + 7, result[0].X);
            Assert.Equal(by + 8, result[0].Y);
        }
    }
}
