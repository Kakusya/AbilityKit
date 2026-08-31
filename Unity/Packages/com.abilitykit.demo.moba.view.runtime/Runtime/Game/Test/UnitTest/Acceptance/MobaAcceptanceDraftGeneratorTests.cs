using System.IO;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class MobaAcceptanceDraftGeneratorTests : MobaAcceptanceTestBase
    {
        [Test]
        public void DraftGenerator_ShouldGenerateProjectileContractDraft()
        {
            var draft = MobaAcceptanceDraftGenerator.GenerateContractDraftForSkill(
                10020101,
                SkillsPath,
                SkillFlowsPath,
                SkillTriggerDirectory);

            Assert.AreEqual(10020101, draft.config.skillId);
            Assert.AreEqual(10020101, draft.config.castFlowId);
            Assert.AreEqual(10020101, draft.config.effectId);
            Assert.AreEqual(10020101, draft.config.triggerId);
            Assert.AreEqual("draft", draft.category);
            Assert.AreEqual(MobaAcceptanceDraftGenerator.GeneratedFrom, draft.generatedFrom);
            Assert.IsNotNull(draft.config.expectedProjectile);
            Assert.IsTrue(draft.mustContain != null && draft.mustContain.Length >= 3);
            Assert.IsTrue(draft.relationships != null && draft.relationships.Length >= 2);
        }

        [Test]
        public void DraftGenerator_ShouldGenerateBuffContractDraft()
        {
            var draft = MobaAcceptanceDraftGenerator.GenerateContractDraftForSkill(
                10010101,
                SkillsPath,
                SkillFlowsPath,
                SkillTriggerDirectory);

            Assert.AreEqual(10010101, draft.config.skillId);
            Assert.AreEqual(10010101, draft.config.effectId);
            Assert.AreEqual(10010101, draft.config.triggerId);
            Assert.AreEqual("draft", draft.category);
            Assert.AreEqual(MobaAcceptanceDraftGenerator.GeneratedFrom, draft.generatedFrom);
            Assert.IsTrue(draft.mustContain != null && draft.mustContain.Length >= 3);
            Assert.IsTrue(draft.relationships != null && draft.relationships.Length >= 2);
        }

        [Test]
        public void DraftGenerator_ShouldResolveNonEqualCastFlowAndAggregateNestedEffects()
        {
            var root = Path.Combine(ArtifactDirectory, "draft-generator-structured-input");
            var triggerDirectory = Path.Combine(root, "triggers");
            Directory.CreateDirectory(triggerDirectory);
            var skillsPath = Path.Combine(root, "skills.json");
            var flowsPath = Path.Combine(root, "skill_flows.json");

            File.WriteAllText(skillsPath, "[{\"Id\":7101,\"CastFlowId\":7201}]");
            File.WriteAllText(flowsPath,
                "[{\"Id\":7201,\"Phases\":["
                + "{\"Type\":10,\"Children\":[{\"Type\":2,\"Timeline\":{\"Events\":[{\"EffectId\":7301},{\"EffectId\":7302}]}}]},"
                + "{\"Type\":12,\"Repeat\":{\"RepeatCount\":2,\"Phase\":{\"Type\":2,\"Timeline\":{\"Events\":[{\"EffectId\":7303}]}}}}"
                + "]}]");
            WriteTrigger(triggerDirectory, 7301, 8301, "add_buff", "\"buffIds\":[901]");
            WriteTrigger(triggerDirectory, 7302, 8302, "spawn_area", "\"areaId\":902");
            WriteTrigger(triggerDirectory, 7303, 8303, "debug_log", "\"message\":\"stage-3\"");

            var draft = MobaAcceptanceDraftGenerator.GenerateContractDraftForSkill(
                7101,
                skillsPath,
                flowsPath,
                triggerDirectory);

            Assert.AreEqual(7201, draft.config.castFlowId);
            CollectionAssert.AreEqual(new[] { 7301, 7302, 7303 }, draft.config.effectIds);
            CollectionAssert.AreEqual(new[] { 8301, 8302, 8303 }, draft.config.triggerIds);
            Assert.AreEqual(3, draft.config.effects.Length);
            Assert.AreEqual(8301, draft.config.triggerId);
            Assert.IsTrue(ContainsTrace(draft, "EffectExecution", 7303));
            Assert.IsTrue(ContainsTrace(draft, "AreaSpawn", 902));
        }

        [Test]
        public void DraftGenerator_ShouldExportContractDraftFile()
        {
            var outputDirectory = Path.Combine(ArtifactDirectory, "draft-generator-tests");
            var outputPath = MobaAcceptanceDraftGenerator.ExportContractDraftForSkill(
                10020101,
                SkillsPath,
                SkillFlowsPath,
                SkillTriggerDirectory,
                outputDirectory);
            var loaded = LoadExpectation(outputPath);

            Assert.IsTrue(File.Exists(outputPath), "Draft export file missing: " + outputPath);
            Assert.AreEqual("skill_10020101_contract_draft", loaded.caseId);
            Assert.AreEqual(10020101, loaded.config.skillId);
            Assert.AreEqual("draft", loaded.category);
            Assert.AreEqual(MobaAcceptanceDraftGenerator.GeneratedFrom, loaded.generatedFrom);
            CollectionAssert.Contains(loaded.config.effectIds, 10020101);
        }

        private static void WriteTrigger(string directory, int fileId, int triggerId, string actionType, string actionArgs)
        {
            File.WriteAllText(
                Path.Combine(directory, "trigger_" + fileId + ".json"),
                "{\"triggers\":[{\"id\":" + triggerId + ",\"actions\":[{\"type\":\"" + actionType + "\"," + actionArgs + "}]}]}");
        }

        private static bool ContainsTrace(MobaAcceptanceExpectation draft, string kind, int configId)
        {
            for (var i = 0; i < draft.mustContain.Length; i++)
            {
                if (draft.mustContain[i].kind == kind && draft.mustContain[i].configId == configId) return true;
            }

            return false;
        }
    }
}
