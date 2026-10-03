using System.Text.Json;
using AbilityKit.Game.Cooking.RichEvidence;
using AbilityKit.Game.Cooking.Session;
namespace AbilityKit.Game.Cooking.NetworkPairVerifier;

internal static class RichPairProof
{
    public static string Verify(JsonElement hostJson, JsonElement clientJson, string runId, string caseId)
    {
        var host = hostJson.Deserialize<RichEndpointReport>(CookingNetworkWireCodec.JsonOptions)!;
        var client = clientJson.Deserialize<RichEndpointReport>(CookingNetworkWireCodec.JsonOptions)!;
        foreach (var report in new[] { host, client }) {
            Check.That(report.SchemaVersion == 1 && report.Suite == "rich-recovery-four-cutpoints-v1" && report.RunId == runId && report.CaseId == caseId && report.Passed && report.Failure is null, "Supported successful rich case/run evidence.");
            Check.That(report.Protocol == 3 && report.LevelFormat == CookingLevelCheckpointCodec.CurrentFormatVersion && report.RecipeSchema == 5, "Supported protocol/checkpoint/schema.");
            Check.That(report.Phases.Count <= 64 && report.Cut is { InjectedExactlyOnce: true } && report.Cut.Snapshots.Count <= 64 && report.Cut.CallerSnapshots.Count <= 32 && report.CallerCommands.Count <= 8192 && report.CallerOutcomes.Count <= 8192 && report.ObservedIngress.Count <= 65536 && report.ObservedReplies.Count <= 8192 && report.ObservedIssued.Count <= 65536 && report.ObservedReady.Count <= 65536 && report.ObservedClose.Count <= 8 && report.CallerReady.Count <= 65536, "Explicit retained evidence count bounds.");
            Check.That(report.Budget is { WholeMs: 600000, InitialMs: 20000, OperationMs: 30000, CutMs: 15000, OwnerDelayMs: 10 } && report.Budget.ElapsedMs <= 600000, "Fixed actual rich budgets.");
            Check.That(report.Provenance.EtMvid != Guid.Empty && report.Provenance.SessionMvid != Guid.Empty && report.Provenance.WireCodecMvid != Guid.Empty, "Real declaring assembly MVIDs.");
            Check.That(report.Provenance.RolePolicyHash == StateProof.TextHash(JsonSerializer.Serialize(report.Roles, CookingNetworkWireCodec.JsonOptions)), "Actual role adaptation identity.");
            foreach (var phase in report.Phases) { Check.That(phase.Capture is not null, "Full phase capture missing."); StateProof.Capture(phase.Capture!); StateProof.Compatibility(phase.Capture!.Capture, report.ContainerRules); if (phase.Caller is not null) StateProof.Caller(phase.Caller); }
            foreach (var capture in report.Cut!.Snapshots) { StateProof.Capture(capture); StateProof.Compatibility(capture.Capture, report.ContainerRules); }
            foreach (var caller in report.Cut.CallerSnapshots) StateProof.Caller(caller);
            Check.That(report.Cut.RecoveredGeneration == report.Cut.OriginalGeneration + 1 && report.Cut.ServerInstance == report.Final?.ServerInstance, "Actual same-instance/new-generation recovery.");
            Check.That(report.Provenance.Files.Single(x => x.RelativePath == "sources/CookingRichRecoveryFixture.cs").Sha256 == report.Provenance.LinkedFixtureHash && report.Provenance.Files.Single(x => x.RelativePath == "sources/CookingRichRecoveryPlanner.cs").Sha256 == report.Provenance.LinkedPlannerHash, "Linked compiled source manifests.");
            foreach (var command in report.CallerCommands) Check.That(JsonSerializer.SerializeToUtf8Bytes(command.Wire, CookingNetworkWireCodec.JsonOptions).Length <= 16384, "Individual command16KiB bound.");
        }
        Check.That(host.Role == "host" && client.Role == "client" && host.Process.Pid != client.Process.Pid || host.Role == "host" && client.Role == "client" && host.Topology == "SeparateHostsRequiresPairedEvidence", "Distinct endpoint roles; physical PID namespaces permitted only declared separate topology.");
        Check.Equal(host.Provenance with { OS = "endpoint-local", Runtime = "endpoint-local" }, client.Provenance with { OS = "endpoint-local", Runtime = "endpoint-local" }, "Identical full frozen binary/helper/config/role provenance."); Check.Equal(host.ContainerRules, client.ContainerRules, "Actual frozen configured container rule identity."); Check.Equal(host.Roles, client.Roles, "Identical explicit role policy.");
        var hc = host.Cut!; var cc = client.Cut!;
        Check.That(hc.CaseId == caseId && cc.CaseId == caseId && hc.Close is not null && hc.OriginalWire is not null && hc.OriginalReply is not null && cc.OriginalWire is not null, "Real close plus genuine original wire/result evidence.");
        Check.Equal(hc.OriginalWire, cc.OriginalWire, "Original real sent/observed command identity.");
        var correlation = hc.OriginalReply!.Correlation;
        var ingress = host.ObservedIngress.Single(x => x.Correlation == correlation && x.Command is not null);
        Check.Equal(ingress.Command, cc.OriginalWire, "Current physical channel delivery equals caller wire.");
        Check.That(hc.Close!.Channel == ingress.Channel && hc.Frames.Any(x => x.Frame.Admissions.Any(a => a.CorrelationId == correlation && a.Accepted) && x.Frame.Dispositions.Any(d => d.CorrelationId == correlation && d.Result?.Outcome == CookingRecipeOutcome.Accepted)), "Genuine actual admission/disposition, not observer-only readiness.");
        var committed = hc.Snapshots.Single(x => x.Id == "committed-cut");
        var atCaller = cc.Snapshots.Single(x => x.Id is "client-cut" or "client-committed-lost-submit");
        Check.That(atCaller.BusinessHash == committed.BusinessHash, "Caller received actual complete committed cut image.");
        Check.That(hc.PreCutIssued is not null && hc.PreCutAck is not null && hc.PreCutAck.Channel == ingress.Channel && hc.PreCutAck.Ack == hc.PreCutIssued.Identity && hc.PreCutAck.Timestamp <= committed.Timestamp && hc.CommittedIssued?.BusinessHash == committed.BusinessHash, "Actual exact issued ACK before consumption and committed full-image publication.");
        Check.That(cc.CommittedCallerImage is not null, "Retained actual committed caller image required independently of latest Paused image."); StateProof.Baseline(cc.CommittedCallerImage!);
        Check.Equal(hc.CommittedIssued!.Identity, cc.CommittedCallerImage!.Identity, "Caller received genuine published cut identity.");
        if (caseId == "manual-paused") Manual(host, client, committed);
        else {
            var held = hc.Snapshots.Single(x => x.Id == "held-close");
            Check.That(hc.OwnerHoldFrames == 0 && held.BusinessHash == committed.BusinessHash && held.Capture.Observation.HostFrameSequence == committed.Capture.Observation.HostFrameSequence, "Zero-owner-frame hold while real close occurs.");
            if (caseId == "automatic-active") {
                var process = committed.Capture.FullRecipe!.Processes.Single(x => x.Id == hc.Process);
                Check.That(process.RequiredTicks == 6 && process.ElapsedTicks < process.RequiredTicks && process.ActiveWorker is null && hc.OriginalWire!.Command.Operation == CookingRecipeOperation.StartProcess, "Actual incomplete unattended6tick start.");
                var endedRecipe = host.Ended?.State.Capture.FullRecipe ?? throw new InvalidOperationException("Complete automatic completion history missing.");
                var ticks = endedRecipe.TickEvents.SelectMany(x => x.Processes).Where(x => x.Process == process.Id).ToArray();
                var completion = ticks.Single(x => x.Completed);
                Check.That(ticks.Length > 0 && ticks.All(x => x.Recipe == process.Recipe && x.Anchor == process.Anchor && x.RequiredTicks == 6 && x.AfterElapsedTicks > x.BeforeElapsedTicks) && completion.AfterElapsedTicks >= 6 && endedRecipe.Processes.All(x => x.Id != process.Id), "Same automatic process completes exactly once through genuine tick history.");
                for (var i = 1; i < ticks.Length; i++) Check.That(ticks[i].BeforeElapsedTicks == ticks[i - 1].AfterElapsedTicks && !ticks[i - 1].Completed, "Automatic tick progress is continuous and never advances after completion.");
                Check.That(ticks[0].BeforeElapsedTicks == 0 && ticks.Any(x => x.AfterElapsedTicks >= process.ElapsedTicks), "Retained history covers original committed progress.");
                if (process.Completion == CookingRecipeCompletionKind.ConsumeInputs) {
                    Check.That(completion.Product is not null, "Consumed-input completion retains actual allocated product identity.");
                    var product = endedRecipe.Items.Single(x => x.Id == completion.Product);
                    Check.That(product.IsProduct && product.Recipe == process.Recipe && product.AllocationSequence > committed.Capture.FullRecipe.NextProductId && endedRecipe.NextProductId >= product.AllocationSequence && process.LockedInputs.Where(id => process.Container is null || id != process.Anchor).All(id => endedRecipe.Items.Single(x => x.Id == id).Removed), "Automatic completion conserves input tombstones and genuine product allocator.");
                } else Check.That(completion.Product is null && endedRecipe.Items.Single(x => x.Id == process.Anchor).Recipe == process.Recipe, "Retained-input automatic completion keeps its actual anchor identity.");
            } else if (caseId == "unbound-cup") {
                var product = committed.Capture.FullRecipe!.Items.Single(x => x.Id == hc.Item);
                Check.That(product.IsProduct && !product.Removed && product.BoundOrder is null && product.Location.Kind == LocationKind.ContainerSlot && product.Location.OwnerId == hc.Container?.Value, "Real complete unbound cup content.");
                var rebound = hc.Snapshots.Single(x => x.Id == "rebound-current-generation");
                Check.Equal(product, rebound.Capture.FullRecipe!.Items.Single(x => x.Id == product.Id), "Cup item/content/version preserved across pure connection transfer.");
            } else if (caseId == "submitted-reply-lost") Lost(host, client, committed);
            else throw new InvalidOperationException("Unsupported cutpoint.");
        }
        Check.That(host.Ended is { Cleared: true, Deliveries: 2, Unmet: 1, Stars: 0 } && host.Ended.EndControl.Accepted, "Actual Host end action.");
        StateProof.Capture(host.Ended!.State); StateProof.Natural(host.Ended.State.Capture);
        var clientEnded = client.Phases.Single(x => x.Name == "ended-natural").Capture!; StateProof.Natural(clientEnded.Capture);
        Check.That(clientEnded.BusinessHash == host.Ended.State.BusinessHash, "Full ended caller/Host consensus.");
        var next = host.Successor ?? throw new InvalidOperationException("Actual durable successor missing.");
        Check.That(next.ActualReadAccepted && next.Transition.Accepted && next.StoreFiles.Count > 0 && next.Target.LevelEpoch == 2 && next.Source == host.Ended.State.Capture.Observation.Scope, "Actual preserved store/read/successor metadata.");
        StateProof.Capture(next.State);
        var payload = next.SavedPayload.Deserialize<CookingMajorBaselinePayload>(CookingNetworkWireCodec.JsonOptions)!; var saved = payload.Kitchen;
        Check.That(payload.SourceScope == next.Source && payload.TargetScope == next.Target && next.Target.MatchScope == next.Source.MatchScope && next.Target.RestaurantRuntime == next.Source.RestaurantRuntime && next.Target.LevelEpoch == next.Source.LevelEpoch + 1 && payload.HostFrameSequence == host.Ended.State.Capture.Observation.HostFrameSequence, "Exact durable source/target epoch/match/restaurant/owner timebase.");
        Check.Equal(payload.Preparation, next.State.Capture.Preparation, "Saved trusted target preparation."); Check.Equal(payload.InstalledLayout, next.State.Capture.InstalledLayout, "Saved actual target layout/geometry.");
        Check.That(payload.ConfigIdentity == next.State.Capture.ConfigurationIdentity && payload.FrontConfigurationIdentity == next.State.Capture.FrontConfigurationIdentity && payload.PreparationConfigurationIdentity == next.State.Capture.PreparationConfigurationIdentity && payload.MenuPolicyIdentity == next.State.Capture.MenuConfigurationIdentity && payload.Choices.Locked && next.State.Capture.MajorProgress?.Locked == true, "Actual config/menu/preparation/front/confirmed major policy.");
        Check.That(saved.LevelScope is null && saved.StateVersion == 0 && saved.LogicalTick == 0 && saved.EventSequence == 0 && saved.NextSettlementSequence == 0 && saved.Orders.Count == 0 && saved.Settlements.Count == 0 && saved.Deduplication.Count == 0 && saved.Events.Count == 0 && saved.TickEvents.Count == 0 && saved.Processes.All(x => x.ActiveWorker is null), "Explicit excluded old-scope orders/settlements/receipts/events/clocks/workers.");
        var old = host.Ended.State.Capture.FullRecipe!;
        var expected = old with { Items = old.Items.Select(x => x.BoundOrder is null ? x : x with { BoundOrder = null, Version = checked(x.Version + 1) }).ToArray(), Processes = old.Processes.Select(x => x with { ActiveWorker = null }).ToArray(), StateVersion=0, LogicalTick=0, EventSequence=0, NextSettlementSequence=0, Orders=Array.Empty<CookingRecipeCheckpointOrder>(),Settlements=Array.Empty<CookingOrderSettlement>(),Deduplication=Array.Empty<CookingRecipeCheckpointDeduplication>(),Events=Array.Empty<CookingRecipeEvent>(),TickEvents=Array.Empty<CookingRecipeTickEvent>(),LevelScope=null,Poses=payload.InstalledLayout!.GeometrySeedPoses };
        Check.Equal(expected, saved, "Complete included physical kitchen/containers/tombstones/allocators/consumed/supply carry and explicit geometry pose reseed.");
        Check.Equal(saved with { LevelScope = next.Target }, next.State.Capture.FullRecipe, "Created authoritative kitchen equals actual durable narrowed payload with new scope bound.");
        Check.That(next.State.Capture.FullFront!.State.ServiceTicks == 0 && next.State.Capture.FullFront.State.Customers.Count == 0 && next.State.Capture.FullFront.State.UnsatisfiedOrders.Count == 0 && next.State.Capture.FullFront.State.Tables.All(x => x.State == CookingFrontTableState.Free), "Excluded prior front customers/orders/clock; actual fresh target front.");
        Check.That(saved.NextProductId == host.Ended.State.Capture.FullRecipe!.NextProductId, "Durable allocator carry."); Check.Equal(saved.Supply, host.Ended.State.Capture.FullRecipe.Supply, "Durable finite supply carry.");
        var hf = host.Final ?? throw new InvalidOperationException("Missing Host final proof."); var cf = client.Final ?? throw new InvalidOperationException("Missing Client final proof.");
        var finalClose = hf.CurrentClose ?? throw new InvalidOperationException("Missing actual final current-channel close.");
        var originalClose = hc.Close ?? throw new InvalidOperationException("Missing original cut channel close.");
        StateProof.Baseline(hf.LocalBaseline); StateProof.Baseline(cf.LocalBaseline);
        Check.That(hf.ExactCurrentReady && cf.ExactCurrentReady && hf.LiveHoldMs == 5000 && cf.LiveHoldMs == 5000 && hf.CurrentClose is not null && finalClose.Channel != originalClose.Channel && hf.CurrentChannelLive && cf.CurrentChannelLive && finalClose.Timestamp > hf.FrozenTimestamp, "Actual final current-channel close following live report hold.");
        Check.That(hf.PreCloseProjection.Participants.Count == 2 && hf.PreCloseProjection.Participants.All(x => x.ConnectedOwnerBinding && x.Ready && !x.CleanupPending) && cf.PreCloseProjection.Participants.All(x => x.ConnectedOwnerBinding && x.Ready && !x.CleanupPending), "Immutable paired pre-close both Ready/no-cleanup.");
        Check.That(hf.BusinessHash == cf.BusinessHash && hf.BusinessHash == CookingNetworkWireCodec.Hash(hf.LocalBaseline.State) && cf.BusinessHash == CookingNetworkWireCodec.Hash(cf.LocalBaseline.State) && hf.ServerInstance == cf.ServerInstance && hf.Scope == cf.Scope, "Deep full final successor consensus.");
        Check.Equal(hf.RemoteIssued, cf.LocalBaseline.Identity, "Actual newest issued remote image equals Client complete baseline."); Check.Equal(hf.RemoteIssued, hf.RemoteAck, "Exact final ACK."); Check.Equal(hf.RemoteIssued, hf.RemoteReady, "Exact actual final Ready."); Check.Equal(hf.RemoteIssued, cf.RemoteReady, "Caller actual Ready.");
        Check.That(host.ObservedIssued.Last(x => x.Channel == finalClose.Channel).Identity == hf.RemoteIssued && host.ObservedReady.Any(x => x.Channel == finalClose.Channel && x.Identity == hf.RemoteReady) && host.ObservedClose.Any(x => x == hf.CurrentClose) && client.CallerReady.Contains(cf.RemoteReady) && host.ObservedIngress.Any(x => x.Channel == finalClose.Channel && x.Ack == hf.RemoteIssued), "Actual final same-channel exact ACK observed.");
        return hf.ServerInstance;
    }
    private static void Manual(RichEndpointReport host, RichEndpointReport client, RichCapture committed)
    {
        var cut = host.Cut!;
        Check.That(cut.LifecycleAtCut == CookingLevelState.Running && cut.Controls.Count == 2 && cut.Controls[0].Accepted && cut.Controls[1].Accepted && host.Roles.OriginalWorker == cut.OriginalParticipant && host.Roles.TakeoverWorker == cut.TakeoverParticipant, "Running genuine Pause/Resume and explicit original/takeover roles.");
        var original = committed.Capture.FullRecipe!.Processes.Single(x => x.Id == cut.Process);
        Check.That(original.ActiveWorker == cut.OriginalParticipant && original.ElapsedTicks == cut.ManualElapsed && original.RequiredTicks == 6, "Original actual manual process worker/progress.");
        var paused = cut.Snapshots.Single(x => x.Id == "paused-cut");
        foreach (var state in cut.Snapshots.Where(x => x.Id.StartsWith("paused-owner-"))) Check.That(state.BusinessHash == paused.BusinessHash && state.Capture.Observation.HostFrameSequence == paused.Capture.Observation.HostFrameSequence, "Paused complete graph/frame invariant.");
        Check.That(cut.Snapshots.Count(x => x.Id.StartsWith("paused-owner-")) == 2, "Two actual paused owner calls.");
        var released = cut.Snapshots.Single(x => x.Id == "resumed-cleanup").Capture.FullRecipe!.Processes.Single(x => x.Id == original.Id);
        Check.That(released.ActiveWorker is null && released.ElapsedTicks == original.ElapsedTicks && released.Anchor == original.Anchor, "Deferred cleanup only after Resume; same process/progress."); Check.Equal(released.LockedInputs, original.LockedInputs, "Manual locked inputs preserved.");
        Check.That(cut.PausedPendingProjection?.Participants.Single(x => x.Participant == cut.OriginalParticipant).CleanupPending == true, "Actual deferred pending cleanup while Paused.");
        var continued = host.CallerCommands.Single(x => x.Wire.Command.Operation == CookingRecipeOperation.ContinueProcess && x.Wire.Command.Process == original.Id && x.Wire.Command.Player == host.Roles.TakeoverWorker);
        Check.That(host.CallerOutcomes.Single(x => x.Correlation == continued.Correlation).Result.Result?.Outcome == CookingRecipeOutcome.Accepted && cut.Frames.Any(x => x.Frame.Admissions.Any(a => a.CorrelationId == continued.Correlation && a.Accepted) && x.Frame.Dispositions.Any(d => d.CorrelationId == continued.Correlation && d.Result?.Outcome == CookingRecipeOutcome.Accepted)), "Real Chef ContinueProcess same identity admitted and executed.");
        Check.That(!cut.Snapshots.Single(x => x.Id == "chef-takeover-completed").Capture.FullRecipe!.Processes.Any(x => x.Id == original.Id), "Original process actually completed.");
    }
    private static void Lost(RichEndpointReport host, RichEndpointReport client, RichCapture committed)
    {
        var cut = client.Cut!; var original = host.Cut!.OriginalReply!.Result;
        Check.That(cut.Dropped is { Count: 1, OriginalWaiterCompleted: false } && !client.CallerOutcomes.Any(x => x.Correlation == cut.Dropped.Correlation) && cut.RetryWire is not null && cut.RetryOutcome is not null, "Exactly one genuine discarded matching result; original application waiter unresolved.");
        Check.Equal(original, cut.Dropped!.Diagnostic, "Discard diagnostic equals actual Host outbound result.");
        Check.That(committed.Capture.FullRecipe!.Settlements.Count == 1 && committed.Capture.FullRecipe.ConsumedProducts.Count > 0 && committed.Capture.FullRecipe.Items.Any(x => x.Removed), "FIRST actual Submit settlement/consumed objects/tombstones.");
        Check.Equal(cut.OriginalWire!.Command, cut.RetryWire!.Wire.Command, "Exact original domain payload retried.");
        Check.That(cut.RetryWire.Wire.StableCommandId == cut.OriginalWire.StableCommandId && cut.RetryWire.Wire.ConnectionGeneration == cut.RecoveredGeneration && cut.RetryWire.Wire.ClientSequence > 0 && cut.RetryOutcome!.Result.DomainCommandId == original.DomainCommandId && cut.RetryOutcome.Result.Result is { IsDuplicate: true, Outcome: CookingRecipeOutcome.Accepted } && cut.RetryOutcome.Result.Result.StateVersion == original.Result!.StateVersion && cut.RetryOutcome.Result.Result.Reason == original.Result.Reason && cut.RetryOutcome.Result.Result.Events.Count == 0, "Current-generation real cached accepted original identity/state version.");
        Check.Equal(cut.RetryOutcome!.Result.Result!.Supply, original.Result!.Supply, "Cached original supply result preserved.");
        Check.Equal(host.ObservedReplies.Single(x => x.Correlation == cut.RetryWire!.Correlation).Result, cut.RetryOutcome.Result, "Actual retry outbound/received cached terminal correlation.");
        var after = cut.Snapshots.Single(x => x.Id == "client-cached-submit-after").Capture.FullRecipe!;
        Check.That(StateProof.Physical(committed.Capture.FullRecipe) == StateProof.Physical(after), "Full Submit replay allocator/items/tombstones/settlements preservation."); Check.Equal(committed.Capture.FullRecipe.Deduplication, after.Deduplication, "Original receipts retained without rewrite.");
    }
}
