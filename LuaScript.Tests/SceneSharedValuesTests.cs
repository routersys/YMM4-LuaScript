namespace LuaScript.Tests
{
    public class SceneSharedValuesTests
    {
        private static Dictionary<string, SceneValue> Writes(params (string Name, SceneValue Value)[] items)
        {
            var writes = new Dictionary<string, SceneValue>(StringComparer.Ordinal);
            foreach (var (name, value) in items)
                writes[name] = value;
            return writes;
        }

        [Fact]
        public void Publish_IsInvisibleWithinSameGeneration()
        {
            var store = new SceneSharedValues();
            store.Publish(10, false, Writes(("key", SceneValue.FromNumber(1d))));
            Assert.Equal(SceneValue.Nil, store.Read(10, false, "key"));
            Assert.Equal(SceneValue.FromNumber(1d), store.Read(11, false, "key"));
        }

        [Fact]
        public void Read_IsOrderIndependentWithinGeneration()
        {
            var store = new SceneSharedValues();
            store.Publish(0, false, Writes(("key", SceneValue.FromNumber(1d))));

            Assert.Equal(SceneValue.FromNumber(1d), store.Read(1, false, "key"));
            store.Publish(1, false, Writes(("key", SceneValue.FromNumber(2d))));
            Assert.Equal(SceneValue.FromNumber(1d), store.Read(1, false, "key"));
            store.Publish(1, false, Writes(("other", SceneValue.FromNumber(3d))));
            Assert.Equal(SceneValue.FromNumber(1d), store.Read(1, false, "key"));

            Assert.Equal(SceneValue.FromNumber(2d), store.Read(2, false, "key"));
            Assert.Equal(SceneValue.FromNumber(3d), store.Read(2, false, "other"));
        }

        [Fact]
        public void Fold_AppliesRemovalsAndOverwrites()
        {
            var store = new SceneSharedValues();
            store.Publish(0, false, Writes(
                ("a", SceneValue.FromNumber(1d)),
                ("b", SceneValue.FromString("s")),
                ("c", SceneValue.FromBoolean(true))));
            Assert.Equal(SceneValue.FromNumber(1d), store.Read(1, false, "a"));

            store.Publish(1, false, Writes(
                ("a", SceneValue.Nil),
                ("b", SceneValue.FromNumber(9d))));

            Assert.Equal(SceneValue.Nil, store.Read(2, false, "a"));
            Assert.Equal(SceneValue.FromNumber(9d), store.Read(2, false, "b"));
            Assert.Equal(SceneValue.FromBoolean(true), store.Read(2, false, "c"));
        }

        [Fact]
        public void BackwardScrub_FoldsPendingOnce()
        {
            var store = new SceneSharedValues();
            store.Publish(100, false, Writes(("key", SceneValue.FromNumber(5d))));
            Assert.Equal(SceneValue.FromNumber(5d), store.Read(50, false, "key"));
            Assert.Equal(SceneValue.FromNumber(5d), store.Read(50, false, "key"));
        }

        [Fact]
        public void ExportStart_ResetsAllValues()
        {
            var store = new SceneSharedValues();
            store.Publish(5, false, Writes(("key", SceneValue.FromNumber(1d))));
            Assert.Equal(SceneValue.FromNumber(1d), store.Read(6, false, "key"));

            Assert.Equal(SceneValue.Nil, store.Read(0, true, "key"));

            store.Publish(0, true, Writes(("key", SceneValue.FromNumber(2d))));
            Assert.Equal(SceneValue.FromNumber(2d), store.Read(1, true, "key"));
        }

        [Fact]
        public void ExportRestart_IsDetectedByBackwardFrame()
        {
            var store = new SceneSharedValues();
            store.Publish(0, true, Writes(("key", SceneValue.FromNumber(1d))));
            Assert.Equal(SceneValue.FromNumber(1d), store.Read(10, true, "key"));

            Assert.Equal(SceneValue.Nil, store.Read(0, true, "key"));
        }

        [Fact]
        public void ExportEnd_KeepsValuesForPreview()
        {
            var store = new SceneSharedValues();
            store.Publish(0, true, Writes(("key", SceneValue.FromNumber(1d))));
            Assert.Equal(SceneValue.FromNumber(1d), store.Read(1, false, "key"));
        }

        [Fact]
        public void Capacity_LimitsDistinctNames_AndKeepsExistingUpdatable()
        {
            var store = new SceneSharedValues();
            var writes = new Dictionary<string, SceneValue>(StringComparer.Ordinal);
            for (int i = 0; i < SceneSharedValues.MaxEntries; i++)
                writes["k" + i] = SceneValue.FromNumber(i);
            store.Publish(0, false, writes);

            store.Publish(1, false, Writes(
                ("overflow", SceneValue.FromNumber(1d)),
                ("k0", SceneValue.FromNumber(-1d))));

            Assert.Equal(SceneValue.Nil, store.Read(2, false, "overflow"));
            Assert.Equal(SceneValue.FromNumber(-1d), store.Read(2, false, "k0"));

            store.Publish(2, false, Writes(
                ("k1", SceneValue.Nil),
                ("replacement", SceneValue.FromNumber(7d))));
            Assert.Equal(SceneValue.Nil, store.Read(3, false, "k1"));
            Assert.Equal(SceneValue.FromNumber(7d), store.Read(3, false, "replacement"));
        }

        [Fact]
        public void ForScene_IsolatesScenes_AndSharesWithinScene()
        {
            string sceneA = Guid.NewGuid().ToString();
            string sceneB = Guid.NewGuid().ToString();

            SceneSharedValues.ForScene(sceneA).Publish(0, false, Writes(("key", SceneValue.FromNumber(1d))));

            Assert.Equal(SceneValue.FromNumber(1d), SceneSharedValues.ForScene(sceneA).Read(1, false, "key"));
            Assert.Equal(SceneValue.Nil, SceneSharedValues.ForScene(sceneB).Read(1, false, "key"));
            Assert.Same(SceneSharedValues.ForScene(sceneA), SceneSharedValues.ForScene(sceneA));
        }
    }
}
