using AbilityKit.Demo.Moba.Systems;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    /// <summary>
    /// 验证诊断状态采样系统的执行顺序常量与依赖约束。
    /// </summary>
    public sealed class MobaDiagnosticSystemOrderTests
    {
        [Test]
        public void DiagnosticStateSample_RunsInLatePhase_AfterActorDespawnCleanup()
        {
            Assert.That(
                MobaSystemOrder.DiagnosticStateSample,
                Is.GreaterThan(MobaSystemOrder.ActorDespawnCleanup));

            // 确认落在 Late 偏移区间内（WorldSystemOrder.Late = 900，DiagnosticStateSample = Base + 900 + 30）
            var lateOffset = MobaSystemOrder.DiagnosticStateSample - MobaSystemOrder.Base;
            Assert.That(lateOffset, Is.GreaterThanOrEqualTo(900));
            Assert.That(lateOffset, Is.LessThan(1000));
        }

        [Test]
        public void DiagnosticStateSample_RunsAfterAllBusinessAndCleanupSystems()
        {
            // 采样必须在所有 Normal 阶段业务系统之后
            Assert.That(
                MobaSystemOrder.DiagnosticStateSample,
                Is.GreaterThan(MobaSystemOrder.GameplayTick));
            Assert.That(
                MobaSystemOrder.DiagnosticStateSample,
                Is.GreaterThan(MobaSystemOrder.BuffLifecycleReconcile));
            Assert.That(
                MobaSystemOrder.DiagnosticStateSample,
                Is.GreaterThan(MobaSystemOrder.EffectsStep));
        }

        [Test]
        public void ValidateKeyDependencies_IncludesDiagnosticSampleCheck()
        {
            var result = MobaSystemOrder.ValidateKeyDependencies();

            Assert.That(result.Passed, Is.True,
                $"System order validation failed: {result.Message}");
        }
    }
}
