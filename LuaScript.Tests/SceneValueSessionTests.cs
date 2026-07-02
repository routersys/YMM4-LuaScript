using System.Text;

namespace LuaScript.Tests
{
    public class SceneValueSessionTests
    {
        private static string NewSceneId() => Guid.NewGuid().ToString();

        [Fact]
        public void Get_ReturnsOwnWriteWithinRun()
        {
            var session = new SceneValueSession();
            session.Begin(NewSceneId(), 0, false);

            Assert.Equal(SceneValue.Nil, session.Get("key"));
            session.Set("key", SceneValue.FromNumber(1d));
            Assert.Equal(SceneValue.FromNumber(1d), session.Get("key"));
            session.Set("key", SceneValue.Nil);
            Assert.Equal(SceneValue.Nil, session.Get("key"));
        }

        [Fact]
        public void Writes_AreDiscardedWithoutPublish()
        {
            string sceneId = NewSceneId();
            var session = new SceneValueSession();
            session.Begin(sceneId, 0, false);
            session.Set("key", SceneValue.FromNumber(1d));

            session.Begin(sceneId, 1, false);
            Assert.Equal(SceneValue.Nil, session.Get("key"));
        }

        [Fact]
        public void PublishedWrites_AreVisibleFromNextGeneration_NotSameGeneration()
        {
            string sceneId = NewSceneId();
            var writer = new SceneValueSession();
            var reader = new SceneValueSession();

            writer.Begin(sceneId, 0, false);
            writer.Set("key", SceneValue.FromNumber(1d));
            writer.Publish();

            reader.Begin(sceneId, 0, false);
            Assert.Equal(SceneValue.Nil, reader.Get("key"));

            reader.Begin(sceneId, 1, false);
            Assert.Equal(SceneValue.FromNumber(1d), reader.Get("key"));
        }

        [Fact]
        public void ReadResults_AreStableWithinRun()
        {
            string sceneId = NewSceneId();
            var writer = new SceneValueSession();
            var reader = new SceneValueSession();

            writer.Begin(sceneId, 0, false);
            writer.Set("key", SceneValue.FromNumber(1d));
            writer.Publish();

            reader.Begin(sceneId, 1, false);
            Assert.Equal(SceneValue.FromNumber(1d), reader.Get("key"));

            writer.Begin(sceneId, 1, false);
            writer.Set("key", SceneValue.FromNumber(2d));
            writer.Publish();

            Assert.Equal(SceneValue.FromNumber(1d), reader.Get("key"));
        }

        [Fact]
        public void Queries_TrackDistinctStoreReadsOnly()
        {
            var session = new SceneValueSession();
            session.Begin(NewSceneId(), 0, false);

            session.Get("a");
            session.Get("a");
            session.Get("b");
            session.Set("c", SceneValue.FromNumber(1d));
            session.Get("c");

            Assert.Equal(2, session.Queries.Count);
            Assert.Equal("a", session.Queries[0].Name);
            Assert.Equal("b", session.Queries[1].Name);
        }

        [Fact]
        public void HasWrites_ReflectsWritesIncludingRemovals()
        {
            var session = new SceneValueSession();
            session.Begin(NewSceneId(), 0, false);
            Assert.False(session.HasWrites);

            session.Set("key", SceneValue.Nil);
            Assert.True(session.HasWrites);

            session.Begin(NewSceneId(), 0, false);
            Assert.False(session.HasWrites);
        }

        [Fact]
        public void Set_TruncatesLongTextAtUtf8Boundary()
        {
            var session = new SceneValueSession();
            session.Begin(NewSceneId(), 0, false);

            string text = new string('あ', 2000);
            session.Set("key", SceneValue.FromString(text));

            var stored = session.Get("key");
            Assert.Equal(SceneValueKind.String, stored.Kind);
            Assert.Equal(SceneSharedValues.MaxTextBytes / 3, stored.Text!.Length);
            Assert.True(Encoding.UTF8.GetByteCount(stored.Text) <= SceneSharedValues.MaxTextBytes);
            Assert.StartsWith(stored.Text, text, StringComparison.Ordinal);
        }

        [Fact]
        public void LongNames_AreTruncatedConsistentlyAcrossSetAndGet()
        {
            string sceneId = NewSceneId();
            var session = new SceneValueSession();
            string name = new string('k', SceneSharedValues.MaxNameBytes + 100);

            session.Begin(sceneId, 0, false);
            session.Set(name, SceneValue.FromNumber(7d));
            Assert.Equal(SceneValue.FromNumber(7d), session.Get(name));
            session.Publish();

            session.Begin(sceneId, 1, false);
            Assert.Equal(SceneValue.FromNumber(7d), session.Get(name[..SceneSharedValues.MaxNameBytes]));
        }

        [Fact]
        public void Sessions_FollowSceneIdChanges()
        {
            string sceneA = NewSceneId();
            string sceneB = NewSceneId();
            var session = new SceneValueSession();

            session.Begin(sceneA, 0, false);
            session.Set("key", SceneValue.FromNumber(1d));
            session.Publish();

            session.Begin(sceneB, 1, false);
            Assert.Equal(SceneValue.Nil, session.Get("key"));

            session.Begin(sceneA, 1, false);
            Assert.Equal(SceneValue.FromNumber(1d), session.Get("key"));
        }
    }
}
