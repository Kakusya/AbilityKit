using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class BattleDiagnosticActorEffectStoreTests
    {
        private BattleDiagnosticSessionScope _scope;
        private BattleDiagnosticActorEffectStore _store;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("effects", "world", 1);
            _store = new BattleDiagnosticActorEffectStore(_scope);
        }

        [Test]
        public void QueryBeforeSnapshot_ReturnsNotProduced()
        {
            var result = _store.QueryActorEffects(1, 0, 10);

            Assert.That(result.Status.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.NotProduced));
        }

        [Test]
        public void ReplaceSnapshot_QueriesEffectsAndDefensivelyCopiesInput()
        {
            var actors = new List<long> { 10, 20 };
            var effects = new List<BattleDiagnosticActorEffect>
            {
                CreateEffect(_scope, 7, 10, 1, BattleDiagnosticEffectDurationPolicy.Duration)
            };

            Assert.That(_store.TryReplaceSnapshot(7, actors, effects), Is.True);
            actors.Clear();
            effects.Clear();

            var result = _store.QueryActorEffects(1, 0, 10);
            var emptyActorResult = _store.QueryActorEffects(2, 7, 20);

            Assert.That(result.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Ready));
            Assert.That(result.Items.Count, Is.EqualTo(1));
            Assert.That(result.Items[0].InstanceId, Is.EqualTo(1));
            Assert.That(result.Items[0].HasRemainingTime, Is.True);
            Assert.That(emptyActorResult.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Empty));
        }

        [Test]
        public void QueryNonLatestFrameOrMissingActor_ReturnsNotCaptured()
        {
            Assert.That(_store.TryReplaceSnapshot(
                7,
                new long[] { 10 },
                Array.Empty<BattleDiagnosticActorEffect>()), Is.True);

            Assert.That(_store.QueryActorEffects(1, 6, 10).Status.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.NotCaptured));
            Assert.That(_store.QueryActorEffects(2, 7, 20).Status.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.NotCaptured));
        }

        [Test]
        public void InvalidOrDuplicateEffect_RejectsWholeReplacement()
        {
            Assert.That(_store.TryReplaceSnapshot(
                1,
                new long[] { 10 },
                new[] { CreateEffect(_scope, 1, 10, 1) }), Is.True);

            var otherScope = new BattleDiagnosticSessionScope("other", "world", 1);
            var invalidScope = _store.TryReplaceSnapshot(
                2,
                new long[] { 10 },
                new[] { CreateEffect(otherScope, 2, 10, 2) });
            var orphanEffect = _store.TryReplaceSnapshot(
                2,
                new long[] { 10 },
                new[] { CreateEffect(_scope, 2, 20, 2) });
            var duplicate = CreateEffect(_scope, 2, 10, 2);
            var duplicateEffect = _store.TryReplaceSnapshot(
                2,
                new long[] { 10 },
                new[] { duplicate, duplicate });

            Assert.That(invalidScope, Is.False);
            Assert.That(orphanEffect, Is.False);
            Assert.That(duplicateEffect, Is.False);
            Assert.That(_store.Revision, Is.EqualTo(1));
            Assert.That(_store.SnapshotFrame, Is.EqualTo(1));
            Assert.That(_store.QueryActorEffects(1, 0, 10).Items[0].InstanceId, Is.EqualTo(1));
        }

        [Test]
        public void FreezeRejectsWritesAndClearAdvancesRevision()
        {
            _store.SetFrozen(true);
            Assert.That(_store.TryReplaceSnapshot(
                1,
                new long[] { 10 },
                Array.Empty<BattleDiagnosticActorEffect>()), Is.False);
            Assert.That(_store.Revision, Is.Zero);

            _store.SetFrozen(false);
            Assert.That(_store.TryReplaceSnapshot(
                1,
                new long[] { 10 },
                Array.Empty<BattleDiagnosticActorEffect>()), Is.True);
            _store.Clear();

            Assert.That(_store.Revision, Is.EqualTo(2));
            Assert.That(_store.SnapshotFrame, Is.EqualTo(BattleDiagnosticFrames.Invalid));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-1f)]
        public void EffectDto_RejectsInvalidTime(float invalidTime)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleDiagnosticActorEffect(
                _scope,
                1,
                10,
                1,
                BattleDiagnosticEffectDurationPolicy.Duration,
                1,
                invalidTime,
                1f,
                true,
                0f,
                false,
                1f,
                0f,
                0,
                false));
        }

        private static BattleDiagnosticActorEffect CreateEffect(
            BattleDiagnosticSessionScope scope,
            int frame,
            long actorId,
            int instanceId,
            BattleDiagnosticEffectDurationPolicy policy = BattleDiagnosticEffectDurationPolicy.Infinite)
        {
            var hasRemainingTime = policy == BattleDiagnosticEffectDurationPolicy.Duration;
            return new BattleDiagnosticActorEffect(
                scope,
                frame,
                actorId,
                instanceId,
                policy,
                1,
                0.5f,
                hasRemainingTime ? 4.5f : 0f,
                hasRemainingTime,
                0f,
                false,
                hasRemainingTime ? 5f : 0f,
                0f,
                2,
                false);
        }
    }

    public sealed class MobaBattleDiagnosticActorEffectSessionTests
    {
        private BattleDiagnosticSessionScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("effects", "world", 1);
        }

        [Test]
        public void SessionWithEffectStore_DeclaresCapabilityAndRoutesQueries()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 16, () => 7, () => 1L);
            var effectStore = new BattleDiagnosticActorEffectStore(_scope);
            effectStore.TryReplaceSnapshot(
                7,
                new long[] { 10 },
                new[]
                {
                    new BattleDiagnosticActorEffect(
                        _scope, 7, 10, 1, BattleDiagnosticEffectDurationPolicy.Infinite,
                        1, 0f, 0f, false, 0f, false, 0f, 0f, 0, false)
                });
            var session = new MobaBattleDiagnosticLocalSession(
                collector.Store,
                collector.StateStore,
                null,
                null,
                null,
                null,
                effectStore);

            var result = session.QueryActorEffects(1, 0, 10);

            Assert.That(session.SessionInfo.Supports(BattleDiagnosticCapabilities.ActorEffects), Is.True);
            Assert.That(session.ActorEffectStoreRevision, Is.EqualTo(1));
            Assert.That(result.Items.Count, Is.EqualTo(1));
        }

        [Test]
        public void SessionWithoutEffectStore_ReturnsUnsupported()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 16, () => 0, () => 1L);
            var session = new MobaBattleDiagnosticLocalSession(collector);

            var result = session.QueryActorEffects(1, 0, 10);

            Assert.That(session.SessionInfo.Supports(BattleDiagnosticCapabilities.ActorEffects), Is.False);
            Assert.That(result.Status.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.Unsupported));
        }

        [Test]
        public void SessionRejectsEffectStoreFromDifferentScope()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 16, () => 0, () => 1L);
            var otherScope = new BattleDiagnosticSessionScope("other", "world", 1);
            var effectStore = new BattleDiagnosticActorEffectStore(otherScope);

            Assert.Throws<ArgumentException>(() => new MobaBattleDiagnosticLocalSession(
                collector.Store,
                collector.StateStore,
                null,
                null,
                null,
                null,
                effectStore));
        }
    }
}
