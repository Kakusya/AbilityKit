using System;
using System.Reflection;
using AbilityKit.Demo.Moba.Services;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaExceptionDiagnosticProducerTests
    {
        private BattleDiagnosticSessionScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("session", "world", 1);
        }

        [Test]
        public void CreateWarningDraft_MapsContextFields()
        {
            var runtime = new MobaSkillCastRuntimeHandle(42L, 2, 400L);
            var context = new MobaBattleDiagnosticContext(
                rootContextId: 400L,
                sourceContextId: 500L,
                runtimeHandle: runtime,
                actorId: 7,
                skillId: 301,
                detail: "detail");

            var draft = MobaExceptionDiagnosticProducer.CreateWarningDraft(in context, "warning message");

            Assert.That(draft.Kind, Is.EqualTo(BattleDiagnosticEventKind.Warning));
            Assert.That(draft.Channel, Is.EqualTo(BattleDiagnosticEventChannel.WarningAndException));
            Assert.That(draft.Outcome, Is.EqualTo(BattleDiagnosticEventOutcome.None));
            Assert.That(draft.SourceActorId, Is.EqualTo(7));
            Assert.That(draft.TargetActorId, Is.Zero);
            Assert.That(draft.ConfigId, Is.EqualTo(301));
            Assert.That(draft.RootContextId, Is.EqualTo(400L));
            Assert.That(draft.ContextId, Is.EqualTo(500L));
            Assert.That(draft.SkillRuntime, Is.EqualTo(new BattleDiagnosticRuntimeHandle(42L, 2)));
            Assert.That(draft.Summary, Is.EqualTo("warning message"));
        }

        [Test]
        public void CreateExceptionDraft_MapsFailedOutcomeAndExceptionType()
        {
            var context = new MobaBattleDiagnosticContext(
                rootContextId: 400L,
                sourceContextId: 500L,
                actorId: 7,
                skillId: 301);
            var exception = new InvalidOperationException("failed");

            var draft = MobaExceptionDiagnosticProducer.CreateExceptionDraft(
                in context,
                exception,
                "exception message");

            Assert.That(draft.Kind, Is.EqualTo(BattleDiagnosticEventKind.Exception));
            Assert.That(draft.Channel, Is.EqualTo(BattleDiagnosticEventChannel.WarningAndException));
            Assert.That(draft.Outcome, Is.EqualTo(BattleDiagnosticEventOutcome.Failed));
            Assert.That(draft.SourceActorId, Is.EqualTo(7));
            Assert.That(draft.ConfigId, Is.EqualTo(301));
            Assert.That(draft.RootContextId, Is.EqualTo(400L));
            Assert.That(draft.ContextId, Is.EqualTo(500L));
            Assert.That(draft.Summary, Does.Contain("exception message"));
            Assert.That(draft.Summary, Does.Contain(nameof(InvalidOperationException)));
        }

        [Test]
        public void CreateWarningDraft_UsesRuntimeRootWhenExplicitRootIsMissing()
        {
            var runtime = new MobaSkillCastRuntimeHandle(42L, 2, 700L);
            var context = new MobaBattleDiagnosticContext(
                rootContextId: 0L,
                sourceContextId: 500L,
                runtimeHandle: runtime);

            var draft = MobaExceptionDiagnosticProducer.CreateWarningDraft(in context, "warning");

            Assert.That(draft.RootContextId, Is.EqualTo(700L));
            Assert.That(draft.SkillRuntime, Is.EqualTo(new BattleDiagnosticRuntimeHandle(42L, 2)));
        }

        [Test]
        public void CreateWarningDraft_WithoutRuntime_ProducesDefaultHandle()
        {
            var context = new MobaBattleDiagnosticContext(sourceContextId: 500L);

            var draft = MobaExceptionDiagnosticProducer.CreateWarningDraft(in context, "warning");

            Assert.That(draft.SkillRuntime, Is.EqualTo(default(BattleDiagnosticRuntimeHandle)));
            Assert.That(draft.RootContextId, Is.Zero);
        }

        [Test]
        public void WarningAndExceptionDrafts_FlowThroughCollectorInStrictSequence()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);
            var context = new MobaBattleDiagnosticContext(actorId: 7, skillId: 301);
            var exception = new InvalidOperationException("failed");
            var warningDraft = MobaExceptionDiagnosticProducer.CreateWarningDraft(in context, "warning");
            var exceptionDraft = MobaExceptionDiagnosticProducer.CreateExceptionDraft(
                in context,
                exception,
                "exception");

            Assert.That(collector.TryCollect(in warningDraft), Is.True);
            Assert.That(collector.TryCollect(in exceptionDraft), Is.True);
            Assert.That(collector.LastSequence, Is.EqualTo(2L));
            Assert.That(collector.Store.Count, Is.EqualTo(2));
        }

        [Test]
        public void WarningDraft_RespectsDisabledChannelWithoutConsumingSequence()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8)
            {
                EnabledChannels = BattleDiagnosticEventChannel.Skill
            };
            var context = new MobaBattleDiagnosticContext(actorId: 7);
            var draft = MobaExceptionDiagnosticProducer.CreateWarningDraft(in context, "warning");

            Assert.That(collector.TryCollect(in draft), Is.False);
            Assert.That(collector.LastSequence, Is.Zero);
            Assert.That(collector.Store.Count, Is.Zero);
        }

        [Test]
        public void DiagnosticsService_WarningAndException_FlowThroughEventCollector()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);
            var service = new MobaBattleDiagnosticsService();
            var collectorField = typeof(MobaBattleDiagnosticsService).GetField(
                "_eventCollector",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(collectorField, Is.Not.Null);
            collectorField.SetValue(service, collector);

            var context = new MobaBattleDiagnosticContext(
                rootContextId: 400L,
                sourceContextId: 500L,
                actorId: 7,
                skillId: 301);
            service.Warning("warning.key", "warning message", in context, maxCount: 2);
            service.Exception(
                "exception.key",
                new InvalidOperationException("failed"),
                "exception message",
                in context,
                maxCount: 2);

            Assert.That(collector.LastSequence, Is.EqualTo(2L));
            Assert.That(collector.Store.Count, Is.EqualTo(2));
            var query = new BattleDiagnosticEventQuery(
                1L,
                BattleDiagnosticFilter.Default,
                new BattleDiagnosticPageRequest(0, 0, BattleDiagnosticPageRequest.DefaultPageSize));
            var events = collector.Store.Query(query);
            Assert.That(events.Items[0].Kind, Is.EqualTo(BattleDiagnosticEventKind.Warning));
            Assert.That(events.Items[1].Kind, Is.EqualTo(BattleDiagnosticEventKind.Exception));
        }
    }
}
