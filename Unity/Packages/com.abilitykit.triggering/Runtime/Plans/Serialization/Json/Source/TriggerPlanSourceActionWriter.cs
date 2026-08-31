using System;
using System.Collections.Generic;
using System.Globalization;
using AbilityKit.Triggering.Eventing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AbilityKit.Triggering.Runtime.Plan.Json
{
    /// <summary>
    /// Writes source-format action definitions and action argument value tokens into runtime JSON.
    /// </summary>
    internal sealed class TriggerPlanSourceActionWriter
    {
        private readonly Dictionary<int, string> _strings;

        public TriggerPlanSourceActionWriter(Dictionary<int, string> strings = null)
        {
            _strings = strings;
        }

        public void WriteAction(
            JsonTextWriter writer,
            JObject action,
            Dictionary<string, ActionSourceDefJson> actionSchemas,
            Dictionary<string, JToken> actionCatalog,
            Func<JObject, Dictionary<string, JToken>, HashSet<string>, JObject> resolveAction)
        {
            if (resolveAction == null) throw new ArgumentNullException(nameof(resolveAction));

            action = resolveAction(action, actionCatalog, new HashSet<string>(StringComparer.OrdinalIgnoreCase)) ?? action;
            var type = action["type"]?.ToString();
            if (string.IsNullOrEmpty(type)) return;

            var actionId = StableStringId.Get("action:" + type);
            var orderedArgs = BuildOrderedActionArgs(action, type, actionSchemas);

            writer.WriteStartObject();
            writer.WritePropertyName("ActionId");
            writer.WriteValue(actionId);

            var positionalCount = 0;
            for (var i = 0; i < orderedArgs.Count && positionalCount < 2; i++)
            {
                if (IsScalarValue(orderedArgs[i].Value))
                {
                    positionalCount++;
                }
            }

            writer.WritePropertyName("Arity");
            writer.WriteValue(positionalCount);

            var writtenPositionalCount = 0;
            for (var i = 0; i < orderedArgs.Count && writtenPositionalCount < 2; i++)
            {
                if (!IsScalarValue(orderedArgs[i].Value))
                {
                    continue;
                }

                writer.WritePropertyName(writtenPositionalCount == 0 ? "Arg0" : "Arg1");
                WriteParamValue(writer, orderedArgs[i].Name, orderedArgs[i].Value);
                writtenPositionalCount++;
            }

            if (orderedArgs.Count > 0)
            {
                writer.WritePropertyName("Args");
                writer.WriteStartObject();
                for (int i = 0; i < orderedArgs.Count; i++)
                {
                    WriteNamedArg(writer, orderedArgs[i].Name, orderedArgs[i].Value);
                }
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        public void WriteParamValue(JsonTextWriter writer, JToken value)
        {
            WriteParamValue(writer, null, value);
        }

        private void WriteParamValue(JsonTextWriter writer, string argName, JToken value)
        {
            if (value == null || value.Type == JTokenType.Null)
            {
                throw new InvalidOperationException("Action parameter value cannot be null.");
            }

            switch (value.Type)
            {
                case JTokenType.Integer:
                case JTokenType.Float:
                    TriggerPlanSourceValueWriter.WriteConstValue(writer, value.Value<double>());
                    break;

                case JTokenType.String:
                    WriteStringValue(writer, argName, value.ToString());
                    break;

                case JTokenType.Boolean:
                    TriggerPlanSourceValueWriter.WriteConstValue(writer, value.Value<bool>() ? 1.0 : 0.0);
                    break;

                case JTokenType.Object:
                    WriteObjectValue(writer, value as JObject);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported action parameter token type: {value.Type}");
            }
        }

        private void WriteNamedArg(JsonTextWriter writer, string argName, JToken value)
        {
            if (value == null || value.Type == JTokenType.Null)
            {
                return;
            }

            if (value.Type == JTokenType.Array)
            {
                WriteArrayArgs(writer, argName, value as JArray);
                return;
            }

            var runtimeArgName = NormalizeRuntimeArgName(argName);
            writer.WritePropertyName(runtimeArgName);
            WriteParamValue(writer, runtimeArgName, value);
        }

        private void WriteArrayArgs(JsonTextWriter writer, string argName, JArray values)
        {
            if (values == null)
            {
                return;
            }

            for (var i = 0; i < values.Count; i++)
            {
                var item = values[i];
                if (item == null || item.Type == JTokenType.Null)
                {
                    continue;
                }

                writer.WritePropertyName(i == 0 ? argName : argName + i);
                WriteParamValue(writer, argName, item);
            }
        }

        private static bool IsScalarValue(JToken value)
        {
            return value != null && value.Type != JTokenType.Array;
        }

        private static string NormalizeRuntimeArgName(string argName)
        {
            return IsTargetPayloadFieldArg(argName) ? "target_payload_field_id" : argName;
        }

        private static List<ActionArgSource> BuildOrderedActionArgs(JObject action, string type, Dictionary<string, ActionSourceDefJson> actionSchemas)
        {
            var result = new List<ActionArgSource>();
            var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var schema = FindActionSchema(type, actionSchemas);

            if (schema?.@params != null)
            {
                foreach (var param in schema.@params)
                {
                    if (param == null || string.IsNullOrEmpty(param.name)) continue;

                    if (action.TryGetValue(param.name, StringComparison.OrdinalIgnoreCase, out var value))
                    {
                        result.Add(new ActionArgSource(param.name, value));
                        consumed.Add(param.name);
                        continue;
                    }

                    if (!param.required && param.defaultValue != null)
                    {
                        result.Add(new ActionArgSource(param.name, JToken.FromObject(param.defaultValue)));
                        consumed.Add(param.name);
                        continue;
                    }

                    if (param.required)
                    {
                        throw new InvalidOperationException($"Required action parameter is missing. action={type} param={param.name}");
                    }
                }
            }

            foreach (var prop in action.Properties())
            {
                if (string.Equals(prop.Name, "type", StringComparison.OrdinalIgnoreCase)) continue;
                if (IsExecutionNodeControlProperty(prop.Name)) continue;
                if (consumed.Contains(prop.Name)) continue;

                result.Add(new ActionArgSource(prop.Name, prop.Value));
            }

            return result;
        }

        private static bool IsExecutionNodeControlProperty(string name)
        {
            return string.Equals(name, "kind", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "action", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "children", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "behaviorRefs", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "behavior_refs", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "items", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "child", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "then", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "else", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "elseChildren", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "condition", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "conditions", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "when", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "until", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "untilCondition", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "until_condition", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "count", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "times", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "repeatCount", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "repeat_count", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "maxIterations", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "max_iterations", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "limit", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "reason", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "behaviorRef", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "behaviorId", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "behavior", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "ref", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "weight", StringComparison.OrdinalIgnoreCase);
        }

        private static ActionSourceDefJson FindActionSchema(string type, Dictionary<string, ActionSourceDefJson> actionSchemas)
        {
            if (string.IsNullOrEmpty(type) || actionSchemas == null)
            {
                return null;
            }

            if (actionSchemas.TryGetValue(type, out var schema))
            {
                return schema;
            }

            foreach (var kvp in actionSchemas)
            {
                if (string.Equals(kvp.Value?.type, type, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            return null;
        }

        private void WriteObjectValue(JsonTextWriter writer, JObject value)
        {
            if (value == null)
            {
                TriggerPlanSourceValueWriter.WriteConstValue(writer, 0);
                return;
            }

            var kind = TriggerPlanSourceJsonUtility.ReadString(value, "kind", "type");
            var payload = TriggerPlanSourceJsonUtility.ReadString(value, "payload", "payload_field", "payloadField", "field");
            if (!string.IsNullOrEmpty(payload) || TriggerPlanSourceJsonUtility.IsKind(kind, "payload", "payload_field", "payloadField"))
            {
                TriggerPlanSourceValueWriter.WritePayloadFieldValue(writer, payload ?? TriggerPlanSourceJsonUtility.ReadString(value, "name", "key"));
                return;
            }

            var constToken = value["const"] ?? value["value"] ?? value["constValue"] ?? value["ConstValue"];
            if (constToken != null && constToken.Type != JTokenType.Object && constToken.Type != JTokenType.Array)
            {
                WriteParamValue(writer, constToken);
                return;
            }

            var varDomain = TriggerPlanSourceJsonUtility.ReadString(value, "var_domain", "varDomain", "domain", "domainId", "DomainId");
            var varKey = TriggerPlanSourceJsonUtility.ReadString(value, "var_key", "varKey", "key", "Key");
            if (!string.IsNullOrEmpty(varDomain) && !string.IsNullOrEmpty(varKey) || TriggerPlanSourceJsonUtility.IsKind(kind, "var", "variable"))
            {
                TriggerPlanSourceValueWriter.WriteVarValue(writer, string.IsNullOrEmpty(varDomain) ? "trigger" : varDomain, varKey);
                return;
            }

            var expr = TriggerPlanSourceJsonUtility.ReadString(value, "expr", "expression", "exprText", "ExprText");
            if (!string.IsNullOrEmpty(expr) || TriggerPlanSourceJsonUtility.IsKind(kind, "expr", "expression"))
            {
                TriggerPlanSourceValueWriter.WriteExprValue(writer, expr ?? string.Empty);
                return;
            }

            throw new InvalidOperationException("Unsupported action parameter value object. Expected const/value, payload, var or expr.");
        }

        private void WriteStringValue(JsonTextWriter writer, string argName, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                TriggerPlanSourceValueWriter.WriteConstValue(writer, 0);
                return;
            }

            if (IsTargetPayloadFieldArg(argName))
            {
                var payloadField = value.StartsWith("payload:", StringComparison.OrdinalIgnoreCase)
                    ? value.Substring("payload:".Length)
                    : value.TrimStart('@');
                TriggerPlanSourceValueWriter.WriteConstValue(writer, StableStringId.Get("payload:" + payloadField));
                return;
            }

            if (value.StartsWith("payload:", StringComparison.OrdinalIgnoreCase))
            {
                TriggerPlanSourceValueWriter.WritePayloadFieldValue(writer, value.Substring("payload:".Length));
                return;
            }

            if (value.StartsWith("@"))
            {
                TriggerPlanSourceValueWriter.WritePayloadFieldValue(writer, value.Substring(1));
                return;
            }

            if (value.StartsWith("$"))
            {
                TriggerPlanSourceValueWriter.WriteVarValue(writer, "trigger", value.TrimStart('$'));
                return;
            }

            if (value.StartsWith("="))
            {
                TriggerPlanSourceValueWriter.WriteExprValue(writer, value.Substring(1).Trim());
                return;
            }

            if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var invariantNumValue) ||
                double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out invariantNumValue))
            {
                TriggerPlanSourceValueWriter.WriteConstValue(writer, invariantNumValue);
                return;
            }

            if (value.StartsWith("%"))
            {
                TriggerPlanSourceValueWriter.WriteVarValue(writer, "trigger", value.TrimStart('%'));
                return;
            }

            if (TryMapLegacyEnumLiteral(argName, value, out var enumValue))
            {
                TriggerPlanSourceValueWriter.WriteConstValue(writer, enumValue);
                return;
            }

            if (CanEncodeAsStringId(argName))
            {
                var stringId = StableStringId.Get("str:" + value);
                if (_strings != null)
                {
                    _strings[stringId] = value;
                }

                TriggerPlanSourceValueWriter.WriteConstValue(writer, stringId);
                return;
            }

            throw new InvalidOperationException($"Unsupported action parameter string value: {value}");
        }

        private static bool IsTargetPayloadFieldArg(string argName)
        {
            return string.Equals(argName, "target_payload_field", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(argName, "targetPayloadField", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(argName, "target_payload_field_id", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(argName, "targetPayloadFieldId", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(argName, "target_actor_payload_field", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(argName, "targetActorPayloadField", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(argName, "target_actor_payload_field_id", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(argName, "targetActorPayloadFieldId", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryMapLegacyEnumLiteral(string argName, string value, out int enumValue)
        {
            enumValue = 0;
            if (string.IsNullOrEmpty(argName) || string.IsNullOrEmpty(value))
            {
                return false;
            }

            if (string.Equals(argName, "targetMode", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argName, "target_mode", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(value, "Explicit", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "ExplicitTarget", StringComparison.OrdinalIgnoreCase))
                {
                    enumValue = 0;
                    return true;
                }
            }

            if (string.Equals(argName, "directionMode", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argName, "direction_mode", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(value, "FromAreaCenterToTarget", StringComparison.OrdinalIgnoreCase))
                {
                    enumValue = 2;
                    return true;
                }
            }

            if (string.Equals(argName, "directionSource", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argName, "direction_source", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(value, "cast_context", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "CastContext", StringComparison.OrdinalIgnoreCase))
                {
                    enumValue = 0;
                    return true;
                }

                if (string.Equals(value, "target_query", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "TargetQuery", StringComparison.OrdinalIgnoreCase))
                {
                    enumValue = 1;
                    return true;
                }
            }

            if (string.Equals(argName, "damageType", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argName, "damage_type", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
                {
                    enumValue = 0;
                    return true;
                }

                if (string.Equals(value, "physical", StringComparison.OrdinalIgnoreCase))
                {
                    enumValue = 1;
                    return true;
                }

                if (string.Equals(value, "magic", StringComparison.OrdinalIgnoreCase))
                {
                    enumValue = 2;
                    return true;
                }

                if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "true_damage", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "trueDamage", StringComparison.OrdinalIgnoreCase))
                {
                    enumValue = 4;
                    return true;
                }
            }

            if (string.Equals(argName, "formulaType", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argName, "formula_type", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
                {
                    enumValue = 0;
                    return true;
                }

                if (string.Equals(value, "fixed", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "standard", StringComparison.OrdinalIgnoreCase))
                {
                    enumValue = 1;
                    return true;
                }
            }

            return false;
        }

        private static bool CanEncodeAsStringId(string argName)
        {
            if (string.IsNullOrEmpty(argName))
            {
                return false;
            }

            return string.Equals(argName, "message", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(argName, "msg", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(argName, "msg_id", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(argName, "reason", StringComparison.OrdinalIgnoreCase);
        }
    }
}
