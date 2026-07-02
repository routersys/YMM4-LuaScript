using MoonSharp.Interpreter;

namespace LuaScript.Tests
{
    public class SceneTableRegistrarTests
    {
        private sealed class Harness
        {
            private readonly object _scope = new();
            private readonly string _sceneId = Guid.NewGuid().ToString();
            private long _generation;

            public Script Script { get; }
            public SceneValueSession Session { get; } = new();

            public Harness()
            {
                Script = new Script(CoreModules.Basic | CoreModules.Math | CoreModules.String | CoreModules.Table);
                var scene = new Table(Script);
                SceneTableRegistrar.RegisterFunctions(scene, Session.Get, Session.Set);
                Script.Globals["scene"] = scene;
                Session.Begin(_scope, _sceneId, _generation, 0);
            }

            public DynValue Run(string code)
            {
                var result = Script.DoString(code);
                Session.Publish();
                return result;
            }

            public void NextFrame()
            {
                _generation++;
                Session.Begin(_scope, _sceneId, _generation, 0);
            }

            public SceneValue Stored(string name)
            {
                NextFrame();
                return Session.Get(name);
            }
        }

        [Fact]
        public void Set_StoresAllSupportedKinds()
        {
            var harness = new Harness();
            harness.Run(
                "scene.set('num', 4.25) " +
                "scene.set('text', 'こんにちは') " +
                "scene.set('flag', false)");

            Assert.Equal(SceneValue.FromNumber(4.25), harness.Stored("num"));
            Assert.Equal(SceneValue.FromString("こんにちは"), harness.Stored("text"));
            Assert.Equal(SceneValue.FromBoolean(false), harness.Stored("flag"));
        }

        [Fact]
        public void Set_NilOrMissingValue_Removes()
        {
            var harness = new Harness();
            harness.Run("scene.set('a', 1) scene.set('b', 2)");
            harness.NextFrame();
            harness.Run("scene.set('a', nil) scene.set('b')");

            Assert.Equal(SceneValue.Nil, harness.Stored("a"));
            Assert.Equal(SceneValue.Nil, harness.Stored("b"));
        }

        [Fact]
        public void Set_UnsupportedTypesAndNames_AreIgnored()
        {
            var harness = new Harness();
            harness.Run("scene.set('keep', 9)");
            harness.NextFrame();
            harness.Run(
                "scene.set('keep', {}) " +
                "scene.set('fn', function() end) " +
                "scene.set(12, 'x') " +
                "scene.set()");

            Assert.Equal(SceneValue.FromNumber(9d), harness.Stored("keep"));
            Assert.Equal(SceneValue.Nil, harness.Stored("fn"));
        }

        [Fact]
        public void Get_ReturnsStoredValuesWithLuaTypes()
        {
            var harness = new Harness();
            harness.Run(
                "scene.set('num', -8.5) " +
                "scene.set('text', 'v') " +
                "scene.set('flag', true)");
            harness.NextFrame();

            var result = harness.Run(
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
            var harness = new Harness();
            var result = harness.Run(
                "scene.set('counter', (scene.get('counter') or 0) + 1) " +
                "scene.set('counter', (scene.get('counter') or 0) + 1) " +
                "return scene.get('counter')");
            Assert.Equal(2d, result.Number);
        }

        [Fact]
        public void Counter_AccumulatesAcrossFrames()
        {
            var harness = new Harness();
            const string script = "scene.set('counter', (scene.get('counter') or 0) + 1) return scene.get('counter')";

            Assert.Equal(1d, harness.Run(script).Number);
            harness.NextFrame();
            Assert.Equal(2d, harness.Run(script).Number);
            harness.NextFrame();
            Assert.Equal(3d, harness.Run(script).Number);
        }
    }
}
