using System.Text;

namespace LuaScript.Tests
{
    public class SceneSharedValuesTests
    {
        [Fact]
        public void Set_Get_RoundTripsAllKinds()
        {
            var store = new SceneSharedValues();
            store.Set("num", SceneValue.FromNumber(1.5));
            store.Set("text", SceneValue.FromString("あいう"));
            store.Set("flagTrue", SceneValue.FromBoolean(true));
            store.Set("flagFalse", SceneValue.FromBoolean(false));

            Assert.Equal(SceneValue.FromNumber(1.5), store.Get("num"));
            Assert.Equal(SceneValue.FromString("あいう"), store.Get("text"));
            Assert.Equal(SceneValue.FromBoolean(true), store.Get("flagTrue"));
            Assert.Equal(SceneValue.FromBoolean(false), store.Get("flagFalse"));
            Assert.Equal(SceneValue.Nil, store.Get("missing"));
        }

        [Fact]
        public void Set_Nil_RemovesValue()
        {
            var store = new SceneSharedValues();
            store.Set("key", SceneValue.FromNumber(1d));
            store.Set("key", SceneValue.Nil);
            Assert.Equal(SceneValue.Nil, store.Get("key"));
        }

        [Fact]
        public void Set_OverwritesValueAndKind()
        {
            var store = new SceneSharedValues();
            store.Set("key", SceneValue.FromNumber(1d));
            store.Set("key", SceneValue.FromString("s"));
            Assert.Equal(SceneValue.FromString("s"), store.Get("key"));
        }

        [Fact]
        public void ForScene_IsolatesScenes_AndSharesWithinScene()
        {
            string sceneA = Guid.NewGuid().ToString();
            string sceneB = Guid.NewGuid().ToString();

            SceneSharedValues.ForScene(sceneA).Set("key", SceneValue.FromNumber(1d));

            Assert.Equal(SceneValue.FromNumber(1d), SceneSharedValues.ForScene(sceneA).Get("key"));
            Assert.Equal(SceneValue.Nil, SceneSharedValues.ForScene(sceneB).Get("key"));
            Assert.Same(SceneSharedValues.ForScene(sceneA), SceneSharedValues.ForScene(sceneA));
        }

        [Fact]
        public void LongText_IsTruncatedAtUtf8Boundary()
        {
            var store = new SceneSharedValues();
            string text = new string('あ', 2000);
            store.Set("key", SceneValue.FromString(text));

            var stored = store.Get("key");
            Assert.Equal(SceneValueKind.String, stored.Kind);
            Assert.True(Encoding.UTF8.GetByteCount(stored.Text!) <= SceneSharedValues.MaxTextBytes);
            Assert.Equal(SceneSharedValues.MaxTextBytes / 3, stored.Text!.Length);
            Assert.StartsWith(stored.Text, text, StringComparison.Ordinal);
        }

        [Fact]
        public void LongName_IsTruncatedConsistentlyOnSetAndGet()
        {
            var store = new SceneSharedValues();
            string name = new string('k', SceneSharedValues.MaxNameBytes + 100);
            store.Set(name, SceneValue.FromNumber(7d));
            Assert.Equal(SceneValue.FromNumber(7d), store.Get(name));
            Assert.Equal(SceneValue.FromNumber(7d), store.Get(name[..SceneSharedValues.MaxNameBytes]));
        }
    }
}
