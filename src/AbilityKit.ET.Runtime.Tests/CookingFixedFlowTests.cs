using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using AbilityKit.Game.Cooking.FlowAcceptance;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

[Trait("FlowStage", "S1")]
public sealed class CookingFixedFlowTests
{
    private static readonly string Root = FindRoot();
    private static readonly string Results = Path.Combine(Root, "local", "Logs", "issue13-s1-worker", "tests-" + Guid.NewGuid().ToString("N"));
    private static readonly SourceStamp TestSource = (SourceStamp)typeof(AbilityKit.Game.Cooking.FlowAcceptance.Program)
        .GetMethod("CaptureSource", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
    private static readonly FlowInvocation TestInvocation = (FlowInvocation)typeof(AbilityKit.Game.Cooking.FlowAcceptance.Program)
        .GetMethod("CaptureInvocation", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
    private static string Output(string name) => Path.Combine(Results, name + "-" + Guid.NewGuid().ToString("N"));
    private static FlowRuleCatalog Catalog() => new(Path.Combine(Root, "Docs", "design", "CookingGame", "testing", "flow-rules.json"));
    private static FlowResult Run(FlowRequest request, IFlowSessionFactory? factory = null, IFlowReportWriter? report = null,
        CancellationToken token = default, FlowDiagnosticFault diagnostic = FlowDiagnosticFault.None) =>
        OnOwner(() => new FlowOrchestrator(factory ?? new OfflineFlowSessionFactory(), Catalog(), report ?? new FlowReport(), TestSource)
            .RunAsync(request, new FlowRunOptions(TestInvocation, diagnostic), token));

    [Theory]
    [InlineData("compete-one-item")]
    [InlineData("pickup-drop-one-item")]
    public void Real_registered_flows_and_two_fresh_runs_have_complete_evidence_and_no_residual(string flow)
    {
        var runs = new List<FlowResult>();
        foreach (var ordinal in new[] { 1, 2 })
        {
            var request = FixedFlows.Request(flow, Output(flow + ordinal));
            var result = Run(request);
            Assert.Equal(FlowStatus.Passed, result.Status);
            Assert.True(result.ExecutionComplete); Assert.True(result.EvidenceComplete);
            Assert.Equal(FlowVerdict.Passed, result.Verdict);
            Assert.Equal(CleanupState.Complete, result.Cleanup.State);
            Assert.All(result.Cleanup.Resources, r => Assert.True(r.ExitConfirmed));
            Assert.Empty(result.Failures);
            Assert.Equal(0, FlowOrchestrator.ExitCode(result.Status));
            var delivered = FlowJson.Read<FlowResult>(File.ReadAllText(Path.Combine(request.OutputRoot, "result.json")));
            Assert.Equal(result.Run, delivered.Run); Assert.Equal(result.Status, delivered.Status);
            var records = File.ReadAllLines(Path.Combine(request.OutputRoot, "events.jsonl")).Select(FlowJson.Read<LoggedEvent>).ToArray();
            Assert.Equal(Enumerable.Range(1, records.Length).Select(i => (long)i), records.Select(e => e.CollectorSequence));
            Assert.All(records, e => Assert.Equal(result.Run.RunId, e.Event.RunId));
            var commands = records.Where(e => e.Event.Kind == FlowEventKind.CommandObserved).Select(e => e.Event.Command!).ToArray();
            Assert.Equal(flow == "compete-one-item" ? 3 : 2, commands.Length);
            Assert.All(commands, c => Assert.NotNull(c.BusinessResult));
            var cuts = records.Where(e => e.Event.Observation?.Available == true).Select(e => e.Event.Observation!).ToArray();
            var final = cuts.Last();
            Assert.All(final.Hands, h => Assert.Equal(HandEvidence.DomainHandIndex, h.Evidence));
            if (flow == "compete-one-item")
            {
                var pair = commands.Where(c => c.StepId == "compete").ToArray();
                var winner = Assert.Single(pair, c => c.BusinessResult!.Outcome == CookingRecipeOutcome.Accepted);
                var loser = Assert.Single(pair, c => c.BusinessResult!.Outcome == CookingRecipeOutcome.Rejected);
                Assert.Null(final.Hands.Single(h => h.ActorId == loser.ActorId).ItemId);
                Assert.Equal(winner.ActorId, Assert.Single(final.Items).OwnerId);
                Assert.Equal(winner.FrozenCommand, commands.Single(c => c.StepId == "retry-winner").FrozenCommand);
                Assert.Single(final.CommandEvents);
            }
            else
            {
                Assert.All(commands, c => Assert.Equal(CookingRecipeOutcome.Accepted, c.BusinessResult!.Outcome));
                Assert.All(final.Hands, h => Assert.Null(h.ItemId));
                Assert.Equal("drop", Assert.Single(final.Items).SlotId);
                Assert.Equal(3, Assert.Single(final.Items).Version);
                Assert.NotEqual(commands[0].BusinessId, commands[1].BusinessId);
            }
            Assert.Contains("CONVERGE:N/A(S1)", result.UnverifiedGoals);
            Assert.Contains("Network:NotRun", result.UnverifiedGoals);
            runs.Add(result);
        }
        Assert.NotEqual(runs[0].Run.RunId, runs[1].Run.RunId);
        Assert.NotEqual(runs[0].Run.AttemptId, runs[1].Run.AttemptId);
    }

    [Fact]
    public void Real_immediate_duplicate_merges_terminal_sources_once_and_admission_without_terminal_is_known()
    {
        OnOwner(async () =>
        {
            var sink = new RecordingSink();
            var request = FixedFlows.Request("compete-one-item", Output("admissions"));
            var run = new RunIdentity(request.RequestId, "admission-run", "attempt", 1);
            var adapter = new OfflineFlowAdapter((_, id) => new FlowFixture(id).CreateHost(queueCapacity: 1));
            await adapter.StartAsync(request, run, sink, default);
            var group = await adapter.DispatchGroupAsync("duplicates", new[]
            {
                new FlowAction("c1", "same", "A", FlowVerb.Pickup, "flow-item", 1, null),
                new FlowAction("c2", "same", "A", FlowVerb.Pickup, "flow-item", 1, null)
            }, 10000, default);
            Assert.True(group.Complete);
            Assert.Equal(2, group.Outcomes.Count);
            Assert.Equal(1, group.Outcomes.Count(o => o.BusinessResult?.IsDuplicate == true));
            var replay = await adapter.ReplayAsync("replay", "c1", "c3", 10000, default);
            Assert.True(replay.Complete);
            Assert.Equal(group.Outcomes[0].FrozenCommand, replay.Outcomes[0].FrozenCommand);
            var cut = await adapter.CaptureAsync("authority", default);
            Assert.Single(cut.CommandEvents);
            Assert.Equal(3, sink.Values.Count(e => e.Kind == FlowEventKind.CommandObserved));
            var next = await adapter.DispatchGroupAsync("queue-limit", new[]
            {
                new FlowAction("c4", "drop-new", "A", FlowVerb.Drop, "flow-item", 2, "drop"),
                new FlowAction("c5", "pickup-new", "B", FlowVerb.Pickup, "flow-item", 2, null)
            }, 10000, default);
            var denied = Assert.Single(next.Outcomes, o => o.AdmissionAccepted == false);
            Assert.Equal(CallCompletion.NotAdmitted, denied.Completion);
            Assert.Equal("QueueFull", denied.AdmissionReason);
            Assert.Null(denied.BusinessResult);
            var closed = await adapter.CloseAsync(10000, default);
            Assert.Same(closed, await adapter.CloseAsync(10000, default));
            Assert.Throws<InvalidOperationException>(() => adapter.CaptureAsync("authority", default));
            return 0;
        });
    }

    [Fact]
    public void Synthetic_rule_controls_detect_hand_location_reject_success_replay_effects_and_missing_goals()
    {
        var request = FixedFlows.Request("compete-one-item", Output("synthetic-source"));
        var result = Run(request);
        Assert.Equal(FlowStatus.Passed, result.Status);
        var records = File.ReadAllLines(Path.Combine(request.OutputRoot, "events.jsonl")).Select(FlowJson.Read<LoggedEvent>).ToArray();
        var commands = records.Where(e => e.Event.Command is not null).Select(e => e.Event.Command!).ToArray();
        var observations = records.Where(e => e.Event.Observation is not null).Select(e => e.Event.Observation!).ToArray();
        var evidence = new FlowEvidence(commands, new Dictionary<string, FlowObservation>
        { ["initial"] = observations[0], ["pickup"] = observations[1], ["retry"] = observations[2] }, true);
        Assert.Equal(FlowVerdict.Passed, Evaluate("C13-OWN", evidence).Verdict);
        var picked = evidence.Cuts["pickup"];
        var winner = commands.Single(c => c.StepId == "compete" && c.BusinessResult!.Outcome == CookingRecipeOutcome.Accepted);
        var loser = commands.Single(c => c.StepId == "compete" && c.BusinessResult!.Outcome == CookingRecipeOutcome.Rejected);
        Assert.Equal(FlowVerdict.Failed, Evaluate("C13-OWN", Replace(evidence, "pickup", picked with
        { Hands = picked.Hands.Select(h => h with { ItemId = "flow-item" }).ToArray() })).Verdict);
        Assert.Equal(FlowVerdict.Failed, Evaluate("C13-OWN", Replace(evidence, "pickup", picked with
        { Items = new[] { picked.Items[0] with { OwnerId = loser.ActorId } } })).Verdict);
        var falseSuccess = picked.CommandEvents[0] with { Player = new(loser.ActorId), Command = new(loser.BusinessId), Sequence = 99 };
        Assert.Equal(FlowVerdict.Failed, Evaluate("C13-REJECT", Replace(evidence, "pickup", picked with
        { CommandEvents = picked.CommandEvents.Append(falseSuccess).ToArray() })).Verdict);
        Assert.Equal(FlowVerdict.Failed, Evaluate("C13-IDEMP", Replace(evidence, "retry", evidence.Cuts["retry"] with
        { CommandEvents = picked.CommandEvents.Append(picked.CommandEvents[0] with { Sequence = 99 }).ToArray() })).Verdict);
        Assert.Equal(FlowVerdict.Failed, Evaluate("C13-IDEMP", evidence with { Commands = commands.Select(c =>
            c.StepId == "retry-winner" ? c with { FrozenCommand = c.FrozenCommand with { ExpectedItemVersion = 2 } } : c).ToArray() }).Verdict);
        Assert.Equal(FlowVerdict.Undetermined, Evaluate("C13-COMPLETE", evidence with
        { Cuts = new Dictionary<string, FlowObservation> { ["initial"] = evidence.Cuts["initial"] } }).Verdict);
        Assert.Equal(FlowVerdict.Undetermined, Evaluate("C13-COMPLETE", evidence with { RequiredEventsComplete = false }).Verdict);
        // Changing only global tick/version is legal; replay compares command effects.
        Assert.Equal(FlowVerdict.Passed, Evaluate("C13-IDEMP", Replace(evidence, "retry", evidence.Cuts["retry"] with
        { StateVersion = 9999, LogicalTick = 9999 })).Verdict);
        Assert.Empty(loser.BusinessResult!.Events);
        Assert.Single(picked.CommandEvents, e => e.Command.Value == winner.BusinessId);
    }

    [Fact]
    public void Real_frame_fault_preserves_accepted_pickup_event_and_executed_terminal_without_clock_advance()
    {
        OnOwner(async () =>
        {
            var request = FixedFlows.Request("compete-one-item", Output("frame-fault"));
            var run = new RunIdentity(request.RequestId, "fault-run", "attempt", 1);
            var fixture = new FaultFixture(run);
            CookingLevelEtHost? host = null;
            var adapter = new OfflineFlowAdapter((_, _) => host = fixture.StartHost());
            await adapter.StartAsync(request, run, new RecordingSink(), default);
            var beforeFrame = host!.HostFrameSequence;
            var beforeTick = fixture.Simulation!.LogicalTick;
            var group = await adapter.DispatchGroupAsync("compete", new[]
            {
                new FlowAction("fault-a", "pickup-a", "A", FlowVerb.Pickup, "flow-item", 1, null),
                new FlowAction("fault-b", "pickup-b", "B", FlowVerb.Pickup, "flow-item", 1, null)
            }, 10000, default);
            Assert.False(group.Complete); Assert.StartsWith("FrameFault", group.ErrorCode);
            var winner = Assert.Single(group.Outcomes, c => c.BusinessResult?.Outcome == CookingRecipeOutcome.Accepted);
            Assert.Equal("Executed", winner.NativeDisposition);
            Assert.Single(winner.BusinessResult!.Events);
            Assert.Equal("flow-item", fixture.Simulation.ItemInHand(new(winner.ActorId))?.Value);
            Assert.Equal(beforeFrame, host.HostFrameSequence); Assert.Equal(beforeTick, fixture.Simulation.LogicalTick);
            Assert.Contains(fixture.Simulation.EventHistory, e => e.Command.Value == winner.BusinessId);
            Assert.True(host.IsFaulted);
            Assert.False((await adapter.CaptureAsync("authority", default)).Available);
            Assert.Equal(CleanupState.Complete, (await adapter.CloseAsync(10000, default)).State);
            return 0;
        });
    }

    [Fact]
    public void Startup_fault_is_blocked_and_runtime_cancel_timeout_preserve_facts_and_independent_cleanup()
    {
        foreach (var mode in new[] { Control.StartupFault, Control.CancelAfterDispatch, Control.Timeout })
        {
            using var cancelled = new CancellationTokenSource();
            var request = FixedFlows.Request("compete-one-item", Output(mode.ToString()));
            ControlledSession? session = null;
            var result = Run(request, new Factory(() => session = new(mode, cancelled)), token: cancelled.Token);
            Assert.Equal(mode == Control.StartupFault ? FlowStatus.Blocked : FlowStatus.Failed, result.Status);
            Assert.False(result.ExecutionComplete); Assert.NotEqual(FlowVerdict.Passed, result.Verdict);
            Assert.Equal(CleanupState.Complete, result.Cleanup.State);
            Assert.Equal(1, session!.CloseCount); Assert.False(session.CleanupTokenCancelled);
            Assert.True(session.FirstFailureExistedAtClose);
            var first = FlowJson.Read<FailurePack>(File.ReadAllText(Path.Combine(request.OutputRoot, "first-failure.json")));
            Assert.Equal(result.Source, first.Source); Assert.Equal(result.Run, first.Run);
            Assert.Equal(TestInvocation.Arguments, first.ActualInvocation.Arguments);
            Assert.Equal(result.Failures[0].Code, first.FirstFailure.Code);
            if (mode == Control.CancelAfterDispatch)
                Assert.Contains(first.EventWindow, e => e.Event.Command?.BusinessResult?.Outcome == CookingRecipeOutcome.Accepted);
            if (mode == Control.Timeout)
            {
                var commands = File.ReadAllLines(Path.Combine(request.OutputRoot, "events.jsonl")).Select(FlowJson.Read<LoggedEvent>)
                    .Where(e => e.Event.Command is not null).Select(e => e.Event.Command!).ToArray();
                Assert.All(commands, c => { Assert.Null(c.BusinessResult); Assert.Equal(CallCompletion.Unknown, c.Completion); });
            }
        }
        var diagnosticRequest = FixedFlows.Request("compete-one-item", Output("explicit-diagnostic"));
        var diagnostic = Run(diagnosticRequest, diagnostic: FlowDiagnosticFault.FailAfterStart);
        Assert.Equal(FlowStatus.Failed, diagnostic.Status); Assert.False(diagnostic.ExecutionComplete);
        Assert.Equal(FlowVerdict.Undetermined, diagnostic.Verdict); Assert.True(diagnostic.EvidenceComplete);
        Assert.Equal(CleanupState.Complete, diagnostic.Cleanup.State);
        Assert.Equal("DiagnosticFailAfterStart", diagnostic.Failures[0].Code);
        Assert.Equal("diagnostic-after-start", diagnostic.Failures[0].StepId);
        Assert.Equal(FailureKind.HarnessOrEnvironmentFailure, diagnostic.Failures[0].Kind);
        Assert.Contains("competing-pickup:NotRun", diagnostic.UnverifiedGoals);
        var diagnosticPack = FlowJson.Read<FailurePack>(File.ReadAllText(Path.Combine(diagnosticRequest.OutputRoot, "failure-pack.json")));
        Assert.Equal(diagnostic.Source, diagnosticPack.Source); Assert.Equal(diagnostic.Run, diagnosticPack.Run);
        Assert.Contains("--diagnostic-fault", diagnosticPack.ReproduceCommand);
        Assert.Contains("fail-after-start", diagnosticPack.ReproduceCommand);
        Assert.All(diagnosticPack.RelevantCuts, c => { Assert.True(c.Available); Assert.Equal(0, c.HostFrame); Assert.Empty(c.CommandEvents); });
        Assert.DoesNotContain(diagnosticPack.EventWindow, e => e.Event.Kind == FlowEventKind.CommandObserved);
    }

    [Fact]
    public void First_product_failure_survives_cleanup_failure_with_synthetic_cut_labelled()
    {
        var request = FixedFlows.Request("compete-one-item", Output("synthetic product cleanup path"));
        ControlledSession? session = null;
        var result = Run(request, new Factory(() => session = new(Control.HandMismatchAndCleanupFault, null)));
        Assert.Equal(FlowStatus.Failed, result.Status); Assert.Equal(FlowVerdict.Failed, result.Verdict);
        Assert.Equal(FailureKind.ProductFailure, result.Failures[0].Kind); Assert.Equal("C13-OWN", result.Failures[0].Code);
        Assert.Contains(result.Failures, f => f.Code == "CleanupIncomplete");
        Assert.Equal(CleanupState.Incomplete, result.Cleanup.State);
        Assert.True(session!.FirstFailureExistedAtClose);
        var pack = FlowJson.Read<FailurePack>(File.ReadAllText(Path.Combine(request.OutputRoot, "failure-pack.json")));
        Assert.Equal(result.Failures[0], pack.FirstFailure with { EvidenceRefs = result.Failures[0].EvidenceRefs });
        Assert.Contains(pack.EventWindow, e => e.Event.Detail == "synthetic hand-index mismatch");
        Assert.Equal(CleanupState.Incomplete, pack.Cleanup.State);
        Assert.Equal(result.Source, pack.Source); Assert.Equal(result.Run, pack.Run);
        Assert.Equal(TestInvocation.Executable, pack.ActualInvocation.Executable);
        Assert.Equal(TestInvocation.Arguments, pack.ActualInvocation.Arguments);
        Assert.Contains("'" + Path.Combine(request.OutputRoot, "request.json") + "'", pack.ReproduceCommand);
        Assert.Contains("--output-root", pack.ReproduceCommand);
        var encoded = JsonNode.Parse(JsonSerializer.Serialize(pack, FlowJson.Options))!;
        foreach (var field in new[] { "source", "actualInvocation" })
        {
            var missing = encoded.DeepClone().AsObject(); missing.Remove(field);
            Assert.Throws<JsonException>(() => FlowJson.Read<FailurePack>(missing.ToJsonString()));
        }
        // Synthetic serialization-only argv with spaces and quotes, never reported as an actual launch.
        var syntheticArguments = new[] { "assembly path.dll", "--request", "request with spaces.json", "quote'boundary" };
        var syntheticPack = pack with { ActualInvocation = pack.ActualInvocation with { Arguments = syntheticArguments } };
        Assert.Equal(syntheticArguments, FlowJson.Read<FailurePack>(JsonSerializer.Serialize(syntheticPack, FlowJson.Options)).ActualInvocation.Arguments);
    }

    [Fact]
    public void Typed_schema_catalog_limits_and_old_outputs_reject_before_host_creation()
    {
        var good = FixedFlows.Request("compete-one-item", Output("json"));
        var serialized = JsonSerializer.Serialize(good, FlowJson.Options);
        Assert.Equal(good.FlowId, FlowJson.Read<FlowRequest>(serialized).FlowId);
        foreach (var mutate in new Action<JsonObject>[]
        {
            o => o["schemaVersion"] = 2, o => o["mode"] = "Unknown", o => o["mode"] = 0,
            o => o["approved"] = true, o => o.Remove("seed"), o => o["budgets"]!["stepMs"] = 20000,
            o => o["logs"]!["maxEvents"] = 5000, o => o["fixture"]!["actors"] = new JsonArray("A", "A"),
            o => o["rules"]![0]!["version"] = "0.9"
        })
        {
            var json = JsonNode.Parse(serialized)!.AsObject(); mutate(json);
            try
            {
                var invalid = FlowJson.Read<FlowRequest>(json.ToJsonString());
                var calls = 0;
                var result = Run(invalid, new Factory(() => { calls++; return new OfflineFlowAdapter(); }));
                Assert.Equal(FlowStatus.Blocked, result.Status); Assert.Equal(0, calls);
            }
            catch (Exception e) when (e is JsonException or InvalidDataException) { }
        }
        Assert.Throws<InvalidDataException>(() => FlowJson.Read<FlowRequest>("{\"schemaVersion\":1,\"schemaVersion\":1}"));
        Assert.Throws<InvalidDataException>(() => FlowJson.Read<FlowRequest>(new string(' ', 65537)));
        Directory.CreateDirectory(good.OutputRoot);
        File.WriteAllText(Path.Combine(good.OutputRoot, "keep.txt"), "old evidence");
        Assert.Equal(FlowStatus.Blocked, Run(good).Status);
        Assert.Equal("old evidence", File.ReadAllText(Path.Combine(good.OutputRoot, "keep.txt")));
        var catalogDocument = JsonNode.Parse(File.ReadAllText(Path.Combine(Root, "Docs/design/CookingGame/testing/flow-rules.json")))!;
        catalogDocument["rules"]![0]!["approvalRef"] = "request-self-approval";
        var badCatalog = Output("catalog") + ".json";
        Directory.CreateDirectory(Path.GetDirectoryName(badCatalog)!); File.WriteAllText(badCatalog, catalogDocument.ToJsonString());
        Assert.Throws<InvalidDataException>(() => new FlowRuleCatalog(badCatalog).ResolveApproved(good.Rules, FlowMode.Offline));
        Assert.Equal(2, AbilityKit.Game.Cooking.FlowAcceptance.Program.Main(new[] { "run" }));
        var untouchedOutput = Output("invalid-cli-diagnostic");
        foreach (var options in new[]
        {
            new[] { "--diagnostic-fault", "unknown" },
            new[] { "--diagnostic-fault", "fail-after-start", "--diagnostic-exit-before-run" },
            new[] { "--output-root", untouchedOutput }
        })
        {
            Assert.Equal(2, AbilityKit.Game.Cooking.FlowAcceptance.Program.Main(new[] { "run", "--request", "unused.json", "--output-root", untouchedOutput }.Concat(options).ToArray()));
            Assert.False(Directory.Exists(untouchedOutput));
        }
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, Enum.GetValues<FlowStatus>().Select(FlowOrchestrator.ExitCode));
    }

    [Fact]
    public void Missing_required_events_and_report_failure_deadline_wrong_identity_or_no_result_fail_closed()
    {
        var missing = Run(FixedFlows.Request("compete-one-item", Output("missing-events")), new Factory(() => new ControlledSession(Control.MissingEvents, null)));
        Assert.Equal(FlowStatus.Failed, missing.Status); Assert.False(missing.EvidenceComplete);
        Assert.Contains(missing.Failures, f => f.Code == "MissingRequiredEvents");
        foreach (var mode in new[] { "write", "deadline", "identity", "missing" })
        {
            var request = FixedFlows.Request("pickup-drop-one-item", Output("report-" + mode));
            var result = Run(request, report: new BrokenReport(mode));
            Assert.Equal(FlowStatus.Failed, result.Status); Assert.False(result.EvidenceComplete);
            Assert.Equal(1, FlowOrchestrator.ExitCode(result.Status));
            Assert.Equal(CleanupState.Complete, result.Cleanup.State);
            Assert.Contains(result.Failures, f => f.Code.StartsWith("ReportWriteFailure:"));
            Assert.False(File.Exists(Path.Combine(request.OutputRoot, "result.json")));
            if (mode is "identity" or "deadline") Assert.True(File.Exists(Path.Combine(request.OutputRoot, "incomplete-result.json")));
        }
    }

    [Fact]
    public async Task Collector_bounds_wrong_run_sequence_overflow_partial_and_write_fault_are_latched()
    {
        var identity = new RunIdentity("request", "collector-run", "attempt", 1);
        FlowEvent Event(long sequence) => new(1, identity.RunId, "test", 1, sequence, "step", null,
            FlowEventKind.StepStarted, null, null, null, null, null, null);
        var limits = new FlowLogLimits(1, 65536, 10, 8388608, 262144);
        foreach (var fault in new[] { "run", "sequence", "size", "count", "write", "partial" })
        {
            var stream = fault == "partial" ? new PartialStream() : new MemoryStream();
            var collector = new FlowEventCollector(Output(fault), identity,
                fault == "count" ? limits with { MaxEvents = 1 } : limits,
                fault == "write" ? () => throw new IOException("Synthetic writer fault") : () => stream);
            if (fault == "run") Assert.False(collector.TryPublish(Event(1) with { RunId = "old-run" }));
            else if (fault == "sequence") Assert.False(collector.TryPublish(Event(2)));
            else if (fault == "size") Assert.False(collector.TryPublish(Event(1) with { Detail = new string('x', 65536) }));
            else
            {
                collector.TryPublish(Event(1));
                if (fault == "count") Assert.False(collector.TryPublish(Event(2)));
            }
            await collector.CompleteAsync(default);
            Assert.False(collector.EvidenceComplete); Assert.NotNull(collector.ErrorCode);
            Assert.False(collector.TryPublish(Event(3)));
        }
        var blockedStream = new GateStream();
        var queue = new FlowEventCollector(Output("queue"), identity, limits, () => blockedStream);
        Assert.True(queue.TryPublish(Event(1)));
        await blockedStream.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(queue.TryPublish(Event(2))); Assert.False(queue.TryPublish(Event(3)));
        blockedStream.Release.TrySetResult();
        await queue.CompleteAsync(default);
        Assert.Equal("CollectorQueueOverflow", queue.ErrorCode);
        Assert.False(queue.EvidenceComplete);
    }

    private static RuleCheck Evaluate(string id, FlowEvidence evidence)
    {
        var approval = Catalog().ResolveApproved(FixedFlows.Request("compete-one-item", Output("unused")).Rules, FlowMode.Offline).Single(r => r.Rule.Id == id);
        return new FlowRuleEvaluator(approval.Rule).Evaluate(evidence, approval);
    }
    private static FlowEvidence Replace(FlowEvidence evidence, string key, FlowObservation cut)
    {
        var cuts = new Dictionary<string, FlowObservation>(evidence.Cuts) { [key] = cut };
        return evidence with { Cuts = cuts };
    }
    private static T OnOwner<T>(Func<Task<T>> action)
    {
        T value = default!;
        var type = typeof(AbilityKit.Game.Cooking.FlowAcceptance.Program).Assembly.GetType(
            "AbilityKit.Game.Cooking.NetworkRichRecoveryAcceptance.SingleThreadOwner", throwOnError: true)!;
        Func<Task<int>> scenario = async () => { value = await action(); return 0; };
        type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new object[] { scenario });
        return value;
    }
    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git"))) return directory.FullName;
        throw new InvalidOperationException("Cooking test workspace not found.");
    }
    private sealed class Factory(Func<IFlowSession> create) : IFlowSessionFactory
    { public IFlowSession Create(FlowMode mode) => create(); }
    private sealed class RecordingSink : IFlowEventSink
    {
        public List<FlowEvent> Values { get; } = new();
        public bool EvidenceComplete => true;
        public bool TryPublish(FlowEvent value) { Values.Add(value); return true; }
    }
    private enum Control { StartupFault, CancelAfterDispatch, Timeout, HandMismatchAndCleanupFault, MissingEvents }
    private sealed class ControlledSession(Control mode, CancellationTokenSource? cancelled) : IFlowSession
    {
        private readonly OfflineFlowAdapter inner = new();
        private FlowRequest request = null!;
        private RunIdentity run = null!;
        private FlowObservation initial = null!;
        private IFlowEventSink events = null!;
        private long sequence;
        public int CloseCount { get; private set; }
        public bool CleanupTokenCancelled { get; private set; }
        public bool FirstFailureExistedAtClose { get; private set; }
        public async ValueTask<FlowObservation> StartAsync(FlowRequest request, RunIdentity run, IFlowEventSink events, CancellationToken token)
        {
            this.request = request; this.run = run; this.events = events;
            if (mode == Control.StartupFault) throw new IOException("Synthetic startup environment fault");
            initial = await inner.StartAsync(request, run, mode == Control.MissingEvents ? new RecordingSink() : events, token);
            return initial;
        }
        public async ValueTask<DispatchResult> DispatchGroupAsync(string step, IReadOnlyList<FlowAction> actions, int timeout, CancellationToken token)
        {
            if (mode == Control.Timeout)
            {
                using var bounded = CancellationTokenSource.CreateLinkedTokenSource(token);
                bounded.CancelAfter(5);
                try { await Task.Delay(Timeout.InfiniteTimeSpan, bounded.Token); } catch (OperationCanceledException) { }
                var outcomes = actions.Select(a => new CommandObservation(step, a.CallId, a.BusinessId, a.ActorId,
                    new CookingRecipeCommand(initial.Fence.Scope.MatchScope, 1, new(a.ActorId), new(a.BusinessId), CookingRecipeOperation.Pickup,
                        Item: new ItemId(a.ItemId), ExpectedItemVersion: a.ExpectedItemVersion), null, null, null, null, null, null, null,
                    CallCompletion.Unknown, "CommandTimeout")).ToArray();
                foreach (var c in outcomes) events.TryPublish(new(1, run.RunId, "synthetic-timeout", 1, ++sequence, step, c.CallId,
                    FlowEventKind.CommandObserved, null, c, null, null, "CommandTimeout", "synthetic wait timeout"));
                return new(false, outcomes, "CommandTimeout");
            }
            var result = await inner.DispatchGroupAsync(step, actions, timeout, token);
            if (mode == Control.CancelAfterDispatch) cancelled!.Cancel();
            return result;
        }
        public ValueTask<DispatchResult> ReplayAsync(string step, string original, string call, int timeout, CancellationToken token) => inner.ReplayAsync(step, original, call, timeout, token);
        public async ValueTask<FlowObservation> CaptureAsync(string observer, CancellationToken token)
        {
            var cut = await inner.CaptureAsync(observer, token);
            if (mode == Control.HandMismatchAndCleanupFault)
            {
                cut = cut with { Hands = cut.Hands.Select(h => h with { ItemId = "flow-item" }).ToArray() };
                events.TryPublish(new(1, run.RunId, "synthetic-hand", 1, ++sequence, "capture", null, FlowEventKind.StateObserved,
                    cut.LogicalTick, null, cut, null, null, "synthetic hand-index mismatch"));
            }
            return cut;
        }
        public ValueTask<ConvergenceResult> WaitForProjectionAsync(ProjectionFence fence, int timeout, CancellationToken token) => inner.WaitForProjectionAsync(fence, timeout, token);
        public async ValueTask<CleanupResult> CloseAsync(int timeout, CancellationToken token)
        {
            CloseCount++; CleanupTokenCancelled = token.IsCancellationRequested;
            FirstFailureExistedAtClose = File.Exists(Path.Combine(request.OutputRoot, "first-failure.json"));
            var result = await inner.CloseAsync(timeout, token);
            if (mode == Control.HandMismatchAndCleanupFault) throw new IOException("Synthetic cleanup fault after real dispose");
            return result;
        }
    }
    private sealed class BrokenReport(string mode) : IFlowReportWriter
    {
        public async Task PublishAsync(string directory, FlowResult result, FailurePack? failure, CancellationToken token)
        {
            if (mode == "write") throw new IOException("Synthetic report write failure");
            if (mode == "deadline")
            {
                await new FlowReport().PublishAsync(directory, result, failure, token);
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return;
            }
            if (mode == "identity") await new FlowReport().PublishAsync(directory, result with { Run = result.Run with { RunId = "stale-run" } }, failure, token);
        }
    }
    private sealed class PartialStream : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default) => base.WriteAsync(buffer[..(buffer.Length / 2)], token);
    }
    private sealed class GateStream : MemoryStream
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
        {
            Started.TrySetResult(); await Release.Task.WaitAsync(token); await base.WriteAsync(buffer, token);
        }
    }
    private sealed class FaultFixture : ICookingLevelGameplayFactory
    {
        private readonly CookingLevelScope scope;
        private readonly CookingRecipeFixture recipe;
        private readonly CookingConfigurationSnapshot configuration;
        public CookingRecipeSimulation? Simulation { get; private set; }
        public FaultFixture(RunIdentity run)
        {
            scope = new(new(new(run.RunId), new("world"), new("match")), new(1), new("level"), 1);
            var items = new[] { new CookingItemDefinition(new("raw"), new HashSet<string> { "cook" }),
                new CookingItemDefinition(new("product"), new HashSet<string> { "cook" }) };
            var stations = new[] { new CookingApplianceDefinition(new("source"), new HashSet<string>()),
                new CookingApplianceDefinition(new("heat"), new HashSet<string> { "heat" }) };
            var recipes = new[] { new CookingRecipeDefinition(new("recipe"), new[] { new DefinitionId("raw") }, new("product"), new("process"), "heat", 2) };
            var players = new[] { new PlayerId("A"), new PlayerId("B") }.ToDictionary(p => p,
                p => new CookingPlayerConfig(p, new HashSet<string> { "cook" }, new HashSet<string> { "source", "heat" }));
            recipe = new(scope.MatchScope, players, items.ToDictionary(i => i.Id), stations.ToDictionary(s => s.Station), recipes.ToDictionary(r => r.Id));
            var registry = new CookingConfigurationRegistry();
            Assert.True(registry.Submit(new(new[] { "heat" }, items, stations, recipes)).Accepted);
            configuration = registry.Current!;
        }
        public CookingRecipeSimulation Create(CookingLevelScope expected, CookingConfigurationSnapshot config)
        {
            Assert.Equal(scope, expected); Assert.Equal(configuration.Identity, config.Identity);
            Simulation = new(recipe, new ThrowAllocator());
            Simulation.AddItem(new("flow-item"), new("raw"), ItemLocation.Station(new("source")));
            Simulation.AddItem(new("process-input"), new("raw"), ItemLocation.Station(new("heat")));
            return Simulation;
        }
        public CookingLevelEtHost StartHost()
        {
            var host = new CookingLevelEtHost(new CookingLevelLifecycle(scope, configuration, this));
            Assert.True(host.Prepare(new(scope.Level, new("map"), new(new("layout"), new[] { new StationSlotId("source"), new StationSlotId("heat") },
                Array.Empty<DefinitionId>()), configuration.Identity)).Accepted);
            Assert.True(host.Start().Accepted);
            var command = new CookingRecipeCommand(scope.MatchScope, 1, new("A"), new("start-process"), CookingRecipeOperation.StartProcess,
                Recipe: new RecipeId("recipe"), Item: new ItemId("process-input"), Station: new StationSlotId("heat"), ExpectedItemVersion: 1);
            Assert.True(host.TryEnqueue(new(scope, command, "setup", "setup-call")).Accepted);
            Assert.Equal(CookingRecipeOutcome.Accepted, Assert.Single(host.Tick().Dispositions).Result!.Outcome);
            return host;
        }
    }
    private sealed class ThrowAllocator : ICookingProductIdAllocator
    { public ItemId GetProductId(long sequence) => throw new InvalidOperationException("Controlled fixed-step allocation failure"); }
}
