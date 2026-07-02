using MoonSharp.Interpreter;

namespace LuaScript.Tests
{
    public class SceneTableRegistrarTests
    {
        private static (Script script, SceneSharedValues store) CreateScript()
        {
            var script = new Script(CoreModules.Basic | CoreModules.Math | CoreModules.String | CoreModules.Table);
            var store = new SceneSharedValues();
            var scene = new Table(script);
            SceneTableRegistrar.RegisterFunctions(scene, store.Get, store.Set);
            script.Globals["scene"] = scene;
            return (script, store);
        }

        [Fact]
        public void Set_StoresAllSupportedKinds()
        {
            var (script, store) = CreateScript();
            script.DoString(
                "scene.set('num', 4.25) " +
                "scene.set('text', 'こんにちは') " +
                "scene.set('flag', false)");

            Assert.Equal(SceneValue.FromNumber(4.25), store.Get("num"));
            Assert.Equal(SceneValue.FromString("こんにちは"), store.Get("text"));
            Assert.Equal(SceneValue.FromBoolean(false), store.Get("flag"));
        }

        [Fact]
        public void Set_NilOrMissingValue_Removes()
        {
            var (script, store) = CreateScript();
            store.Set("a", SceneValue.FromNumber(1d));
            store.Set("b", SceneValue.FromNumber(2d));
            script.DoString("scene.set('a', nil) scene.set('b')");
            Assert.Equal(SceneValue.Nil, store.Get("a"));
            Assert.Equal(SceneValue.Nil, store.Get("b"));
        }

        [Fact]
        public void Set_UnsupportedTypesAndNames_AreIgnored()
        {
            var (script, store) = CreateScript();
            store.Set("keep", SceneValue.FromNumber(9d));
            script.DoString(
                "scene.set('keep', {}) " +
                "scene.set('fn', function() end) " +
                "scene.set(12, 'x') " +
                "scene.set()");

            Assert.Equal(SceneValue.FromNumber(9d), store.Get("keep"));
            Assert.Equal(SceneValue.Nil, store.Get("fn"));
        }

        [Fact]
        public void Get_ReturnsStoredValuesWithLuaTypes()
        {
            var (script, store) = CreateScript();
            store.Set("num", SceneValue.FromNumber(-8.5));
            store.Set("text", SceneValue.FromString("v"));
            store.Set("flag", SceneValue.FromBoolean(true));

            var result = script.DoString(
                "return scene.get('num'), scene.get('text'), scene.get('flag'), scene.get('missing'), scene.get(1)");

            Assert.Equal(DataType.Tuple, result.Type);
            Assert.Equal(-8.5d, result.Tuple[0].Number);
            Assert.Equal("v", result.Tuple[1].String);
            Assert.True(result.Tuple[2].Boolean);
            Assert.Equal(DataType.Nil, result.Tuple[3].Type);
            Assert.Equal(DataType.Nil, result.Tuple[4].Type);
        }

        [Fact]
        public void SetAndGet_RoundTripWithinScript()
        {
            var (script, _) = CreateScript();
            var result = script.DoString(
                "scene.set('counter', (scene.get('counter') or 0) + 1) " +
                "scene.set('counter', (scene.get('counter') or 0) + 1) " +
                "return scene.get('counter')");
            Assert.Equal(2d, result.Number);
        }
    }
}
