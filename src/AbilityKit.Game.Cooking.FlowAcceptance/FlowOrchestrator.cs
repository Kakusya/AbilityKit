using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;

namespace AbilityKit.Game.Cooking.FlowAcceptance;

public sealed class FlowOrchestrator : IFlowOrchestrator
{
    private readonly IFlowSessionFactory sessions;
    private readonly IFlowRuleCatalog catalog;
    private readonly IFlowReportWriter reports;
    private readonly SourceStamp source;
    private readonly Func<string, RunIdentity, FlowLogLimits, FlowEventCollector> createCollector;

    public FlowOrchestrator(IFlowSessionFactory sessions, IFlowRuleCatalog catalog, IFlowReportWriter reports, SourceStamp source,
        Func<string, RunIdentity, FlowLogLimits, FlowEventCollector>? createCollector = null)
    {
        this.sessions = sessions; this.catalog = catalog; this.reports = reports; this.source = source;
        this.createCollector = createCollector ?? ((path, run, logs) => new(path, run, logs));
    }

    public async Task<FlowResult> RunAsync(FlowRequest request, FlowRunOptions options, CancellationToken cancellationToken)
    {
        var total = Stopwatch.StartNew();
        var startup = Stopwatch.StartNew();
        var budgets = request.Budgets ?? new FlowBudgets(10000, 10000, 10000, 120000, 10000, 2000);
        var run = new RunIdentity(request.RequestId, "run-" + Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), 1);
        var checks = new List<RuleCheck>();
        var failures = new List<FlowFailure>();
        var cleanup = new CleanupResult(CleanupState.NotNeeded, Array.Empty<ResourceOutcome>(), Array.Empty<string>());
        var context = new FlowRunContext(run, Array.Empty<ApprovedRule>(), _ => { });
        FlowEventCollector? collector = null;
        FlowResourceScope? resources = null;
        IFlowSession? session = null;
        FailurePack? frozenFailure = null;
        bool started = false, startupAttempted = false, executed = false, evidenceComplete = false, ownedDirectory = false;
        bool callerCancellationRecorded = false;
        long startupMs = 0, executeMs = 0, resetMs = 0, sequence = 0;
        string directory = request.OutputRoot;
        IReadOnlyList<ApprovedRule> approvals = Array.Empty<ApprovedRule>();
        using var active = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            FlowJson.Validate(request);
            if (options is null || !Enum.IsDefined(options.DiagnosticFault) || options.ActualInvocation is null ||
                string.IsNullOrWhiteSpace(options.ActualInvocation.Executable) || string.IsNullOrWhiteSpace(options.ActualInvocation.WorkingDirectory) ||
                options.ActualInvocation.Arguments is null || options.ActualInvocation.Arguments.Any(a => a is null))
                throw new InvalidDataException("Invalid explicit invocation options.");
            options = options with { ActualInvocation = options.ActualInvocation with
                { Arguments = Array.AsReadOnly(options.ActualInvocation.Arguments.ToArray()) } };
            request = request with { Fixture = request.Fixture with { Actors = Array.AsReadOnly(request.Fixture.Actors.ToArray()) },
                Rules = Array.AsReadOnly(request.Rules.ToArray()), OutputRoot = Path.GetFullPath(request.OutputRoot) };
            approvals = catalog.ResolveApproved(request.Rules, request.Mode);
            directory = Path.GetFullPath(request.OutputRoot);
            // Atomic reservation refuses an existing destination, including a concurrent run.
            var staging = directory + ".claim-" + run.RunId;
            Directory.CreateDirectory(staging);
            Directory.Move(staging, directory);
            ownedDirectory = true;
            startupAttempted = true;
            active.CancelAfter(Math.Min(budgets.StartupMs, RemainingActive()));
            await FlowReport.WriteAtomicAsync(Path.Combine(directory, "identity.json"), JsonSerializer.Serialize(run, FlowJson.Options), 8192, active.Token);
            await FlowReport.WriteAtomicAsync(Path.Combine(directory, "request.json"), JsonSerializer.Serialize(request, FlowJson.Options), 65536, active.Token);
            var assemblyPath = typeof(FlowOrchestrator).Assembly.Location;
            await FlowReport.WriteAtomicAsync(Path.Combine(directory, "provenance.json"), JsonSerializer.Serialize(new
            { source, assemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assemblyPath))),
                sourceMeaning = "Invocation checkout; build inputs not archived", buildMs = (long?)null }, FlowJson.Options), 8192, active.Token);
            collector = createCollector(Path.Combine(directory, "events.jsonl"), run, request.Logs);
            context = new(run, approvals, Record);
            session = sessions.Create(request.Mode);
            resources = new(session);
            using (var startupToken = CancellationTokenSource.CreateLinkedTokenSource(active.Token))
            {
                startupToken.CancelAfter(Math.Max(0, Math.Min(budgets.StartupMs - (int)startup.ElapsedMilliseconds, RemainingActive())));
                try
                {
                    var initial = await session.StartAsync(request, run, collector, startupToken.Token);
                    context.Cuts["initial"] = initial;
                    context.CheckAfterAwait(startupToken.Token);
                    if (startup.ElapsedMilliseconds > budgets.StartupMs) throw new TimeoutException("Startup budget exceeded.");
                    if (!initial.Available) throw new FlowExecutionException("StartupUnavailable", "startup");
                    started = true;
                    active.CancelAfter(RemainingActive());
                }
                finally { startupMs = startup.ElapsedMilliseconds; }
            }
            if (options.DiagnosticFault == FlowDiagnosticFault.FailAfterStart)
            {
                collector.TryPublish(new(1, run.RunId, "orchestrator", run.RunGeneration, ++sequence, "diagnostic-after-start", null,
                    FlowEventKind.RoleFaulted, context.Cuts["initial"].LogicalTick, null, null, null,
                    "DiagnosticFailAfterStart", "Explicit test-host diagnostic before gameplay dispatch"));
                throw new FlowExecutionException("DiagnosticFailAfterStart", "diagnostic-after-start");
            }
            var execution = Stopwatch.StartNew();
            try
            {
                await FixedFlows.ExecuteAsync(request, session, context, StepBudget, active.Token);
                context.CheckAfterAwait(active.Token);
                executed = true;
            }
            finally { executeMs = execution.ElapsedMilliseconds; }
        }
        catch (Exception e)
        {
            var flowError = e as FlowExecutionException;
            // Failed product checks are already recorded and remain first even if later work fails.
            if (!failures.Any(f => f.Kind == FailureKind.ProductFailure) || flowError?.Code != "ProductRuleNotPassed")
                AddFailure(FailureKind.HarnessOrEnvironmentFailure,
                    flowError?.Code ?? (e is OperationCanceledException ? "CancelledOrDeadline" : e.GetType().Name),
                    flowError?.Step ?? (started ? "execution" : startupAttempted ? "startup" : "validation"), "Complete bounded flow", e.GetType().Name);
            // Capture a final available committed cut without claiming cancellation reverted actions.
            if (session is not null && context.Cuts.ContainsKey("initial"))
            {
                using var captureToken = new CancellationTokenSource(Math.Max(1, Math.Min(1000, RemainingTotal())));
                try { context.Cuts["failure"] = await session.CaptureAsync("authority", captureToken.Token); }
                catch (Exception) { /* Last valid cuts and returned terminals remain in the failure pack. */ }
            }
        }
        finally
        {
            if (collector is not null && (!collector.EvidenceComplete || !collector.HasRequiredEvents(context.Evidence())))
                AddFailure(FailureKind.HarnessOrEnvironmentFailure, collector.ErrorCode ?? "MissingRequiredEvents", "evidence",
                    "Required facts complete before cleanup", "Evidence incomplete");
            if (failures.Count != 0 && ownedDirectory)
            {
                // Freeze the first failure and its committed facts BEFORE Close can dispose the host.
                frozenFailure = MakePack(new(CleanupState.Incomplete, Array.Empty<ResourceOutcome>(), new[] { "CleanupNotStarted" }));
                using var snapshotToken = new CancellationTokenSource(Math.Max(1, Math.Min(budgets.PublishMs, RemainingTotal())));
                try
                {
                    await FlowReport.WriteAtomicAsync(Path.Combine(directory, "first-failure.json"),
                        JsonSerializer.Serialize(frozenFailure, FlowJson.Options), 2097152, snapshotToken.Token);
                }
                catch (Exception e) { AddFailure(FailureKind.HarnessOrEnvironmentFailure, "FailureSnapshotWrite:" + e.GetType().Name,
                    "failure", "First failure file delivered", "Write failed"); }
            }
            var reset = Stopwatch.StartNew();
            if (resources is not null) cleanup = await resources.CloseAsync(Math.Max(1, Math.Min(budgets.ResetMs, RemainingTotal() - budgets.PublishMs)));
            resetMs = reset.ElapsedMilliseconds;
            if (cleanup.State == CleanupState.Incomplete)
                AddFailure(FailureKind.HarnessOrEnvironmentFailure, "CleanupIncomplete", "cleanup", "All owned resources exited", string.Join(",", cleanup.Errors));
            if (collector is not null)
            {
                var executionEvidence = context.Evidence();
                evidenceComplete = collector.HasRequiredEvents(executionEvidence);
                var completeApproval = approvals.Single(a => a.Rule.Id == "C13-COMPLETE");
                var completeCheck = new FlowRuleEvaluator(completeApproval.Rule).Evaluate(context.Evidence(evidenceComplete), completeApproval);
                Record(completeCheck);
                if (completeCheck.Verdict == FlowVerdict.Undetermined)
                    AddFailure(FailureKind.HarnessOrEnvironmentFailure, "ExecutionFactsIncomplete", "completion", completeCheck.Expected, completeCheck.Actual);
                using var flushToken = new CancellationTokenSource(Math.Max(1, Math.Min(budgets.PublishMs, RemainingTotal())));
                try { await collector.CompleteAsync(flushToken.Token); }
                catch (Exception e) { AddFailure(FailureKind.HarnessOrEnvironmentFailure, "CollectorIncomplete:" + e.GetType().Name, "evidence", "Writer exited", "Unconfirmed"); }
                evidenceComplete &= collector.EvidenceComplete;
                if (!evidenceComplete) AddFailure(FailureKind.HarnessOrEnvironmentFailure, collector.ErrorCode ?? "MissingRequiredEvents",
                    "evidence", "All required events delivered", "Evidence incomplete");
            }
        }

        if (total.ElapsedMilliseconds >= budgets.OverallMs && ownedDirectory)
            AddFailure(FailureKind.HarnessOrEnvironmentFailure, "OverallDeadline", "completion", "Entire run within overall budget", "Budget exhausted");
        ObserveCallerCancellation();
        var productFailure = failures.Any(f => f.Kind == FailureKind.ProductFailure);
        var productRules = checks.Where(c => c.Rule.Id != "C13-COMPLETE").ToArray();
        var verdict = productFailure ? FlowVerdict.Failed : executed && productRules.Length >= 3 && productRules.All(c => c.Verdict == FlowVerdict.Passed)
            ? FlowVerdict.Passed : FlowVerdict.Undetermined;
        var passed = executed && verdict == FlowVerdict.Passed && evidenceComplete && cleanup.State == CleanupState.Complete &&
            failures.Count == 0 && checks.Any(c => c.Rule.Id == "C13-COMPLETE" && c.Verdict == FlowVerdict.Passed);
        var result = new FlowResult(1, run, source, request.FlowId, request.FlowVersion, request.Mode,
            passed ? FlowStatus.Passed : started ? FlowStatus.Failed : FlowStatus.Blocked, executed, verdict, evidenceComplete, cleanup,
            checks.ToArray(), context.CompletedGoals.ToArray(),
            (request.FlowId == "compete-one-item" ? new[] { "competing-pickup", "exact-replay" } : new[] { "pickup-drop" })
                .Except(context.CompletedGoals).Select(g => g + ":NotRun")
                .Concat(new[] { "CONVERGE:N/A(S1)", "Unity:NotRun", "Network:NotRun", "PhysicalLAN:NotRun", "OrcaAutomaticWake:NotRun" }).ToArray(),
            failures.ToArray(), new(null, startupMs, executeMs, resetMs, total.ElapsedMilliseconds));
        if (ownedDirectory)
        {
            if (failures.Count > 0 && frozenFailure is null) frozenFailure = MakePack(cleanup);
            if (frozenFailure is not null) frozenFailure = frozenFailure with { Cleanup = cleanup };
            using var publishToken = new CancellationTokenSource(Math.Max(1, Math.Min(budgets.PublishMs, RemainingTotal())));
            try
            {
                await reports.PublishAsync(directory, result, frozenFailure, publishToken.Token);
                // A returning publisher alone is not proof of delivered result identity.
                var resultFile = Path.Combine(directory, "result.json");
                if (!File.Exists(resultFile) || new FileInfo(resultFile).Length > 524288 ||
                    !File.Exists(Path.Combine(directory, "events.jsonl")) || !File.Exists(Path.Combine(directory, "summary.txt")) ||
                    !File.Exists(Path.Combine(directory, "report.html"))) throw new InvalidDataException("Incomplete published result artifacts.");
                var published = JsonSerializer.Deserialize<FlowResult>(await File.ReadAllTextAsync(resultFile, publishToken.Token), FlowJson.Options)
                    ?? throw new InvalidDataException("Missing published result.");
                if (published.Run != run || published.Source != source || published.Status != result.Status ||
                    published.EvidenceComplete != result.EvidenceComplete || published.ExecutionComplete != result.ExecutionComplete)
                    throw new InvalidDataException("Published result identity mismatch.");
                result = published;
                if (total.ElapsedMilliseconds >= budgets.OverallMs) throw new TimeoutException("Publication exceeded overall deadline.");
                // Successful delivery is accepted only at this validated readback boundary.
                // Cleanup/publication use independent tokens; caller cancellation still governs acceptance.
                if (ObserveCallerCancellation())
                {
                    if (published.Status == FlowStatus.Passed)
                    {
                        File.Move(resultFile, Path.Combine(directory, "incomplete-result.json"), overwrite: false);
                        frozenFailure = MakePack(cleanup);
                        await FlowReport.WriteAtomicAsync(Path.Combine(directory, "failure-pack.json"),
                            JsonSerializer.Serialize(frozenFailure, FlowJson.Options), 2097152, publishToken.Token);
                    }
                    result = result with { Status = started ? FlowStatus.Failed : FlowStatus.Blocked, Failures = failures.ToArray() };
                }
            }
            catch (Exception e)
            {
                AddFailure(FailureKind.HarnessOrEnvironmentFailure, "ReportWriteFailure:" + e.GetType().Name, "publish", "Complete atomic result published last", "Publication failed");
                var completionMarker = Path.Combine(directory, "result.json");
                if (File.Exists(completionMarker))
                    File.Move(completionMarker, Path.Combine(directory, "incomplete-result.json"), overwrite: false);
                result = result with { Status = FlowStatus.Failed, EvidenceComplete = false, Failures = failures.ToArray() };
            }
        }
        return result;

        int RemainingTotal() => Math.Max(0, budgets.OverallMs - (int)total.ElapsedMilliseconds);
        int RemainingActive() => Math.Max(1, RemainingTotal() - budgets.ResetMs - budgets.PublishMs);
        int StepBudget()
        {
            active.Token.ThrowIfCancellationRequested();
            if (!collector!.EvidenceComplete || RemainingTotal() <= budgets.ResetMs + budgets.PublishMs)
                throw new FlowExecutionException("EvidenceOrActiveDeadline", "dispatch");
            return Math.Min(budgets.StepMs, RemainingActive());
        }
        void Record(RuleCheck check)
        {
            checks.Add(check);
            collector!.TryPublish(new(1, run.RunId, "orchestrator", run.RunGeneration, ++sequence, check.StepId, null,
                FlowEventKind.RuleChecked, null, null, null, check, null, null));
            if (check.Verdict == FlowVerdict.Failed)
                failures.Add(new(check.Rule.Id == "C13-COMPLETE" ? FailureKind.HarnessOrEnvironmentFailure : FailureKind.ProductFailure,
                    check.Rule.Id, check.StepId, null, check.Expected, check.Actual, check.EvidenceRefs));
        }
        void AddFailure(FailureKind kind, string code, string step, string expected, string actual) =>
            failures.Add(new(kind, code, step, null, expected, actual, context.Cuts.Keys.Select(k => "cut:" + k).ToArray()));
        bool ObserveCallerCancellation()
        {
            if (!cancellationToken.IsCancellationRequested) return false;
            if (!callerCancellationRecorded)
            {
                AddFailure(FailureKind.HarnessOrEnvironmentFailure, "CallerCancelled", "completion",
                    "Caller permits completion through publication/readback", "Caller cancellation observed before completion");
                callerCancellationRecorded = true;
            }
            return true;
        }
        FailurePack MakePack(CleanupResult cleanupResult) => new(run, request, source, options.ActualInvocation, failures[0], context.Cuts.Values.ToArray(),
            collector?.EventWindow ?? Array.Empty<LoggedEvent>(),
            "& " + string.Join(" ", new[] { "dotnet", typeof(FlowOrchestrator).Assembly.Location, "run", "--request",
                Path.Combine(directory, "request.json"), "--output-root", directory + ".reproduce-" + run.AttemptId }
                .Concat(options.DiagnosticFault == FlowDiagnosticFault.FailAfterStart ? new[] { "--diagnostic-fault", "fail-after-start" } : Array.Empty<string>())
                .Select(value => "'" + value.Replace("'", "''") + "'")), cleanupResult);
    }

    public static int ExitCode(FlowStatus status) => status switch
    { FlowStatus.Passed => 0, FlowStatus.Failed => 1, FlowStatus.Blocked => 2, FlowStatus.Skipped => 3, FlowStatus.NotRun => 4, _ => 1 };
}
