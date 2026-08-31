using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AbilityKit.Triggering.Runtime.Plan.Json
{
    internal sealed class TriggerPlanJsonParser
    {
        private readonly TriggerPlanSourceConverter _sourceConverter;

        public TriggerPlanJsonParser()
            : this(new TriggerPlanSourceConverter())
        {
        }

        public TriggerPlanJsonParser(TriggerPlanSourceConverter sourceConverter)
        {
            _sourceConverter = sourceConverter ?? throw new ArgumentNullException(nameof(sourceConverter));
        }

        public TriggerPlanJsonParseResult Parse(string json, string sourceName = null, TriggerPlanJsonParseOptions options = null)
        {
            options = options ?? TriggerPlanJsonParseOptions.Default;
            var diagnostics = new List<TriggerPlanJsonDiagnostic>();

            if (string.IsNullOrEmpty(json))
            {
                diagnostics.Add(Error("Trigger plan json is empty", sourceName));
                return new TriggerPlanJsonParseResult(false, TriggerPlanJsonFormat.Unknown, null, diagnostics);
            }

            try
            {
                var normalizedJson = NormalizeRootJson(json, sourceName, diagnostics);
                var root = JObject.Parse(normalizedJson);
                var format = DetectFormat(root, options, sourceName, diagnostics);

                if (format == TriggerPlanJsonFormat.Source)
                {
                    var runtimeJson = _sourceConverter.ConvertSourceToRuntimeJson(normalizedJson);
                    var dto = JsonConvert.DeserializeObject<TriggerPlanJsonDatabase.TriggerPlanDatabaseDto>(runtimeJson);
                    return BuildResult(format, dto, diagnostics, options);
                }

                if (format == TriggerPlanJsonFormat.Runtime)
                {
                    var dto = JsonConvert.DeserializeObject<TriggerPlanJsonDatabase.TriggerPlanDatabaseDto>(normalizedJson);
                    return BuildResult(format, dto, diagnostics, options);
                }

                diagnostics.Add(Error("Unknown trigger plan json format", sourceName));
                return new TriggerPlanJsonParseResult(false, format, null, diagnostics);
            }
            catch (Exception ex)
            {
                diagnostics.Add(Error(ex.Message, sourceName, exception: ex));
                return new TriggerPlanJsonParseResult(false, TriggerPlanJsonFormat.Unknown, null, diagnostics);
            }
        }

        public TriggerPlanJsonFormat DetectFormat(string json, TriggerPlanJsonParseOptions options = null)
        {
            if (string.IsNullOrEmpty(json)) return TriggerPlanJsonFormat.Unknown;
            var diagnostics = new List<TriggerPlanJsonDiagnostic>();
            var normalizedJson = NormalizeRootJson(json, null, diagnostics);
            return DetectFormat(JObject.Parse(normalizedJson), options ?? TriggerPlanJsonParseOptions.Default, null, diagnostics);
        }

        private static string NormalizeRootJson(string json, string sourceName, List<TriggerPlanJsonDiagnostic> diagnostics)
        {
            var token = JToken.Parse(json);
            if (token is JObject)
            {
                return json;
            }

            if (token is JArray)
            {
                diagnostics?.Add(Warning("Source trigger list detected by array root", sourceName));
                var root = new JObject
                {
                    ["triggers"] = token
                };
                return root.ToString(Formatting.None);
            }

            return json;
        }

        private static TriggerPlanJsonParseResult BuildResult(
            TriggerPlanJsonFormat format,
            TriggerPlanJsonDatabase.TriggerPlanDatabaseDto dto,
            List<TriggerPlanJsonDiagnostic> diagnostics,
            TriggerPlanJsonParseOptions options)
        {
            if (dto == null)
            {
                diagnostics.Add(Error("Parsed trigger plan dto is null"));
                return new TriggerPlanJsonParseResult(false, format, null, diagnostics);
            }

            var success = true;
            if (options.TreatWarningsAsErrors)
            {
                for (int i = 0; i < diagnostics.Count; i++)
                {
                    if (diagnostics[i].Severity == TriggerPlanJsonDiagnosticSeverity.Warning)
                    {
                        success = false;
                        break;
                    }
                }
            }

            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity == TriggerPlanJsonDiagnosticSeverity.Error)
                {
                    success = false;
                    break;
                }
            }

            return new TriggerPlanJsonParseResult(success, format, dto, diagnostics);
        }

        private static TriggerPlanJsonFormat DetectFormat(
            JObject root,
            TriggerPlanJsonParseOptions options,
            string sourceName,
            List<TriggerPlanJsonDiagnostic> diagnostics)
        {
            if (root == null) return TriggerPlanJsonFormat.Unknown;

            var explicitFormat = ReadString(root, "format", "kind", "schema", "$schema");
            if (!string.IsNullOrEmpty(explicitFormat))
            {
                if (explicitFormat.IndexOf("source", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return TriggerPlanJsonFormat.Source;
                }

                if (explicitFormat.IndexOf("runtime", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    explicitFormat.IndexOf("plan", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return TriggerPlanJsonFormat.Runtime;
                }
            }

            if (HasProperty(root, "Triggers", StringComparison.Ordinal))
            {
                return TriggerPlanJsonFormat.Runtime;
            }

            if (HasSourceFormatHints(root))
            {
                if (options.RequireExplicitSourceFormat)
                {
                    diagnostics?.Add(Error("Source format requires explicit format/schema marker", sourceName));
                    return TriggerPlanJsonFormat.Unknown;
                }

                diagnostics?.Add(Warning("Source format detected by legacy field hints", sourceName));
                return TriggerPlanJsonFormat.Source;
            }

            return TriggerPlanJsonFormat.Unknown;
        }

        private static bool HasSourceFormatHints(JObject root)
        {
            return HasProperty(root, "triggers", StringComparison.Ordinal) ||
                   HasProperty(root, "actions", StringComparison.OrdinalIgnoreCase) ||
                   HasProperty(root, "conditions", StringComparison.OrdinalIgnoreCase) ||
                   HasProperty(root, "version", StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadString(JObject root, params string[] names)
        {
            if (root == null || names == null) return null;
            for (int i = 0; i < names.Length; i++)
            {
                if (root.TryGetValue(names[i], StringComparison.OrdinalIgnoreCase, out var token))
                {
                    return token?.ToString();
                }
            }

            return null;
        }

        private static bool HasProperty(JObject root, string name, StringComparison comparison)
        {
            foreach (var property in root.Properties())
            {
                if (string.Equals(property.Name, name, comparison))
                {
                    return true;
                }
            }

            return false;
        }

        private static TriggerPlanJsonDiagnostic Error(string message, string sourceName = null, string path = null, Exception exception = null)
        {
            return new TriggerPlanJsonDiagnostic(TriggerPlanJsonDiagnosticSeverity.Error, message, sourceName, path, exception);
        }

        private static TriggerPlanJsonDiagnostic Warning(string message, string sourceName = null, string path = null)
        {
            return new TriggerPlanJsonDiagnostic(TriggerPlanJsonDiagnosticSeverity.Warning, message, sourceName, path);
        }
    }
}
