using LuaScript.Compat;

namespace LuaScript.Tests
{
    public class AviUtlCompatMapTests
    {
        [Fact]
        public void Resolve_KnownEffect_ReturnsTargetType()
        {
            Assert.True(AviUtlCompatMap.Default.TryResolve("ぼかし", AviUtlEngine.AviUtl, out var mapping));
            Assert.Equal("GaussianBlurEffect", mapping.TargetType);
        }

        [Fact]
        public void Resolve_ParameterTransform_IsInverseOfExoExport()
        {
            AviUtlCompatMap.Default.TryResolve("ぼかし", AviUtlEngine.AviUtl, out var mapping);
            Assert.True(mapping.TryGetParameter("範囲", out var parameter));
            Assert.Equal("Blur", parameter.Property);
            Assert.Equal(5d, parameter.Transform(10d));
        }

        [Fact]
        public void Resolve_AngleParameter_IsIdentity()
        {
            AviUtlCompatMap.Default.TryResolve("方向ブラー", AviUtlEngine.AviUtl, out var mapping);
            Assert.True(mapping.TryGetParameter("角度", out var parameter));
            Assert.Equal(45d, parameter.Transform(45d));
        }

        [Fact]
        public void Resolve_BothEngine_MatchesAviUtl2()
        {
            Assert.True(AviUtlCompatMap.Default.TryResolve("クロマキー", AviUtlEngine.AviUtl2, out var mapping));
            Assert.Equal("ChromaKeyEffect", mapping.TargetType);
        }

        [Fact]
        public void Resolve_UndeclaredEffect_DoesNotResolve()
        {
            Assert.False(AviUtlCompatMap.Default.TryResolve("存在しないエフェクト", AviUtlEngine.AviUtl, out _));
        }

        [Fact]
        public void ResolveTarget_DefaultsToAviUtl()
        {
            Assert.Equal(AviUtlEngine.AviUtl, AviUtlCompatMap.ResolveTarget("obj.effect(\"ぼかし\", \"範囲\", 10)"));
        }

        [Fact]
        public void ResolveTarget_DirectiveSelectsAviUtl2()
        {
            Assert.Equal(AviUtlEngine.AviUtl2, AviUtlCompatMap.ResolveTarget("--!aviutl2\nobj.effect(\"ぼかし\", \"範囲\", 10)"));
        }

        [Fact]
        public void Engine_BothIncludesEveryTarget()
        {
            Assert.True(AviUtlEngine.Both.Includes(AviUtlEngine.AviUtl));
            Assert.True(AviUtlEngine.Both.Includes(AviUtlEngine.AviUtl2));
            Assert.False(AviUtlEngine.AviUtl.Includes(AviUtlEngine.AviUtl2));
        }
    }
}
