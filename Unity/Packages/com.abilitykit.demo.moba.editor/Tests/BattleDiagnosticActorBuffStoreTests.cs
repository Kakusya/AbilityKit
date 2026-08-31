using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class BattleDiagnosticActorBuffStoreTests
    {
        private BattleDiagnosticSessionScope _scope;
        private BattleDiagnosticActorBuffStore _store;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("buffs", "world", 1);
            _store = new BattleDiagnosticActorBuffStore(_scope);
        }

        [Test]
        public void QueryBeforeSnapshot_ReturnsNotProduced()
        {
            var result = _store.QueryActorBuffs(1, 0, 10);

            Assert.That(result.Status.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.NotProduced));
        }

        [Test]
        public void ReplaceSnapshot_QueriesBuffsAndDefensivelyCopiesInput()
        {
            var actors = new List<long> { 10, 20 };
            var buffs = new List<BattleDiagnosticActorBuff>
            {
                CreateBuff(_scope, 7, 10, 1001, sourceActorId: 30, stackCount: 2)
            };

            Assert.That(_store.TryReplaceSnapshot(7, actors, buffs), Is.True);
            actors.Clear();
            buffs.Clear();

            var result = _store.QueryActorBuffs(1, 0, 10);
            var emptyActorResult = _store.QueryActorBuffs(2, 7, 20);

            Assert.That(result.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Ready));
            Assert.That(result.Items.Count, Is.EqualTo(1));
            Assert.That(result.Items[0].BuffId, Is.EqualTo(1001));
            Assert.That(result.Items[0].StackCount, Is.EqualTo(2));
            Assert.That(emptyActorResult.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Empty));
        }

        [Test]
        public void QueryNonLatestFrameOrMissingActor_ReturnsNotCaptured()
        {
            Assert.That(_store.TryReplaceSnapshot(
                7,
                new long[] { 10 },
                Array.Empty<BattleDiagnosticActorBuff>()), Is.True);

            var oldFrame = _store.QueryActorBuffs(1, 6, 10);
            var missingActor = _store.QueryActorBuffs(2, 7, 20);

            Assert.That(oldFrame.Status.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.NotCaptured));
            Assert.That(missingActor.Status.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.NotCaptured));
        }

        [Test]
        public void InvalidOrDuplicateBuff_RejectsWholeReplacement()
        {
            Assert.That(_store.TryReplaceSnapshot(
                1,
                new long[] { 10 },
                new[] { CreateBuff(_scope, 1, 10, 1001) }), Is.True);

            var wrongScope = new BattleDiagnosticSessionScope("other", "world", 1);
            var invalidScope = _store.TryReplaceSnapshot(
                2,
                new long[] { 10 },
                new[] { CreateBuff(wrongScope, 2, 10, 1002) });
            var orphanBuff = _store.TryReplaceSnapshot(
                2,
                new long[] { 10 },
                new[] { CreateBuff(_scope, 2, 20, 1002) });
            var duplicateBuff = CreateBuff(
                _scope,
                2,
                10,
                1002,
                sourceActorId: 30,
                sourceContextId: 40,
                runtimeContextId: 50);
            var duplicateInstance = _store.TryReplaceSnapshot(
                2,
                new long[] { 10 },
                new[] { duplicateBuff, duplicateBuff });

            Assert.That(invalidScope, Is.False);
            Assert.That(orphanBuff, Is.False);
            Assert.That(duplicateInstance, Is.False);
            Assert.That(_store.Revision, Is.EqualTo(1));
            Assert.That(_store.SnapshotFrame, Is.EqualTo(1));
            Assert.That(_store.QueryActorBuffs(1, 0, 10).Items[0].BuffId, Is.EqualTo(1001));
        }

        [Test]
        public void DistinctRuntimeContexts_AllowSameBuffAndSource()
        {
            var first = CreateBuff(
                _scope,
                3,
                10,
                1001,
                sourceActorId: 20,
                sourceContextId: 30,
                runtimeContextId: 40);
            var second = CreateBuff(
                _scope,
                3,
                10,
                1001,
                sourceActorId: 20,
                sourceContextId: 30,
                runtimeContextId: 41);

            Assert.That(_store.TryReplaceSnapshot(
                3,
                new long[] { 10 },
                new[] { first, second }), Is.True);
            Assert.That(_store.QueryActorBuffs(1, 3, 10).Items.Count, Is.EqualTo(2));
        }

        [Test]
        public void MissingContextIds_AllowDistinctRuntimeEntries()
        {
            var first = CreateBuff(_scope, 3, 10, 1001, sourceActorId: 20);
            var second = CreateBuff(_scope, 3, 10, 1001, sourceActorId: 20);

            Assert.That(_store.TryReplaceSnapshot(
                3,
                new long[] { 10 },
                new[] { first, second }), Is.True);
            Assert.That(_store.QueryActorBuffs(1, 3, 10).Items.Count, Is.EqualTo(2));
        }

        [Test]
        public void FreezeRejectsWritesAndClearAdvancesRevision()
        {
            _store.SetFrozen(true);
            Assert.That(_store.TryReplaceSnapshot(
                1,
                new long[] { 10 },
                Array.Empty<BattleDiagnosticActorBuff>()), Is.False);
            Assert.That(_store.Revision, Is.Zero);

            _store.SetFrozen(false);
            Assert.That(_store.TryReplaceSnapshot(
                1,
                new long[] { 10 },
                Array.Empty<BattleDiagnosticActorBuff>()), Is.True);
            _store.Clear();

            Assert.That(_store.Revision, Is.EqualTo(2));
            Assert.That(_store.SnapshotFrame, Is.EqualTo(BattleDiagnosticFrames.Invalid));
        }

        private static BattleDiagnosticActorBuff CreateBuff(
            BattleDiagnosticSessionScope scope,
            int frame,
            long actorId,
            int buffId,
            long sourceActorId = 0,
            int stackCount = 1,
            long sourceContextId = 0,
            long runtimeContextId = 0)
        {
            return new BattleDiagnosticActorBuff(
                scope,
                frame,
                actorId,
                buffId,
                sourceActorId,
                stackCount,
                5f,
                1f,
                sourceContextId,
                runtimeContextId,
                1,
                default,
                0,
                0,
                3,
                "Test Buff");
        }
    }

    public sealed class MobaBattleDiagnosticActorBuffSessionTests
    {
        private BattleDiagnosticSessionScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("buffs", "world", 1);
        }

        [Test]
        public void SessionWithBuffStore_DeclaresCapabilityAndRoutesQueries()
        {
            var collector = new MobaBattleDiagnosticEventCollector(
                _scope,
                16,
                () => 7,
                () => 1L);
            var buffStore = new BattleDiagnosticActorBuffStore(_scope);
            buffStore.TryReplaceSnapshot(
                7,
                new long[] { 10 },
                new[]
                {
                    new BattleDiagnosticActorBuff(
                        _scope,
                        7,
                        10,
                        1001,
                        20,
                        1,
                        5f,
                        1f,
                        30,
                        40,
                        1,
                        default,
                        30,
                        0,
                        3,
                        "Test Buff")
                });
            var session = new MobaBattleDiagnosticLocalSession(
                collector.Store,
                collector.StateStore,
                null,
                null,
                buffStore);

            var result = session.QueryActorBuffs(1, 0, 10);

            Assert.That(session.SessionInfo.Supports(BattleDiagnosticCapabilities.ActorBuffs), Is.True);
            Assert.That(session.ActorBuffStoreRevision, Is.EqualTo(1));
            Assert.That(result.Items.Count, Is.EqualTo(1));
        }

        [Test]
        public void SessionWithoutBuffStore_ReturnsUnsupported()
        {
            var collector = new MobaBattleDiagnosticEventCollector(
                _scope,
                16,
                () => 0,
                () => 1L);
            var session = new MobaBattleDiagnosticLocalSession(collector);

            var result = session.QueryActorBuffs(1, 0, 10);

            Assert.That(session.SessionInfo.Supports(BattleDiagnosticCapabilities.ActorBuffs), Is.False);
            Assert.That(result.Status.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.Unsupported));
        }

        [Test]
        public void SessionRejectsBuffStoreFromDifferentScope()
        {
            var collector = new MobaBattleDiagnosticEventCollector(
                _scope,
                16,
                () => 0,
                () => 1L);
            var otherScope = new BattleDiagnosticSessionScope("other", "world", 1);
            var buffStore = new BattleDiagnosticActorBuffStore(otherScope);

            Assert.Throws<ArgumentException>(() => new MobaBattleDiagnosticLocalSession(
                collector.Store,
                collector.StateStore,
                null,
                null,
                buffStore));
        }
    }
}
