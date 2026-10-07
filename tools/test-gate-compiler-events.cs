using System.Collections;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

internal static class Program
{
    private const string CapturePrefix = "ABILITYKIT_CAPTURE|";
    private static readonly HashSet<string> CompilerFileParameters = new(StringComparer.Ordinal)
    {
        "Sources",
        "References",
        "Analyzers",
        "AdditionalFiles",
        "AnalyzerConfigFiles",
        "EmbeddedFiles",
        "Resources",
        "LinkResources"
    };

    public static int Main(string[] args)
    {
        if (args.Length != 5 || File.Exists(args[1]))
        {
            Console.Error.WriteLine("Usage: reader <binlog> <new-manifest> <run-id> <result-id> <invocation-id>");
            return 2;
        }

        var state = new ReplayState(args[2], args[3], args[4]);
        try
        {
            var source = new BinaryLogReplayEventSource();
            source.AnyEventRaised += state.OnEvent;
            source.Replay(args[0]);
        }
        catch (Exception exception)
        {
            state.Errors.Add("ReplayFailure:" + exception.GetType().Name);
        }

        state.Finish();
        var json = JsonSerializer.Serialize(state.Manifest(), new JsonSerializerOptions { WriteIndented = false });
        var temporary = args[1] + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, json, new UTF8Encoding(false));
        File.Move(temporary, args[1]);
        return state.Errors.Count == 0 ? 0 : 1;
    }

    private sealed class ReplayState
    {
        private readonly string runId;
        private readonly string resultId;
        private readonly string invocationId;
        private readonly Dictionary<string, ProjectRecord> projects = new(StringComparer.Ordinal);
        private readonly Dictionary<string, TargetRecord> activeTargets = new(StringComparer.Ordinal);
        private readonly HashSet<string> skippedTargets = new(StringComparer.Ordinal);
        private readonly List<TargetRecord> targets = new();
        private readonly Dictionary<string, TaskRecord> activeTasks = new(StringComparer.Ordinal);
        private readonly List<TaskRecord> tasks = new();
        private readonly Dictionary<string, List<InputRecord>> taskInputs = new(StringComparer.Ordinal);
        private readonly List<CaptureRecord> captures = new();
        private int buildStarted;
        private int buildFinished;

        public ReplayState(string runId, string resultId, string invocationId)
        {
            this.runId = runId;
            this.resultId = resultId;
            this.invocationId = invocationId;
        }

        public List<string> Errors { get; } = new();

        public void OnEvent(object? sender, BuildEventArgs e)
        {
            switch (e)
            {
                case BuildStartedEventArgs:
                    buildStarted++;
                    break;
                case BuildFinishedEventArgs finished:
                    buildFinished++;
                    if (!finished.Succeeded)
                    {
                        Errors.Add("BuildFinishedUnsuccessful");
                    }
                    break;
                case ProjectStartedEventArgs started:
                    AddProject(started);
                    break;
                case TargetStartedEventArgs started when string.Equals(started.TargetName, "CoreCompile", StringComparison.Ordinal):
                    StartTarget(started);
                    break;
                case TargetFinishedEventArgs finished when string.Equals(finished.TargetName, "CoreCompile", StringComparison.Ordinal):
                    FinishTarget(finished);
                    break;
                case TaskStartedEventArgs started when string.Equals(started.TaskName, "Csc", StringComparison.Ordinal):
                    StartTask(started);
                    break;
                case TaskFinishedEventArgs finished when string.Equals(finished.TaskName, "Csc", StringComparison.Ordinal):
                    FinishTask(finished);
                    break;
                case BuildMessageEventArgs message when message.Message?.StartsWith(CapturePrefix, StringComparison.Ordinal) == true:
                    AddCapture(message);
                    break;
            }

            if (e.GetType().Name == "TargetSkippedEventArgs" &&
                string.Equals(ReadString(e, "TargetName"), "CoreCompile", StringComparison.Ordinal))
            {
                var key = TargetKey(e.BuildEventContext);
                if (skippedTargets.Add(key))
                {
                    targets.Add(new TargetRecord(Context(e.BuildEventContext), FullPath(ReadString(e, "ProjectFile")), "Skipped", null));
                }
            }

            if (e.GetType().Name == "TaskParameterEventArgs" &&
                string.Equals(ReadString(e, "Kind"), "TaskInput", StringComparison.Ordinal))
            {
                AddTaskInputs(e);
            }
        }

        private void AddProject(ProjectStartedEventArgs e)
        {
            var key = ProjectKey(e.BuildEventContext);
            var properties = new List<PropertyRecord>();
            var value = e.GetType().GetProperty("GlobalProperties")?.GetValue(e);
            if (value is IEnumerable entries)
            {
                foreach (var entry in entries)
                {
                    var name = ReadString(entry, "Key");
                    var propertyValue = ReadString(entry, "Value") ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        properties.Add(new PropertyRecord(name, HashText(propertyValue)));
                    }
                }
            }
            properties.Sort((left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));
            projects[key] = new ProjectRecord(Context(e.BuildEventContext), FullPath(e.ProjectFile), properties, HashProperties(properties));
        }

        private void StartTarget(TargetStartedEventArgs e)
        {
            var key = TargetKey(e.BuildEventContext);
            if (activeTargets.ContainsKey(key))
            {
                Errors.Add("DuplicateCoreCompileStart:" + key);
                return;
            }
            activeTargets[key] = new TargetRecord(Context(e.BuildEventContext), FullPath(e.ProjectFile), "Started", null);
        }

        private void FinishTarget(TargetFinishedEventArgs e)
        {
            var key = TargetKey(e.BuildEventContext);
            if (!activeTargets.Remove(key, out var started))
            {
                Errors.Add("CoreCompileFinishWithoutStart:" + key);
                return;
            }
            if (!skippedTargets.Contains(key))
            {
                targets.Add(started with { State = "Finished", Succeeded = e.Succeeded });
            }
        }

        private void StartTask(TaskStartedEventArgs e)
        {
            var key = TaskKey(e.BuildEventContext);
            if (activeTasks.ContainsKey(key))
            {
                Errors.Add("DuplicateCscStart:" + key);
                return;
            }
            activeTasks[key] = new TaskRecord(Context(e.BuildEventContext), FullPath(e.ProjectFile), false, Array.Empty<InputRecord>());
        }

        private void FinishTask(TaskFinishedEventArgs e)
        {
            var key = TaskKey(e.BuildEventContext);
            if (!activeTasks.Remove(key, out var started))
            {
                Errors.Add("CscFinishWithoutStart:" + key);
                return;
            }
            taskInputs.TryGetValue(key, out var inputs);
            var normalized = (inputs ?? new List<InputRecord>())
                .GroupBy(input => input.ParameterName + "\0" + input.ItemSpec + "\0" + input.Path, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(input => input.ParameterName, StringComparer.Ordinal)
                .ThenBy(input => input.ItemSpec, StringComparer.Ordinal)
                .ThenBy(input => input.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            tasks.Add(started with { Succeeded = e.Succeeded, Inputs = normalized });
        }

        private void AddTaskInputs(BuildEventArgs e)
        {
            var parameterName = ReadString(e, "ParameterName");
            if (string.IsNullOrWhiteSpace(parameterName) || !CompilerFileParameters.Contains(parameterName))
            {
                return;
            }
            var key = TaskKey(e.BuildEventContext);
            if (!activeTasks.TryGetValue(key, out var task) || string.IsNullOrWhiteSpace(task.Project))
            {
                // Other MSBuild tasks use the same parameter names. Only parameters
                // raised inside an active Csc task belong to the compiler closure.
                return;
            }
            var items = e.GetType().GetProperty("Items")?.GetValue(e) as IEnumerable;
            if (items is null)
            {
                return;
            }
            if (!taskInputs.TryGetValue(key, out var inputs))
            {
                inputs = new List<InputRecord>();
                taskInputs[key] = inputs;
            }
            foreach (var item in items)
            {
                var itemSpec = item is ITaskItem taskItem ? taskItem.ItemSpec : ReadString(item, "ItemSpec");
                if (string.IsNullOrWhiteSpace(itemSpec))
                {
                    Errors.Add("RequiredCompilerInputItemSpecMissing:" + parameterName);
                    continue;
                }
                try
                {
                    var projectDirectory = Path.GetDirectoryName(task.Project);
                    if (string.IsNullOrWhiteSpace(projectDirectory))
                    {
                        throw new InvalidOperationException("Project directory missing.");
                    }
                    var fullPath = Path.GetFullPath(Path.IsPathRooted(itemSpec) ? itemSpec : Path.Combine(projectDirectory, itemSpec));
                    if (!File.Exists(fullPath))
                    {
                        Errors.Add("RequiredCompilerInputUnavailable:" + parameterName + ":" + itemSpec);
                        continue;
                    }
                    var info = new FileInfo(fullPath);
                    inputs.Add(new InputRecord(parameterName, itemSpec, fullPath, info.Length, HashFile(fullPath)));
                }
                catch (Exception exception)
                {
                    Errors.Add("RequiredCompilerInputUnreadable:" + parameterName + ":" + itemSpec + ":" + exception.GetType().Name);
                }
            }
        }

        private void AddCapture(BuildMessageEventArgs e)
        {
            var parts = e.Message!.Split('|');
            if (parts.Length != 10)
            {
                Errors.Add("MalformedCaptureMessage");
                return;
            }
            captures.Add(new CaptureRecord(
                parts[1], parts[2], parts[3], parts[4], FullPath(parts[5]), parts[6], parts[7], parts[8], parts[9],
                Context(e.BuildEventContext)));
        }

        public void Finish()
        {
            if (buildStarted != 1 || buildFinished != 1)
            {
                Errors.Add($"BuildBoundaryCount:{buildStarted}:{buildFinished}");
            }
            foreach (var key in activeTargets.Keys)
            {
                Errors.Add("CoreCompileMissingEnd:" + key);
            }
            foreach (var key in activeTasks.Keys)
            {
                Errors.Add("CscMissingEnd:" + key);
            }
            if (tasks.Any(task => !task.Succeeded))
            {
                Errors.Add("CscUnsuccessful");
            }
            if (captures.Any(capture => capture.RunId != runId || capture.ResultId != resultId || capture.InvocationId != invocationId))
            {
                Errors.Add("CaptureOwnerMismatch");
            }
        }

        public object Manifest() => new
        {
            schemaVersion = 1,
            runId,
            resultId,
            invocationId,
            complete = Errors.Count == 0,
            buildStarted,
            buildFinished,
            projects = projects.Values.OrderBy(project => project.Context.ProjectContextId).ToArray(),
            coreCompile = targets.OrderBy(target => target.Context.ProjectContextId).ThenBy(target => target.Context.TargetId).ToArray(),
            csc = tasks.OrderBy(task => task.Context.ProjectContextId).ThenBy(task => task.Context.TaskId).ToArray(),
            captures = captures.OrderBy(capture => capture.Context.ProjectContextId).ThenBy(capture => capture.CaptureId, StringComparer.Ordinal).ToArray(),
            errors = Errors.ToArray()
        };

        private static string? ReadString(object? instance, string propertyName)
        {
            try
            {
                return instance?.GetType().GetProperty(propertyName)?.GetValue(instance)?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static string? FullPath(string? path) => string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
        private static string ProjectKey(BuildEventContext? context) => $"{context?.SubmissionId}:{context?.NodeId}:{context?.ProjectContextId}";
        private static string TargetKey(BuildEventContext? context) => ProjectKey(context) + $":{context?.TargetId}";
        private static string TaskKey(BuildEventContext? context) => TargetKey(context) + $":{context?.TaskId}";

        private static EventContextRecord Context(BuildEventContext? context) => new(
            context?.SubmissionId ?? -1,
            context?.NodeId ?? -1,
            context?.ProjectInstanceId ?? -1,
            context?.ProjectContextId ?? -1,
            context?.TargetId ?? -1,
            context?.TaskId ?? -1,
            context?.EvaluationId ?? -1);

        private static string HashFile(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        private static string HashText(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        private static string HashProperties(IEnumerable<PropertyRecord> properties) => HashText(string.Join("\n", properties.Select(property => property.Name + "\0" + property.ValueSha256)));
    }

    private sealed record EventContextRecord(int SubmissionId, int NodeId, int ProjectInstanceId, int ProjectContextId, int TargetId, int TaskId, int EvaluationId);
    private sealed record PropertyRecord(string Name, string ValueSha256);
    private sealed record ProjectRecord(EventContextRecord Context, string? Project, IReadOnlyList<PropertyRecord> GlobalProperties, string PropertiesFingerprint);
    private sealed record TargetRecord(EventContextRecord Context, string? Project, string State, bool? Succeeded);
    private sealed record TaskRecord(EventContextRecord Context, string? Project, bool Succeeded, IReadOnlyList<InputRecord> Inputs);
    private sealed record CaptureRecord(string RunId, string ResultId, string InvocationId, string CaptureId, string? Project, string Tfm, string Configuration, string Platform, string RuntimeIdentifier, EventContextRecord Context);
    private sealed record InputRecord(string ParameterName, string ItemSpec, string Path, long Bytes, string Sha256);
}
