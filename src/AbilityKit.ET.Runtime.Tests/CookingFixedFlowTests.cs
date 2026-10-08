using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;
using System.Text;
using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using AbilityKit.Game.Cooking.FlowAcceptance;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

public sealed class CookingFixedFlowTests
{
    private static readonly string Root = FindRoot();
    private static readonly string Results = Path.Combine(Root, "local", "Logs", "issue13-s2-worker", "tests-" + Guid.NewGuid().ToString("N"));
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
    [Trait("FlowStage", "S1")]
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
    [Trait("FlowStage", "S1")]
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
    [Trait("FlowStage", "S1")]
    [Trait("FlowStage", "S2")]
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
        // Synthetic partial terminals cannot prove a reject count, but do not hide independent violations.
        var partial = evidence with { Commands = commands.Where(c => c.StepId == "compete").Select(c =>
            c.CallId == winner.CallId ? c with { BusinessResult = null, Completion = CallCompletion.Unknown,
                NativeDisposition = null, ErrorCode = "CommandTimeout" } : c).ToArray() };
        Assert.Equal(FlowVerdict.Undetermined, Evaluate("C13-REJECT", partial).Verdict);
        Assert.Equal(FlowVerdict.Failed, Evaluate("C13-REJECT", Replace(partial, "pickup", picked with
        { CommandEvents = picked.CommandEvents.Append(falseSuccess).ToArray() })).Verdict);
        Assert.Equal(FlowVerdict.Failed, Evaluate("C13-OWN", Replace(partial, "pickup", picked with
        { Hands = picked.Hands.Select(h => h with { ItemId = "flow-item" }).ToArray() })).Verdict);
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
    [Trait("FlowStage", "S1")]
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
    [Trait("FlowStage", "S1")]
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
                Assert.Equal(FlowVerdict.Undetermined, result.Verdict);
                Assert.DoesNotContain(result.Failures, f => f.Kind == FailureKind.ProductFailure);
                Assert.Equal("CommandTimeout", result.Failures[0].Code);
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
    [Trait("FlowStage", "S1")]
    public void First_product_failure_survives_cleanup_failure_with_synthetic_cut_labelled()
    {
        var request = FixedFlows.Request("compete-one-item", Output("synthetic product cleanup path"));
        using var cancelled = new CancellationTokenSource();
        ControlledSession? session = null;
        var result = Run(request, new Factory(() => session = new(Control.HandMismatchAndCleanupFault, null)),
            report: new BrokenReport("cancel", cancelled), token: cancelled.Token);
        Assert.Equal(FlowStatus.Failed, result.Status); Assert.Equal(FlowVerdict.Failed, result.Verdict);
        Assert.Equal(FailureKind.ProductFailure, result.Failures[0].Kind); Assert.Equal("C13-OWN", result.Failures[0].Code);
        Assert.Contains(result.Failures, f => f.Code == "CleanupIncomplete");
        Assert.Contains(result.Failures, f => f.Code == "CallerCancelled");
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
    [Trait("FlowStage", "S1")]
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
    [Trait("FlowStage", "S1")]
    [Trait("FlowStage", "S2")]
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
        using var cancelled = new CancellationTokenSource();
        var lateRequest = FixedFlows.Request("pickup-drop-one-item", Output("report-late-cancel"));
        var late = Run(lateRequest, report: new BrokenReport("cancel", cancelled), token: cancelled.Token);
        Assert.Equal(FlowStatus.Failed, late.Status); Assert.Equal(1, FlowOrchestrator.ExitCode(late.Status));
        Assert.True(late.ExecutionComplete); Assert.Equal(FlowVerdict.Passed, late.Verdict);
        Assert.True(late.EvidenceComplete); Assert.Equal(CleanupState.Complete, late.Cleanup.State);
        Assert.DoesNotContain(late.Failures, f => f.Kind == FailureKind.ProductFailure || f.Code.StartsWith("ReportWriteFailure:"));
        Assert.Equal("CallerCancelled", Assert.Single(late.Failures).Code);
        Assert.False(File.Exists(Path.Combine(lateRequest.OutputRoot, "result.json")));
        var unaccepted = FlowJson.Read<FlowResult>(File.ReadAllText(Path.Combine(lateRequest.OutputRoot, "incomplete-result.json")));
        Assert.Equal(FlowStatus.Passed, unaccepted.Status); Assert.Equal(late.Run, unaccepted.Run);
        var latePack = FlowJson.Read<FailurePack>(File.ReadAllText(Path.Combine(lateRequest.OutputRoot, "failure-pack.json")));
        Assert.Equal("CallerCancelled", latePack.FirstFailure.Code); Assert.Equal(late.Source, latePack.Source);
    }

    [Fact]
    [Trait("FlowStage", "S1")]
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
        // Synthetic payloads never become required facts under a mismatched event kind.
        var scope = new CookingLevelScope(new(new("collector"), new("world"), new("match")), new(1), new("level"), 1);
        var command = new CommandObservation("step", "call", "business", "A",
            new CookingRecipeCommand(scope.MatchScope, 1, new("A"), new("business"), CookingRecipeOperation.Pickup),
            null, null, null, null, null, null, null, CallCompletion.Unknown, "synthetic");
        var observation = new FlowObservation("synthetic", ObservationOrigin.Authority, true, null,
            new(1, scope, null, null), 0, 0, 0, null, null, Array.Empty<ItemProbe>(), Array.Empty<HandProbe>(), Array.Empty<CookingRecipeEvent>());
        var check = new RuleCheck(new("C13-OWN", "1.0"), FlowVerdict.Undetermined, "step", "synthetic", "synthetic", Array.Empty<string>());
        foreach (var wrong in new[]
        {
            Event(1) with { Command = command, CallId = command.CallId },
            Event(1) with { Kind = FlowEventKind.RoleFaulted, Observation = observation },
            Event(1) with { Kind = FlowEventKind.RoleStopped, Check = check },
            Event(1) with { Kind = FlowEventKind.CommandObserved, Command = command, CallId = command.CallId, Observation = observation },
            Event(1) with { Kind = FlowEventKind.CommandObserved, Command = command, CallId = "wrong-call" },
            Event(1) with { Kind = FlowEventKind.RoleReady, Observation = observation, Command = command },
            Event(1) with { Kind = FlowEventKind.StateObserved, Observation = observation, Check = check },
            Event(1) with { Kind = FlowEventKind.RuleChecked, Check = check, Observation = observation }
        })
        {
            var collector = new FlowEventCollector(Output("wrong-kind"), identity, limits, () => new MemoryStream());
            Assert.False(collector.TryPublish(wrong));
            Assert.False(collector.HasRequiredEvents(new(new[] { command }, new Dictionary<string, FlowObservation> { ["synthetic"] = observation }, true)));
            Assert.Empty(collector.EventWindow);
            await collector.CompleteAsync(default);
            Assert.False(collector.EvidenceComplete); Assert.Equal("InvalidEventIdentitySequenceOrPayload", collector.ErrorCode);
        }
    }

    private static RuleCheck Evaluate(string id, FlowEvidence evidence)
    {
        var approval = Catalog().ResolveApproved(FixedFlows.Request("compete-one-item", Output("unused")).Rules, FlowMode.Offline).Single(r => r.Rule.Id == id);
        return new FlowRuleEvaluator(approval.Rule).Evaluate(evidence, approval);
    }

    private static readonly string NetworkResults = Path.Combine(Root, "local", "Logs", "issue13-s2-revision", "tests-" + Guid.NewGuid().ToString("N"));
    private static string NetworkOutput(string name) => Path.Combine(NetworkResults, name + "-" + Guid.NewGuid().ToString("N"));
    private static FlowRequest NetworkRequest(string flow, string output)
    {
        var request = FixedFlows.Request(flow, output);
        return request with { Mode = FlowMode.Network, Rules = request.Rules.Append(new RuleRef("C13-CONVERGE", "1.0")).ToArray() };
    }

    [Theory]
    [Trait("FlowStage", "S2")]
    [InlineData("compete-one-item")]
    [InlineData("pickup-drop-one-item")]
    public void Real_network_flows_use_three_children_current_projections_and_confirm_native_exits(string flow)
    {
        var request = NetworkRequest(flow, NetworkOutput(flow));
        var result = Run(request, new FixedFlowSessionFactory());
        Assert.Equal(FlowStatus.Passed, result.Status); Assert.Empty(result.Failures);
        Assert.True(result.ExecutionComplete); Assert.True(result.EvidenceComplete);
        Assert.Equal(FlowVerdict.Passed, result.Verdict); Assert.Equal(CleanupState.Complete, result.Cleanup.State);
        Assert.Equal(3, result.Cleanup.Resources.Count);
        Assert.Equal(3, result.Cleanup.Resources.Select(r => r.ProcessId).Distinct().Count());
        Assert.All(result.Cleanup.Resources, r => { Assert.True(r.ExitConfirmed); Assert.Null(r.ErrorCode); Assert.NotEqual(Environment.ProcessId, r.ProcessId); });
        Assert.DoesNotContain("Network:NotRun", result.UnverifiedGoals);
        Assert.All(result.Checks, c => Assert.Equal(FlowVerdict.Passed, c.Verdict));
        var records = File.ReadAllLines(Path.Combine(request.OutputRoot, "events.jsonl")).Select(FlowJson.Read<LoggedEvent>).ToArray();
        Assert.All(records, e => Assert.Equal(result.Run.RunId, e.Event.RunId));
        foreach (var host in records.GroupBy(r => r.Event.HostId))
            Assert.Equal(Enumerable.Range(1, host.Count()).Select(n => (long)n), host.Select(e => e.Event.HostSequence));
        var commands = records.Where(r => r.Event.Command is not null).Select(r => r.Event.Command!).ToArray();
        Assert.Equal(flow == "compete-one-item" ? 3 : 2, commands.Length);
        Assert.All(commands, c => { Assert.Equal(0, c.FrozenCommand.SimulationBatch); Assert.Null(c.WireCorrelationId); Assert.NotNull(c.DomainCommandId); });
        var ready = records.Where(e => e.Event.Kind == FlowEventKind.RoleReady).Select(e => e.Event.Observation!).ToArray();
        Assert.Equal(3, ready.Length);
        var authority = ready.Single(c => c.Origin == ObservationOrigin.Authority);
        Assert.All(ready.Where(c => c.Origin == ObservationOrigin.Client), c =>
        { Assert.Equal(authority.Fence.ServerSessionInstance, c.Fence.ServerSessionInstance); Assert.Equal(authority.Fence.Scope, c.Fence.Scope); Assert.True(c.Fence.ConnectionGeneration > 0); });
        var pickupCheck = records.First(e => e.Event.Check?.Rule.Id == "C13-CONVERGE");
        Assert.True(pickupCheck.CollectorSequence < records.First(e => e.Event.Command?.StepId == (flow == "compete-one-item" ? "retry-winner" : "drop")).CollectorSequence);
        if (flow == "compete-one-item")
        {
            var winner = Assert.Single(commands.Where(c => c.StepId == "compete"), c => c.BusinessResult!.Outcome == CookingRecipeOutcome.Accepted);
            var loser = Assert.Single(commands.Where(c => c.StepId == "compete"), c => c.BusinessResult!.Outcome == CookingRecipeOutcome.Rejected);
            Assert.Empty(loser.BusinessResult!.Events);
            var replay = commands.Single(c => c.StepId == "retry-winner");
            Assert.Equal(winner.FrozenCommand, replay.FrozenCommand); Assert.Equal(winner.DomainCommandId, replay.DomainCommandId);
            Assert.True(replay.BusinessResult!.IsDuplicate); Assert.Empty(replay.BusinessResult.Events);
            var cut = records.Last(r => r.Event.Observation?.Origin == ObservationOrigin.Authority).Event.Observation!;
            Assert.Single(cut.CommandEvents, e => e.Command.Value == winner.DomainCommandId);
        }
        using var proof = JsonDocument.Parse(File.ReadAllText(Path.Combine(request.OutputRoot, "network-resources.json")));
        Assert.Equal(result.Run.RunId, proof.RootElement.GetProperty("run").GetProperty("runId").GetString());
        var children = proof.RootElement.GetProperty("children").EnumerateArray().ToArray();
        Assert.Equal(3, children.Length);
        Assert.All(children, child =>
        {
            Assert.Equal(0, child.GetProperty("exitCode").GetInt32()); Assert.True(child.GetProperty("stopped").GetBoolean());
            Assert.True(child.GetProperty("readerComplete").GetBoolean()); Assert.True(child.GetProperty("diagnosticsComplete").GetBoolean());
            Assert.Equal(JsonValueKind.Null, child.GetProperty("error").ValueKind);
        });
    }

    [Fact]
    [Trait("FlowStage", "S2")]
    public void Synthetic_role_protocol_and_projection_fences_reject_wrong_binding_old_payload_and_partial_input()
    {
        var request = NetworkRequest("compete-one-item", NetworkOutput("synthetic"));
        var run = new RunIdentity(request.RequestId, "synthetic-run", "attempt", 1);
        var initialize = new RoleControl(1, run, "server", 1, "init", RoleControlKind.Initialize,
            new("server", null, null, request.Fixture, request.Budgets, request.Logs), null, null, null, null);
        void Validate(RoleControl c, RunIdentity? frozen, long sequence) => InvokeProtocol("ValidateControl", c, "server", frozen, sequence);
        Validate(initialize, null, 1);
        foreach (var wrong in new[] { initialize with { SchemaVersion = 2 }, initialize with { RoleId = "client-a" },
            initialize with { ControlSequence = 2 }, initialize with { Action = new("call", "business", "A", FlowVerb.Pickup, "flow-item", 1, null) } })
            Assert.Throws<TargetInvocationException>(() => Validate(wrong, null, 1));
        Assert.Throws<TargetInvocationException>(() => Validate(initialize with { ControlSequence = 2 }, run, 2));
        var stop = initialize with { Init = null, Kind = RoleControlKind.Stop, ControlSequence = 2, ControlId = "stop" };
        Assert.Throws<TargetInvocationException>(() => Validate(stop with { Run = run with { RunGeneration = 2 } }, run, 2));
        var json = JsonSerializer.Serialize(initialize, FlowJson.Options);
        Assert.ThrowsAny<Exception>(() => FlowJson.Read<RoleControl>(json.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"unknown\":true")));
        Assert.ThrowsAny<Exception>(() => FlowJson.Read<RoleControl>(json.Replace("\"controlId\":\"init\",", "")));
        foreach (var text in new[] { "partial", new string('x', 65537) })
            Assert.ThrowsAny<Exception>(() => ((Task<string?>)InvokeProtocol("ReadLineAsync", new MemoryStream(Encoding.UTF8.GetBytes(text)), CancellationToken.None)!).GetAwaiter().GetResult());
        var scope = new FlowFixture(run).Scope;
        var authority = new BindingFence(1, scope, "server-instance", null);
        var item = new ItemProbe("flow-item", 2, false, "PlayerHand", "A", null);
        var hands = new[] { new HandProbe("A", "flow-item", HandEvidence.DomainHandIndex), new HandProbe("B", null, HandEvidence.DomainHandIndex) };
        var expected = new ProjectionFence(authority, new[] { "client-a", "client-b" }, 10, new[] { item }, hands);
        var observed = new FlowObservation("client-a", ObservationOrigin.Client, true, null, authority with { ConnectionGeneration = 7 },
            11, 12, 13, 5, true, new[] { item }, hands.Select(h => h with { Evidence = HandEvidence.ClientProjection }).ToArray(), Array.Empty<CookingRecipeEvent>());
        bool Matches(FlowObservation value) => (bool)typeof(NetworkFlowAdapter).GetMethod("ProjectionMatches", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { value, expected })!;
        Assert.True(Matches(observed)); // installed fields can converge without claiming exact target ACK.
        foreach (var bad in new[] { observed with { StateVersion = 9 }, observed with { BaselineSequence = 0 }, observed with { Available = false },
            observed with { SynchronizedObserved = false }, observed with { SynchronizedObserved = null },
            observed with { Fence = observed.Fence with { RunGeneration = 2 } }, observed with { Fence = observed.Fence with { ServerSessionInstance = "old" } },
            observed with { Fence = observed.Fence with { Scope = new(scope.MatchScope, scope.RestaurantRuntime, scope.Level, 2) } }, observed with { Fence = observed.Fence with { ConnectionGeneration = 0 } },
            observed with { Items = new[] { item with { OwnerId = "B" } } }, observed with { Hands = observed.Hands.Select(h => h with { ItemId = null }).ToArray() } })
            Assert.False(Matches(bad));
        var eventValue = new FlowEvent(1, run.RunId, "server", 1, 1, "initial", null, FlowEventKind.RoleReady, null, null,
            observed with { ObserverId = "authority", Origin = ObservationOrigin.Authority, Fence = authority }, null, null, null);
        var ready = new RoleReply(1, run, "server", 1, "init", RoleReplyKind.Ready, eventValue, "127.0.0.1:1234", null);
        InvokeProtocol("ValidateReply", ready, run, "server", 1L, 1L);
        foreach (var bad in new[] { ready with { Run = run with { AttemptId = "old" } }, ready with { HostSequence = 2 },
            ready with { Event = eventValue with { HostSequence = 2 } }, ready with { Kind = RoleReplyKind.Armed }, ready with { Event = null } })
            Assert.Throws<TargetInvocationException>(() => InvokeProtocol("ValidateReply", bad, run, "server", 1L, 1L));
        // Armed consumes reply sequence without consuming event sequence.
        InvokeProtocol("ValidateReply", new RoleReply(1, run, "server", 2, "arm", RoleReplyKind.Armed, null, null, null), run, "server", 2L, 1L);
        InvokeProtocol("ValidateReply", ready with { HostSequence = 3, Event = eventValue with { HostSequence = 2 } }, run, "server", 3L, 2L);
        // Synthetic network replay controls exercise actual domain identity, not wire stable-ID guesses.
        var domain = AbilityKit.Game.Cooking.Session.CookingNetworkWireCodec.DomainId(authority.ServerSessionInstance!, scope, new("A"), "pickup-A").Value;
        var rejectedDomain = AbilityKit.Game.Cooking.Session.CookingNetworkWireCodec.DomainId(authority.ServerSessionInstance!, scope, new("B"), "pickup-B").Value;
        var committed = new CookingRecipeEvent(1, 3, new("A"), new(domain), CookingRecipeOperation.Pickup, null, null, new("flow-item"), "synthetic accepted pickup");
        var frozen = new CookingRecipeCommand(scope.MatchScope, 0, new("A"), new("pickup-A"), CookingRecipeOperation.Pickup,
            Item: new ItemId("flow-item"), ExpectedItemVersion: 1);
        var winner = new CommandObservation("compete", "call-A", "pickup-A", "A", frozen, null, null, "Executed", domain, null, "None",
            new(CookingRecipeOutcome.Accepted, CookingRecipeRejectionReason.None, 10, false, new[] { committed }), CallCompletion.Terminal, null);
        var loser = winner with { CallId = "call-B", BusinessId = "pickup-B", ActorId = "B", DomainCommandId = rejectedDomain,
            FrozenCommand = frozen with { Player = new("B"), Command = new("pickup-B") },
            BusinessResult = CookingRecipeCommandResult.Reject(CookingRecipeRejectionReason.ItemStale, 10) };
        var replay = winner with { StepId = "retry-winner", CallId = "call-retry", BusinessResult = winner.BusinessResult! with
            { IsDuplicate = true, Events = Array.Empty<CookingRecipeEvent>() } };
        var initial = eventValue.Observation! with { StateVersion = 9, Items = new[] { item with { Version = 1, LocationKind = "StationSlot", OwnerId = null, SlotId = "source" } },
            Hands = hands.Select(h => h with { ItemId = null }).ToArray() };
        var pickup = initial with { StateVersion = 10, Items = new[] { item }, Hands = hands, CommandEvents = new[] { committed } };
        var cuts = new Dictionary<string, FlowObservation> { ["initial"] = initial, ["pickup"] = pickup, ["retry"] = pickup with { StateVersion = 11 },
            ["initial.client-a"] = observed, ["initial.client-b"] = observed with { ObserverId = "client-b", Fence = observed.Fence with { ConnectionGeneration = 2 } } };
        foreach (var call in new[] { "call-A", "call-retry" }) foreach (var point in new[] { "before", "after" }) cuts["call." + call + "." + point] = observed;
        var evidence = new FlowEvidence(new[] { winner, loser, replay }, cuts, true);
        RuleCheck NetworkEvaluate(string id, FlowEvidence value)
        {
            var approval = Catalog().ResolveApproved(request.Rules, FlowMode.Network).Single(r => r.Rule.Id == id);
            var evaluator = (IFlowRule)Activator.CreateInstance(typeof(FlowRuleEvaluator), BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { approval.Rule, FlowMode.Network }, null)!;
            return evaluator.Evaluate(value, approval);
        }
        Assert.Equal(FlowVerdict.Passed, NetworkEvaluate("C13-IDEMP", evidence).Verdict);
        var convergence = Replace(Replace(evidence, "pickup.client-a", observed), "pickup.client-b", cuts["initial.client-b"]);
        Assert.Equal(FlowVerdict.Passed, NetworkEvaluate("C13-CONVERGE", convergence).Verdict);
        foreach (var invalid in new bool?[] { false, null })
        {
            Assert.Equal(FlowVerdict.Undetermined, NetworkEvaluate("C13-IDEMP", Replace(evidence, "call.call-retry.after",
                observed with { SynchronizedObserved = invalid })).Verdict);
            Assert.Equal(FlowVerdict.Undetermined, NetworkEvaluate("C13-IDEMP", Replace(evidence, "initial.client-a",
                observed with { SynchronizedObserved = invalid })).Verdict);
            Assert.Equal(FlowVerdict.Undetermined, NetworkEvaluate("C13-CONVERGE", Replace(convergence, "pickup.client-b",
                cuts["initial.client-b"] with { SynchronizedObserved = invalid })).Verdict);
            Assert.Equal(FlowVerdict.Undetermined, NetworkEvaluate("C13-CONVERGE", Replace(convergence, "initial.client-a",
                observed with { SynchronizedObserved = invalid })).Verdict);
            Assert.Throws<TargetInvocationException>(() => typeof(NetworkFlowAdapter).GetMethod("ValidateClient", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, new object?[] { observed with { SynchronizedObserved = invalid }, authority, "client-a", observed.Fence }));
        }
        foreach (var variation in new[] { replay with { DomainCommandId = null }, replay with { DomainCommandId = "replacement" },
            replay with { BusinessResult = null }, replay with { Completion = CallCompletion.Unknown }, replay with { NativeDisposition = "Cancelled" },
            replay with { FrozenCommand = frozen with { ExpectedItemVersion = 2 } } })
            Assert.Equal(FlowVerdict.Undetermined, NetworkEvaluate("C13-IDEMP", evidence with { Commands = new[] { winner, loser, variation } }).Verdict);
        Assert.Equal(FlowVerdict.Undetermined, NetworkEvaluate("C13-IDEMP", Replace(evidence, "call.call-retry.after",
            observed with { Fence = observed.Fence with { ConnectionGeneration = 8 } })).Verdict);
        foreach (var variation in new[] { replay with { BusinessResult = replay.BusinessResult! with { StateVersion = 11 } },
            replay with { BusinessResult = replay.BusinessResult! with { Events = new[] { committed } } } })
            Assert.Equal(FlowVerdict.Failed, NetworkEvaluate("C13-IDEMP", evidence with { Commands = new[] { winner, loser, variation } }).Verdict);
        Assert.Equal(FlowVerdict.Failed, NetworkEvaluate("C13-IDEMP", Replace(evidence, "retry", pickup with
        { CommandEvents = new[] { committed, committed with { Sequence = 2 } } })).Verdict);
        Assert.Equal(FlowVerdict.Failed, NetworkEvaluate("C13-IDEMP", Replace(evidence, "retry", pickup with
        { Hands = hands.Select(h => h with { ItemId = null }).ToArray() })).Verdict);
        var falseRejectedEffect = committed with { Command = new(rejectedDomain), Player = new("B"), Sequence = 2 };
        Assert.NotEqual(loser.BusinessId, falseRejectedEffect.Command.Value);
        Assert.Equal(FlowVerdict.Failed, NetworkEvaluate("C13-REJECT", Replace(evidence, "pickup", pickup with
        { CommandEvents = new[] { committed, falseRejectedEffect } })).Verdict);
        Assert.Equal(FlowVerdict.Failed, NetworkEvaluate("C13-REJECT", Replace(Replace(evidence, "call.call-retry.after",
            observed with { SynchronizedObserved = false }), "pickup", pickup with { CommandEvents = new[] { committed, falseRejectedEffect } })).Verdict);
        Assert.Equal(FlowVerdict.Failed, NetworkEvaluate("C13-OWN", Replace(Replace(evidence, "initial.client-b",
            cuts["initial.client-b"] with { SynchronizedObserved = null }), "pickup", pickup with { Hands = hands.Select(h => h with { ItemId = null }).ToArray() })).Verdict);
        Assert.Equal(FlowVerdict.Undetermined, NetworkEvaluate("C13-REJECT", evidence with { Commands = new[] { winner, loser with { DomainCommandId = null }, replay } }).Verdict);
        Assert.Equal(FlowVerdict.Failed, NetworkEvaluate("C13-REJECT", Replace(evidence with
        { Commands = new[] { winner with { BusinessResult = null, Completion = CallCompletion.Unknown }, loser, replay } }, "pickup", pickup with
        { CommandEvents = new[] { committed, falseRejectedEffect } })).Verdict);
    }

    [Fact]
    [Trait("FlowStage", "S2")]
    public async Task Real_child_EOF_protocol_fault_and_sent_command_cancellation_preserve_facts_and_exit_evidence()
    {
        var request = NetworkRequest("compete-one-item", NetworkOutput("native-controls"));
        var run = new RunIdentity(request.RequestId, "native-role-run", "attempt", 1);
        Directory.CreateDirectory(request.OutputRoot);
        foreach (var mode in new[] { "eof", "old-run", "server-arm" })
        {
            var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
                RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { typeof(AbilityKit.Game.Cooking.FlowAcceptance.Program).Assembly.Location, "role", "--id", "server" }) info.ArgumentList.Add(arg);
            using var child = Process.Start(info)!;
            try
            {
            var init = new RoleControl(1, run, "server", 1, "init", RoleControlKind.Initialize,
                new("server", null, null, request.Fixture, request.Budgets, request.Logs), null, null, null, null);
            child.StandardInput.WriteLine(JsonSerializer.Serialize(init, FlowJson.Options)); child.StandardInput.Flush();
            var line = await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
            File.WriteAllText(Path.Combine(request.OutputRoot, mode + "-startup.json"), JsonSerializer.Serialize(new { processId = child.Id, firstLine = line }, FlowJson.Options));
            var ready = FlowJson.Read<RoleReply>(line!); Assert.Equal(RoleReplyKind.Ready, ready.Kind);
            if (mode != "eof")
            {
                var bad = new RoleControl(1, mode == "old-run" ? run with { RunId = "old-run" } : run, "server", 2, "bad",
                    mode == "old-run" ? RoleControlKind.Observe : RoleControlKind.ArmAction, null, null,
                    mode == "server-arm" ? "unknown-call" : null, mode == "server-arm" ? "barrier" : null, mode == "old-run" ? "capture" : null);
                child.StandardInput.WriteLine(JsonSerializer.Serialize(bad, FlowJson.Options)); child.StandardInput.Flush();
            }
            child.StandardInput.Close();
            var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
            using var exitWait = new CancellationTokenSource(10000);
            await child.WaitForExitAsync(exitWait.Token);
            var rest = await stdout; var diagnostic = await stderr;
            Assert.Equal(mode == "eof" ? 1 : 2, child.ExitCode);
            Assert.DoesNotContain("\"kind\":\"Stopped\"", rest);
            File.WriteAllText(Path.Combine(request.OutputRoot, mode + ".json"), JsonSerializer.Serialize(new
            { mode, processId = child.Id, child.ExitCode, ready, stdout = rest, stderr = diagnostic }, FlowJson.Options));
            }
            finally
            {
                if (!child.HasExited)
                {
                    child.StandardInput.Close();
                    using var cleanup = new CancellationTokenSource(1000);
                    try { await child.WaitForExitAsync(cleanup.Token); }
                    catch (OperationCanceledException) { child.Kill(); await child.WaitForExitAsync(); }
                }
            }
        }
        OnOwner(async () => { await ReadyThenDisconnect(request); return 0; });
        OnOwner(async () =>
        {
            var networkRequest = NetworkRequest("compete-one-item", NetworkOutput("cancel-after-send"));
            Directory.CreateDirectory(networkRequest.OutputRoot);
            using var cancelled = new CancellationTokenSource();
            var sink = new CancelOnSentSink(cancelled);
            var adapter = new NetworkFlowAdapter();
            var identity = new RunIdentity(networkRequest.RequestId, "sent-run", "attempt", 1);
            var initial = await adapter.StartAsync(networkRequest, identity, sink, default);
            try
            {
                await Assert.ThrowsAsync<ArgumentException>(async () => await adapter.ReplayAsync("retry", "unknown-call", "new-call", 10000, default));
                var dispatch = await adapter.DispatchGroupAsync("pickup", new[] { new FlowAction("sent-call", "sent-business", "A", FlowVerb.Pickup,
                    "flow-item", 1, null) }, 10000, cancelled.Token);
                Assert.True(cancelled.IsCancellationRequested); Assert.False(dispatch.Complete);
                Assert.DoesNotContain(dispatch.Outcomes, c => c.BusinessResult?.Outcome == CookingRecipeOutcome.Rejected);
                Assert.All(dispatch.Outcomes, c => Assert.NotEqual(CallCompletion.NotAdmitted, c.Completion));
                using var wait = new CancellationTokenSource(1000);
                FlowObservation current;
                do { current = await adapter.CaptureAsync("authority", wait.Token); if (current.Items[0].Version == 2) break; await Task.Delay(5, wait.Token); }
                while (!wait.IsCancellationRequested);
                Assert.Equal(2, current.Items[0].Version); Assert.Equal("A", current.Items[0].OwnerId);
                Assert.Single(current.CommandEvents, e => e.Player.Value == "A");
                // Exact repeated Arm/Release controls cannot execute a sent action a second time.
                var liveRoles = (System.Collections.IEnumerable)typeof(NetworkFlowAdapter).GetField("roles", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(adapter)!;
                var sender = liveRoles.Cast<object>().Single(r => (string)r.GetType().GetField("Id", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(r)! == "client-a");
                var sent = ((System.Collections.IEnumerable)sender.GetType().GetField("Sent", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(sender)!).Cast<RoleControl>().ToArray();
                var arm = sent.Single(c => c.Kind == RoleControlKind.ArmAction);
                var release = sent.Single(c => c.Kind == RoleControlKind.ReleaseBarrier);
                foreach (var repeat in new[] { arm, release })
                {
                    var task = (Task<string>)typeof(NetworkFlowAdapter).GetMethod("Send", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(adapter,
                        new object?[] { sender, repeat.Kind, CancellationToken.None, repeat.ControlId, repeat.Init, repeat.Action, repeat.ReplayOfCallId,
                            repeat.BarrierId, repeat.ObservationPoint })!;
                    await task;
                }
                await adapter.CaptureAsync("client-a", wait.Token); // observes control processing after both duplicate messages.
                var afterControls = await adapter.CaptureAsync("authority", wait.Token);
                Assert.Equal(current.Items, afterControls.Items); Assert.Equal(current.Hands, afterControls.Hands);
                Assert.Equal(current.CommandEvents, afterControls.CommandEvents);
                File.WriteAllText(Path.Combine(networkRequest.OutputRoot, "sent-cancellation-facts.json"), JsonSerializer.Serialize(new { initial, dispatch, current }, FlowJson.Options));
                // An actual child crash after committed effects stops this attempt, without a fabricated Stopped.
                var roleList = (System.Collections.IEnumerable)typeof(NetworkFlowAdapter).GetField("roles", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(adapter)!;
                var crashed = roleList.Cast<object>().Single(r => (string)r.GetType().GetField("Id", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(r)! == "client-b");
                var process = (Process)crashed.GetType().GetField("Process", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(crashed)!;
                process.Kill(); await process.WaitForExitAsync();
                File.WriteAllText(Path.Combine(networkRequest.OutputRoot, "actual-crash-native.json"), JsonSerializer.Serialize(new
                { processId = process.Id, process.ExitCode, knownToTest = true }, FlowJson.Options));
                await Assert.ThrowsAnyAsync<Exception>(async () => await adapter.CaptureAsync("authority", default));
                // Synthetic OS failures are instance-private seams; the actual owned crash above remains separate evidence.
                typeof(NetworkFlowAdapter).GetField("hasExited", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(adapter,
                    new Func<Process, bool>(p => p.Id == process.Id ? throw new IOException("SyntheticExitQuery") : p.HasExited));
                typeof(NetworkFlowAdapter).GetField("terminate", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(adapter,
                    new Action<Process>(p => { if (p.Id == process.Id) throw new IOException("SyntheticOwnedTermination"); p.Kill(); }));
                typeof(NetworkFlowAdapter).GetField("readExitCode", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(adapter,
                    new Func<Process, int>(p => p.Id == process.Id ? throw new IOException("SyntheticExitCode") : p.ExitCode));
            }
            finally
            {
                var closed = await adapter.CloseAsync(10000, default);
                Assert.Equal(CleanupState.Incomplete, closed.State);
                Assert.All(closed.Resources.Where(r => r.RoleId != "client-b"), r => { Assert.True(r.ExitConfirmed); Assert.Null(r.ErrorCode); });
                Assert.Contains(closed.Resources, r => r.RoleId == "client-b" && !r.ExitConfirmed && r.ErrorCode is not null);
                Assert.Contains(closed.Errors, e => e.Contains("StopOrExit:IOException:SyntheticExitQuery"));
                Assert.Contains(closed.Errors, e => e.Contains("ExitQuery:IOException:SyntheticExitQuery"));
                Assert.Contains(closed.Errors, e => e.Contains("OwnedTermination:IOException:SyntheticOwnedTermination"));
                Assert.Contains(closed.Errors, e => e.Contains("UnconfirmedExit:IOException:SyntheticExitCode"));
                using var resources = JsonDocument.Parse(File.ReadAllText(Path.Combine(networkRequest.OutputRoot, "network-resources.json")));
                var children = resources.RootElement.GetProperty("children").EnumerateArray().ToArray();
                Assert.All(children, c => { Assert.True(c.GetProperty("readerComplete").GetBoolean()); Assert.True(c.GetProperty("diagnosticsComplete").GetBoolean()); });
                Assert.All(children.Where(c => c.GetProperty("roleId").GetString() != "client-b"), c =>
                { Assert.True(c.GetProperty("stopped").GetBoolean()); Assert.Equal(0, c.GetProperty("exitCode").GetInt32()); });
                Assert.Equal(JsonValueKind.Null, children.Single(c => c.GetProperty("roleId").GetString() == "client-b").GetProperty("exitCode").ValueKind);
                File.WriteAllText(Path.Combine(networkRequest.OutputRoot, "cleanup-synthetic-os-facts.json"), JsonSerializer.Serialize(closed, FlowJson.Options));
                Assert.Same(closed, await adapter.CloseAsync(10000, default));
            }
            return 0;
        });
    }

    private static async Task ReadyThenDisconnect(FlowRequest request)
    {
        var run = new RunIdentity(request.RequestId, "disconnect-run", "attempt", 1);
        using var authority = new FlowFixture(run).CreateHost();
        var listener = new AbilityKit.Network.Transport.LiteNet.LiteNetChannelListener(System.Net.IPAddress.Loopback, 0, "abilitykit-cooking-v3");
        var accepted = new TaskCompletionSource<AbilityKit.Network.Host.IServerChannel>(TaskCreationOptions.RunContinuationsAsynchronously);
        listener.ChannelAccepted += channel => accepted.TrySetResult(channel);
        using var server = new AbilityKit.Game.Cooking.Session.CookingNetworkSessionHost(new CookingNetworkAuthorityAdapter(authority),
            listener,
            new Dictionary<PlayerId, string> { [new("A")] = "flow-A", [new("B")] = "flow-B" });
        server.Start();
        var endpoint = server.Endpoint;
        var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { typeof(AbilityKit.Game.Cooking.FlowAcceptance.Program).Assembly.Location, "role", "--id", "client-a" }) info.ArgumentList.Add(arg);
        using var child = Process.Start(info)!;
        File.WriteAllText(Path.Combine(request.OutputRoot, "disconnect-child-start.json"), JsonSerializer.Serialize(new
        { processId = child.Id, endpoint, assembly = typeof(AbilityKit.Game.Cooking.FlowAcceptance.Program).Assembly.Location }, FlowJson.Options));
        var diagnostic = child.StandardError.ReadToEndAsync();
        var replies = new List<RoleReply>();
        using var wait = new CancellationTokenSource(10000);
        try
        {
            var initialize = new RoleControl(1, run, "client-a", 1, "init", RoleControlKind.Initialize,
                new("client-a", "A", endpoint, null, request.Budgets, request.Logs), null, null, null, null);
            await child.StandardInput.WriteLineAsync(JsonSerializer.Serialize(initialize, FlowJson.Options));
            await child.StandardInput.FlushAsync(wait.Token);
            var ready = await ReadWhilePumping(); replies.Add(ready);
            Assert.Equal(RoleReplyKind.Ready, ready.Kind);
            Assert.True(ready.Event!.Observation!.Available); Assert.True(ready.Event.Observation.SynchronizedObserved);
            Assert.True(ready.Event.Observation.BaselineSequence > 0);
            var observe = new RoleControl(1, run, "client-a", 2, "before-disconnect", RoleControlKind.Observe, null, null, null, null, "capture");
            await child.StandardInput.WriteLineAsync(JsonSerializer.Serialize(observe, FlowJson.Options)); await child.StandardInput.FlushAsync(wait.Token);
            var historical = await ReadWhilePumping(); replies.Add(historical);
            Assert.Equal(FlowEventKind.StateObserved, historical.Event!.Kind); Assert.True(historical.Event.Observation!.SynchronizedObserved);
            Assert.False(child.HasExited);
            File.WriteAllText(Path.Combine(request.OutputRoot, "disconnect-before.json"), JsonSerializer.Serialize(new { ready, historical }, FlowJson.Options));
            // Close the real connection owned by this test server. Keep its listener alive to deliver the disconnect packet.
            // Neither the external client process nor its control pipe is stopped by the test.
            (await accepted.Task.WaitAsync(wait.Token)).Close();
            var aliveAtDisconnect = !child.HasExited;
            string? observeWriteError = null;
            try
            {
                await child.StandardInput.WriteLineAsync(JsonSerializer.Serialize(observe with
                { ControlSequence = 3, ControlId = "after-disconnect" }, FlowJson.Options));
                await child.StandardInput.FlushAsync(wait.Token);
            }
            catch (IOException e) { observeWriteError = e.Message; }
            var later = new List<RoleReply>();
            while (true)
            {
                var line = await child.StandardOutput.ReadLineAsync().WaitAsync(wait.Token);
                Assert.NotNull(line);
                var reply = FlowJson.Read<RoleReply>(line); later.Add(reply);
                if (reply.Kind == RoleReplyKind.Fault) break;
            }
            // Observe the invalid-session fault while both process and control pipe are still alive.
            var aliveAtFault = !child.HasExited;
            Assert.True(aliveAtFault);
            child.StandardInput.Close(); // Only after the fault, allow the owned pipe reader to finish cleanup.
            var tailTask = child.StandardOutput.ReadToEndAsync();
            await child.WaitForExitAsync(wait.Token);
            var tail = await tailTask.WaitAsync(wait.Token); var stderr = await diagnostic.WaitAsync(wait.Token);
            later.AddRange(tail.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => FlowJson.Read<RoleReply>(line.TrimEnd('\r'))));
            replies.AddRange(later);
            // A cut received before the asynchronous disconnect callback is history; the latched fault ends current evidence/actions.
            var fault = Assert.Single(later, r => r.Kind == RoleReplyKind.Fault);
            Assert.Contains("RoleClientSessionInvalid", fault.ErrorCode!);
            Assert.Equal(1, child.ExitCode); Assert.DoesNotContain(later, r => r.Kind == RoleReplyKind.Stopped);
            Assert.DoesNotContain(replies.SkipWhile(r => r != fault).Skip(1), r => r.Event?.Observation?.Available == true || r.Kind == RoleReplyKind.Armed);
            File.WriteAllText(Path.Combine(request.OutputRoot, "ready-then-disconnect.json"), JsonSerializer.Serialize(new
            { run, parentProcessId = Environment.ProcessId, processId = child.Id, endpoint,
                aliveAtDisconnect, aliveAtFault, controlPipeClosedAfterFault = true, child.ExitCode, historical, replies, observeWriteError, stderr }, FlowJson.Options));
        }
        finally
        {
            if (!child.HasExited)
            {
                child.StandardInput.Close();
                using var cleanup = new CancellationTokenSource(1000);
                try { await child.WaitForExitAsync(cleanup.Token); }
                catch (OperationCanceledException) { child.Kill(); await child.WaitForExitAsync(); }
            }
        }

        async Task<RoleReply> ReadWhilePumping()
        {
            var line = child.StandardOutput.ReadLineAsync();
            while (!line.IsCompleted) { server.ProcessOwnerFrame(); await Task.Delay(5, wait.Token); }
            return FlowJson.Read<RoleReply>((await line.WaitAsync(wait.Token))!);
        }
    }

    private static object? InvokeProtocol(string method, params object?[] args) => typeof(NetworkFlowAdapter).Assembly
        .GetType("AbilityKit.Game.Cooking.FlowAcceptance.FlowRoleProtocol")!.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);
    private sealed class CancelOnSentSink(CancellationTokenSource cancelled) : IFlowEventSink
    {
        public bool EvidenceComplete => true;
        public bool TryPublish(FlowEvent value)
        { if (value.Kind == FlowEventKind.StepStarted && value.HostId == "client-a") cancelled.Cancel(); return true; }
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
    private sealed class BrokenReport(string mode, CancellationTokenSource? caller = null) : IFlowReportWriter
    {
        public async Task PublishAsync(string directory, FlowResult result, FailurePack? failure, CancellationToken token)
        {
            if (mode == "write") throw new IOException("Synthetic report write failure");
            if (mode == "cancel")
            {
                await new FlowReport().PublishAsync(directory, result, failure, token);
                caller!.Cancel(); // Deterministic cancellation after marker write, before orchestrator readback acceptance.
                return;
            }
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
