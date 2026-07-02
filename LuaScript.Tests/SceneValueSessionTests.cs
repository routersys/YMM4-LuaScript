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
            session.Begin(new object(), NewSceneId(), 0, 0);

            Assert.Equal(SceneValue.Nil, session.Get("key"));
            session.Set("key", SceneValue.FromNumber(1d));
            Assert.Equal(SceneValue.FromNumber(1d), session.Get("key"));
            session.Set("key", SceneValue.Nil);
            Assert.Equal(SceneValue.Nil, session.Get("key"));
        }

        [Fact]
        public void Writes_AreDiscardedWithoutPublish()
        {
            object scope = new();
            string sceneId = NewSceneId();
            var session = new SceneValueSession();
            session.Begin(scope, sceneId, 0, 0);
            session.Set("key", SceneValue.FromNumber(1d));

            session.Begin(scope, sceneId, 1, 0);
            Assert.Equal(SceneValue.Nil, session.Get("key"));
        }

        [Fact]
        public void PublishedWrites_AreVisibleFromNextFrame_WithoutIntermediateFrames()
        {
            object scope = new();
            string sceneId = NewSceneId();
            var writer = new SceneValueSession();
            var reader = new SceneValueSession();

            writer.Begin(scope, sceneId, 0, 0);
            writer.Set("key", SceneValue.FromNumber(1d));
            writer.Publish();

            reader.Begin(scope, sceneId, 0, 1);
            Assert.Equal(SceneValue.Nil, reader.Get("key"));

            reader.Begin(scope, sceneId, 1, 1);
            Assert.Equal(SceneValue.FromNumber(1d), reader.Get("key"));

            reader.Begin(scope, sceneId, 1000, 1);
            Assert.Equal(SceneValue.FromNumber(1d), reader.Get("key"));
        }

        [Fact]
        public void ReadResults_AreStableWithinRun()
        {
            object scope = new();
            string sceneId = NewSceneId();
            var writer = new SceneValueSession();
            var reader = new SceneValueSession();

            writer.Begin(scope, sceneId, 0, 0);
            writer.Set("key", SceneValue.FromNumber(1d));
            writer.Publish();

            reader.Begin(scope, sceneId, 1, 1);
            Assert.Equal(SceneValue.FromNumber(1d), reader.Get("key"));

            writer.Begin(scope, sceneId, 0, 0);
            writer.Set("key", SceneValue.FromNumber(2d));
            writer.Publish();

            Assert.Equal(SceneValue.FromNumber(1d), reader.Get("key"));
        }

        [Fact]
        public void BackwardSeek_ReadsValuesCommittedBeforeThatFrameOnly()
        {
            object scope = new();
            string sceneId = NewSceneId();
            var writer = new SceneValueSession();
            var reader = new SceneValueSession();

            writer.Begin(scope, sceneId, 10, 0);
            writer.Set("key", SceneValue.FromNumber(1d));
            writer.Publish();

            writer.Begin(scope, sceneId, 20, 0);
            writer.Set("key", SceneValue.FromNumber(2d));
            writer.Publish();

            reader.Begin(scope, sceneId, 25, 1);
            Assert.Equal(SceneValue.FromNumber(2d), reader.Get("key"));

            reader.Begin(scope, sceneId, 15, 1);
            Assert.Equal(SceneValue.FromNumber(1d), reader.Get("key"));

            reader.Begin(scope, sceneId, 5, 1);
            Assert.Equal(SceneValue.Nil, reader.Get("key"));
        }

        [Fact]
        public void SameFrame_HigherLayerWinsAcrossSessions()
        {
            object scope = new();
            string sceneId = NewSceneId();
            var lower = new SceneValueSession();
            var upper = new SceneValueSession();
            var reader = new SceneValueSession();

            upper.Begin(scope, sceneId, 0, 5);
            upper.Set("key", SceneValue.FromNumber(5d));
            upper.Publish();

            lower.Begin(scope, sceneId, 0, 3);
            lower.Set("key", SceneValue.FromNumber(3d));
            lower.Publish();

            reader.Begin(scope, sceneId, 1, 0);
            Assert.Equal(SceneValue.FromNumber(5d), reader.Get("key"));
        }

        [Fact]
        public void Scopes_IsolateSessionsSharingSceneId()
        {
            string sceneId = NewSceneId();
            var writer = new SceneValueSession();
            var reader = new SceneValueSession();

            writer.Begin(new object(), sceneId, 0, 0);
            writer.Set("key", SceneValue.FromNumber(1d));
            writer.Publish();

            reader.Begin(new object(), sceneId, 1, 0);
            Assert.Equal(SceneValue.Nil, reader.Get("key"));
        }

        [Fact]
        public void Queries_TrackDistinctStoreReadsOnly()
        {
            var session = new SceneValueSession();
            session.Begin(new object(), NewSceneId(), 0, 0);

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
            object scope = new();
            var session = new SceneValueSession();
            session.Begin(scope, NewSceneId(), 0, 0);
            Assert.False(session.HasWrites);

            session.Set("key", SceneValue.Nil);
            Assert.True(session.HasWrites);

            session.Begin(scope, NewSceneId(), 0, 0);
            Assert.False(session.HasWrites);
        }

        [Fact]
        public void Set_TruncatesLongTextAtUtf8Boundary()
        {
            var session = new SceneValueSession();
            session.Begin(new object(), NewSceneId(), 0, 0);

            string text = new('あ', 2000);
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
            object scope = new();
            string sceneId = NewSceneId();
            var session = new SceneValueSession();
            string name = new('k', SceneSharedValues.MaxNameBytes + 100);

            session.Begin(scope, sceneId, 0, 0);
            session.Set(name, SceneValue.FromNumber(7d));
            Assert.Equal(SceneValue.FromNumber(7d), session.Get(name));
            session.Publish();

            session.Begin(scope, sceneId, 1, 0);
            Assert.Equal(SceneValue.FromNumber(7d), session.Get(name[..SceneSharedValues.MaxNameBytes]));
        }

        [Fact]
        public void Sessions_FollowSceneIdChanges()
        {
            object scope = new();
            string sceneA = NewSceneId();
            string sceneB = NewSceneId();
            var session = new SceneValueSession();

            session.Begin(scope, sceneA, 0, 0);
            session.Set("key", SceneValue.FromNumber(1d));
            session.Publish();

            session.Begin(scope, sceneB, 1, 0);
            Assert.Equal(SceneValue.Nil, session.Get("key"));

            session.Begin(scope, sceneA, 1, 0);
            Assert.Equal(SceneValue.FromNumber(1d), session.Get("key"));
        }
    }
}
