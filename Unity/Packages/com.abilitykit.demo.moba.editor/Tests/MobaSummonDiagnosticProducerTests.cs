using AbilityKit.Demo.Moba.Services;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    /// <summary>
    /// 验证 Summon Spawn/Ended Producer（MobaSummonService）的诊断草稿生成与采集。
    /// </summary>
    public sealed class MobaSummonDiagnosticProducerTests
    {
        private BattleDiagnosticSessionScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("session", "world", 1);
        }

        private static SummonSourceContext CreateSourceContext(
            int sourceActorId = 7,
            int summonActorId = 21,
            int summonConfigId = 501,
            long sourceContextId = 600L,
            long rootContextId = 500L,
            long ownerContextId = 0L,
            MobaSkillCastRuntimeHandle skillRuntimeHandle = default,
            MobaGameplayOrigin origin = default)
        {
            return new SummonSourceContext(
                sourceActorId,
                summonActorId,
                summonConfigId,
                sourceContextId,
                rootContextId,
                ownerContextId,
                in skillRuntimeHandle,
                in origin);
        }

        // ===== SummonSpawned 草稿映射 =====

        [Test]
        public void CreateSummonSpawnedDraft_MapsAllFields()
        {
            var sourceContext = CreateSourceContext(
                sourceActorId: 7,
                summonActorId: 21,
                summonConfigId: 501,
                sourceContextId: 600L,
                rootContextId: 500L,
                skillRuntimeHandle: new MobaSkillCastRuntimeHandle(42, 2, 500));

            var draft = MobaSummonService.CreateSummonSpawnedDraft(21, 501, in sourceContext);

            Assert.That(draft.Kind, Is.EqualTo(BattleDiagnosticEventKind.SummonSpawned));
            Assert.That(draft.Channel, Is.EqualTo(BattleDiagnosticEventChannel.TemporaryEntity));
            Assert.That(draft.Outcome, Is.EqualTo(BattleDiagnosticEventOutcome.Succeeded));
            Assert.That(draft.SourceActorId, Is.EqualTo(7));
            Assert.That(draft.TargetActorId, Is.EqualTo(21));
            Assert.That(draft.ConfigId, Is.EqualTo(501));
            Assert.That(draft.RootContextId, Is.EqualTo(500));
            Assert.That(draft.ContextId, Is.EqualTo(600));
            Assert.That(draft.SkillRuntime, Is.EqualTo(new BattleDiagnosticRuntimeHandle(42, 2)));
            Assert.That(draft.Summary, Does.Contain("501"));
            Assert.That(draft.Summary, Does.Contain("21"));
        }

        [Test]
        public void CreateSummonSpawnedDraft_WithoutSkillRuntime_ProducesDefaultHandle()
        {
            var sourceContext = CreateSourceContext(
                sourceActorId: 7,
                summonActorId: 21,
                summonConfigId: 501,
                sourceContextId: 600L,
                rootContextId: 500L,
                skillRuntimeHandle: default);

            var draft = MobaSummonService.CreateSummonSpawnedDraft(21, 501, in sourceContext);

            Assert.That(draft.SkillRuntime, Is.EqualTo(default(BattleDiagnosticRuntimeHandle)));
        }

        [Test]
        public void CreateSummonSpawnedDraft_WithoutRootContext_FallsBackToSourceContextId()
        {
            var sourceContext = CreateSourceContext(
                sourceActorId: 7,
                summonActorId: 21,
                summonConfigId: 501,
                sourceContextId: 900L,
                rootContextId: 0L,
                skillRuntimeHandle: default);

            var draft = MobaSummonService.CreateSummonSpawnedDraft(21, 501, in sourceContext);

            Assert.That(draft.ContextId, Is.EqualTo(900));
            Assert.That(draft.RootContextId, Is.EqualTo(900));
        }

        // ===== SummonEnded 草稿映射 =====

        [Test]
        public void CreateSummonEndedDraft_MapsAllFields()
        {
            var sourceContext = CreateSourceContext(
                sourceActorId: 7,
                summonActorId: 21,
                summonConfigId: 501,
                sourceContextId: 600L,
                rootContextId: 500L,
                skillRuntimeHandle: new MobaSkillCastRuntimeHandle(42, 2, 500));

            var draft = MobaSummonService.CreateSummonEndedDraft(21, 501, 3, in sourceContext);

            Assert.That(draft.Kind, Is.EqualTo(BattleDiagnosticEventKind.SummonEnded));
            Assert.That(draft.Channel, Is.EqualTo(BattleDiagnosticEventChannel.TemporaryEntity));
            Assert.That(draft.Outcome, Is.EqualTo(BattleDiagnosticEventOutcome.Succeeded));
            Assert.That(draft.SourceActorId, Is.EqualTo(7));
            Assert.That(draft.TargetActorId, Is.EqualTo(21));
            Assert.That(draft.ConfigId, Is.EqualTo(501));
            Assert.That(draft.RootContextId, Is.EqualTo(500));
            Assert.That(draft.ContextId, Is.EqualTo(600));
            Assert.That(draft.SkillRuntime, Is.EqualTo(new BattleDiagnosticRuntimeHandle(42, 2)));
            Assert.That(draft.Summary, Does.Contain("501"));
            Assert.That(draft.Summary, Does.Contain("21"));
            Assert.That(draft.Summary, Does.Contain("despawnReason=3"));
        }

        [Test]
        public void CreateSummonEndedDraft_WithoutSkillRuntime_ProducesDefaultHandle()
        {
            var sourceContext = CreateSourceContext(
                sourceActorId: 7,
                summonActorId: 21,
                summonConfigId: 501,
                sourceContextId: 600L,
                rootContextId: 500L,
                skillRuntimeHandle: default);

            var draft = MobaSummonService.CreateSummonEndedDraft(21, 501, 1, in sourceContext);

            Assert.That(draft.SkillRuntime, Is.EqualTo(default(BattleDiagnosticRuntimeHandle)));
        }

        // ===== Collector 流转 =====

        [Test]
        public void SummonSpawnedDraft_FlowsThroughCollector()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);
            var sourceContext = CreateSourceContext(
                sourceActorId: 7,
                summonActorId: 21,
                summonConfigId: 501,
                sourceContextId: 600L,
                rootContextId: 500L);

            var draft = MobaSummonService.CreateSummonSpawnedDraft(21, 501, in sourceContext);
            var accepted = collector.TryCollect(in draft);

            Assert.That(accepted, Is.True);
            Assert.That(collector.LastSequence, Is.EqualTo(1));
            Assert.That(collector.Store.Count, Is.EqualTo(1));
        }

        [Test]
        public void SummonEndedDraft_FlowsThroughCollector()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);
            var sourceContext = CreateSourceContext(
                sourceActorId: 7,
                summonActorId: 21,
                summonConfigId: 501,
                sourceContextId: 600L,
                rootContextId: 500L);

            var draft = MobaSummonService.CreateSummonEndedDraft(21, 501, 2, in sourceContext);
            var accepted = collector.TryCollect(in draft);

            Assert.That(accepted, Is.True);
            Assert.That(collector.LastSequence, Is.EqualTo(1));
            Assert.That(collector.Store.Count, Is.EqualTo(1));
        }

        [Test]
        public void SummonDrafts_RespectDisabledChannel()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);
            collector.EnabledChannels = BattleDiagnosticEventChannel.Skill;

            var sourceContext = CreateSourceContext(
                sourceActorId: 7,
                summonActorId: 21,
                summonConfigId: 501,
                sourceContextId: 600L,
                rootContextId: 500L);

            var spawnDraft = MobaSummonService.CreateSummonSpawnedDraft(21, 501, in sourceContext);
            var endedDraft = MobaSummonService.CreateSummonEndedDraft(21, 501, 1, in sourceContext);

            Assert.That(collector.TryCollect(in spawnDraft), Is.False);
            Assert.That(collector.TryCollect(in endedDraft), Is.False);
            Assert.That(collector.LastSequence, Is.Zero);
            Assert.That(collector.Store.Count, Is.Zero);
        }

        [Test]
        public void SummonSpawnAndEnd_ProduceStrictSequence()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);
            var sourceContext = CreateSourceContext(
                sourceActorId: 7,
                summonActorId: 21,
                summonConfigId: 501,
                sourceContextId: 600L,
                rootContextId: 500L);

            var spawnDraft = MobaSummonService.CreateSummonSpawnedDraft(21, 501, in sourceContext);
            var endedDraft = MobaSummonService.CreateSummonEndedDraft(21, 501, 1, in sourceContext);

            collector.TryCollect(in spawnDraft);
            collector.TryCollect(in endedDraft);

            Assert.That(collector.LastSequence, Is.EqualTo(2));
            Assert.That(collector.Store.Count, Is.EqualTo(2));
        }
    }
}
