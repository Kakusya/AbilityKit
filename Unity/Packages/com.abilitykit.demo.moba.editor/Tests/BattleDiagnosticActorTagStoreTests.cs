using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class BattleDiagnosticActorTagStoreTests
    {
        private BattleDiagnosticSessionScope _scope;
        private BattleDiagnosticActorTagStore _store;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("tags", "world", 1);
            _store = new BattleDiagnosticActorTagStore(_scope);
        }

        [Test]
        public void QueryBeforeSnapshot_ReturnsNotProduced()
        {
            var result = _store.QueryActorTags(1, 0, 10);

            Assert.That(result.Status.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.NotProduced));
        }

        [Test]
        public void ReplaceSnapshot_QueriesTagsAndDefensivelyCopiesInput()
        {
            var actors = new List<long> { 10, 20 };
            var tags = new List<BattleDiagnosticActorTag>
            {
                new BattleDiagnosticActorTag(_scope, 7, 10, 1001, "State.Stunned")
            };

            Assert.That(_store.TryReplaceSnapshot(7, actors, tags), Is.True);
            actors.Clear();
            tags.Clear();

            var result = _store.QueryActorTags(1, 0, 10);
            var emptyActorResult = _store.QueryActorTags(2, 7, 20);

            Assert.That(result.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Ready));
            Assert.That(result.Items.Count, Is.EqualTo(1));
            Assert.That(result.Items[0].TagId, Is.EqualTo(1001));
            Assert.That(result.Items[0].Name, Is.EqualTo("State.Stunned"));
            Assert.That(emptyActorResult.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Empty));
        }

        [Test]
        public void QueryNonLatestFrameOrMissingActor_ReturnsNotCaptured()
        {
            Assert.That(_store.TryReplaceSnapshot(
                7,
                new long[] { 10 },
                Array.Empty<BattleDiagnosticActorTag>()), Is.True);

            Assert.That(_store.QueryActorTags(1, 6, 10).Status.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.NotCaptured));
            Assert.That(_store.QueryActorTags(2, 7, 20).Status.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.NotCaptured));
        }

        [Test]
        public void InvalidOrDuplicateTag_RejectsWholeReplacement()
        {
            Assert.That(_store.TryReplaceSnapshot(
                1,
                new long[] { 10 },
                new[] { new BattleDiagnosticActorTag(_scope, 1, 10, 1001, "Old") }), Is.True);

            var otherScope = new BattleDiagnosticSessionScope("other", "world", 1);
            var invalidScope = _store.TryReplaceSnapshot(
                2,
                new long[] { 10 },
                new[] { new BattleDiagnosticActorTag(otherScope, 2, 10, 1002, "Other") });
            var orphanTag = _store.TryReplaceSnapshot(
                2,
                new long[] { 10 },
                new[] { new BattleDiagnosticActorTag(_scope, 2, 20, 1002, "Orphan") });
            var duplicate = new BattleDiagnosticActorTag(_scope, 2, 10, 1002, "Duplicate");
            var duplicateTag = _store.TryReplaceSnapshot(
                2,
                new long[] { 10 },
                new[] { duplicate, duplicate });

            Assert.That(invalidScope, Is.False);
            Assert.That(orphanTag, Is.False);
            Assert.That(duplicateTag, Is.False);
            Assert.That(_store.Revision, Is.EqualTo(1));
            Assert.That(_store.SnapshotFrame, Is.EqualTo(1));
            Assert.That(_store.QueryActorTags(1, 0, 10).Items[0].TagId, Is.EqualTo(1001));
        }

        [Test]
        public void FreezeRejectsWritesAndClearAdvancesRevision()
        {
            _store.SetFrozen(true);
            Assert.That(_store.TryReplaceSnapshot(
                1,
                new long[] { 10 },
                Array.Empty<BattleDiagnosticActorTag>()), Is.False);
            Assert.That(_store.Revision, Is.Zero);

            _store.SetFrozen(false);
            Assert.That(_store.TryReplaceSnapshot(
                1,
                new long[] { 10 },
                Array.Empty<BattleDiagnosticActorTag>()), Is.True);
            _store.Clear();

            Assert.That(_store.Revision, Is.EqualTo(2));
            Assert.That(_store.SnapshotFrame, Is.EqualTo(BattleDiagnosticFrames.Invalid));
        }
    }

    public sealed class MobaBattleDiagnosticActorTagSessionTests
    {
        private BattleDiagnosticSessionScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("tags", "world", 1);
        }

        [Test]
        public void SessionWithTagStore_DeclaresCapabilityAndRoutesQueries()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 16, () => 7, () => 1L);
            var tagStore = new BattleDiagnosticActorTagStore(_scope);
            tagStore.TryReplaceSnapshot(
                7,
                new long[] { 10 },
                new[] { new BattleDiagnosticActorTag(_scope, 7, 10, 1001, "State.Stunned") });
            var session = new MobaBattleDiagnosticLocalSession(
                collector.Store,
                collector.StateStore,
                null,
                null,
                null,
                tagStore);

            var result = session.QueryActorTags(1, 0, 10);

            Assert.That(session.SessionInfo.Supports(BattleDiagnosticCapabilities.ActorTags), Is.True);
            Assert.That(session.ActorTagStoreRevision, Is.EqualTo(1));
            Assert.That(result.Items.Count, Is.EqualTo(1));
        }

        [Test]
        public void SessionWithoutTagStore_ReturnsUnsupported()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 16, () => 0, () => 1L);
            var session = new MobaBattleDiagnosticLocalSession(collector);

            var result = session.QueryActorTags(1, 0, 10);

            Assert.That(session.SessionInfo.Supports(BattleDiagnosticCapabilities.ActorTags), Is.False);
            Assert.That(result.Status.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.Unsupported));
        }

        [Test]
        public void SessionRejectsTagStoreFromDifferentScope()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 16, () => 0, () => 1L);
            var otherScope = new BattleDiagnosticSessionScope("other", "world", 1);
            var tagStore = new BattleDiagnosticActorTagStore(otherScope);

            Assert.Throws<ArgumentException>(() => new MobaBattleDiagnosticLocalSession(
                collector.Store,
                collector.StateStore,
                null,
                null,
                null,
                tagStore));
        }
    }
}
