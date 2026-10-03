using System.Diagnostics;
using System.Net;
using System.Text.Json;
using AbilityKit.ET.Runtime.Tests;
using AbilityKit.Game.Cooking.EtRuntime;
using AbilityKit.Game.Cooking.RichEvidence;
using AbilityKit.Game.Cooking.Session;
using AbilityKit.Network.Transport.LiteNet;
using AbilityKit.Network.Protocol;
namespace AbilityKit.Game.Cooking.NetworkRichRecoveryAcceptance;

internal sealed class RichRunner(string role, string caseId, string runId, string address, int port, string topology, string source, string dirty, string reportPath, RichDiagnosticOptions? diagnosticOptions = null)
{
    private readonly RichDiagnosticOptions _diagnosticOptions = diagnosticOptions ?? new(false, null, null);
    private readonly RichCommandPathDiagnostics? _diagnostics = RichCommandPathDiagnostics.Create(diagnosticOptions);
    private long? _resultWaitStart, _projectionWaitStart, _flightSendBefore, _flightSendAfter;
    private readonly CookingRichRecoveryFixture _fixture = new();
    private readonly long _start = Environment.TickCount64, _deadline = Environment.TickCount64 + 600000;
    private readonly List<RichPhase> _phases = new();
    private readonly List<RichCapture> _cutSnapshots = new();
    private readonly List<RichCallerCheckpoint> _cutCallers = new();
    private readonly List<RichConsumption> _cutFrames = new();
    private readonly List<CookingNetworkControlResult> _controls = new();
    private readonly HashSet<string> _consumed = new();
    private long _frames, _commands;
    private string? _flightCorrelation; private long? _flightTargetVersion; private RichFailureDiagnostic? _failureDiagnostic;
    private string _stage = "initial";
    private bool _injected, _clientCut;
    private ProcessId? _process; private ItemId? _item, _container; private long? _manualElapsed;
    private long _originalGeneration, _recoveredGeneration;
    private CookingLevelState _cutLifecycle;
    private RichPeer? _peer;
    private RichObserver? _observer;
    private CookingLevelEtHost? _host;
    private CookingNetworkSessionHost? _session;
    private CookingRichRecoveryPlanner? _planner;
    private RichEnded? _ended; private RichSuccessor? _successor; private RichFinal? _final;
    private RichCallerCommand? _retryWire; private RichCallerOutcome? _retryOutcome;
    private CookingNetworkWireCommand? _originalWire; private RichWireReply? _originalReply;
    private RichClose? _close;
    private CookingNetworkBaseline? _committedCallerImage;
    private CookingNetworkSessionProjection? _pausedPending;
    private RichIssued? _preCutIssued, _committedIssued; private RichWireInput? _preCutAck;
    private readonly DateTimeOffset _processStart = DateTimeOffset.UtcNow;
    private RichRolePolicy Roles => new(CookingRichRecoveryFixture.Partner, CookingRichRecoveryFixture.Chef, CookingRichRecoveryFixture.Partner, caseId == "submitted-reply-lost" ? CookingRichRecoveryFixture.Partner : CookingRichRecoveryFixture.Chef,
        caseId == "manual-paused" ? CookingRichRecoveryFixture.Partner : CookingRichRecoveryFixture.Chef,
        caseId is "manual-paused" or "submitted-reply-lost" ? CookingRichRecoveryFixture.Chef : CookingRichRecoveryFixture.Partner);
    private CookingNetworkAuthorityCapture State => _session?.LatestCapture ?? _peer!.Baseline.State;
    private void Check() { RichProof.Require(Environment.TickCount64 < _deadline, "Whole600s budget: " + _stage); _peer?.Poll(); if (_observer?.Failure is { } failure) throw new InvalidOperationException("Real transport/observer failure", failure); }
    private async Task Wait(Func<bool> predicate, string stage, bool pump = true, int ms = 30000)
    {
        _stage = stage; var until = Math.Min(_deadline, Environment.TickCount64 + ms);
        var wait = _diagnostics?.BeginWait(stage, _peerPlayer.Value, until, _deadline, _flightCorrelation,
            _peer?.Sent.LastOrDefault(x => x.Correlation == _flightCorrelation), _flightSendBefore,
            _flightSendAfter, _resultWaitStart, _projectionWaitStart, _flightTargetVersion, _peer?.DiagnosticState());
        bool? observedPredicate = null; var status = "ACTIVE";
        try {
            while (Environment.TickCount64 < until) {
                Check(); observedPredicate = predicate(); if (observedPredicate.Value) { status = "SUCCESS"; return; }
                if (pump && _session is not null) await Pump(); else await Task.Delay(2);
            }
            Check(); observedPredicate = predicate(); status = observedPredicate.Value ? "SUCCESS" : "OPERATION_EXPIRED";
            RichProof.Require(observedPredicate.Value, "Operation deadline: " + stage);
        } catch {
            if (Environment.TickCount64 >= _deadline) status = "WHOLE_EXPIRED";
            else if (status == "ACTIVE") status = _peer?.Failure is not null ? "CALLBACK_ERROR" : "EXCEPTION";
            throw;
        } finally {
            if (wait is { } id) _diagnostics!.EndWait(id, status, observedPredicate, _flightTargetVersion, _peer?.DiagnosticState());
        }
    }
    private Task StageWait(Func<bool> predicate, string stage, bool pump = true) => Wait(predicate, stage, pump, checked((int)Math.Max(1, _deadline - Environment.TickCount64)));
    private RichCapture Save(string label, CookingNetworkAuthorityCapture? actual = null) { var capture = RichProof.Capture(label, actual ?? State); _cutSnapshots.Add(capture); return capture; }
    private void Phase(string name, string? marker = null)
    {
        Check(); _stage = name;
        _phases.Add(new(_phases.Count + 1, name, Stopwatch.GetTimestamp(), marker, marker is null ? null : CookingNetworkWireCodec.DomainId(_peer!.Binding.ServerSessionInstance, State.Observation.Scope, _peerPlayer, marker).Value,
            RichProof.Capture("phase-" + name, State), _peer!.CurrentBusinessGrant is null ? null : _peer.Checkpoint("phase-" + name)));
        Console.WriteLine($"PHASE {name} commands={_commands} frames={_frames}"); Console.Out.Flush();
    }
    private PlayerId _peerPlayer => role == "host" ? CookingRichRecoveryFixture.Chef : CookingRichRecoveryFixture.Partner;
    private bool Marker(PlayerId player, string stable, CookingNetworkAuthorityCapture? state = null)
    {
        var s = state ?? State;
        var id = CookingNetworkWireCodec.DomainId(_peer!.Binding.ServerSessionInstance, s.Observation.Scope, player, stable);
        return s.FullRecipe!.Deduplication.Any(x => x.Player == player && x.Command == id && x.Outcome == CookingRecipeOutcome.Accepted);
    }
    private async Task Mark(string stable)
    {
        var pose = _peer!.Baseline.State.FullRecipe!.Poses!.Single(x => x.Player == _peerPlayer);
        await Send(new(State.Observation.Scope.MatchScope, 0, _peerPlayer, new(stable), CookingRecipeOperation.Move, FacingX: pose.FacingX, FacingY: pose.FacingY)); Phase(stable, stable);
    }
    private CookingRichRecoveryPlanner Planner() => new(_fixture, _peerPlayer, () => _peer!.Baseline.State, Send,
        async () => { Check(); if (_session is not null) await Pump(); else await Task.Delay(2); }, async commands => {
            var results = new List<CookingRecipeCommandResult>(); foreach (var c in commands) results.Add(await Send(c)); return results;
        });
    private bool RelevantStart(RichWireInput input)
    {
        var wire = input.Command;
        if (_injected || wire is null || wire.Command.Player != CookingRichRecoveryFixture.Partner || _consumed.Contains(input.Correlation)) return false;
        if (caseId == "submitted-reply-lost") return wire.Command.Operation == CookingRecipeOperation.SubmitOrder;
        if (caseId == "unbound-cup") return wire.Command.Container == _fixture.ServingVessels["D31"] && wire.Command.Operation is CookingRecipeOperation.PutIn or CookingRecipeOperation.ServePortion;
        if (wire.Command.Operation != CookingRecipeOperation.StartProcess || wire.Command.Recipe is null) return false;
        var recipe = _fixture.Content.Recipes[wire.Command.Recipe.Value];
        return caseId == "automatic-active" && recipe.Execution == CookingRecipeExecutionKind.Automatic || caseId == "manual-paused" && State.Observation.Lifecycle.State == CookingLevelState.Running && recipe.Execution == CookingRecipeExecutionKind.Manual;
    }
    private async Task Pump()
    {
        Check();
        var candidate = _observer!.Received.FirstOrDefault(RelevantStart);
        if (candidate is not null) {
            // No frames while waiting for ACK of the actual current issue; remote Poll remains live.
            var issued = _observer.Images.Last(x => x.Channel == candidate.Channel);
            await Wait(() => _observer.Received.Any(x => x.Channel == candidate.Channel && x.Ack == issued.Identity), "cut-exact-current-ack", false, 15000);
            _preCutIssued = issued; _preCutAck = _observer.Received.Last(x => x.Channel == candidate.Channel && x.Ack == issued.Identity);
        }
        var before = RichProof.Capture("owner-before-" + _frames, State);
        if (candidate is not null) _cutSnapshots.Add(before);
        var diagnosticBefore = _diagnostics?.Active == true ? DiagnosticFrameBefore() : null;
        CookingNetworkOwnerFrameResult? frame;
        try { frame = _session!.ProcessOwnerFrame(); }
        catch (Exception e) { if (diagnosticBefore is not null) DiagnosticFrameAfter(diagnosticBefore, null, e); throw; }
        if (diagnosticBefore is not null) DiagnosticFrameAfter(diagnosticBefore, frame, null);
        _frames++;
        if (frame is not null) foreach (var admission in frame.Admissions) _consumed.Add(admission.CorrelationId);
        if (candidate?.Command?.Command.Operation == CookingRecipeOperation.StartProcess && frame?.Dispositions.SingleOrDefault(x => x.CorrelationId == candidate.Correlation)?.Result is { Outcome: CookingRecipeOutcome.Accepted }) {
            _originalWire = candidate.Command; _originalReply = _observer.Replies.Single(x => x.Correlation == candidate.Correlation);
            _cutFrames.Add(new(before.Id, "committed-cut", frame)); await HostCut(candidate);
        } else if (!_injected && caseId == "unbound-cup" && frame is not null) {
            var transfer = _observer.Received.LastOrDefault(x => !_consumed.Contains("cut:" + x.Correlation) && x.Command?.Command.Container == _fixture.ServingVessels["D31"] && x.Command.Command.Operation is CookingRecipeOperation.PutIn or CookingRecipeOperation.ServePortion && frame.Dispositions.Any(d => d.CorrelationId == x.Correlation && d.Result?.Outcome == CookingRecipeOutcome.Accepted));
            if (transfer is not null) { _originalWire = transfer.Command; _originalReply = _observer.Replies.Single(x => x.Correlation == transfer.Correlation); _cutFrames.Add(new(before.Id, "committed-cut", frame)); await HostCut(transfer); }
        } else if (!_injected && caseId == "submitted-reply-lost" && frame is not null) {
            var submitted = frame.Dispositions.FirstOrDefault(x => x.Participant == CookingRichRecoveryFixture.Partner && x.Result?.Outcome == CookingRecipeOutcome.Accepted && _observer.Received.Any(i => i.Correlation == x.CorrelationId && i.Command?.Command.Operation == CookingRecipeOperation.SubmitOrder));
            if (submitted is not null) {
                var input = _observer.Received.Single(x => x.Correlation == submitted.CorrelationId); _originalWire = input.Command; _originalReply = _observer.Replies.Single(x => x.Correlation == input.Correlation);
                _cutFrames.Add(new(before.Id, "committed-cut", frame)); await HostCut(input);
            }
        }
        if (_injected && caseId == "manual-paused" && frame is not null && frame.Admissions.Any(a => a.Participant == CookingRichRecoveryFixture.Chef && _peer!.Sent.Any(c => c.Correlation == a.CorrelationId && c.Wire.Command.Operation == CookingRecipeOperation.ContinueProcess && c.Wire.Command.Process == _process))) {
            _cutSnapshots.Add(before); var after = Save("takeover-continued"); _cutFrames.Add(new(before.Id, after.Id, frame));
        }
        await Task.Delay(10);
    }
    private async Task HostCut(RichWireInput input)
    {
        _injected = true; _cutLifecycle = State.Observation.Lifecycle.State;
        _originalGeneration = input.Command!.ConnectionGeneration; _item = input.Command.Command.Item; _container = input.Command.Command.Container;
        var committed = Save("committed-cut");
        _committedIssued = _observer!.Images.LastOrDefault(x => x.Channel == input.Channel && x.BusinessHash == committed.BusinessHash);
        RichProof.Require(_committedIssued is not null, "Genuine committed cut full baseline was actually published before hold.");
        if (input.Command.Command.Operation == CookingRecipeOperation.StartProcess) {
            var active = State.Observation.Recipe!.Processes.Single(x => x.Anchor == _item);
            _process = active.Id; _manualElapsed = active.ElapsedTicks;
            RichProof.Require(active.ElapsedTicks < active.RequiredTicks && active.RequiredTicks == 6, "Genuine incomplete6tick cut.");
        }
        if (caseId == "manual-paused") {
            RichProof.Require(_cutLifecycle == CookingLevelState.Running && State.Observation.Recipe!.Processes.Single(x => x.Id == _process).ActiveWorker == CookingRichRecoveryFixture.Partner, "Running manual original Partner.");
            var pause = _session!.ApplyControl(new(CookingNetworkControlKind.Pause, "real-manual-pause")); _controls.Add(pause); RichProof.Require(pause.Accepted, "Actual lifecycle Pause."); Save("paused-cut");
            await Wait(() => _observer!.ClosedChannels.Any(x => x.Channel == input.Channel), "manual-real-close", true, 15000);
            var canonical = _host!.Observe().CanonicalText(); var hostFrame = _host.HostFrameSequence;
            for (var i = 0; i < 2; i++) { _session.ProcessOwnerFrame(); _frames++; Save("paused-owner-" + i); }
            _pausedPending = _session.LatestSessionProjection; RichProof.Require(_pausedPending.Participants.Single(x => x.Participant == CookingRichRecoveryFixture.Partner).CleanupPending, "Actual worker cleanup deferred while Paused.");
            RichProof.Require(_host.Observe().CanonicalText() == canonical && _host.HostFrameSequence == hostFrame, "Paused full canonical/frame unchanged.");
            var resume = _session.ApplyControl(new(CookingNetworkControlKind.Resume, "real-manual-resume")); _controls.Add(resume); RichProof.Require(resume.Accepted, "Actual Resume."); Save("resumed-before-cleanup");
            var cleanup = _session.ProcessOwnerFrame(); _frames++; Save("resumed-cleanup");
            if (cleanup is not null) _cutFrames.Add(new("resumed-before-cleanup", "resumed-cleanup", cleanup));
            var released = State.Observation.Recipe!.Processes.Single(x => x.Id == _process);
            RichProof.Require(released.ActiveWorker is null && released.ElapsedTicks == _manualElapsed, "Same manual process/progress release after Resume.");
        } else {
            if (caseId == "automatic-active") RichProof.Require(State.Observation.Recipe!.Processes.Single(x => x.Id == _process).ActiveWorker is null, "Unattended actual automatic process.");
            if (caseId == "unbound-cup") {
                var product = State.Observation.Recipe!.Items.Single(x => x.Id == _item);
                RichProof.Require(product.BoundOrder is null && product.Location.OwnerId == _fixture.ServingVessels["D31"].Value, "Actual completed unbound cup.");
            }
            var hold = _host!.Observe().CanonicalText(); var sequence = _host.HostFrameSequence;
            await Wait(() => _observer!.ClosedChannels.Any(x => x.Channel == input.Channel), "actual-cut-close-without-owner-advance", false, 15000);
            RichProof.Require(_host.Observe().CanonicalText() == hold && _host.HostFrameSequence == sequence, "Read-only scheduler hold no owner frames."); Save("held-close");
            var cleanup = _session!.ProcessOwnerFrame(); _frames++; Save("cleanup-evolution");
            if (cleanup is not null) _cutFrames.Add(new(committed.Id, "cleanup-evolution", cleanup));
        }
        _close = _observer!.ClosedChannels.Single(x => x.Channel == input.Channel);
        await Wait(() => _session!.LatestSessionProjection.Participants.Single(x => x.Participant == CookingRichRecoveryFixture.Partner) is { Ready: true } p && p.ConnectionGeneration == _originalGeneration + 1, "actual-rebind-ready", true, 15000);
        _recoveredGeneration = _session!.LatestSessionProjection.Participants.Single(x => x.Participant == CookingRichRecoveryFixture.Partner).ConnectionGeneration; Save("rebound-current-generation");
        if (caseId == "manual-paused") {
            await Wait(() => Marker(CookingRichRecoveryFixture.Partner, "remote-parked-after-cut"), "remote-real-parking");
            await _planner!.CompleteAvailableManualHandoff(); await _planner.Go("chef-parking");
            RichProof.Require(!State.Observation.Recipe!.Processes.Any(x => x.Id == _process), "Chef completed original manual ProcessId.");
            await Mark("chef-completed-manual-cut"); Save("chef-takeover-completed");
        }
    }
    private async Task<CookingRecipeCommandResult> Send(CookingRecipeCommand command)
    {
        Check(); _commands++; if (_commands % 50 == 0) { Console.WriteLine($"ACTIVITY {_stage} commands={_commands} frames={_frames} elapsed={Environment.TickCount64-_start}"); Console.Out.Flush(); } var stable = command.Command.Value; var correlation = (role == "host" ? "chef-" : "remote-") + stable;
        _flightCorrelation = correlation; _flightTargetVersion = null;
        if (_diagnostics is not null) { _resultWaitStart = null; _projectionWaitStart = null; _flightSendBefore = null; _flightSendAfter = null; }
        var before = _peer!.Baseline.State;
        if (command.Operation == CookingRecipeOperation.ServePortion) Save("portion-before-" + _commands);
        var lost = role == "client" && !_clientCut && caseId == "submitted-reply-lost" && command.Operation == CookingRecipeOperation.SubmitOrder;
        if (lost) _peer.DropCorrelation = correlation;
        var wire = _peer.Command(command, stable, correlation); _peer.Watch(wire, before.Observation.Lifecycle.State);
        if (_diagnostics is not null) { _resultWaitStart = RichCommandPathDiagnostics.Now;
            _flightSendBefore = _peer.LastDiagnosticSend?.Before; _flightSendAfter = _peer.LastDiagnosticSend?.After; }
        if (lost) {
            await Wait(() => _peer.Dropped is not null && _peer.Baseline.State.FullRecipe!.Settlements.Count == 1, "real-submit-drop-and-complete-commit", false);
            RichProof.Require(!_peer.Has(correlation), "Original application waiter not completed by diagnostic.");
            _cutLifecycle = _peer.Baseline.State.Observation.Lifecycle.State; _item = command.Item; _container = command.Container;
            _originalWire = wire; _committedCallerImage = _peer.WatchedCommittedImage ?? throw new InvalidOperationException("Actual original committed Submit image missing."); var committed = Save("client-committed-lost-submit", _committedCallerImage.State); _cutCallers.Add(_peer.Checkpoint("client-lost-submit", stable));
            await ClientRebind();
            var retryCorrelation = "retry-" + correlation; _peer.Command(command, stable, retryCorrelation);
            await Wait(() => _peer.Has(retryCorrelation) && _peer.Baseline.State.FullRecipe!.Deduplication.Any(x => x.Command == _peer.Result(retryCorrelation).DomainCommandId), "actual-cached-submit-response", false);
            _retryWire = _peer.Sent.Single(x => x.Correlation == retryCorrelation); _retryOutcome = _peer.Results.Single(x => x.Correlation == retryCorrelation);
            RichProof.Retry(committed.Capture.FullRecipe!, _peer.Baseline.State.FullRecipe!, _peer.Dropped!.Diagnostic, _retryOutcome.Result);
            _clientCut = true; Save("client-cached-submit-after"); await Mark("remote-recovery-complete"); return _retryOutcome.Result.Result!;
        }
        await Wait(() => _peer.Has(correlation), "terminal-" + correlation);
        var result = _peer.Result(correlation); RichProof.Require(result.Result?.Outcome == CookingRecipeOutcome.Accepted, "Actual legal command: " + command.Operation + "/" + result.Reason + "/" + result.Result?.Reason);
        _flightTargetVersion = result.Result!.StateVersion;
        if (_diagnostics is not null) _projectionWaitStart = RichCommandPathDiagnostics.Now;
        await Wait(() => _peer.Baseline.State.Observation.Recipe!.Version >= result.Result!.StateVersion, "committed-caller-projection");
        _diagnostics?.Projection(correlation);
        _flightCorrelation = null; _flightTargetVersion = null;
        if (_diagnostics is not null) { _resultWaitStart = null; _projectionWaitStart = null; _flightSendBefore = null; _flightSendAfter = null; }
        if (command.Operation == CookingRecipeOperation.ServePortion) {
            var old = before.FullRecipe!.Items.Single(x => x.Id == command.Item); var after = _peer.Baseline.State.FullRecipe!;
            RichProof.Require(old.RemainingPortions > 0 && after.Items.Single(x => x.Id == old.Id).RemainingPortions == old.RemainingPortions - 1 && after.NextProductId == before.FullRecipe.NextProductId + 1 && after.Containers.Single(x => x.Id == command.Container).ItemIds.Count == before.FullRecipe.Containers.Single(x => x.Id == command.Container).ItemIds.Count + 1, "Real portion decrement/allocation conservation."); Save("portion-after-" + _commands);
        }
        if (role == "client" && !_clientCut) {
            var manual = caseId == "manual-paused" && command.Operation == CookingRecipeOperation.StartProcess && before.Observation.Lifecycle.State == CookingLevelState.Running && _fixture.Content.Recipes[command.Recipe!.Value].Execution == CookingRecipeExecutionKind.Manual;
            var auto = caseId == "automatic-active" && command.Operation == CookingRecipeOperation.StartProcess && _fixture.Content.Recipes[command.Recipe!.Value].Execution == CookingRecipeExecutionKind.Automatic;
            var cup = caseId == "unbound-cup" && command.Container == _fixture.ServingVessels["D31"] && command.Operation is CookingRecipeOperation.PutIn or CookingRecipeOperation.ServePortion;
            if (manual || auto || cup) {
                _committedCallerImage = _peer.WatchedCommittedImage ?? throw new InvalidOperationException("Actual retained committed cut image missing.");
                _originalWire = wire; _cutLifecycle = _committedCallerImage.State.Observation.Lifecycle.State; _item = command.Item; _container = command.Container;
                if (manual || auto) { var process = _committedCallerImage.State.Observation.Recipe!.Processes.Single(x => x.Anchor == command.Item); _process = process.Id; _manualElapsed = process.ElapsedTicks; }
                Save("client-cut", _committedCallerImage.State); _cutCallers.Add(_peer.Checkpoint("client-cut", stable));
                if (manual) await Wait(() => _peer.Baseline.State.Observation.Lifecycle.State == CookingLevelState.Paused, "real-paused-baseline", false, 15000);
                await ClientRebind(); _clientCut = true;
                if (manual) { await _planner!.Go("partner-parking"); await Mark("remote-parked-after-cut"); await Wait(() => Marker(CookingRichRecoveryFixture.Chef, "chef-completed-manual-cut"), "real-Chef-takeover", false); }
                else await Mark("remote-recovery-complete");
                Save("client-recovered-cut");
            }
        }
        return result.Result!;
    }
    private async Task ClientRebind()
    {
        _originalGeneration = _peer!.Binding.ConnectionGeneration; _peer.Dispose(); await Task.Delay(250); _peer.Reopen(address, port);
        await Wait(() => _peer.HasBaseline && _peer.Binding.ConnectionGeneration == _originalGeneration + 1 && _peer.CurrentBusinessGrant?.Identity.ConnectionGeneration == _originalGeneration + 1, "current-generation-full-image-exact-Ready", false, 15000);
        _recoveredGeneration = _peer.Binding.ConnectionGeneration; _cutCallers.Add(_peer.Checkpoint("client-rebind"));
    }
    public async Task<int> Run()
    {
        try {
            RichProof.Require(role is "host" or "client", "Role.");
            RichProof.Require(!_diagnosticOptions.Enabled || _diagnosticOptions.Participant == CookingRichRecoveryFixture.Chef.Value ||
                _diagnosticOptions.Participant == CookingRichRecoveryFixture.Partner.Value, "Diagnostic participant must be in the real rich roster.");
            RichProof.Require(runId != "missing" && System.Text.RegularExpressions.Regex.IsMatch(source,"^[0-9a-fA-F]{40}$") && dirty is "clean" or "dirty", "Explicit frozen source/run provenance.");
            RichProof.Require(caseId is "manual-paused" or "automatic-active" or "unbound-cup" or "submitted-reply-lost", "Cutpoint.");
            if (role == "host") await Host(); else await Client();
            Write(true); return 0;
        } catch (Exception error) {
            try { DiagnosticSnapshot("failure"); } catch (Exception observation) { _diagnostics?.Failure(observation); }
            try { _failureDiagnostic = CaptureFailure(); } catch { /* Original failure remains authoritative. */ }
            try { Write(false, error); }
            catch (Exception exportError) {
                // Report failure must never replace the original operation failure.
                Console.Error.WriteLine("ORIGINAL_FAILURE " + RichCommandPathDiagnostics.Text(error.ToString()).Text);
                Console.Error.WriteLine("EXPORT_FAILURE " + RichCommandPathDiagnostics.Text(exportError.ToString()).Text);
                try {
                    Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
                    File.WriteAllText(reportPath, JsonSerializer.Serialize(new { passed=false, suite="rich-recovery-four-cutpoints-v1", role,runId,caseId,stage=_stage,
                        failure=RichCommandPathDiagnostics.Text(error.ToString()).Text,exportFailure=RichCommandPathDiagnostics.Text(exportError.ToString()).Text,
                        failureDetail=RichCommandPathDiagnostics.Text(error.ToString()),exportFailureDetail=RichCommandPathDiagnostics.Text(exportError.ToString()),
                        coverage="INCOMPLETE_FAILED_EXPORT",failureDiagnostic=_failureDiagnostic,diagnosticOptions=_diagnosticOptions,commandPathDiagnostics=_diagnostics?.Export() }));
                } catch (Exception fallbackError) {
                    Console.Error.WriteLine("FALLBACK_EXPORT_FAILURE " + RichCommandPathDiagnostics.Text(fallbackError.ToString()).Text);
                }
                return 1;
            }
            Console.Error.WriteLine(error); return 1;
        }
        finally { _peer?.Dispose(); _session?.Dispose(); _observer?.Dispose(); _host?.Dispose(); }
    }
    private async Task Host()
    {
        _host = new(new CookingLevelLifecycle(CookingRichRecoveryFixture.InitialScope, _fixture.Content.Snapshot, _fixture));
        RichProof.Require(_host.BeginPreparation(_fixture.Preparation(_host.Binding.LevelScope)).Accepted, "Actual finite preparation.");
        var progress = new CookingMajorProgress(); progress.Lock(); var storePath = Path.Combine(Path.GetDirectoryName(reportPath)!, "checkpoint"); var store = new CookingMajorCheckpointStore(storePath);
        CookingLevelHostOperationResult Transition(string request) {
            if (request == "start") { var ready = _host.CompletePreparation(); return ready.Accepted ? _host.Start() : ready; }
            if (request == "end") { var finish = _host.TryFinishService(); return finish.Accepted ? _host.CompleteEnd() : finish; }
            if (request == "successor") { var target = new CookingLevelScope(_host.Binding.LevelScope.MatchScope, _host.Binding.LevelScope.RestaurantRuntime, new("service-2"), 2); var next = _host.CreateSuccessor(target.Level, target.LevelEpoch, _fixture.Preparation(target), progress, store); return new(next.Accepted, next.Reason.ToString(), _host.Lifecycle.State.ToString(), _host.Lifecycle.Version, Array.Empty<CookingLevelPendingDisposition>()); }
            return new(false, "Unauthorized", _host.Lifecycle.State.ToString(), _host.Lifecycle.Version, Array.Empty<CookingLevelPendingDisposition>());
        }
        _observer = new(new LiteNetChannelListener(IPAddress.Parse(address), port, "abilitykit-cooking-v3", maximumBufferedReceiveBytes: checked(new CookingNetworkSessionOptions().FrameBytes + 4 + NetworkPacketHeader.Size)), _diagnostics);
        _session = new(new CookingNetworkAuthorityAdapter(_host, Transition), _observer, new Dictionary<PlayerId,string> { [CookingRichRecoveryFixture.Chef] = "rich-chef", [CookingRichRecoveryFixture.Partner] = "rich-partner" });
        _session.Start(); port = _session.Port; _peer = new(CookingRichRecoveryFixture.Chef, "rich-chef", _session.CreateLocalClientTransport, _diagnostics); _peer.Open("inprocess", 1); _planner = Planner();
        await Wait(() => _peer.CurrentBusinessGrant is not null, "local-full-grant", true, 20000);
        Console.WriteLine($"READY {port} {Environment.ProcessId}"); Console.Out.Flush();
        await _planner.Procure(); await _planner.PrepareManualHandoff(); await Mark("chef-procurement-prepared");
        await StageWait(() => Marker(CookingRichRecoveryFixture.Partner, "partner-drink-prepared"), "remote-prepared-D31", true);
        await _planner.PrepareComponents("F01"); await _planner.Go("chef-parking");
        RichProof.Require(State.FullFront!.State.ServiceTicks == 0 && State.FullRecipe!.Supply!.Deliveries.Count > 0, "Preparing finite/shared supply and no service ticks.");
        var start = _session.ApplyControl(new(CookingNetworkControlKind.ExecuteAuthorizedTransition, "start")); RichProof.Require(start.Accepted, "Authorized Running start."); await Mark("chef-running");
        var drinkDefinition = _fixture.Catalog.Document.Menus.Single(x => x.SourceId == "D31").Product;
        var drink = State.Observation.Recipe!.Items.Single(x => x.Definition == drinkDefinition && x.Location.OwnerId == _fixture.ServingVessels["D31"].Value);
        RichProof.Require(drink.BoundOrder is null, "Unbound drink made by Partner.");
        if (caseId == "submitted-reply-lost") await StageWait(() => Marker(CookingRichRecoveryFixture.Partner, "partner-first-drink-delivered"), "remote-first-real-submit-and-recovery");
        else await _planner.Deliver(drink.Id, "D31");
        if (caseId == "manual-paused") { await StageWait(() => Marker(CookingRichRecoveryFixture.Partner, "partner-salad-prepared"), "remote-running-F01-and-real-takeover"); var definition = _fixture.Catalog.Document.Menus.Single(x => x.SourceId == "F01").Product; var salad = State.Observation.Recipe!.Items.Single(x => x.Definition == definition && x.Location.OwnerId == _fixture.ServingVessels["F01"].Value); await _planner.Deliver(salad.Id, "F01"); await Mark("chef-delivered-both"); }
        else { var salad = await _planner.ProduceAndPlate("F01");
            if (caseId == "submitted-reply-lost") { await _planner.Deliver(salad, "F01"); await Mark("chef-delivered-both"); }
            else { await _planner.Go("chef-parking"); await Mark("chef-salad-prepared"); } }
        await StageWait(() => Marker(CookingRichRecoveryFixture.Partner, "partner-service-done"), "remote-delivery-and-recovery-completed");
        RichProof.Require(_injected, "Required rich actual cut never injected.");
        CookingNetworkControlResult? end = null;
        for (var i = 0; i < 1000 && end?.Accepted != true; i++) { Check(); end = _session.ApplyControl(new(CookingNetworkControlKind.ExecuteAuthorizedTransition, "end")); if (!end.Accepted) await Pump(); }
        RichProof.Require(end?.Accepted == true, "Natural closing1000tick budget."); RichProof.Natural(State);
        _ended = new(RichProof.Capture("ended-natural", State), end!, 2, 1, 0, true); Phase("ended-natural");
        await Wait(() => { var issued = _observer.Images.LastOrDefault(); return issued is not null && issued.BusinessHash == _ended.State.BusinessHash && _observer.Received.Any(x => x.Ack == issued.Identity) && _observer.Grants.Any(x => x.Identity == issued.Identity); }, "remote-complete-ended-image");
        var oldScope = State.Observation.Scope;
        var successor = _session.ApplyControl(new(CookingNetworkControlKind.ExecuteAuthorizedTransition, "successor")); RichProof.Require(successor.Accepted, "Real durable successor.");
        var read = store.ReadBaseline(oldScope.MatchScope); RichProof.Require(read.Accepted, "Actual durable ReadBaseline.");
        RichProof.Require(read.Payload!.Kitchen.NextProductId == _ended.State.Capture.FullRecipe!.NextProductId && JsonSerializer.Serialize(read.Payload.Kitchen.Supply!.Balances) == JsonSerializer.Serialize(_ended.State.Capture.FullRecipe.Supply!.Balances) && JsonSerializer.Serialize(read.Payload.Kitchen.Supply.Deliveries) == JsonSerializer.Serialize(_ended.State.Capture.FullRecipe.Supply.Deliveries), "Actual durable allocator/finite stock carry.");
        _successor = new(oldScope, State.Observation.Scope, successor, Files(storePath), true, JsonSerializer.SerializeToElement(read.Payload, CookingNetworkWireCodec.JsonOptions), RichProof.Capture("created-successor", State), "accepted S14 narrowed carry; durable baseline3/Level current/Recipe5"); Phase("created-successor");
        await Wait(() => {
            var image = _observer.Images.LastOrDefault();
            return _peer.CurrentGranted && _peer.Baseline.Identity.Scope.LevelEpoch == 2 && _session.LatestSessionProjection.Participants.All(p => p.ConnectedOwnerBinding && p.Ready && !p.CleanupPending) && image is not null && image.Identity.Scope.LevelEpoch == 2 && _observer.Received.Any(x => x.Channel == image.Channel && x.Ack == image.Identity) && _observer.Grants.Any(x => x.Channel == image.Channel && x.Identity == image.Identity);
        }, "final-newest-complete-successor-ready");
        var current = _observer.Images.Last(); var ack = _observer.Received.Last(x => x.Ack == current.Identity).Ack!; var readyIdentity = _observer.Grants.Last(x => x.Identity == current.Identity).Identity;
        _final = new(CookingNetworkWireCodec.Hash(State), _session.ServerSessionInstance, State.Observation.Scope, _session.LatestSessionProjection, _peer.Baseline, current.Identity, ack, readyIdentity, true, null, 5000, Stopwatch.GetTimestamp(), !_observer.ClosedChannels.Any(x => x.Channel == current.Channel));
        RichProof.Require(_final.CurrentChannelLive && _peer.Connected, "Both actual physical/local current channels live at immutable freeze.");
        await Wait(() => _observer.ClosedChannels.Any(x => x.Channel == current.Channel), "actual-current-remote-close-after-report", true, 15000);
        _final = _final with { CurrentClose = _observer.ClosedChannels.Single(x => x.Channel == current.Channel) };
    }
    private async Task Client()
    {
        _peer = new(CookingRichRecoveryFixture.Partner, "rich-partner", () => new LiteNetTransport("abilitykit-cooking-v3"), _diagnostics); _peer.Open(address, port); _planner = Planner();
        await Wait(() => _peer.CurrentBusinessGrant is not null, "initial-remote-full-grant", false, 20000);
        await StageWait(() => Marker(CookingRichRecoveryFixture.Chef, "chef-procurement-prepared"), "legal-preparation-milestone", false);
        await _planner.CompleteAvailableManualHandoff(); var drink = await _planner.ProduceAndPlate("D31"); await _planner.Go("partner-parking"); await Mark("partner-drink-prepared");
        await StageWait(() => Marker(CookingRichRecoveryFixture.Chef, "chef-running"), "genuine-Running-start", false);
        if (caseId == "manual-paused") { var salad = await _planner.ProduceAndPlate("F01"); await _planner.Go("partner-parking"); await Mark("partner-salad-prepared"); await StageWait(() => Marker(CookingRichRecoveryFixture.Chef, "chef-delivered-both"), "actual-cross-deliveries", false); }
        else if (caseId == "submitted-reply-lost") { await _planner.Deliver(drink, "D31"); await _planner.Go("partner-parking"); await Mark("partner-first-drink-delivered"); await StageWait(() => Marker(CookingRichRecoveryFixture.Chef, "chef-delivered-both"), "actual-second-Chef-delivery", false); }
        else { await StageWait(() => Marker(CookingRichRecoveryFixture.Chef, "chef-salad-prepared"), "actual-salad-product", false); var definition = _fixture.Catalog.Document.Menus.Single(x => x.SourceId == "F01").Product; var salad = State.Observation.Recipe!.Items.Single(x => x.Definition == definition && x.Location.OwnerId == _fixture.ServingVessels["F01"].Value); await _planner.Deliver(salad.Id, "F01"); }
        RichProof.Require(_clientCut, "Required genuine remote recovery cut missing."); await Mark("partner-service-done");
        await StageWait(() => State.Observation.Lifecycle.State == CookingLevelState.Ended, "complete-ended-image", false); RichProof.Natural(State); Phase("ended-natural");
        await Wait(() => State.Observation.Scope.LevelEpoch == 2 && State.Observation.Lifecycle.State == CookingLevelState.Created && _peer.CurrentGranted, "actual-successor-newest-issued-exact-Ready", false); Phase("created-successor");
        var baseline = _peer.Baseline; RichProof.Require(baseline.Session.Participants.All(x => x.ConnectedOwnerBinding && x.Ready && !x.CleanupPending), "Both live Ready/noCleanup final view.");
        _final = new(CookingNetworkWireCodec.Hash(baseline.State), _peer.Binding.ServerSessionInstance, baseline.Identity.Scope, baseline.Session, baseline, baseline.Identity, baseline.Identity, _peer.Checkpoint("final").ActualReady, true, null, 5000, Stopwatch.GetTimestamp(), _peer.Connected);
        RichProof.Require(_final.CurrentChannelLive, "Actual remote channel live at immutable final freeze.");
        Check(); RichProof.Require(Environment.TickCount64 + 5000 < _deadline, "Final hold within whole600s."); Write(true); await Task.Delay(5000);
    }
    private static IReadOnlyList<ArtifactHash> Files(string directory) => Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Order().Select(p => new ArtifactHash(Path.GetRelativePath(directory, p).Replace('\\', '/'), RichProof.Sha(File.ReadAllBytes(p)))).ToArray();
    private IReadOnlyList<ArtifactHash> Canonicals()
    {
        var states = _cutSnapshots.Select(x => x.Capture).Concat(_phases.Where(x => x.Capture is not null).Select(x => x.Capture!.Capture)).Concat(_cutCallers.SelectMany(x => new[] { x.Baseline.State, x.GrantedBaseline.State })).Concat(_phases.Where(x => x.Caller is not null).SelectMany(x => new[] { x.Caller!.Baseline.State, x.Caller.GrantedBaseline.State })).Concat(_cutFrames.Where(x => x.Frame.Capture.State is not null).Select(x => x.Frame.Capture.State!)).Concat(_controls.Where(x => x.Capture.State is not null).Select(x => x.Capture.State!)).ToList();
        if (_ended is not null) states.Add(_ended.State.Capture); if (_successor is not null) states.Add(_successor.State.Capture); if (_final is not null) states.Add(_final.LocalBaseline.State);
        var files = new Dictionary<string, ArtifactHash>(); var root = Path.GetDirectoryName(reportPath)!;
        foreach (var state in states) foreach (var entry in new[] { (Text: state.FullRecipe!.CanonicalText(), Kind: "recipe"), (Text: state.FullFront?.CanonicalText(), Kind: "front") }) {
            if (entry.Text is null) continue; var hash = RichProof.TextHash(entry.Text); var relative = "canonical/" + hash + "." + entry.Kind + ".txt";
            if (files.ContainsKey(relative)) continue; var path = Path.Combine(root, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (File.Exists(path)) RichProof.Require(RichProof.Sha(File.ReadAllBytes(path)) == hash, "Existing canonical evidence was changed."); else File.WriteAllText(path, entry.Text, new System.Text.UTF8Encoding(false));
            files.Add(relative, new(relative,hash));
        }
        return files.Values.OrderBy(x => x.RelativePath,StringComparer.Ordinal).ToArray();
    }
    private RichFailureDiagnostic CaptureFailure()
    {
        var absent = new List<string>(); CookingNetworkBaseline? baseline = null; RichCallerCheckpoint? checkpoint = null;
        CookingNetworkBaselineIdentity? grant = null; string? hash = null; int? size = null;
        var sizeStatus = "unavailable: no existing validated baseline";
        try { if (_peer?.HasBaseline == true) baseline = _peer.Baseline; } catch (Exception e) { absent.Add("baseline: " + e.Message); }
        if (baseline is not null) {
            try { hash = CookingNetworkWireCodec.Hash(baseline.State); } catch (Exception e) { absent.Add("businessHash: " + e.Message); }
            try { size = CookingNetworkWireCodec.Encode(CookingNetworkMessageKind.Baseline, "failure-size-probe", baseline).Length; sizeStatus = size <= 8*1024*1024 ? "recomputed envelope bytes; not actual last received bytes" : "recomputed envelope exceeds existing8MiB admission; no artifact emitted"; }
            catch (Exception e) { sizeStatus = "recomputed size unavailable: " + e.Message; }
            try { grant = _peer!.CurrentBusinessGrant?.Identity; checkpoint = _peer.Checkpoint("failure-metadata", _flightCorrelation); }
            catch (Exception e) { absent.Add("exact ACK/Ready ordinals unavailable: " + e.Message); }
        }
        var flight = _peer?.Sent.LastOrDefault(x => x.Correlation == _flightCorrelation);
        var terminal = _peer?.Results.LastOrDefault(x => x.Correlation == _flightCorrelation);
        return new(DateTimeOffset.UtcNow, Environment.TickCount64-_start, _stage, flight, terminal,
            _flightTargetVersion ?? terminal?.Result.Result?.StateVersion, baseline?.Identity, baseline?.State.Observation.Recipe?.Version,
            hash, size, sizeStatus, grant, _peer?.Grants.LastOrDefault(), checkpoint?.ExactAckSent,
            checkpoint?.BaselineReceiveOrdinal, checkpoint?.AckSendOrdinal, checkpoint?.ReadyReceiveOrdinal,
            _observer?.Images.LastOrDefault(), _observer?.Received.LastOrDefault(x => x.Ack is not null),
            _observer?.Grants.LastOrDefault(), absent.Count == 0 ? null : string.Join("; ", absent));
    }
    private void Write(bool passed, Exception? error = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        var bin = AppContext.BaseDirectory;
        var metadata = typeof(RichRunner).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false).Cast<System.Reflection.AssemblyMetadataAttribute>().ToDictionary(x => x.Key, x => x.Value);
        RichProof.Require(metadata.GetValueOrDefault("LinkedFixtureSha256") == RichProof.Sha(File.ReadAllBytes(Path.Combine(bin, "sources", "CookingRichRecoveryFixture.cs"))) && metadata.GetValueOrDefault("LinkedPlannerSha256") == RichProof.Sha(File.ReadAllBytes(Path.Combine(bin, "sources", "CookingRichRecoveryPlanner.cs"))), "Compiled linked source / copied source SHA mismatch.");
        var provenance = new RichProvenance(source, dirty, Files(bin), typeof(CookingLevelEtHost).Assembly.ManifestModule.ModuleVersionId, typeof(CookingNetworkSessionHost).Assembly.ManifestModule.ModuleVersionId, typeof(CookingNetworkWireCodec).Assembly.ManifestModule.ModuleVersionId, RichProof.Sha(File.ReadAllBytes(Path.Combine(bin, CookingMenuCatalog.ContentFileName))), _fixture.Catalog.Sha256, _fixture.Content.Identity, RichProof.TextHash(JsonSerializer.Serialize(Roles, CookingNetworkWireCodec.JsonOptions)), RichProof.Sha(File.ReadAllBytes(Path.Combine(bin, "sources", "CookingRichRecoveryFixture.cs"))), RichProof.Sha(File.ReadAllBytes(Path.Combine(bin, "sources", "CookingRichRecoveryPlanner.cs"))), State.FrontConfigurationIdentity, State.PreparationConfigurationIdentity, State.InstalledLayout is null ? null : RichProof.TextHash(JsonSerializer.Serialize(State.InstalledLayout, CookingNetworkWireCodec.JsonOptions)), System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, System.Runtime.InteropServices.RuntimeInformation.OSDescription);
        var cut = _injected || _clientCut ? new RichCut(caseId, true, _cutLifecycle, CookingRichRecoveryFixture.Partner, CookingRichRecoveryFixture.Chef, _originalGeneration, _recoveredGeneration, _peer!.Binding.ServerSessionInstance, _process, _item, _container, _cutSnapshots.ToArray(), _cutCallers.ToArray(), _cutFrames.ToArray(), _controls.ToArray(), _close, _originalWire, _originalReply, _retryWire, _retryOutcome, _peer.Dropped, _manualElapsed, 0, caseId == "manual-paused" ? "chef-completed-manual-cut" : "remote-recovery-complete", _preCutIssued, _preCutAck, _committedIssued, _pausedPending, _committedCallerImage) : null;
        var report = new RichEndpointReport(1, "rich-recovery-four-cutpoints-v1", role, runId, caseId, passed, _stage, error?.ToString(), new(Environment.ProcessId, _processStart, null, RichProof.Sha(File.ReadAllBytes(typeof(RichRunner).Assembly.Location))), topology, role == "host" ? _session?.Endpoint ?? address : address + ":" + port, CookingNetworkWireCodec.ProtocolVersion, CookingLevelCheckpointCodec.CurrentFormatVersion, 5, provenance, Roles,
            new(600000,20000,30000,15000,10,Environment.TickCount64-_start,_commands,_frames), _phases.ToArray(), cut, _ended, _successor, _final, _observer?.Received.ToArray() ?? Array.Empty<RichWireInput>(), _observer?.Replies.ToArray() ?? Array.Empty<RichWireReply>(), _peer?.Sent.ToArray() ?? Array.Empty<RichCallerCommand>(), _peer?.Results.ToArray() ?? Array.Empty<RichCallerOutcome>(), _peer?.Dropped, _observer?.Images.ToArray() ?? Array.Empty<RichIssued>(), _observer?.Grants.ToArray() ?? Array.Empty<RichWireGrant>(), _observer?.ClosedChannels.ToArray() ?? Array.Empty<RichClose>(), _peer?.Grants.ToArray() ?? Array.Empty<CookingNetworkBaselineIdentity>(), _fixture.Content.Items.Values.Where(x => x.Container is not null).OrderBy(x => x.Id.Value, StringComparer.Ordinal).Select(x => new RichContainerRule(x.Id, x.Container!.Capacity, x.Container.AcceptedDefinitions.OrderBy(d => d.Value, StringComparer.Ordinal).ToArray(), x.Container.DisposableOnSubmission)).ToArray(), Canonicals(), _failureDiagnostic, _diagnosticOptions, _diagnostics?.Export());
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions(CookingNetworkWireCodec.JsonOptions) { WriteIndented = true }));
    }
    private RichDiagnosticFrame DiagnosticFrameBefore()
    {
        DiagnosticSnapshot("selected-ingress-before");
        return new(0, RichCommandPathDiagnostics.Now, 0, _session!.LatestCapture.Observation.HostFrameSequence, null,
            _session.LatestCapture.Observation.Recipe?.Version, null, _session.LatestSessionProjection.Participants.ToArray(),
            Array.Empty<CookingNetworkParticipantProjection>(), Array.Empty<RichDiagnosticDisposition>(), Array.Empty<RichDiagnosticDisposition>(), null, null, null);
    }
    private void DiagnosticFrameAfter(RichDiagnosticFrame before, CookingNetworkOwnerFrameResult? frame, Exception? error)
    {
        var correlation = _diagnostics!.SelectedCorrelation;
        var admissions = frame?.Admissions.Where(x => x.CorrelationId == correlation).Select(x => new RichDiagnosticDisposition(
            x.CorrelationId, x.CommandId.Value, x.Reason.ToString(), x.Accepted ? "AcceptedAdmission" : "RejectedAdmission",
            x.Disposition?.Result?.Outcome.ToString(), x.Disposition?.Result?.StateVersion)).ToArray() ?? Array.Empty<RichDiagnosticDisposition>();
        var dispositions = frame?.Dispositions.Where(x => x.CorrelationId == correlation).Select(x => new RichDiagnosticDisposition(
            x.CorrelationId, x.CommandId.Value, x.Reason, x.Kind.ToString(), x.Result?.Outcome.ToString(), x.Result?.StateVersion)).ToArray() ?? Array.Empty<RichDiagnosticDisposition>();
        var receipt = _session!.LatestCapture.FullRecipe?.Deduplication.FirstOrDefault(x => x.Command.Value == _diagnostics.SelectedDomain);
        _diagnostics.Frame(before with { After = RichCommandPathDiagnostics.Now, FrameAfter = _session.LatestCapture.Observation.HostFrameSequence,
            VersionAfter = _session.LatestCapture.Observation.Recipe?.Version, ParticipantsAfter = _session.LatestSessionProjection.Participants.ToArray(),
            Admissions = admissions, Dispositions = dispositions, SelectedReceipt = _session.LatestCapture.FullRecipe is null ? null : receipt is not null,
            SelectedReceiptOutcome = receipt?.Outcome.ToString(), Failure = error is null ? null : RichCommandPathDiagnostics.Text(error.ToString()) });
        if (admissions.Length != 0) DiagnosticSnapshot("selected-admission-after");
        if (dispositions.Length != 0) DiagnosticSnapshot("selected-disposition-after");
        if (receipt is not null) DiagnosticSnapshot("selected-receipt-after");
    }
    private void DiagnosticSnapshot(string milestone)
    {
        if (_diagnostics is null || _session is null || !_diagnostics.ReserveSnapshot(milestone)) return;
        var before = RichCommandPathDiagnostics.Now; var allocated = GC.GetAllocatedBytesForCurrentThread();
        var actual = _session.Diagnostics;
        var after = RichCommandPathDiagnostics.Now; allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        var timings = actual.Timings.Where(x => x.Correlation == _diagnostics.SelectedCorrelation).ToArray();
        var ingress = _observer?.Received.LastOrDefault(x => x.Correlation == _diagnostics.SelectedCorrelation);
        var reply = _observer?.Replies.LastOrDefault(x => x.Correlation == _diagnostics.SelectedCorrelation);
        var channel = ingress?.Channel;
        _diagnostics.Snapshot(new(0, milestone, before, after, allocated, actual.Pending, timings.Take(16).ToArray(),
            ingress?.Ordinal, ingress?.Timestamp, channel, reply?.Correlation,
            _observer?.Images.LastOrDefault(x => x.Channel == channel), _observer?.Received.LastOrDefault(x => x.Channel == channel && x.Ack is not null),
            _observer?.Grants.LastOrDefault(x => x.Channel == channel), timings.Length));
    }
}
