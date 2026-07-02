namespace LuaScript.Tests
{
    public class SceneSharedValuesTests
    {
        private static Dictionary<string, SceneValue> Writes(params (string Name, SceneValue Value)[] entries)
        {
            var writes = new Dictionary<string, SceneValue>(StringComparer.Ordinal);
            foreach (var (name, value) in entries)
                writes[name] = value;
            return writes;
        }

        [Fact]
        public void Publish_IsInvisibleAtOwnFrame_AndVisibleImmediatelyAfter()
        {
            var store = new SceneSharedValues();
            store.Publish(10, 0, Writes(("key", SceneValue.FromNumber(1d))));
            Assert.Equal(SceneValue.Nil, store.Read(10, "key"));
            Assert.Equal(SceneValue.FromNumber(1d), store.Read(11, "key"));
            Assert.Equal(SceneValue.FromNumber(1d), store.Read(1000, "key"));
        }

        [Fact]
        public void Read_IsOrderIndependentWithinFrame()
        {
            var store = new SceneSharedValues();
            store.Publish(0, 0, Writes(("key", SceneValue.FromNumber(1d))));

            Assert.Equal(SceneValue.FromNumber(1d), store.Read(1, "key"));
            store.Publish(1, 0, Writes(("key", SceneValue.FromNumber(2d))));
            Assert.Equal(SceneValue.FromNumber(1d), store.Read(1, "key"));
            store.Publish(1, 1, Writes(("other", SceneValue.FromNumber(3d))));
            Assert.Equal(SceneValue.FromNumber(1d), store.Read(1, "key"));

            Assert.Equal(SceneValue.FromNumber(2d), store.Read(2, "key"));
            Assert.Equal(SceneValue.FromNumber(3d), store.Read(2, "other"));
        }

        [Fact]
        public void RandomSeek_ReadsAreDeterministicPerFrame()
        {
            var store = new SceneSharedValues();
            store.Publish(10, 0, Writes(("key", SceneValue.FromNumber(1d))));
            store.Publish(20, 0, Writes(("key", SceneValue.FromNumber(2d))));

            Assert.Equal(SceneValue.Nil, store.Read(5, "key"));
            Assert.Equal(SceneValue.FromNumber(1d), store.Read(15, "key"));
            Assert.Equal(SceneValue.FromNumber(2d), store.Read(25, "key"));
            Assert.Equal(SceneValue.FromNumber(1d), store.Read(15, "key"));
            Assert.Equal(SceneValue.Nil, store.Read(5, "key"));

            store.Publish(30, 0, Writes(("key", SceneValue.FromNumber(3d))));
            Assert.Equal(SceneValue.Nil, store.Read(5, "key"));
            Assert.Equal(SceneValue.FromNumber(1d), store.Read(15, "key"));
            Assert.Equal(SceneValue.FromNumber(2d), store.Read(25, "key"));
            Assert.Equal(SceneValue.FromNumber(3d), store.Read(35, "key"));
        }

        [Fact]
        public void BackwardSeekPublish_InsertsIntoHistoryInOrder()
        {
            var store = new SceneSharedValues();
            store.Publish(20, 0, Writes(("key", SceneValue.FromNumber(2d))));
            store.Publish(10, 0, Writes(("key", SceneValue.FromNumber(1d))));

            Assert.Equal(SceneValue.Nil, store.Read(10, "key"));
            Assert.Equal(SceneValue.FromNumber(1d), store.Read(15, "key"));
            Assert.Equal(SceneValue.FromNumber(2d), store.Read(25, "key"));
        }

        [Fact]
        public void NilWrite_RecordsRemovalWithoutErasingHistory()
        {
            var store = new SceneSharedValues();
            store.Publish(10, 0, Writes(("key", SceneValue.FromNumber(1d))));
            store.Publish(20, 0, Writes(("key", SceneValue.Nil)));

            Assert.Equal(SceneValue.FromNumber(1d), store.Read(15, "key"));
            Assert.Equal(SceneValue.Nil, store.Read(25, "key"));

            store.Publish(30, 0, Writes(("key", SceneValue.FromNumber(3d))));
            Assert.Equal(SceneValue.Nil, store.Read(25, "key"));
            Assert.Equal(SceneValue.FromNumber(3d), store.Read(31, "key"));
        }

        [Fact]
        public void SameFrameConflict_HigherLayerWinsRegardlessOfPublishOrder()
        {
            var forward = new SceneSharedValues();
            forward.Publish(0, 3, Writes(("key", SceneValue.FromNumber(3d))));
            forward.Publish(0, 5, Writes(("key", SceneValue.FromNumber(5d))));

            var reverse = new SceneSharedValues();
            reverse.Publish(0, 5, Writes(("key", SceneValue.FromNumber(5d))));
            reverse.Publish(0, 3, Writes(("key", SceneValue.FromNumber(3d))));

            Assert.Equal(SceneValue.FromNumber(5d), forward.Read(1, "key"));
            Assert.Equal(SceneValue.FromNumber(5d), reverse.Read(1, "key"));
        }

        [Fact]
        public void SameFrameSameLayer_LaterPublishReplaces()
        {
            var store = new SceneSharedValues();
            store.Publish(0, 2, Writes(("key", SceneValue.FromNumber(1d))));
            store.Publish(0, 2, Writes(("key", SceneValue.FromNumber(2d))));

            Assert.Equal(SceneValue.FromNumber(2d), store.Read(1, "key"));
        }

        [Fact]
        public void Republish_IsIdempotent()
        {
            var store = new SceneSharedValues();
            for (int i = 0; i < 5; i++)
                store.Publish(7, 1, Writes(("key", SceneValue.FromNumber(9d))));

            Assert.Equal(SceneValue.Nil, store.Read(7, "key"));
            Assert.Equal(SceneValue.FromNumber(9d), store.Read(8, "key"));
        }

        [Fact]
        public void Capacity_LimitsDistinctNames_AndKeepsExistingUpdatable()
        {
            var store = new SceneSharedValues();
            var writes = new Dictionary<string, SceneValue>(StringComparer.Ordinal);
            for (int i = 0; i < SceneSharedValues.MaxEntries; i++)
                writes["k" + i] = SceneValue.FromNumber(i);
            store.Publish(0, 0, writes);

            store.Publish(1, 0, Writes(
                ("overflow", SceneValue.FromNumber(1d)),
                ("k0", SceneValue.FromNumber(-1d))));

            Assert.Equal(SceneValue.Nil, store.Read(2, "overflow"));
            Assert.Equal(SceneValue.FromNumber(-1d), store.Read(2, "k0"));

            store.Publish(2, 0, Writes(("k1", SceneValue.Nil)));
            Assert.Equal(SceneValue.Nil, store.Read(3, "k1"));
        }

        [Fact]
        public void NilWritesToUnknownNames_DoNotConsumeCapacity()
        {
            var store = new SceneSharedValues();
            var removals = new Dictionary<string, SceneValue>(StringComparer.Ordinal);
            for (int i = 0; i < SceneSharedValues.MaxEntries; i++)
                removals["ghost" + i] = SceneValue.Nil;
            store.Publish(0, 0, removals);

            store.Publish(1, 0, Writes(("real", SceneValue.FromNumber(1d))));
            Assert.Equal(SceneValue.FromNumber(1d), store.Read(2, "real"));
        }

        [Fact]
        public void HistoryBudget_EvictsOldestVersionsAndKeepsNewest()
        {
            var store = new SceneSharedValues();
            string payload = new('x', SceneSharedValues.MaxTextBytes - 5);
            int frames = (int)(SceneSharedValues.MaxHistoryCostBytes /
                (SceneSharedValues.MaxTextBytes * 2L)) + 200;

            for (int i = 0; i < frames; i++)
                store.Publish(i, 0, Writes(("key", SceneValue.FromString(payload + i.ToString("D5")))));

            Assert.Equal(SceneValue.Nil, store.Read(1, "key"));
            Assert.Equal(
                SceneValue.FromString(payload + (frames - 1).ToString("D5")),
                store.Read(frames, "key"));
        }

        [Fact]
        public void ForScene_IsolatesScopesAndScenes_AndSharesWithinBoth()
        {
            object scopeA = new();
            object scopeB = new();
            string sceneA = Guid.NewGuid().ToString();
            string sceneB = Guid.NewGuid().ToString();

            SceneSharedValues.ForScene(scopeA, sceneA).Publish(0, 0, Writes(("key", SceneValue.FromNumber(1d))));

            Assert.Equal(SceneValue.FromNumber(1d), SceneSharedValues.ForScene(scopeA, sceneA).Read(1, "key"));
            Assert.Equal(SceneValue.Nil, SceneSharedValues.ForScene(scopeA, sceneB).Read(1, "key"));
            Assert.Equal(SceneValue.Nil, SceneSharedValues.ForScene(scopeB, sceneA).Read(1, "key"));
            Assert.Same(SceneSharedValues.ForScene(scopeA, sceneA), SceneSharedValues.ForScene(scopeA, sceneA));
        }
    }
}
