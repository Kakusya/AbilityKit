using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class MobaScenarioSetupActionTests : MobaAcceptanceTestBase
    {
        [Test]
        public void ScenarioSetupActions_ShouldMoveActorAndSetAttributes()
        {
            var expectation = CreateSetupActionSmokeExpectation(
                "scenario_setup_move_attr_smoke",
                new[]
                {
                    new MobaAcceptanceSetupActionExpectation
                    {
                        action = "move_to",
                        actorAlias = "target",
                        position = new MobaAcceptanceVector3Expectation { x = 4f, y = 0f, z = 2f }
                    },
                    new MobaAcceptanceSetupActionExpectation
                    {
                        action = "set_attr",
                        actorAlias = "target",
                        property = "hp",
                        value = 777f
                    },
                    new MobaAcceptanceSetupActionExpectation
                    {
                        action = "set_attr",
                        actorAlias = "target",
                        property = "max_hp",
                        value = 1200f
                    }
                },
                new[]
                {
                    new MobaAcceptanceStateExpectation
                    {
                        alias = "target",
                        property = "position",
                        comparator = "eq",
                        expectedVector = new MobaAcceptanceVector3Expectation { x = 4f, y = 0f, z = 2f },
                        tolerance = new MobaAcceptanceVector3Expectation { x = 0.01f, y = 0.01f, z = 0.01f }
                    },
                    new MobaAcceptanceStateExpectation
                    {
                        alias = "target",
                        property = "hp",
                        comparator = "eq",
                        expectedFloat = 777f
                    },
                    new MobaAcceptanceStateExpectation
                    {
                        alias = "target",
                        property = "maxHp",
                        comparator = "eq",
                        expectedFloat = 1200f
                    }
                });

            var summary = RunExpectation(expectation, exportArtifacts: false);

            AssertPassed(summary);
        }

        [Test]
        public void ScenarioSetupActions_ShouldSpawnActorAndBindAlias()
        {
            var expectation = CreateSetupActionSmokeExpectation(
                "scenario_setup_spawn_actor_smoke",
                new[]
                {
                    new MobaAcceptanceSetupActionExpectation
                    {
                        action = "spawn_actor",
                        alias = "summon",
                        teamId = 1,
                        heroId = 1,
                        attributeTemplateId = 1001,
                        unitSubType = 2,
                        mainType = 1,
                        sourceAlias = "caster",
                        sourceKind = "Summon",
                        position = new MobaAcceptanceVector3Expectation { x = 2f, y = 0f, z = 3f }
                    }
                },
                new[]
                {
                    new MobaAcceptanceStateExpectation
                    {
                        alias = "summon",
                        property = "exists",
                        comparator = "eq",
                        expectedBool = true
                    },
                    new MobaAcceptanceStateExpectation
                    {
                        alias = "summon",
                        property = "teamId",
                        comparator = "eq",
                        expectedInt = 1
                    },
                    new MobaAcceptanceStateExpectation
                    {
                        alias = "summon",
                        property = "position",
                        comparator = "eq",
                        expectedVector = new MobaAcceptanceVector3Expectation { x = 2f, y = 0f, z = 3f },
                        tolerance = new MobaAcceptanceVector3Expectation { x = 0.01f, y = 0.01f, z = 0.01f }
                    }
                });

            var summary = RunExpectation(expectation, exportArtifacts: false);

            AssertPassed(summary);
        }

        [Test]
        public void ScenarioSetupActions_ShouldAddAndRemoveBuffs()
        {
            var addExpectation = CreateSetupActionSmokeExpectation(
                "scenario_setup_add_buff_smoke",
                new[]
                {
                    new MobaAcceptanceSetupActionExpectation
                    {
                        action = "add_buff",
                        targetAlias = "target",
                        sourceAlias = "caster",
                        buffId = 101,
                        durationOverrideMs = 10000
                    }
                },
                new[]
                {
                    new MobaAcceptanceStateExpectation
                    {
                        alias = "target",
                        property = "hasBuff",
                        comparator = "eq",
                        expectedInt = 101,
                        expectedBool = true
                    }
                });
            var addSummary = RunExpectation(addExpectation, exportArtifacts: false);
            AssertPassed(addSummary);
            Assert.IsTrue(addSummary.result.buffApplied);

            var removeExpectation = CreateSetupActionSmokeExpectation(
                "scenario_setup_remove_buff_smoke",
                new[]
                {
                    new MobaAcceptanceSetupActionExpectation
                    {
                        action = "add_buff",
                        targetAlias = "target",
                        sourceAlias = "caster",
                        buffId = 101,
                        durationOverrideMs = 10000
                    },
                    new MobaAcceptanceSetupActionExpectation
                    {
                        action = "remove_buff",
                        targetAlias = "target",
                        sourceAlias = "caster",
                        buffId = 101,
                        removeAll = true
                    }
                },
                new[]
                {
                    new MobaAcceptanceStateExpectation
                    {
                        alias = "target",
                        property = "hasBuff",
                        comparator = "eq",
                        expectedInt = 101,
                        expectedBool = false
                    }
                });
            var removeSummary = RunExpectation(removeExpectation, exportArtifacts: false);
            AssertPassed(removeSummary);
            Assert.IsTrue(removeSummary.result.buffApplied);
        }

        private static MobaAcceptanceExpectation CreateSetupActionSmokeExpectation(string caseId, MobaAcceptanceSetupActionExpectation[] setupActions, MobaAcceptanceStateExpectation[] stateExpectations)
        {
            return new MobaAcceptanceExpectation
            {
                caseId = caseId,
                worldId = caseId + "_world",
                tickRate = 30,
                accelerated = true,
                category = "draft",
                tags = new[] { "scenario", "setup_actions", "smoke" },
                scenario = new MobaAcceptanceScenarioExpectation
                {
                    category = "draft",
                    tags = new[] { "scenario", "setup_actions", "smoke" },
                    actors = new[]
                    {
                        new MobaAcceptanceActorExpectation
                        {
                            alias = "caster",
                            playerId = "p1",
                            teamId = 1,
                            heroId = 1001,
                            attributeTemplateId = 1001,
                            basicAttackSkillId = 10010001,
                            skillIds = new[] { 10010101, 10010201, 10010301 },
                            hasSpawnPosition = true,
                            spawnPosition = new MobaAcceptanceVector3Expectation { x = 0f, y = 0f, z = 0f }
                        },
                        new MobaAcceptanceActorExpectation
                        {
                            alias = "target",
                            playerId = "p2",
                            teamId = 2,
                            heroId = 1001,
                            attributeTemplateId = 1001,
                            basicAttackSkillId = 10010001,
                            skillIds = new[] { 10010101, 10010201, 10010301 },
                            hasSpawnPosition = true,
                            spawnPosition = new MobaAcceptanceVector3Expectation { x = 6f, y = 0f, z = 0f }
                        }
                    },
                    setupActions = setupActions,
                    stateExpectations = stateExpectations
                }
            };
        }
    }
}
