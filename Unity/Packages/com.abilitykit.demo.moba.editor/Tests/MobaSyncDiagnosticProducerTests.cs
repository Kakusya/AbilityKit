using AbilityKit.Demo.Moba.Services;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaSyncDiagnosticProducerTests
    {
        private BattleDiagnosticSessionScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("session", "world", 1);
        }

        [Test]
        public void CreateSnapshotReceivedDraft_MapsSyncEventFieldsAndTypedPayload()
        {
            var draft = MobaSyncDiagnosticProducer.CreateSnapshotReceivedDraft(120, 4294967295U);

            Assert.That(draft.Kind, Is.EqualTo(BattleDiagnosticEventKind.Sync));
            Assert.That(draft.Channel, Is.EqualTo(BattleDiagnosticEventChannel.Sync));
            Assert.That(draft.Outcome, Is.EqualTo(BattleDiagnosticEventOutcome.Succeeded));
            Assert.That(draft.SourceActorId, Is.Zero);
            Assert.That(draft.TargetActorId, Is.Zero);
            Assert.That(draft.ConfigId, Is.Zero);
            Assert.That(draft.RootContextId, Is.Zero);
            Assert.That(draft.ContextId, Is.Zero);
            Assert.That(draft.SkillRuntime, Is.EqualTo(default(BattleDiagnosticRuntimeHandle)));
            Assert.That(draft.Payload.Kind, Is.EqualTo(BattleDiagnosticPayloadKind.SyncSnapshotReceived));
            Assert.That(draft.Payload.SchemaVersion,
                Is.EqualTo(BattleDiagnosticSyncSnapshotReceivedPayload.CurrentSchemaVersion));
            Assert.That(draft.Payload.TryGetSyncSnapshotReceived(out var payload), Is.True);
            Assert.That(payload.AuthoritativeFrame, Is.EqualTo(120));
            Assert.That(payload.StateHash, Is.EqualTo(4294967295U));
        }

        [Test]
        public void CreateSnapshotReceivedDraft_SummaryContainsAuthoritativeValues()
        {
            var draft = MobaSyncDiagnosticProducer.CreateSnapshotReceivedDraft(-1, 4294967295U);

            Assert.That(draft.Summary, Does.Contain("kind=SnapshotReceived"));
            Assert.That(draft.Summary, Does.Contain("authoritativeFrame=-1"));
            Assert.That(draft.Summary, Does.Contain("stateHash=4294967295"));
        }

        [Test]
        public void SnapshotReceivedDrafts_FlowThroughCollectorWithPayloadAndStrictSequence()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);
            var first = MobaSyncDiagnosticProducer.CreateSnapshotReceivedDraft(120, 101U);
            var second = MobaSyncDiagnosticProducer.CreateSnapshotReceivedDraft(121, 102U);

            Assert.That(collector.TryCollect(in first), Is.True);
            Assert.That(collector.TryCollect(in second), Is.True);
            Assert.That(collector.LastSequence, Is.EqualTo(2L));
            Assert.That(collector.Store.Count, Is.EqualTo(2));

            var query = new BattleDiagnosticEventQuery(
                1,
                BattleDiagnosticFilter.Default,
                new BattleDiagnosticPageRequest(collector.Store.Revision, 0, 8));
            var events = collector.Store.Query(query);
            Assert.That(events.Items[1].Payload.TryGetSyncSnapshotReceived(out var payload), Is.True);
            Assert.That(payload.AuthoritativeFrame, Is.EqualTo(121));
            Assert.That(payload.StateHash, Is.EqualTo(102U));
        }

        [Test]
        public void SnapshotReceivedDraft_RespectsDisabledChannelWithoutConsumingSequence()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8)
            {
                EnabledChannels = BattleDiagnosticEventChannel.Skill
            };
            var draft = MobaSyncDiagnosticProducer.CreateSnapshotReceivedDraft(120, 101U);

            Assert.That(collector.TryCollect(in draft), Is.False);
            Assert.That(collector.LastSequence, Is.Zero);
            Assert.That(collector.Store.Count, Is.Zero);
        }

        [Test]
        public void EmptyPayload_IsSafeAndCannotBeReadAsSyncSnapshot()
        {
            var payload = BattleDiagnosticEventPayload.None;

            Assert.That(payload.HasValue, Is.False);
            Assert.That(payload.Kind, Is.EqualTo(BattleDiagnosticPayloadKind.None));
            Assert.That(payload.SchemaVersion, Is.Zero);
            Assert.That(payload.TryGetSyncSnapshotReceived(out var snapshot), Is.False);
            Assert.That(snapshot, Is.EqualTo(default(BattleDiagnosticSyncSnapshotReceivedPayload)));
        }
    }
}
