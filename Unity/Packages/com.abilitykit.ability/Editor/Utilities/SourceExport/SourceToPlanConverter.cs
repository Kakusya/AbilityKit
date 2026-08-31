#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.Config.Source;
using AbilityKit.Ability.Editor.Utilities;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    /// <summary>
    /// Source JSON → Plan JSON 转换器
    /// </summary>
    internal static class SourceToPlanConverter
    {
        /// <summary>
        /// 转换结果
        /// </summary>
        public class ConvertResult
        {
            public bool Success = true;
            public TriggerPlanDatabaseDto PlanDatabase;
            public List<string> Errors = new List<string>();
            public List<string> Warnings = new List<string>();
            public Dictionary<string, int> ActionNameToId = new Dictionary<string, int>();
            public Dictionary<string, int> ConditionNameToId = new Dictionary<string, int>();
            public Dictionary<string, int> EventNameToId = new Dictionary<string, int>();

            public void AddError(string msg) { Errors.Add(msg); Success = false; }
            public void AddWarning(string msg) { Warnings.Add(msg); }
        }

        /// <summary>
        /// 从 Source JSON 字符串转换
        /// </summary>
        public static ConvertResult Convert(string sourceJson)
        {
            var result = new ConvertResult();

            if (string.IsNullOrWhiteSpace(sourceJson))
            {
                result.AddError("Source JSON is empty");
                return result;
            }

            TriggerSourceConfig source;
            try
            {
                source = JsonConvert.DeserializeObject<TriggerSourceConfig>(sourceJson);
                if (source == null)
                {
                    result.AddError("Failed to parse Source JSON");
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.AddError($"JSON parse error: {ex.Message}");
                return result;
            }

            return Convert(source);
        }

        /// <summary>
        /// 从 Source 配置对象转换
        /// </summary>
        public static ConvertResult Convert(TriggerSourceConfig source)
        {
            var result = new ConvertResult();
            var planDb = new TriggerPlanDatabaseDto();

            if (source == null)
            {
                result.AddError("Source config is null");
                return result;
            }

            // 1. 构建名称→ID 映射表
            BuildNameToIdMaps(source, result);
            var conditionCatalog = BuildConditionCatalog(source.ConditionGroups);
            var actionCatalog = BuildActionCatalog(source.ActionGroups);

            // 2. 转换触发器
            if (source.Triggers != null)
            {
                for (int i = 0; i < source.Triggers.Count; i++)
                {
                    var trigger = source.Triggers[i];
                    ConvertTrigger(trigger, planDb, result, conditionCatalog, actionCatalog);
                }
            }

            // 3. 更新元数据时间
            if (source.Metadata != null)
            {
                source.Metadata.LastModified = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            }

            result.PlanDatabase = planDb;
            return result;
        }

        /// <summary>
        /// 构建名称到 ID 的映射表
        /// </summary>
        private static void BuildNameToIdMaps(TriggerSourceConfig source, ConvertResult result)
        {
            // 动作类型 → ID
            foreach (var type in TriggerActionTypeRegistry.Instance.Keys)
            {
                var id = StableStringId.Get("action:" + type);
                result.ActionNameToId[type] = id;
            }

            // 条件类型 → ID
            foreach (var type in TriggerConditionTypeRegistry.Instance.Keys)
            {
                var id = StableStringId.Get("condition:" + type);
                result.ConditionNameToId[type] = id;
            }

            // 事件名称 → ID（从触发器配置中收集）
            if (source.Triggers != null)
            {
                var eventNames = source.Triggers
                    .Where(t => !string.IsNullOrEmpty(t.Event))
                    .Select(t => t.Event)
                    .Distinct();

                foreach (var evt in eventNames)
                {
                    var id = StableStringId.Get("event:" + evt);
                    result.EventNameToId[evt] = id;
                }
            }

            // 添加内置复合类型
            result.ActionNameToId["seq"] = StableStringId.Get("action:seq");
            result.ConditionNameToId["all"] = StableStringId.Get("condition:all");
            result.ConditionNameToId["any"] = StableStringId.Get("condition:any");
            result.ConditionNameToId["not"] = StableStringId.Get("condition:not");
        }

        /// <summary>
        /// 转换单个触发器
        /// </summary>
        private static void ConvertTrigger(
            SourceTriggerConfig trigger,
            TriggerPlanDatabaseDto planDb,
            ConvertResult result,
            Dictionary<string, SourceConditionGroupConfig> conditionCatalog,
            Dictionary<string, SourceActionGroupConfig> actionCatalog)
        {
            if (trigger == null) return;

            if (trigger.Id <= 0)
            {
                result.AddWarning($"Trigger '{trigger.Name ?? "<unnamed>"}' has invalid Id <= 0, skipped");
                return;
            }

            if (!trigger.Enabled)
            {
                result.AddWarning($"Trigger {trigger.Id} '{trigger.Name}' is disabled, skipped");
                return;
            }

            var resolvedConditions = ResolveConditionList(trigger.Conditions, trigger.ConditionRefs, conditionCatalog, result, $"trigger:{trigger.Id}", new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            var resolvedActions = ResolveActionList(trigger.Actions, trigger.ActionRefs, actionCatalog, result, $"trigger:{trigger.Id}", new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            if (resolvedActions.Count == 0)
            {
                result.AddWarning($"Trigger {trigger.Id} '{trigger.Name}' has no actions, skipped");
                return;
            }

            var dto = new TriggerPlanDto
            {
                TriggerId = trigger.Id,
                EventName = trigger.Event ?? string.Empty,
                AllowExternal = trigger.AllowExternal,
                Phase = GetPhaseValue(trigger.Phase),
                Priority = trigger.Priority,
                Template = ConvertTemplate(trigger.Template, planDb.Strings, result)
            };

            // 解析 EventId
            if (!string.IsNullOrEmpty(trigger.Event))
            {
                dto.EventId = StableStringId.Get("event:" + trigger.Event);
            }

            // 转换条件
            dto.Predicate = ConvertCondition(resolvedConditions, result, conditionCatalog);

            // 转换动作
            dto.Actions = ConvertActions(resolvedActions, planDb.Strings, result, actionCatalog);

            if (dto.Actions == null || dto.Actions.Count == 0)
            {
                result.AddWarning($"Trigger {trigger.Id} '{trigger.Name}' has no valid actions after conversion, skipped");
                return;
            }

            planDb.Triggers.Add(dto);
        }

        /// <summary>
        /// 转换条件列表
        /// </summary>
        private static PredicatePlanDto ConvertCondition(
            List<SourceConditionConfig> conditions,
            ConvertResult result,
            Dictionary<string, SourceConditionGroupConfig> conditionCatalog)
        {
            var predicate = new PredicatePlanDto
            {
                Kind = "expr",
                Nodes = new List<BoolExprNodeDto>()
            };

            if (conditions == null || conditions.Count == 0)
            {
                predicate.Kind = "none";
                return predicate;
            }

            if (conditions.Count == 1)
            {
                var singleCondition = conditions[0];
                ConvertConditionToExpr(singleCondition, predicate.Nodes, result, conditionCatalog, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }
            else
            {
                // 多个条件用 AND 连接
                var allCondition = new SourceConditionConfig { Type = "all", Items = conditions };
                ConvertConditionToExpr(allCondition, predicate.Nodes, result, conditionCatalog, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }

            return predicate;
        }

        /// <summary>
        /// 转换条件为表达式节点
        /// </summary>
        private static void ConvertConditionToExpr(
            SourceConditionConfig condition,
            List<BoolExprNodeDto> nodes,
            ConvertResult result,
            Dictionary<string, SourceConditionGroupConfig> conditionCatalog,
            HashSet<string> resolving)
        {
            if (condition == null) return;

            if (!string.IsNullOrEmpty(condition.Ref))
            {
                var resolved = ResolveConditionRef(condition.Ref, conditionCatalog, result, "condition", resolving);
                ConvertConditionListToExpr(resolved, SourceConditionPlanMapping.BoolKindAnd, nodes, result, conditionCatalog, resolving);
                return;
            }

            var type = condition.Type ?? string.Empty;
            if (string.Equals(type, "all", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "and", StringComparison.OrdinalIgnoreCase))
            {
                var resolvedItems = ResolveConditionList(condition.Items, condition.ConditionRefs, conditionCatalog, result, condition.Type, resolving);
                ConvertConditionListToExpr(resolvedItems, SourceConditionPlanMapping.BoolKindAnd, nodes, result, conditionCatalog, resolving);
                return;
            }

            if (string.Equals(type, "any", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "or", StringComparison.OrdinalIgnoreCase))
            {
                var resolvedItems = ResolveConditionList(condition.Items, condition.ConditionRefs, conditionCatalog, result, condition.Type, resolving);
                ConvertConditionListToExpr(resolvedItems, SourceConditionPlanMapping.BoolKindOr, nodes, result, conditionCatalog, resolving);
                return;
            }

            if (string.Equals(type, "not", StringComparison.OrdinalIgnoreCase))
            {
                var inlineItems = new List<SourceConditionConfig>();
                if (condition.Item != null)
                {
                    inlineItems.Add(condition.Item);
                }

                if (condition.Items != null)
                {
                    inlineItems.AddRange(condition.Items);
                }

                var resolvedItems = ResolveConditionList(inlineItems, condition.ConditionRefs, conditionCatalog, result, condition.Type, resolving);
                ConvertConditionListToExpr(resolvedItems, SourceConditionPlanMapping.BoolKindAnd, nodes, result, conditionCatalog, resolving);
                nodes.Add(new BoolExprNodeDto { Kind = SourceConditionPlanMapping.BoolKindNot });
                return;
            }

            if (!SourceConditionPlanMapping.TryGetCompareOpForConditionType(type, out var compareOp))
            {
                result.AddWarning($"Unknown condition type: {condition.Type}, skipped");
                return;
            }

            var node = new BoolExprNodeDto
            {
                Kind = SourceConditionPlanMapping.BoolKindCompareNumeric,
                CompareOp = compareOp
            };

            if (condition.Args != null)
            {
                if (condition.Args.TryGetValue("left", out var leftObj))
                {
                    node.Left = ConvertConditionOperand("left", leftObj, result);
                }
                else if (condition.Args.TryGetValue("arg_name", out var argNameObj) ||
                         condition.Args.TryGetValue("var_name", out argNameObj))
                {
                    var argName = argNameObj?.ToString() ?? "arg1";
                    node.Left = CreatePayloadRef(argName);
                }

                if (condition.Args.TryGetValue("right", out var rightObj))
                {
                    node.Right = ConvertConditionOperand("right", rightObj, result);
                }
                else if (condition.Args.TryGetValue("value", out var valueObj))
                {
                    node.Right = ConvertValue("value", valueObj, null, result);
                }
                else if (condition.Args.TryGetValue("threshold", out valueObj))
                {
                    node.Right = ConvertValue("threshold", valueObj, null, result);
                }
            }

            node.Left = node.Left ?? CreatePayloadRef("arg1");
            node.Right = node.Right ?? CreateConstRef(0);
            nodes.Add(node);
        }

        private static NumericValueRefDto ConvertConditionOperand(string key, object value, ConvertResult result)
        {
            var text = value is JValue jValue ? jValue.Value<string>() : value?.ToString();
            if (!string.IsNullOrEmpty(text) && text.StartsWith("payload:", StringComparison.OrdinalIgnoreCase))
            {
                return CreatePayloadRef(text.Substring("payload:".Length));
            }

            return ConvertValue(key, value, null, result);
        }

        private static void ConvertConditionListToExpr(
            List<SourceConditionConfig> conditions,
            string logicalOp,
            List<BoolExprNodeDto> nodes,
            ConvertResult result,
            Dictionary<string, SourceConditionGroupConfig> conditionCatalog,
            HashSet<string> resolving)
        {
            if (conditions == null || conditions.Count == 0)
            {
                nodes.Add(new BoolExprNodeDto { Kind = SourceConditionPlanMapping.BoolKindConst, ConstValue = true });
                return;
            }

            for (var i = 0; i < conditions.Count; i++)
            {
                ConvertConditionToExpr(conditions[i], nodes, result, conditionCatalog, resolving);
                if (i > 0)
                {
                    nodes.Add(new BoolExprNodeDto { Kind = logicalOp });
                }
            }
        }

        /// <summary>
        /// 转换动作列表
        /// </summary>
        private static List<ActionCallPlanDto> ConvertActions(
            List<SourceActionConfig> actions,
            Dictionary<int, string> stringTable,
            ConvertResult result,
            Dictionary<string, SourceActionGroupConfig> actionCatalog)
        {
            var plans = new List<ActionCallPlanDto>();

            if (actions == null) return plans;

            foreach (var action in actions)
            {
                if (action != null && string.Equals(action.Type, "seq", StringComparison.OrdinalIgnoreCase))
                {
                    var resolvedItems = ResolveActionList(action.Items, action.ActionRefs, actionCatalog, result, action.Type, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                    plans.AddRange(ConvertActions(resolvedItems, stringTable, result, actionCatalog));
                    continue;
                }

                var plan = ConvertAction(action, stringTable, result, actionCatalog, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                if (plan != null)
                {
                    plans.Add(plan);
                }
            }

            return plans;
        }

        /// <summary>
        /// 转换单个动作
        /// </summary>
        private static ActionCallPlanDto ConvertAction(
            SourceActionConfig action,
            Dictionary<int, string> stringTable,
            ConvertResult result,
            Dictionary<string, SourceActionGroupConfig> actionCatalog,
            HashSet<string> resolving)
        {
            if (action == null) return null;

            if (!string.IsNullOrEmpty(action.Ref))
            {
                var resolved = ResolveActionRef(action.Ref, actionCatalog, result, "action", resolving);
                if (resolved.Count != 1)
                {
                    result.AddWarning($"Action reference '{action.Ref}' must resolve to exactly one action in this context");
                    return null;
                }

                return ConvertAction(resolved[0], stringTable, result, actionCatalog, resolving);
            }

            var plan = new ActionCallPlanDto();

            // 解析动作 ID
            var actionId = TriggerPlanCompilerResolvers.ResolveActionId(action.Type);
            if (actionId.Value == 0)
            {
                result.AddWarning($"Unknown action type: {action.Type}");
                return null;
            }

            plan.ActionId = actionId.Value;

            // 处理 seq 复合动作 - ConvertActions 会在列表层展平。
            if (action.Type == "seq")
            {
                return null;
            }

            // 先保留源参数，再做 runtime 参数映射；
            // 对已知 action，Arity 必须代表“可执行参数个数”而不是 Args 字典总数。
            plan.Args = new Dictionary<string, NumericValueRefDto>();
            plan.Arity = 0;

            if (action.Args != null)
            {
                foreach (var kvp in action.Args)
                {
                    var valueRef = ConvertValue(kvp.Key, kvp.Value, stringTable, result);
                    if (valueRef != null)
                    {
                        plan.Args[kvp.Key] = valueRef;
                        plan.Arity++;
                    }
                }
            }

            // 处理特殊动作类型的参数映射
            MapActionParams(action.Type, action.Args, plan, stringTable, result);
            var explicitArity = TryGetExecutableArity(action.Type);
            if (explicitArity >= 0)
            {
                plan.Arity = (byte)explicitArity;
            }

            return plan;
        }

        private static TriggerTemplateBindingDto ConvertTemplate(
            SourceTriggerTemplateConfig template,
            Dictionary<int, string> stringTable,
            ConvertResult result)
        {
            if (template == null)
            {
                return null;
            }

            var dto = new TriggerTemplateBindingDto
            {
                TemplateId = template.Id,
                Bindings = new Dictionary<string, NumericValueRefDto>(StringComparer.OrdinalIgnoreCase)
            };

            if (template.Bindings != null)
            {
                foreach (var kvp in template.Bindings)
                {
                    if (string.IsNullOrEmpty(kvp.Key))
                    {
                        result.AddWarning("Template binding with empty key skipped");
                        continue;
                    }

                    dto.Bindings[kvp.Key] = ConvertValue(kvp.Key, kvp.Value, stringTable, result);
                }
            }

            return string.IsNullOrEmpty(dto.TemplateId) && dto.Bindings.Count == 0 ? null : dto;
        }

        private static Dictionary<string, SourceConditionGroupConfig> BuildConditionCatalog(Dictionary<string, SourceConditionGroupConfig> groups)
        {
            var catalog = new Dictionary<string, SourceConditionGroupConfig>(StringComparer.OrdinalIgnoreCase);
            if (groups == null) return catalog;

            foreach (var kvp in groups)
            {
                if (!string.IsNullOrEmpty(kvp.Key) && kvp.Value != null)
                {
                    catalog[kvp.Key] = kvp.Value;
                }

                if (!string.IsNullOrEmpty(kvp.Value?.Id))
                {
                    catalog[kvp.Value.Id] = kvp.Value;
                }
            }

            return catalog;
        }

        private static Dictionary<string, SourceActionGroupConfig> BuildActionCatalog(Dictionary<string, SourceActionGroupConfig> groups)
        {
            var catalog = new Dictionary<string, SourceActionGroupConfig>(StringComparer.OrdinalIgnoreCase);
            if (groups == null) return catalog;

            foreach (var kvp in groups)
            {
                if (!string.IsNullOrEmpty(kvp.Key) && kvp.Value != null)
                {
                    catalog[kvp.Key] = kvp.Value;
                }

                if (!string.IsNullOrEmpty(kvp.Value?.Id))
                {
                    catalog[kvp.Value.Id] = kvp.Value;
                }
            }

            return catalog;
        }

        private static List<SourceConditionConfig> ResolveConditionList(
            List<SourceConditionConfig> inlineConditions,
            List<string> conditionRefs,
            Dictionary<string, SourceConditionGroupConfig> catalog,
            ConvertResult result,
            string sourcePath,
            HashSet<string> resolving)
        {
            var resolved = new List<SourceConditionConfig>();
            if (conditionRefs != null)
            {
                foreach (var refId in conditionRefs)
                {
                    resolved.AddRange(ResolveConditionRef(refId, catalog, result, sourcePath, resolving));
                }
            }

            if (inlineConditions != null)
            {
                foreach (var condition in inlineConditions)
                {
                    if (condition == null) continue;
                    if (!string.IsNullOrEmpty(condition.Ref))
                    {
                        resolved.AddRange(ResolveConditionRef(condition.Ref, catalog, result, sourcePath, resolving));
                    }
                    else
                    {
                        resolved.Add(condition);
                    }
                }
            }

            return resolved;
        }

        private static List<SourceConditionConfig> ResolveConditionRef(
            string refId,
            Dictionary<string, SourceConditionGroupConfig> catalog,
            ConvertResult result,
            string sourcePath,
            HashSet<string> resolving)
        {
            var resolved = new List<SourceConditionConfig>();
            if (string.IsNullOrEmpty(refId)) return resolved;

            if (catalog == null || !catalog.TryGetValue(refId, out var group) || group == null)
            {
                result.AddError($"Condition group reference not found: {refId} at {sourcePath}");
                return resolved;
            }

            if (!resolving.Add(refId))
            {
                result.AddError($"Cyclic condition group reference detected: {refId} at {sourcePath}");
                return resolved;
            }

            try
            {
                if (group.Conditions == null) return resolved;
                foreach (var condition in group.Conditions)
                {
                    if (condition == null) continue;
                    if (!string.IsNullOrEmpty(condition.Ref))
                    {
                        resolved.AddRange(ResolveConditionRef(condition.Ref, catalog, result, refId, resolving));
                    }
                    else
                    {
                        resolved.Add(condition);
                    }
                }
            }
            finally
            {
                resolving.Remove(refId);
            }

            return resolved;
        }

        private static List<SourceActionConfig> ResolveActionList(
            List<SourceActionConfig> inlineActions,
            List<string> actionRefs,
            Dictionary<string, SourceActionGroupConfig> catalog,
            ConvertResult result,
            string sourcePath,
            HashSet<string> resolving)
        {
            var resolved = new List<SourceActionConfig>();
            if (actionRefs != null)
            {
                foreach (var refId in actionRefs)
                {
                    resolved.AddRange(ResolveActionRef(refId, catalog, result, sourcePath, resolving));
                }
            }

            if (inlineActions != null)
            {
                foreach (var action in inlineActions)
                {
                    if (action == null) continue;
                    if (!string.IsNullOrEmpty(action.Ref))
                    {
                        resolved.AddRange(ResolveActionRef(action.Ref, catalog, result, sourcePath, resolving));
                    }
                    else
                    {
                        resolved.Add(action);
                    }
                }
            }

            return resolved;
        }

        private static List<SourceActionConfig> ResolveActionRef(
            string refId,
            Dictionary<string, SourceActionGroupConfig> catalog,
            ConvertResult result,
            string sourcePath,
            HashSet<string> resolving)
        {
            var resolved = new List<SourceActionConfig>();
            if (string.IsNullOrEmpty(refId)) return resolved;

            if (catalog == null || !catalog.TryGetValue(refId, out var group) || group == null)
            {
                result.AddError($"Action group reference not found: {refId} at {sourcePath}");
                return resolved;
            }

            if (!resolving.Add(refId))
            {
                result.AddError($"Cyclic action group reference detected: {refId} at {sourcePath}");
                return resolved;
            }

            try
            {
                if (group.Actions == null) return resolved;
                foreach (var action in group.Actions)
                {
                    if (action == null) continue;
                    if (!string.IsNullOrEmpty(action.Ref))
                    {
                        resolved.AddRange(ResolveActionRef(action.Ref, catalog, result, refId, resolving));
                    }
                    else
                    {
                        resolved.Add(action);
                    }
                }
            }
            finally
            {
                resolving.Remove(refId);
            }

            return resolved;
        }

        private static int TryGetExecutableArity(string actionType)
        {
            switch (actionType)
            {
                case "debug_log":
                case "shoot_projectile":
                case "give_damage":
                case "heal":
                case "modify_resource":
                case "spawn_area":
                case "remove_area":
                case "play_effect":
                case "add_buff":
                case "remove_buff":
                case "set_move_input_blocked":
                case "set_skill_input_blocked":
                case "set_move_direction_lock":
                case "set_move_direction_forward_only":
                case "set_move_direction_8dir":
                case "set_move_direction_free":
                case "clear_move_direction_mode":
                case "advance_gameplay_counter":
                    return 2;
                default:
                    return -1;
            }
        }

        /// <summary>
        /// 特殊动作类型的参数映射
        /// </summary>
        private static void MapActionParams(
            string actionType,
            Dictionary<string, object> args,
            ActionCallPlanDto plan,
            Dictionary<int, string> stringTable,
            ConvertResult result)
        {
            if (args == null) return;

            switch (actionType)
            {
                case "debug_log":
                    // message 参数需要存入字符串表
                    if (args.TryGetValue("message", out var msgObj))
                    {
                        var msg = msgObj?.ToString() ?? "";
                        var strId = StableStringId.Get("str:" + msg);
                        plan.Args["msg_id"] = new NumericValueRefDto
                        {
                            Kind = "Const",
                            ConstValue = strId
                        };

                        if (!stringTable.ContainsKey(strId))
                        {
                            stringTable[strId] = msg;
                        }
                    }

                    if (args.TryGetValue("dump_args", out var dumpObj))
                    {
                        var dump = System.Convert.ToDouble(dumpObj) != 0;
                        plan.Args["dump"] = new NumericValueRefDto
                        {
                            Kind = "Const",
                            ConstValue = dump ? 1 : 0
                        };
                    }
                    break;

                case "shoot_projectile":
                    // Support both source-style entity args and Unity-authored runtime-style ids.
                    MapEntityArg(args, "launcher", "launcher_id", plan);
                    MapEntityArg(args, "target", "target_actor_id", plan);
                    MapIntArgAlias(args, plan, "launcher_id", "launcher_id", "launcherId", "launcher");
                    MapIntArgAlias(args, plan, "target_actor_id", "target_actor_id", "targetActorId", "target_id", "targetId", "target");
                    MapIntArgAlias(args, plan, "projectile_id", "projectile_id", "projectileId", "projectile");
                    MapIntArgAlias(args, plan, "continuous_process_id", "continuous_process_id", "continuousProcessId", "continuous_id", "continuousId");
                    MapIntArgAlias(args, plan, "target_query_id", "target_query_id", "targetQueryId", "query_template_id", "queryTemplateId");
                    MapIntArgAlias(args, plan, "target_max_count", "target_max_count", "targetMaxCount", "max_count", "maxCount");
                    MapIntArgAlias(args, plan, "target_source", "target_source", "targetSource");
                    MapBoolArgAlias(args, plan, "track_target", "track_target", "trackTarget", "tracking", "homing");
                    MapFloatArgAlias(args, plan, "speed", "speed");
                    break;

                case "give_damage":
                    MapEntityArg(args, "from", "from_id", plan);
                    MapEntityArg(args, "to", "to_id", plan);
                    MapExprArg(args, "amount", "damage_amount", plan);
                    MapIntArgAlias(args, plan, "from_id", "fromId", "from");
                    MapIntArgAlias(args, plan, "to_id", "toId", "to");
                    MapFloatArgAlias(args, plan, "damage_amount", "amount", "damage_value", "value", "damageValue");
                    MapIntArgAlias(args, plan, "damage_type", "damageType");
                    MapIntArgAlias(args, plan, "reason_param", "reasonParam", "reason");
                    MapIntArgAlias(args, plan, "target_query_id", "targetQueryId", "queryTemplateId");
                    break;

                case "add_buff":
                    MapEntityArg(args, "target", "target_actor_id", plan);
                    MapIntArgAlias(args, plan, "target_actor_id", "target_actor_id", "targetActorId", "target_id", "targetId", "target");
                    MapBoolArgAlias(args, plan, "target_self", "target_self", "targetSelf", "self");
                    MapIntListArgAliases(args, plan, new[] { "buff_id", "buffIds" }, "buffids", "buff_ids", "buffid", "buff_id", "id", "ids");
                    break;

                case "remove_buff":
                    MapEntityArg(args, "target", "target_actor_id", plan);
                    MapIntArgAlias(args, plan, "target_actor_id", "target_actor_id", "targetActorId", "target_id", "targetId", "target");
                    MapBoolArgAlias(args, plan, "target_self", "target_self", "targetSelf", "self");
                    MapIntArgAlias(args, plan, "buff_id", "buff_id", "buffId", "buffid", "id");
                    MapIntArgAlias(args, plan, "source_actor_id", "source_actor_id", "sourceActorId", "source_id", "sourceId");
                    MapBoolArgAlias(args, plan, "remove_all", "remove_all", "removeAll", "all", "clear");
                    MapBoolArgAlias(args, plan, "remove_slow", "remove_slow", "removeSlow");
                    MapIntArgAlias(args, plan, "reason", "reason", "remove_reason", "removeReason");
                    break;

                case "heal":
                    MapEntityArg(args, "target", "target_actor_id", plan);
                    MapIntArgAlias(args, plan, "target_actor_id", "target_actor_id", "targetActorId", "target_id", "targetId", "target");
                    MapBoolArgAlias(args, plan, "target_self", "target_self", "targetSelf", "self");
                    MapFloatArgAlias(args, plan, "amount", "amount", "heal_amount", "value");
                    MapIntArgAlias(args, plan, "heal_type", "heal_type", "healType", "type");
                    MapIntArgAlias(args, plan, "reason_kind", "reason_kind", "reasonKind");
                    MapIntArgAlias(args, plan, "reason_param", "reason_param", "reasonParam", "reason");
                    break;

                case "modify_resource":
                    MapEntityArg(args, "target", "target_actor_id", plan);
                    MapIntArgAlias(args, plan, "target_actor_id", "target_actor_id", "targetActorId", "target_id", "targetId", "target");
                    MapBoolArgAlias(args, plan, "target_self", "target_self", "targetSelf", "self");
                    MapIntArgAlias(args, plan, "resource_type", "resource_type", "resourceType", "type");
                    MapFloatArgAlias(args, plan, "amount", "amount", "delta", "value");
                    MapFloatArgAlias(args, plan, "min", "min", "min_value", "minValue");
                    MapFloatArgAlias(args, plan, "max", "max", "max_value", "maxValue");
                    break;

                case "advance_gameplay_counter":
                    MapIntArgAlias(args, plan, "key_id", "key_id", "keyId", "keyid", "id");
                    MapPayloadFieldArgAlias(args, plan, "scope_payload_field_id", "scope_payload_field", "scopePayloadField", "scope_field", "field");
                    MapIntArgAlias(args, plan, "scope_payload_field_id", "scope_payload_field_id", "scopePayloadFieldId", "scope_field_id", "field_id");
                    MapFloatArgAlias(args, plan, "threshold", "threshold", "limit", "count");
                    MapFloatArgAlias(args, plan, "delta", "delta", "step", "amount");
                    MapFloatArgAlias(args, plan, "reset_value", "reset_value", "resetValue", "reset");
                    MapIntArgAlias(args, plan, "trigger_id", "trigger_id", "triggerId", "on_threshold_trigger_id", "onThresholdTriggerId");
                    break;

                case "spawn_area":
                    MapIntArgAlias(args, plan, "area_id", "area_id", "areaId", "aoe_id", "aoeId", "id");
                    MapIntArgAlias(args, plan, "position_mode", "position_mode", "positionMode", "position");
                    MapFloatArgAlias(args, plan, "radius", "radius", "radius_override", "radiusOverride");
                    MapIntArgAlias(args, plan, "duration_frames", "duration_frames", "durationFrames", "lifetime_frames", "lifetimeFrames");
                    MapIntArgAlias(args, plan, "duration_ms", "duration_ms", "durationMs", "lifetime_ms", "lifetimeMs");
                    MapIntArgAlias(args, plan, "stay_interval_frames", "stay_interval_frames", "stayIntervalFrames", "stay_frames", "stayFrames");
                    MapIntArgAlias(args, plan, "collision_layer_mask", "collision_layer_mask", "collisionLayerMask", "layer_mask", "layerMask");
                    MapFloatArgAlias(args, plan, "offset_x", "offset_x", "offsetX", "x");
                    MapFloatArgAlias(args, plan, "offset_y", "offset_y", "offsetY", "y");
                    MapFloatArgAlias(args, plan, "offset_z", "offset_z", "offsetZ", "z");
                    break;
            }
        }

        /// <summary>
        /// 映射实体参数
        /// </summary>
        private static void MapEntityArg(Dictionary<string, object> args, string sourceKey, string targetKey, ActionCallPlanDto plan)
        {
            if (args.TryGetValue(sourceKey, out var obj))
            {
                plan.Args[targetKey] = ConvertValue(sourceKey, obj, null, null);
            }
        }

        /// <summary>
        /// 映射整数参数
        /// </summary>
        private static void MapIntArg(Dictionary<string, object> args, string key, ActionCallPlanDto plan)
        {
            if (args.TryGetValue(key, out var obj) && obj != null)
            {
                plan.Args[key] = ConvertValue(key, obj, null, null);
            }
        }

        /// <summary>
        /// 映射整数参数别名，命中首个值后写入目标 key。
        /// </summary>
        private static void MapIntArgAlias(Dictionary<string, object> args, ActionCallPlanDto plan, string targetKey, params string[] candidateKeys)
        {
            for (int i = 0; i < candidateKeys.Length; i++)
            {
                var sourceKey = candidateKeys[i];
                if (args.TryGetValue(sourceKey, out var obj) && obj != null)
                {
                    plan.Args[targetKey] = ConvertValue(sourceKey, obj, null, null);
                    return;
                }
            }
        }

        /// <summary>
        /// 映射布尔参数别名，命中首个值后写入目标 key。
        /// </summary>
        private static void MapBoolArgAlias(Dictionary<string, object> args, ActionCallPlanDto plan, string targetKey, params string[] candidateKeys)
        {
            MapIntArgAlias(args, plan, targetKey, candidateKeys);
        }

        private static void MapPayloadFieldArgAlias(Dictionary<string, object> args, ActionCallPlanDto plan, string targetKey, params string[] candidateKeys)
        {
            for (int i = 0; i < candidateKeys.Length; i++)
            {
                var sourceKey = candidateKeys[i];
                if (!args.TryGetValue(sourceKey, out var obj) || obj == null)
                {
                    continue;
                }

                var fieldName = obj is JValue jValue ? jValue.Value<string>() : obj.ToString();
                if (string.IsNullOrEmpty(fieldName))
                {
                    continue;
                }

                plan.Args[targetKey] = new NumericValueRefDto
                {
                    Kind = "Const",
                    ConstValue = StableStringId.Get("payload:" + fieldName)
                };
                return;
            }
        }

        /// <summary>
        /// 将整型列表展开为 runtime schema 可读取的具名参数集合。
        /// </summary>
        private static void MapIntListArgAliases(Dictionary<string, object> args, ActionCallPlanDto plan, string[] sourceKeys, params string[] aliasKeys)
        {
            if (args == null || sourceKeys == null || aliasKeys == null || aliasKeys.Length == 0)
            {
                return;
            }

            for (int sourceIndex = 0; sourceIndex < sourceKeys.Length; sourceIndex++)
            {
                var sourceKey = sourceKeys[sourceIndex];
                if (!args.TryGetValue(sourceKey, out var obj) || obj == null)
                {
                    continue;
                }

                if (TryGetNumberList(obj, out var values) && values.Count > 0)
                {
                    for (int i = 0; i < values.Count; i++)
                    {
                        var aliasBase = aliasKeys[Math.Min(i, aliasKeys.Length - 1)];
                        var targetKey = i == 0 ? aliasBase : aliasBase + "_" + i;
                        plan.Args[targetKey] = new NumericValueRefDto
                        {
                            Kind = "Const",
                            ConstValue = values[i]
                        };
                    }
                    return;
                }

                if (TryGetSingleNumber(obj, out var singleValue) && singleValue > 0)
                {
                    plan.Args[aliasKeys[0]] = new NumericValueRefDto
                    {
                        Kind = "Const",
                        ConstValue = singleValue
                    };
                    return;
                }
            }
        }

        /// <summary>
        /// 映射浮点数参数
        /// </summary>
        private static void MapFloatArg(Dictionary<string, object> args, string key, ActionCallPlanDto plan)
        {
            MapIntArg(args, key, plan); // 使用相同逻辑，double 可表示 float
        }

        /// <summary>
        /// 映射浮点数参数别名，命中首个值后写入目标 key。
        /// </summary>
        private static void MapFloatArgAlias(Dictionary<string, object> args, ActionCallPlanDto plan, string targetKey, params string[] candidateKeys)
        {
            MapIntArgAlias(args, plan, targetKey, candidateKeys);
        }

        /// <summary>
        /// 映射表达式参数
        /// </summary>
        private static void MapExprArg(Dictionary<string, object> args, string sourceKey, string targetKey, ActionCallPlanDto plan)
        {
            if (args.TryGetValue(sourceKey, out var obj))
            {
                plan.Args[targetKey] = ConvertValue(sourceKey, obj, null, null);
            }
        }

        /// <summary>
        /// 转换值为 NumericValueRefDto
        /// </summary>
        private static NumericValueRefDto ConvertValue(string key, object value, Dictionary<int, string> stringTable, ConvertResult result)
        {
            if (value == null)
            {
                return new NumericValueRefDto { Kind = "Const", ConstValue = 0 };
            }

            if (value is JToken token)
            {
                return ConvertJTokenValue(token, result);
            }

            // 字符串可能是实体引用或表达式
            if (value is string strValue)
            {
                return ConvertStringValue(strValue);
            }

            // 数字
            if (value is double dValue)
            {
                return new NumericValueRefDto { Kind = "Const", ConstValue = dValue };
            }

            if (value is int iValue)
            {
                return new NumericValueRefDto { Kind = "Const", ConstValue = iValue };
            }

            if (value is long lValue)
            {
                return new NumericValueRefDto { Kind = "Const", ConstValue = lValue };
            }

            // 布尔值
            if (value is bool bValue)
            {
                return new NumericValueRefDto { Kind = "Const", ConstValue = bValue ? 1 : 0 };
            }

            // 其他类型转字符串处理
            return ConvertStringValue(value.ToString());
        }

        private static bool TryGetNumberList(object value, out List<double> values)
        {
            values = null;
            if (value == null)
            {
                return false;
            }

            if (value is JArray jArray)
            {
                var list = new List<double>(jArray.Count);
                for (int i = 0; i < jArray.Count; i++)
                {
                    var item = jArray[i];
                    if (item == null || (item.Type != JTokenType.Integer && item.Type != JTokenType.Float))
                    {
                        continue;
                    }

                    list.Add(item.Value<double>());
                }

                values = list;
                return list.Count > 0;
            }

            if (value is IEnumerable<int> intValues)
            {
                values = intValues.Select(static v => (double)v).ToList();
                return values.Count > 0;
            }

            if (value is IEnumerable<long> longValues)
            {
                values = longValues.Select(static v => (double)v).ToList();
                return values.Count > 0;
            }

            if (value is IEnumerable<double> doubleValues)
            {
                values = doubleValues.ToList();
                return values.Count > 0;
            }

            if (value is IEnumerable<float> floatValues)
            {
                values = floatValues.Select(static v => (double)v).ToList();
                return values.Count > 0;
            }

            return false;
        }

        private static bool TryGetSingleNumber(object value, out double number)
        {
            number = 0;
            if (value == null)
            {
                return false;
            }

            if (value is JValue jValue)
            {
                if (jValue.Type == JTokenType.Integer || jValue.Type == JTokenType.Float)
                {
                    number = jValue.Value<double>();
                    return true;
                }

                return false;
            }

            switch (value)
            {
                case int i:
                    number = i;
                    return true;
                case long l:
                    number = l;
                    return true;
                case float f:
                    number = f;
                    return true;
                case double d:
                    number = d;
                    return true;
                default:
                    return false;
            }
        }

        private static NumericValueRefDto ConvertJTokenValue(JToken token, ConvertResult result)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return new NumericValueRefDto { Kind = "Const", ConstValue = 0 };
            }

            if (token.Type == JTokenType.String)
            {
                return ConvertStringValue(token.Value<string>());
            }

            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                return new NumericValueRefDto { Kind = "Const", ConstValue = token.Value<double>() };
            }

            if (token.Type == JTokenType.Boolean)
            {
                return new NumericValueRefDto { Kind = "Const", ConstValue = token.Value<bool>() ? 1 : 0 };
            }

            if (token is JObject obj)
            {
                return ConvertObjectValue(obj, result);
            }

            return ConvertStringValue(token.ToString(Formatting.None));
        }

        private static NumericValueRefDto ConvertObjectValue(JObject obj, ConvertResult result)
        {
            if (obj == null)
            {
                return new NumericValueRefDto { Kind = "Const", ConstValue = 0 };
            }

            var kind = ReadString(obj, "Kind") ?? ReadString(obj, "kind");
            if (string.IsNullOrEmpty(kind))
            {
                result?.AddWarning("Value object without Kind was converted to Const(0)");
                return new NumericValueRefDto { Kind = "Const", ConstValue = 0 };
            }

            var dto = new NumericValueRefDto { Kind = kind };
            dto.ConstValue = ReadDouble(obj, "ConstValue", ReadDouble(obj, "constValue", 0));
            dto.BoardId = ReadInt(obj, "BoardId", ReadInt(obj, "boardId", 0));
            dto.KeyId = ReadInt(obj, "KeyId", ReadInt(obj, "keyId", 0));
            dto.FieldId = ReadInt(obj, "FieldId", ReadInt(obj, "fieldId", 0));
            dto.DomainId = ReadString(obj, "DomainId") ?? ReadString(obj, "domainId");
            dto.Key = ReadString(obj, "Key") ?? ReadString(obj, "key");
            dto.ExprText = ReadString(obj, "ExprText") ?? ReadString(obj, "exprText");
            return dto;
        }

        private static string ReadString(JObject obj, string key)
        {
            return obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var value) ? value.Value<string>() : null;
        }

        private static int ReadInt(JObject obj, string key, int defaultValue)
        {
            return obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var value) && value.Type != JTokenType.Null ? value.Value<int>() : defaultValue;
        }

        private static double ReadDouble(JObject obj, string key, double defaultValue)
        {
            return obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var value) && value.Type != JTokenType.Null ? value.Value<double>() : defaultValue;
        }

        /// <summary>
        /// 转换字符串值为 NumericValueRefDto
        /// </summary>
        private static NumericValueRefDto ConvertStringValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return new NumericValueRefDto { Kind = "Const", ConstValue = 0 };
            }

            // 模板参数引用
            if (value.StartsWith("@"))
            {
                return new NumericValueRefDto { Kind = "TemplateParam", Key = value.Substring(1) };
            }

            // 黑板引用：bb:actor.hp / blackboard:actor.hp
            if (TryCreateBlackboardRef(value, out var blackboardRef))
            {
                return blackboardRef;
            }

            // 实体引用
            if (value.StartsWith("$"))
            {
                return CreateEntityRef(value);
            }

            // 表达式
            if (value.StartsWith("=") || value.Contains("."))
            {
                return new NumericValueRefDto { Kind = "Expr", ExprText = value };
            }

            // 尝试解析为数字
            if (double.TryParse(value, out var dValue))
            {
                return new NumericValueRefDto { Kind = "Const", ConstValue = dValue };
            }

            // 默认作为常量字符串（会存入字符串表）
            return new NumericValueRefDto { Kind = "Const", ConstValue = 0 };
        }

        private static bool TryCreateBlackboardRef(string value, out NumericValueRefDto dto)
        {
            dto = null;
            if (string.IsNullOrEmpty(value)) return false;

            const string shortPrefix = "bb:";
            const string longPrefix = "blackboard:";
            string path;
            if (value.StartsWith(shortPrefix, StringComparison.OrdinalIgnoreCase))
            {
                path = value.Substring(shortPrefix.Length);
            }
            else if (value.StartsWith(longPrefix, StringComparison.OrdinalIgnoreCase))
            {
                path = value.Substring(longPrefix.Length);
            }
            else
            {
                return false;
            }

            var split = path.IndexOf('.');
            if (split <= 0 || split >= path.Length - 1)
            {
                dto = new NumericValueRefDto { Kind = "Const", ConstValue = 0 };
                return true;
            }

            var boardName = path.Substring(0, split);
            var keyName = path.Substring(split + 1);
            dto = new NumericValueRefDto
            {
                Kind = "Blackboard",
                BoardId = BlackboardIdMapper.BoardId(boardName),
                KeyId = BlackboardIdMapper.KeyId($"{boardName}.{keyName}")
            };
            return true;
        }

        /// <summary>
        /// 创建实体引用
        /// </summary>
        private static NumericValueRefDto CreateEntityRef(string entityRef)
        {
            if (string.IsNullOrEmpty(entityRef)) return new NumericValueRefDto { Kind = "Const", ConstValue = 0 };

            // 常见上下文变量映射
            switch (entityRef.ToLower())
            {
                case "$caster":
                    return new NumericValueRefDto { Kind = "Const", ConstValue = 1 }; // payload:caster
                case "$target":
                    return new NumericValueRefDto { Kind = "Const", ConstValue = 2 }; // payload:target
                case "$self":
                    return new NumericValueRefDto { Kind = "Const", ConstValue = 3 }; // payload:self
                default:
                    // 自定义变量
                    var varId = StableStringId.Get("var:" + entityRef);
                    return new NumericValueRefDto { Kind = "Var", DomainId = "context", Key = entityRef };
            }
        }

        /// <summary>
        /// 创建 Payload 字段引用
        /// </summary>
        private static NumericValueRefDto CreatePayloadRef(string fieldName)
        {
            var fieldId = StableStringId.Get("payload:" + fieldName);
            return new NumericValueRefDto { Kind = "PayloadField", FieldId = fieldId };
        }

        /// <summary>
        /// 创建常量引用
        /// </summary>
        private static NumericValueRefDto CreateConstRef(object value)
        {
            return ConvertValue(null, value, null, null);
        }

        /// <summary>
        /// 获取阶段值
        /// </summary>
        private static int GetPhaseValue(string phase)
        {
            if (string.IsNullOrEmpty(phase)) return 0;

            switch (phase.ToLower())
            {
                case "immediate": return 0;
                case "early": return 1;
                case "late": return 2;
                default:
                    if (int.TryParse(phase, out var v)) return v;
                    return 0;
            }
        }
    }
}
#endif
