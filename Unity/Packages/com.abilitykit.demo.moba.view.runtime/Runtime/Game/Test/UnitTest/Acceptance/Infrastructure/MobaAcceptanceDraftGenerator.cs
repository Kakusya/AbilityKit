using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.Config.Source;
using AbilityKit.Demo.Moba.Share.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AbilityKit.Game.Test.UnitTest
{
    public static class MobaAcceptanceDraftGenerator
    {
        public const string GeneratedFrom = "config-draft-generator-v2";

        public static MobaAcceptanceExpectation GenerateContractDraftForSkill(
            int skillId,
            string skillsPath,
            string skillFlowsPath,
            string triggerDirectory,
            string caseIdSuffix = "contract_draft")
        {
            var resolvedSkillsPath = ResolveRequiredFile(skillsPath, "Skill config missing.");
            var resolvedSkillFlowsPath = ResolveRequiredFile(skillFlowsPath, "Skill flow config missing.");
            var resolvedTriggerDirectory = MobaAcceptanceRunner.ResolveProjectRelativePath(triggerDirectory);
            if (!Directory.Exists(resolvedTriggerDirectory))
            {
                throw new DirectoryNotFoundException("Skill trigger directory missing: " + resolvedTriggerDirectory);
            }

            var skills = DeserializeArray<SkillDTO>(resolvedSkillsPath);
            var skill = FindById(skills, skillId, item => item.Id);
            if (skill == null) throw new InvalidOperationException("Skill entry missing for skill " + skillId + ".");
            if (skill.CastFlowId <= 0) throw new InvalidOperationException("CastFlowId is not configured for skill " + skillId + ".");

            var skillFlows = DeserializeArray<SkillFlowDTO>(resolvedSkillFlowsPath);
            var flow = FindById(skillFlows, skill.CastFlowId, item => item.Id);
            if (flow == null)
            {
                throw new InvalidOperationException("Skill flow " + skill.CastFlowId + " referenced by skill " + skillId + " is missing.");
            }

            var effectIds = new List<int>();
            var directTriggerIds = new List<int>();
            CollectPhaseReferences(flow.Phases, effectIds, directTriggerIds);
            if (effectIds.Count == 0 && directTriggerIds.Count == 0)
            {
                throw new InvalidOperationException("No effect or trigger references configured for skill " + skillId + ".");
            }

            var expectedActions = new List<MobaAcceptanceActionExpectation>();
            var mustContain = new List<MobaAcceptanceTraceExpectation>
            {
                new MobaAcceptanceTraceExpectation { kind = "SkillCast", configId = skillId, minCount = 1 }
            };
            var relationships = new List<MobaAcceptanceRelationshipExpectation>();
            var effectContracts = new List<MobaAcceptanceEffectExpectation>();
            var loadedTriggerIds = new List<int>();
            MobaAcceptanceProjectileExpectation primaryProjectile = null;

            for (var i = 0; i < effectIds.Count; i++)
            {
                LoadTriggerContracts(
                    effectIds[i],
                    effectIds[i],
                    resolvedTriggerDirectory,
                    expectedActions,
                    mustContain,
                    relationships,
                    effectContracts,
                    loadedTriggerIds,
                    ref primaryProjectile);
            }

            for (var i = 0; i < directTriggerIds.Count; i++)
            {
                LoadTriggerContracts(
                    0,
                    directTriggerIds[i],
                    resolvedTriggerDirectory,
                    expectedActions,
                    mustContain,
                    relationships,
                    effectContracts,
                    loadedTriggerIds,
                    ref primaryProjectile);
            }

            if (effectContracts.Count == 0)
            {
                throw new InvalidOperationException("No configured trigger file found for skill " + skillId + ".");
            }

            var primaryContract = FindPrimaryContract(effectContracts);
            return new MobaAcceptanceExpectation
            {
                caseId = "skill_" + skillId + "_" + caseIdSuffix,
                description = "Generated lightweight acceptance contract draft for skill " + skillId + ".",
                worldId = "skill_" + skillId + "_draft_world",
                tickRate = 30,
                accelerated = true,
                category = "draft",
                tags = new[] { "generated", "contract-draft" },
                generatedFrom = GeneratedFrom,
                input = new MobaAcceptanceInputExpectation
                {
                    playerId = "p1",
                    slot = 1,
                    phase = "Press"
                },
                config = new MobaAcceptanceConfigExpectation
                {
                    skillId = skillId,
                    castFlowId = skill.CastFlowId,
                    effectId = primaryContract.effectId,
                    triggerId = primaryContract.triggerId,
                    effectIds = effectIds.ToArray(),
                    triggerIds = loadedTriggerIds.ToArray(),
                    effects = effectContracts.ToArray(),
                    expectedActions = expectedActions.ToArray(),
                    expectedProjectile = primaryProjectile
                },
                mustContain = mustContain.ToArray(),
                mustNotContain = Array.Empty<MobaAcceptanceTraceExpectation>(),
                relationships = relationships.ToArray()
            };
        }

        public static string ExportContractDraftForSkill(
            int skillId,
            string skillsPath,
            string skillFlowsPath,
            string triggerDirectory,
            string outputDirectory,
            string caseIdSuffix = "contract_draft")
        {
            var draft = GenerateContractDraftForSkill(skillId, skillsPath, skillFlowsPath, triggerDirectory, caseIdSuffix);
            return ExportContractDraft(draft, outputDirectory);
        }

        public static string ExportContractDraft(MobaAcceptanceExpectation draft, string outputDirectory)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentException("Output directory is required.", nameof(outputDirectory));

            var resolvedOutputDirectory = MobaAcceptanceRunner.ResolveProjectRelativePath(outputDirectory);
            Directory.CreateDirectory(resolvedOutputDirectory);
            var fileName = SanitizeFileName(string.IsNullOrWhiteSpace(draft.caseId) ? "contract_draft" : draft.caseId) + ".expected.json";
            var outputPath = Path.Combine(resolvedOutputDirectory, fileName);
            File.WriteAllText(outputPath, JsonUtility.ToJson(draft, true));
            return outputPath;
        }

        private static string ResolveRequiredFile(string path, string message)
        {
            var resolvedPath = MobaAcceptanceRunner.ResolveProjectRelativePath(path);
            if (!File.Exists(resolvedPath)) throw new FileNotFoundException(message, resolvedPath);
            return resolvedPath;
        }

        private static T[] DeserializeArray<T>(string path)
        {
            var items = JsonConvert.DeserializeObject<T[]>(File.ReadAllText(path));
            if (items == null) throw new InvalidOperationException("Failed to deserialize config array: " + path);
            return items;
        }

        private static T FindById<T>(T[] items, int id, Func<T, int> idSelector) where T : class
        {
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i] != null && idSelector(items[i]) == id) return items[i];
            }

            return null;
        }

        private static void CollectPhaseReferences(
            SkillPhaseDTO[] phases,
            List<int> effectIds,
            List<int> triggerIds)
        {
            if (phases == null) return;
            for (var i = 0; i < phases.Length; i++)
            {
                var phase = phases[i];
                if (phase == null) continue;

                var events = phase.Timeline != null ? phase.Timeline.Events : null;
                if (events != null)
                {
                    for (var eventIndex = 0; eventIndex < events.Length; eventIndex++)
                    {
                        AddUniquePositive(effectIds, events[eventIndex] != null ? events[eventIndex].EffectId : 0);
                    }
                }

                var ruleTriggerIds = phase.RulePlan != null ? phase.RulePlan.TriggerIds : null;
                AddUniquePositive(triggerIds, ruleTriggerIds);
                CollectPhaseReferences(phase.Children, effectIds, triggerIds);
                if (phase.Repeat != null && phase.Repeat.Phase != null)
                {
                    CollectPhaseReferences(new[] { phase.Repeat.Phase }, effectIds, triggerIds);
                }
            }
        }

        private static void LoadTriggerContracts(
            int effectId,
            int fileId,
            string triggerDirectory,
            List<MobaAcceptanceActionExpectation> allActions,
            List<MobaAcceptanceTraceExpectation> mustContain,
            List<MobaAcceptanceRelationshipExpectation> relationships,
            List<MobaAcceptanceEffectExpectation> contracts,
            List<int> loadedTriggerIds,
            ref MobaAcceptanceProjectileExpectation primaryProjectile)
        {
            var triggerPath = Path.Combine(triggerDirectory, "trigger_" + fileId + ".json");
            if (!File.Exists(triggerPath)) return;

            var source = JsonConvert.DeserializeObject<TriggerSourceConfig>(File.ReadAllText(triggerPath));
            if (source == null || source.Triggers == null) return;

            for (var i = 0; i < source.Triggers.Count; i++)
            {
                var trigger = source.Triggers[i];
                if (trigger == null || trigger.Id <= 0) continue;

                AddUniquePositive(loadedTriggerIds, trigger.Id);
                var actions = new List<MobaAcceptanceActionExpectation>();
                MobaAcceptanceProjectileExpectation projectile = null;
                if (effectId > 0)
                {
                    AddUniqueTrace(mustContain, "EffectExecution", effectId, 0);
                }

                CollectActions(trigger.Actions, effectId, actions, allActions, mustContain, relationships, ref projectile);
                contracts.Add(new MobaAcceptanceEffectExpectation
                {
                    effectId = effectId,
                    triggerId = trigger.Id,
                    expectedActions = actions.ToArray(),
                    expectedProjectile = projectile
                });
                if (primaryProjectile == null && projectile != null) primaryProjectile = projectile;
            }
        }

        private static void CollectActions(
            List<SourceActionConfig> sourceActions,
            int effectId,
            List<MobaAcceptanceActionExpectation> contractActions,
            List<MobaAcceptanceActionExpectation> allActions,
            List<MobaAcceptanceTraceExpectation> mustContain,
            List<MobaAcceptanceRelationshipExpectation> relationships,
            ref MobaAcceptanceProjectileExpectation projectile)
        {
            if (sourceActions == null) return;
            for (var i = 0; i < sourceActions.Count; i++)
            {
                var action = sourceActions[i];
                if (action == null) continue;
                if (!string.IsNullOrEmpty(action.Type))
                {
                    var actionId = StableActionId(action.Type);
                    var expectation = new MobaAcceptanceActionExpectation { actionId = actionId, type = action.Type };
                    contractActions.Add(expectation);
                    allActions.Add(expectation);
                    if (effectId > 0)
                    {
                        AddUniqueTrace(mustContain, "EffectAction", actionId, effectId);
                        AddRelationship(relationships, effectId, "EffectAction", actionId);
                        AddSemanticExpectations(action, effectId, mustContain, relationships, ref projectile);
                    }
                }

                CollectActions(action.Items, effectId, contractActions, allActions, mustContain, relationships, ref projectile);
            }
        }

        private static void AddSemanticExpectations(
            SourceActionConfig action,
            int effectId,
            List<MobaAcceptanceTraceExpectation> mustContain,
            List<MobaAcceptanceRelationshipExpectation> relationships,
            ref MobaAcceptanceProjectileExpectation projectile)
        {
            var type = action.Type;
            if (string.Equals(type, "add_buff", StringComparison.OrdinalIgnoreCase))
                AddTraceExpectations("BuffApply", ReadIntArgs(action.Args, "buff_id", "buffId", "buff_ids", "buffIds"), effectId, mustContain, relationships);
            else if (string.Equals(type, "remove_buff", StringComparison.OrdinalIgnoreCase))
                AddTraceExpectations("BuffRemove", ReadIntArgs(action.Args, "buff_id", "buffId", "buff_ids", "buffIds"), effectId, mustContain, relationships);
            else if (string.Equals(type, "spawn_area", StringComparison.OrdinalIgnoreCase))
                AddTraceExpectations("AreaSpawn", ReadIntArgs(action.Args, "area_id", "areaId", "aoe_id", "aoeId"), effectId, mustContain, relationships);
            else if (string.Equals(type, "summon", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "spawn_summon", StringComparison.OrdinalIgnoreCase))
                AddTraceExpectations("SummonSpawn", ReadIntArgs(action.Args, "summon_id", "summonId", "unit_id", "unitId"), effectId, mustContain, relationships);
            else if (string.Equals(type, "give_damage", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "take_damage", StringComparison.OrdinalIgnoreCase))
                AddTraceExpectations("DamageAttack", ReadIntArgs(action.Args, "reason_param", "reasonParam", "damage_id", "damageId"), effectId, mustContain, relationships);
            else if (string.Equals(type, "heal", StringComparison.OrdinalIgnoreCase))
                AddTraceExpectations("DamageApply", ReadIntArgs(action.Args, "reason_param", "reasonParam", "heal_id", "healId"), effectId, mustContain, relationships);
            else if (string.Equals(type, "play_presentation", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "play_effect", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "play_sound", StringComparison.OrdinalIgnoreCase))
                AddTraceExpectations("PresentationPlay", ReadIntArgs(action.Args, "presentation_id", "presentationId", "effect_id", "effectId", "sound_id", "soundId"), effectId, mustContain, relationships);

            if (string.Equals(type, "shoot_projectile", StringComparison.OrdinalIgnoreCase)
                && TryReadFirstIntArg(action.Args, out var projectileId, "projectileId", "projectile_id")
                && TryReadFirstIntArg(action.Args, out var launcherId, "launcherId", "launcher_id"))
            {
                projectile = new MobaAcceptanceProjectileExpectation { launcherId = launcherId, projectileId = projectileId };
                AddTraceExpectations("ProjectileLaunch", new[] { projectileId }, effectId, mustContain, relationships);
            }
        }

        private static int[] ReadIntArgs(Dictionary<string, object> args, params string[] names)
        {
            var values = new List<int>();
            if (args == null) return values.ToArray();
            for (var i = 0; i < names.Length; i++)
            {
                if (TryGetArg(args, names[i], out var value)) AddIntegers(value, values);
            }

            return values.ToArray();
        }

        private static bool TryReadFirstIntArg(Dictionary<string, object> args, out int value, params string[] names)
        {
            var values = ReadIntArgs(args, names);
            value = values.Length > 0 ? values[0] : 0;
            return value > 0;
        }

        private static bool TryGetArg(Dictionary<string, object> args, string name, out object value)
        {
            foreach (var pair in args)
            {
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static void AddIntegers(object value, List<int> values)
        {
            if (value == null) return;
            if (value is JArray array)
            {
                for (var i = 0; i < array.Count; i++) AddIntegers(array[i], values);
                return;
            }

            if (value is JValue scalar && scalar.Value != null)
            {
                AddIntegers(scalar.Value, values);
                return;
            }

            if (value is long longValue && longValue > 0 && longValue <= int.MaxValue) AddUniquePositive(values, (int)longValue);
            else if (value is int intValue) AddUniquePositive(values, intValue);
        }

        private static MobaAcceptanceEffectExpectation FindPrimaryContract(List<MobaAcceptanceEffectExpectation> contracts)
        {
            for (var i = 0; i < contracts.Count; i++)
            {
                if (contracts[i].effectId > 0) return contracts[i];
            }

            return contracts[0];
        }

        private static void AddTraceExpectations(
            string kind,
            int[] configIds,
            int effectId,
            List<MobaAcceptanceTraceExpectation> mustContain,
            List<MobaAcceptanceRelationshipExpectation> relationships)
        {
            if (string.IsNullOrEmpty(kind) || configIds == null) return;
            for (var i = 0; i < configIds.Length; i++)
            {
                if (configIds[i] <= 0) continue;
                AddUniqueTrace(mustContain, kind, configIds[i], effectId);
                AddRelationship(relationships, effectId, kind, configIds[i]);
            }
        }

        private static void AddUniqueTrace(List<MobaAcceptanceTraceExpectation> traces, string kind, int configId, int underEffectId)
        {
            for (var i = 0; i < traces.Count; i++)
            {
                if (traces[i].kind == kind && traces[i].configId == configId && traces[i].underEffectId == underEffectId) return;
            }

            traces.Add(new MobaAcceptanceTraceExpectation { kind = kind, configId = configId, underEffectId = underEffectId, minCount = 1 });
        }

        private static void AddRelationship(
            List<MobaAcceptanceRelationshipExpectation> relationships,
            int effectId,
            string childKind,
            int childConfigId)
        {
            if (effectId <= 0) return;
            for (var i = 0; i < relationships.Count; i++)
            {
                var item = relationships[i];
                if (item.parentConfigId == effectId && item.childKind == childKind && item.childConfigId == childConfigId) return;
            }

            relationships.Add(new MobaAcceptanceRelationshipExpectation
            {
                parentKind = "EffectExecution",
                parentConfigId = effectId,
                childKind = childKind,
                childConfigId = childConfigId
            });
        }

        private static void AddUniquePositive(List<int> values, int value)
        {
            if (value > 0 && !values.Contains(value)) values.Add(value);
        }

        private static void AddUniquePositive(List<int> values, int[] source)
        {
            if (source == null) return;
            for (var i = 0; i < source.Length; i++) AddUniquePositive(values, source[i]);
        }

        private static string SanitizeFileName(string value)
        {
            var chars = value == null ? Array.Empty<char>() : value.ToCharArray();
            var filtered = new List<char>(chars.Length);
            for (var i = 0; i < chars.Length; i++)
            {
                var ch = chars[i];
                if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == '.') filtered.Add(ch);
            }

            return filtered.Count > 0 ? new string(filtered.ToArray()) : "contract_draft";
        }

        private static int StableActionId(string actionType)
        {
            return StableHash32("action:" + actionType);
        }

        private static int StableHash32(string value)
        {
            unchecked
            {
                const uint offsetBasis = 2166136261u;
                const uint prime = 16777619u;
                var hash = offsetBasis;
                for (var i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= prime;
                }

                return (int)(hash & 0x7FFFFFFF);
            }
        }
    }
}
