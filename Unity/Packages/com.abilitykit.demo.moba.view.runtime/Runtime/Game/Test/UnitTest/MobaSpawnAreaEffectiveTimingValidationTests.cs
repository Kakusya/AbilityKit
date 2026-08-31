using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Runtime.Config.Plans;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class MobaSpawnAreaEffectiveTimingValidationTests
    {
        private const int TriggerId = 990101;
        private const int AreaId = 490101;
        private const string ErrorCode =
            "moba.trigger.plan.spawn_area_effective_duration_before_delay";

        [Test]
        public void ValidateSpawnAreaEffectiveTimings_RejectsOverrideShorterThanDelay()
        {
            var report = Validate(
                configDurationMs: 700,
                delayMs: 600,
                durationOverride: NumericValueRef.Const(500));

            AssertReportContainsCode(report, ErrorCode);
            Assert.IsTrue(report.ShouldBlockStartup);
        }

        [Test]
        public void ValidateSpawnAreaEffectiveTimings_AcceptsOverrideLongEnoughForDelay()
        {
            var report = Validate(
                configDurationMs: 700,
                delayMs: 600,
                durationOverride: NumericValueRef.Const(700));

            AssertReportDoesNotContainCode(report, ErrorCode);
            Assert.AreEqual(0, report.ErrorCount, report.FormatAllEntries());
        }

        [Test]
        public void ValidateSpawnAreaEffectiveTimings_UsesConfigDurationWithoutOverride()
        {
            var report = Validate(
                configDurationMs: 700,
                delayMs: 600,
                durationOverride: null);

            AssertReportDoesNotContainCode(report, ErrorCode);
            Assert.AreEqual(0, report.ErrorCount, report.FormatAllEntries());
        }

        [Test]
        public void ValidateSpawnAreaEffectiveTimings_SkipsDynamicDurationOverride()
        {
            var report = Validate(
                configDurationMs: 500,
                delayMs: 600,
                durationOverride: NumericValueRef.Var("skill", "area_duration_ms"));

            AssertReportDoesNotContainCode(report, ErrorCode);
            Assert.AreEqual(0, report.ErrorCount, report.FormatAllEntries());
        }

        private static MobaRuntimeValidationReport Validate(
            int configDurationMs,
            int delayMs,
            NumericValueRef? durationOverride)
        {
            var area = new AoeMO(new AoeDTO
            {
                Id = AreaId,
                DurationMs = configDurationMs,
                DelayMs = delayMs,
                OnDelayTriggerIds = new[] { 990102 }
            });

            var args = new Dictionary<string, ActionArgValue>
            {
                ["area_id"] = ActionArgValue.OfConst(AreaId, "area_id")
            };
            if (durationOverride.HasValue)
            {
                args["duration_ms"] = ActionArgValue.Of(
                    durationOverride.Value,
                    "duration_ms");
            }

            var action = ActionCallPlan.WithArgs(
                TriggeringConstants.SpawnAreaId,
                args);
            var plan = new TriggerPlan<object>(
                phase: 0,
                priority: 0,
                triggerId: TriggerId,
                actions: new[] { action });
            var database = new TriggerPlanJsonDatabase();
            var record = new TriggerPlanJsonDatabase.Record(
                TriggerId,
                "test.spawn_area.timing",
                1,
                TriggerPlanScope.OwnerBound,
                in plan);
            database.AddRecord(in record);

            var report = new MobaRuntimeValidationReport();
            MobaBattleConfigReferenceValidator.ValidateSpawnAreaEffectiveTimings(
                database,
                id => id == AreaId ? area : null,
                report);
            return report;
        }

        private static void AssertReportContainsCode(
            MobaRuntimeValidationReport report,
            string code)
        {
            for (var i = 0; i < report.Entries.Count; i++)
            {
                if (string.Equals(
                        report.Entries[i].Code,
                        code,
                        StringComparison.Ordinal))
                {
                    return;
                }
            }

            Assert.Fail(
                "Expected validation code was not reported: "
                + code
                + Environment.NewLine
                + report.FormatAllEntries());
        }

        private static void AssertReportDoesNotContainCode(
            MobaRuntimeValidationReport report,
            string code)
        {
            for (var i = 0; i < report.Entries.Count; i++)
            {
                Assert.AreNotEqual(
                    code,
                    report.Entries[i].Code,
                    report.FormatAllEntries());
            }
        }
    }
}
