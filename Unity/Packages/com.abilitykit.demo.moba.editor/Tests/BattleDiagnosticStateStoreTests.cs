using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class BattleDiagnosticStateStoreTests
    {
        private BattleDiagnosticSessionScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("session", "world", 1);
        }

        [Test]
        public void EmptyStore_QueriesReturnNotProduced()
        {
            var store = new BattleDiagnosticStateStore(_scope);

            Assert.That(store.QueryWorld(0).HasValue, Is.False);
            var actors = store.QueryActors(1, 0);
            Assert.That(actors.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Unavailable));
            Assert.That(actors.Status.Availability, Is.EqualTo(BattleDiagnosticDataAvailability.NotProduced));
        }

        [Test]
        public void TryReplaceSnapshot_CommitsWorldAndActorsWithSingleRevision()
        {
            var store = new BattleDiagnosticStateStore(_scope);
            var actors = new List<BattleDiagnosticActorSummary>
            {
                MakeActor(5, 1, BattleDiagnosticActorKind.Hero),
                MakeActor(5, 2, BattleDiagnosticActorKind.Minion)
            };

            var accepted = store.TryReplaceSnapshot(MakeWorld(5, 2), actors);

            Assert.That(accepted, Is.True);
            Assert.That(store.Revision, Is.EqualTo(1));
            Assert.That(store.SnapshotFrame, Is.EqualTo(5));
            Assert.That(store.QueryWorld(0).Value.Frame, Is.EqualTo(5));
            var result = store.QueryActors(1, 0);
            Assert.That(result.Items.Count, Is.EqualTo(2));
            Assert.That(result.Items[0].Frame, Is.EqualTo(5));
        }

        [Test]
        public void TryReplaceSnapshot_FreezesCallerActorList()
        {
            var store = new BattleDiagnosticStateStore(_scope);
            var actors = new List<BattleDiagnosticActorSummary>
            {
                MakeActor(5, 1, BattleDiagnosticActorKind.Hero)
            };
            store.TryReplaceSnapshot(MakeWorld(5, 1), actors);

            actors.Clear();

            Assert.That(store.ActorCount, Is.EqualTo(1));
            Assert.That(store.QueryActors(1, 0).Items.Count, Is.EqualTo(1));
        }

        [Test]
        public void TryReplaceSnapshot_RejectsForeignScopeWithoutChangingSnapshotOrRevision()
        {
            var store = new BattleDiagnosticStateStore(_scope);
            store.TryReplaceSnapshot(
                MakeWorld(5, 1),
                new[] { MakeActor(5, 1, BattleDiagnosticActorKind.Hero) });
            var revision = store.Revision;
            var foreignScope = new BattleDiagnosticSessionScope("other", "world", 1);
            var foreignWorld = new BattleDiagnosticWorldSummary(foreignScope, 6, 1000L, 0, 0, 0);

            var accepted = store.TryReplaceSnapshot(foreignWorld, new BattleDiagnosticActorSummary[0]);

            Assert.That(accepted, Is.False);
            Assert.That(store.Revision, Is.EqualTo(revision));
            Assert.That(store.QueryWorld(0).Value.Frame, Is.EqualTo(5));
        }

        [Test]
        public void TryReplaceSnapshot_RejectsActorFromDifferentFrameAtomically()
        {
            var store = new BattleDiagnosticStateStore(_scope);
            store.TryReplaceSnapshot(
                MakeWorld(5, 1),
                new[] { MakeActor(5, 1, BattleDiagnosticActorKind.Hero) });
            var revision = store.Revision;

            var accepted = store.TryReplaceSnapshot(
                MakeWorld(6, 1),
                new[] { MakeActor(5, 2, BattleDiagnosticActorKind.Minion) });

            Assert.That(accepted, Is.False);
            Assert.That(store.Revision, Is.EqualTo(revision));
            Assert.That(store.QueryWorld(0).Value.Frame, Is.EqualTo(5));
            Assert.That(store.QueryActors(1, 0).Items[0].ActorId, Is.EqualTo(1));
        }

        [Test]
        public void TryReplaceSnapshot_RejectsActorCountMismatchAtomically()
        {
            var store = new BattleDiagnosticStateStore(_scope);

            var accepted = store.TryReplaceSnapshot(
                MakeWorld(5, 2),
                new[] { MakeActor(5, 1, BattleDiagnosticActorKind.Hero) });

            Assert.That(accepted, Is.False);
            Assert.That(store.Revision, Is.Zero);
            Assert.That(store.QueryWorld(0).HasValue, Is.False);
        }

        [Test]
        public void FrozenStore_RejectsAtomicSnapshot()
        {
            var store = new BattleDiagnosticStateStore(_scope);
            store.SetFrozen(true);

            var accepted = store.TryReplaceSnapshot(MakeWorld(1, 0), new BattleDiagnosticActorSummary[0]);

            Assert.That(accepted, Is.False);
            Assert.That(store.Revision, Is.Zero);
        }

        [Test]
        public void QueryWorldAndActors_FrameZeroReturnsLatestAndMatchingFrameReturnsSnapshot()
        {
            var store = new BattleDiagnosticStateStore(_scope);
            store.TryReplaceSnapshot(
                MakeWorld(7, 1),
                new[] { MakeActor(7, 1, BattleDiagnosticActorKind.Hero) });

            Assert.That(store.QueryWorld(0).Value.Frame, Is.EqualTo(7));
            Assert.That(store.QueryWorld(7).Value.Frame, Is.EqualTo(7));
            Assert.That(store.QueryActors(1, 0).Items[0].Frame, Is.EqualTo(7));
            Assert.That(store.QueryActors(2, 7).Items[0].Frame, Is.EqualTo(7));
        }

        [Test]
        public void QueryWorldAndActors_NonLatestFrameIsUnavailable()
        {
            var store = new BattleDiagnosticStateStore(_scope);
            store.TryReplaceSnapshot(
                MakeWorld(7, 1),
                new[] { MakeActor(7, 1, BattleDiagnosticActorKind.Hero) });

            Assert.That(store.QueryWorld(6).HasValue, Is.False);
            var actors = store.QueryActors(1, 6);
            Assert.That(actors.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Unavailable));
            Assert.That(actors.Status.Availability, Is.EqualTo(BattleDiagnosticDataAvailability.NotCaptured));
            StringAssert.Contains("latest-only snapshot is frame 7", actors.Status.Message);
        }

        [Test]
        public void EmptyActorSnapshot_IsAProducedSnapshot()
        {
            var store = new BattleDiagnosticStateStore(_scope);
            store.TryReplaceSnapshot(MakeWorld(7, 0), new BattleDiagnosticActorSummary[0]);

            var actors = store.QueryActors(1, 0);

            Assert.That(actors.Status.Availability, Is.EqualTo(BattleDiagnosticDataAvailability.Available));
            Assert.That(actors.Items.Count, Is.Zero);
        }

        [Test]
        public void Clear_ResetsSnapshotAndAdvancesRevisionOnce()
        {
            var store = new BattleDiagnosticStateStore(_scope);
            store.TryReplaceSnapshot(
                MakeWorld(1, 1),
                new[] { MakeActor(1, 1, BattleDiagnosticActorKind.Hero) });
            var revisionBefore = store.Revision;

            store.Clear();

            Assert.That(store.Revision, Is.EqualTo(revisionBefore + 1));
            Assert.That(store.ActorCount, Is.Zero);
            Assert.That(store.SnapshotFrame, Is.EqualTo(BattleDiagnosticFrames.Invalid));
            Assert.That(store.QueryWorld(0).HasValue, Is.False);
        }

        [Test]
        public void SeparateReplaceApis_RemainAvailableForCompatibility()
        {
            var store = new BattleDiagnosticStateStore(_scope);

            Assert.That(store.TryReplaceWorld(MakeWorld(5, 1)), Is.True);
            Assert.That(store.TryReplaceActors(
                new[] { MakeActor(5, 1, BattleDiagnosticActorKind.Hero) }), Is.True);
            Assert.That(store.Revision, Is.EqualTo(2));
        }

        private BattleDiagnosticWorldSummary MakeWorld(int frame, int actorCount)
        {
            return new BattleDiagnosticWorldSummary(_scope, frame, 1000L, actorCount, 0, 0);
        }

        private BattleDiagnosticActorSummary MakeActor(
            int frame,
            long id,
            BattleDiagnosticActorKind kind)
        {
            return new BattleDiagnosticActorSummary(
                _scope, frame, id, kind, 0, 0, 0, 0, 0, 100f, 100f, true);
        }
    }
}
