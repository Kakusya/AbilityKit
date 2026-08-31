using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Eventing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static AbilityKit.Triggering.Runtime.Plan.Json.TriggerPlanSourceJsonUtility;
using static AbilityKit.Triggering.Runtime.Plan.Json.TriggerPlanSourceValueWriter;

namespace AbilityKit.Triggering.Runtime.Plan.Json
{
    internal sealed class TriggerPlanSourceConditionWriter
    {
        public delegate void ParamValueWriter(JsonTextWriter writer, JToken value);

        public void WritePredicate(JsonTextWriter writer, List<JObject> conditions, ParamValueWriter writeParamValue)
        {
            if (writeParamValue == null)
            {
                throw new ArgumentNullException(nameof(writeParamValue));
            }

            writer.WriteStartObject();

            if (conditions == null || conditions.Count == 0)
            {
                writer.WritePropertyName("Kind");
                writer.WriteValue("none");
                writer.WritePropertyName("Nodes");
                writer.WriteNull();
            }
            else
            {
                writer.WritePropertyName("Kind");
                writer.WriteValue("expr");
                writer.WritePropertyName("Nodes");
                writer.WriteStartArray();

                WriteConditionList(writer, conditions, "And", writeParamValue);

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        private static void WriteConditionList(JsonTextWriter writer, IReadOnlyList<JObject> conditions, string logicalOp, ParamValueWriter writeParamValue)
        {
            if (conditions == null || conditions.Count == 0)
            {
                throw new InvalidOperationException($"Condition group '{logicalOp}' must contain at least one condition.");
            }

            for (var i = 0; i < conditions.Count; i++)
            {
                WriteCondition(writer, conditions[i], writeParamValue);
                if (i > 0)
                {
                    WriteBoolOperator(writer, logicalOp);
                }
            }
        }

        private static void WriteCondition(JsonTextWriter writer, JObject cond, ParamValueWriter writeParamValue)
        {
            if (cond == null)
            {
                throw new InvalidOperationException("Condition item cannot be null.");
            }

            var type = cond["type"]?.ToString();
            if (string.IsNullOrEmpty(type))
            {
                throw new InvalidOperationException("Condition type is required.");
            }

            switch (type)
            {
                case "all":
                case "and":
                    WriteConditionList(writer, ReadConditionItems(cond), "And", writeParamValue);
                    break;

                case "any":
                case "or":
                    WriteConditionList(writer, ReadConditionItems(cond), "Or", writeParamValue);
                    break;

                case "not":
                    WriteConditionList(writer, ReadConditionItems(cond), "And", writeParamValue);
                    WriteBoolOperator(writer, "Not");
                    break;

                case "compare":
                case "compare_numeric":
                    WriteCompareNode(writer, cond, cond["op"]?.ToString() ?? cond["compare_op"]?.ToString() ?? cond["compareOp"]?.ToString(), writeParamValue);
                    break;

                case "arg_eq":
                    WriteCompareNode(writer, cond, "Equal", writeParamValue);
                    break;

                case "arg_gt":
                    WriteCompareNode(writer, cond, "GreaterThan", writeParamValue);
                    break;

                case "arg_gte":
                    WriteCompareNode(writer, cond, "GreaterThanOrEqual", writeParamValue);
                    break;

                case "arg_lt":
                    WriteCompareNode(writer, cond, "LessThan", writeParamValue);
                    break;

                case "arg_lte":
                    WriteCompareNode(writer, cond, "LessThanOrEqual", writeParamValue);
                    break;

                case "arg_neq":
                    WriteCompareNode(writer, cond, "NotEqual", writeParamValue);
                    break;

                case "health_percent":
                    WriteHealthPercentNode(writer, cond);
                    break;

                case "has_buff":
                    WriteHasBuffNode(writer, cond);
                    break;

                case "owner_matches_payload_source":
                    WriteOwnerMatchesPayloadNode(writer, "predicate:owner_matches_payload_source");
                    break;

                case "owner_matches_payload_target":
                    WriteOwnerMatchesPayloadNode(writer, "predicate:owner_matches_payload_target");
                    break;

                case "target_is_flying_projectile":
                    WriteOwnerMatchesPayloadNode(writer, "predicate:target_is_flying_projectile");
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported condition type: {type}");
            }
        }

        public static List<JObject> ReadConditionItems(JObject cond)
        {
            var items = new List<JObject>();
            var inner = cond["items"] ?? cond["item"] ?? cond["conditions"] ?? cond["condition"];
            if (inner is JArray arr)
            {
                foreach (var item in arr)
                {
                    if (item is JObject obj)
                    {
                        items.Add(obj);
                    }
                }
            }
            else if (inner is JObject obj)
            {
                items.Add(obj);
            }

            return items;
        }

        private static void WriteBoolOperator(JsonTextWriter writer, string kind)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("Kind");
            writer.WriteValue(kind);
            writer.WriteEndObject();
        }

        private static void WriteCompareNode(JsonTextWriter writer, JObject cond, string op, ParamValueWriter writeParamValue)
        {
            var argName = cond["arg_name"]?.ToString();
            var leftVarDomain = (cond["left_var_domain"] ?? cond["var_domain"])?.ToString();
            var leftVarKey = (cond["left_var_key"] ?? cond["var_key"])?.ToString();

            writer.WriteStartObject();
            writer.WritePropertyName("Kind");
            writer.WriteValue("CompareNumeric");
            writer.WritePropertyName("CompareOp");
            writer.WriteValue(NormalizeCompareOp(op));
            writer.WritePropertyName("Left");
            if (cond["left"] != null)
            {
                writeParamValue(writer, cond["left"]);
            }
            else if (!string.IsNullOrEmpty(leftVarDomain) && !string.IsNullOrEmpty(leftVarKey))
            {
                WriteVarValue(writer, leftVarDomain, leftVarKey);
            }
            else
            {
                WritePayloadFieldValue(writer, argName);
            }

            writer.WritePropertyName("Right");
            writeParamValue(writer, cond["right"] ?? cond["value"]);
            writer.WriteEndObject();
        }

        private static void WriteHealthPercentNode(JsonTextWriter writer, JObject cond)
        {
            var threshold = ReadFloat(cond, 50f, "threshold", "value");
            var compareType = ReadInt(cond, 0, "compare_type", "compareType");
            var compareOp = compareType switch
            {
                0 => "LessThan",
                1 => "GreaterThan",
                _ => throw new InvalidOperationException($"Unsupported health_percent compare_type: {compareType}")
            };

            writer.WriteStartObject();
            writer.WritePropertyName("Kind");
            writer.WriteValue("CompareNumeric");
            writer.WritePropertyName("CompareOp");
            writer.WriteValue(compareOp);
            writer.WritePropertyName("Left");
            WritePayloadFieldValue(writer, "target_hp");
            writer.WritePropertyName("Right");
            WriteScaledPayloadFieldValue(writer, "target_max_hp", threshold / 100d);
            writer.WriteEndObject();
        }

        private static void WriteHasBuffNode(JsonTextWriter writer, JObject cond)
        {
            var buffId = ReadInt(cond, 0, "buff_id", "buffId");
            if (buffId <= 0)
            {
                throw new InvalidOperationException("has_buff condition requires a positive buff_id.");
            }

            var checkStack = ReadInt(cond, 0, "check_stack", "checkStack");
            var targetMode = ReadInt(cond, 0, "target_mode", "targetMode");
            var functionKey = targetMode == 1 ? "predicate:has_buff_owner" : "predicate:has_buff";

            writer.WriteStartObject();
            writer.WritePropertyName("Kind");
            writer.WriteValue("Function");
            writer.WritePropertyName("FunctionId");
            writer.WriteValue(StableStringId.Get(functionKey));
            writer.WritePropertyName("FunctionArity");
            writer.WriteValue(2);
            writer.WritePropertyName("Left");
            WriteConstValue(writer, buffId);
            writer.WritePropertyName("Right");
            WriteConstValue(writer, checkStack > 0 ? 1d : 0d);
            writer.WriteEndObject();
        }

        private static void WriteOwnerMatchesPayloadNode(JsonTextWriter writer, string functionKey)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("Kind");
            writer.WriteValue("Function");
            writer.WritePropertyName("FunctionId");
            writer.WriteValue(StableStringId.Get(functionKey));
            writer.WritePropertyName("FunctionArity");
            writer.WriteValue(2);
            writer.WritePropertyName("Left");
            WriteConstValue(writer, 0d);
            writer.WritePropertyName("Right");
            WriteConstValue(writer, 0d);
            writer.WriteEndObject();
        }

        private static void WriteScaledPayloadFieldValue(JsonTextWriter writer, string payloadField, double scale)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("Kind");
            writer.WriteValue("PayloadField");
            writer.WritePropertyName("FieldId");
            writer.WriteValue(string.IsNullOrEmpty(payloadField) ? 0 : StableStringId.Get("payload:" + payloadField));
            writer.WritePropertyName("HasScale");
            writer.WriteValue(true);
            writer.WritePropertyName("Scale");
            writer.WriteValue(scale);
            writer.WriteEndObject();
        }
    }
}
