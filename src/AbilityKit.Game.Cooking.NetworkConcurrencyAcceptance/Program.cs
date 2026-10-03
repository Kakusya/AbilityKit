using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using AbilityKit.Game.Cooking.NetworkConcurrencyAcceptance;
using AbilityKit.Game.Cooking.Session;
using AbilityKit.Network.Transport.LiteNet;

string Option(string key, string fallback) { var i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
var role = args.FirstOrDefault() ?? "help";
var address = Option("--ip", "127.0.0.1"); var port = int.Parse(Option("--port", "0"));
var runId = Option("--run-id", "missing"); var source = Option("--source", "UNSET"); var dirty = Option("--dirty", "UNSET");
var order = Option("--order", "remote-first"); var topology = Option("--topology", "SeparateHostsRequiresPairedEvidence");
var reportPath = Path.GetFullPath(Option("--report", role + ".json"));
var deadline = Environment.TickCount64 + 180000; var clock = Stopwatch.StartNew();
Action? poll = null;
var evidence = new List<object>(); var checks = new List<string>(); string stage = "initial"; object? final = null;
void Check(bool condition, string name) { ConcurrencyFixture.Require(condition, name); checks.Add(name); }
void Wait(Func<bool> done, Action? pump = null, int milliseconds = 10000)
{
    var until = Math.Min(deadline, Environment.TickCount64 + milliseconds);
    while (Environment.TickCount64 < until) {
        poll?.Invoke(); if (done()) return;
        pump?.Invoke(); Thread.Sleep(2);
    }
    poll?.Invoke(); ConcurrencyFixture.Require(done(), "Deadline at " + stage);
}
void Write(bool passed, Exception? error = null)
{
    Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
    var binaries = Directory.GetFiles(AppContext.BaseDirectory).Where(p => p.EndsWith(".dll") || p.EndsWith(".json") || p.EndsWith(".exe")).Order().ToDictionary(p => Path.GetFileName(p)!, p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
    File.WriteAllText(reportPath, JsonSerializer.Serialize(new {
        schema = 1, fixture = "real-et-spatial-concurrency-v1", role, passed, runId, source, dirty, order, topology,
        physicalTwoPc = "NOT_VERIFIED", formalPerformanceTarget = "UNSET", stage, failure = error?.ToString(),
        pid = Environment.ProcessId, machine = Environment.MachineName, address, port,
        os = System.Runtime.InteropServices.RuntimeInformation.OSDescription, runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        protocol = CookingNetworkWireCodec.ProtocolVersion, checkpointFormat = CookingLevelCheckpointCodec.CurrentFormatVersion,
        etMvid = typeof(CookingLevelEtHost).Assembly.ManifestModule.ModuleVersionId,
        sessionMvid = typeof(CookingNetworkSessionHost).Assembly.ManifestModule.ModuleVersionId,
        binaries, elapsedMs = clock.Elapsed.TotalMilliseconds, checks, evidence, final
    }, new JsonSerializerOptions(CookingNetworkWireCodec.JsonOptions) { WriteIndented = true }));
}
void Healthy(PassivePeer peer, ObservedListener? observer = null)
{
    peer.Poll();
    if (peer.Failure is { } failure) throw new InvalidOperationException("Passive peer failure", failure);
    if (observer?.Failure is { } failure2) throw new InvalidOperationException("Readonly observer failure", failure2);
}
Func<CookingNetworkWireCommand, CookingNetworkWireCommand>? Alter(int phase, long oldGeneration = 0) => phase switch {
    11 => w => w with { Scope = new CookingLevelScope(w.Scope.MatchScope, w.Scope.RestaurantRuntime, w.Scope.Level, w.Scope.LevelEpoch + 1), ClientSequence = w.ClientSequence + 1000000 },
    12 => w => w with { ServerSessionInstance = "wrong-instance", ClientSequence = w.ClientSequence + 1000000 },
    13 => w => w with { ClientSequence = 1 },
    17 => w => w with { ConnectionGeneration = oldGeneration }, _ => null
};
string Stable(int phase) => phase is 14 or 15 or 16 ? "original-retry-identity" : "phase-" + (phase == 20 ? 1 : phase == 21 ? 4 : phase);
try {
    Check((role is "host" or "client") && runId != "missing" && (order is "remote-first" or "local-first"), "legal-run-options");
    if (role == "host") {
        var fixture = new ConcurrencyFixture(); using var host = fixture.Host();
        using var observer = new ObservedListener(new LiteNetChannelListener(IPAddress.Parse(address), port, "abilitykit-cooking-v3"));
        using var session = new CookingNetworkSessionHost(new CookingNetworkAuthorityAdapter(host), observer,
            new Dictionary<PlayerId, string> { [ConcurrencyFixture.Chef] = "control-chef-credential", [ConcurrencyFixture.Partner] = "control-partner-credential" });
        session.Start(); port = session.Port;
        using var local = new PassivePeer(ConcurrencyFixture.Chef, "control-chef-credential", session.CreateLocalClientTransport);
        poll = () => Healthy(local, observer);
        void Pump() { Healthy(local, observer); session.ProcessOwnerFrame(); }
        local.Open("inprocess", 1); stage = "local-join"; Wait(() => local.HasBaseline && local.CurrentBusinessGrant is not null, Pump);
        Console.WriteLine($"READY {port} {Environment.ProcessId}"); Console.Out.Flush();
        stage = "remote-join"; Wait(() => session.LatestSessionProjection.Participants.Single(x => x.Participant == ConcurrencyFixture.Partner).Ready, Pump, 20000);
        Check(local.CurrentBusinessGrant is not null, "local-complete-baseline-exact-ack");
        long firstGeneration = session.LatestSessionProjection.Participants.Single(x => x.Participant == ConcurrencyFixture.Partner).ConnectionGeneration;
        for (var phase = 1; phase <= 21; phase++) {
            stage = "marker-" + phase;
            // Marker is a legal movement command; only the trusted scheduler decides when to pump.
            local.Command(ConcurrencyFixture.Move(ConcurrencyFixture.Chef, marker: true), "marker-" + phase, "marker-" + phase);
            Wait(() => local.Has("marker-" + phase) && observer.Images.Any(x => x.Phase == phase), Pump);
            Check(local.Result("marker-" + phase).Result?.Outcome == CookingRecipeOutcome.Accepted, "legal-marker-" + phase);
            var before = session.LatestCapture!;
            if (phase == 16) {
                stage = "actual-close-retirement";
                Wait(() => !session.LatestSessionProjection.Participants.Single(x => x.Participant == ConcurrencyFixture.Partner).ConnectedOwnerBinding, Pump);
                Check(observer.ClosedChannels.Count > 0, "actual-remote-channel-closed");
                stage = "current-generation-rebind";
                Wait(() => session.LatestSessionProjection.Participants.Single(x => x.Participant == ConcurrencyFixture.Partner) is { Ready: true } p && p.ConnectionGeneration > firstGeneration, Pump);
                Check(session.LatestSessionProjection.Participants.Single(x => x.Participant == ConcurrencyFixture.Partner).ConnectionGeneration == firstGeneration + 1, "strictly-new-generation");
                before = session.LatestCapture!;
            }
            var originalPhase = phase == 20 ? 1 : phase == 21 ? 4 : phase;
            var localCommand = phase is 20 or 21 ? local.Sent.Single(x => x.Correlation == "local-" + originalPhase).Wire.Command : ConcurrencyFixture.Action(phase, ConcurrencyFixture.Chef, before);
            var preCallbacks = host.Observe().CanonicalText();
            stage = "held-prefix-" + phase;
            if (order == "local-first") local.Command(localCommand, "local-phase-" + originalPhase, "local-" + phase);
            Wait(() => { Healthy(local, observer); return observer.Has("remote-" + phase); }); // no owner work while awaiting actual ingress
            if (order == "remote-first") local.Command(localCommand, "local-phase-" + originalPhase, "local-" + phase);
            Check(preCallbacks == host.Observe().CanonicalText(), "callback-readonly-" + phase);
            Check(session.Diagnostics.Pending >= 2, "both-inputs-pending-" + phase);
            Check(!local.Has("local-" + phase), "local-no-terminal-before-owner-" + phase);
            var delivered = observer.Received.Single(x => x.Kind == CookingNetworkMessageKind.Command && x.Correlation == "remote-" + phase);
            Check(delivered.Command is not null && delivered.Command.Command.Player == ConcurrencyFixture.Partner && delivered.Command.StableCommandId == Stable(phase), "real-remote-correlation-" + phase);
            var liveGeneration = session.LatestSessionProjection.Participants.Single(x => x.Participant == ConcurrencyFixture.Partner).ConnectionGeneration;
            Check(delivered.Command!.ConnectionGeneration == (phase == 17 ? firstGeneration : liveGeneration) && delivered.Command.ServerSessionInstance == (phase == 12 ? "wrong-instance" : session.ServerSessionInstance), "exact-channel-generation-instance-" + phase);
            Check(delivered.Channel == observer.Images.Last().Channel, "current-physical-remote-channel-" + phase);
            var frame = session.ProcessOwnerFrame() ?? throw new InvalidOperationException("No real consume frame.");
            var after = session.LatestCapture!;
            // Network delivery can finish asynchronously, but no additional business frame is consumed here.
            Wait(() => local.Has("local-" + phase));
            var remoteTerminal = observer.Replies.Single(x => x.Correlation == "remote-" + phase).Result;
            Proof.Frame(phase, before, after, frame, local.Result("local-" + phase), remoteTerminal);
            if (phase is 20 or 21) {
                var originalLocal = local.Result("local-" + originalPhase);
                var originalRemote = observer.Replies.Single(x => x.Correlation == "remote-" + originalPhase).Result;
                Check(local.Result("local-" + phase).DomainCommandId == originalLocal.DomainCommandId && local.Result("local-" + phase).Result!.Outcome == originalLocal.Result!.Outcome && local.Result("local-" + phase).Result!.Reason == originalLocal.Result.Reason && local.Result("local-" + phase).Result!.StateVersion == originalLocal.Result.StateVersion, "local-contested-cached-outcome-" + phase);
                Check(remoteTerminal.DomainCommandId == originalRemote.DomainCommandId && remoteTerminal.Result!.Outcome == originalRemote.Result!.Outcome && remoteTerminal.Result.Reason == originalRemote.Result.Reason && remoteTerminal.Result.StateVersion == originalRemote.Result.StateVersion, "remote-contested-cached-outcome-" + phase);
            }
            if (phase == 14) Check(delivered.Command!.ClientSequence < observer.Received.Single(x => x.Correlation == "remote-11").Command!.ClientSequence && delivered.Command.ClientSequence < observer.Received.Single(x => x.Correlation == "remote-12").Command!.ClientSequence, "legal-lower-sequence-after-invalid-high-guards");
            evidence.Add(new { phase, before, delivered, localWire = local.Sent.Single(x => x.Correlation == "local-" + phase).Wire, frame, local = local.Result("local-" + phase), remote = remoteTerminal, after });
            Check(true, "full-control-proof-" + phase);
            Console.WriteLine("PHASE " + phase); Console.Out.Flush();
        }
        stage = "stable-final-paused-baseline";
        Check(host.Pause().Accepted, "trusted-final-pause-after-business");
        Pump(); var frozen = session.LatestCapture!;
        var hash = CookingNetworkWireCodec.Hash(frozen);
        Wait(() => {
            if (!local.HasBaseline || CookingNetworkWireCodec.Hash(local.Baseline.State) != hash || !local.CurrentGranted) return false;
            if (session.LatestSessionProjection.Participants.Count != 2 || session.LatestSessionProjection.Participants.Any(x => !x.ConnectedOwnerBinding || !x.Ready || x.CleanupPending)) return false;
            var image = observer.Images.LastOrDefault();
            return image is not null && image.BusinessHash == hash && image.Identity.ConnectionGeneration == firstGeneration + 1 &&
                observer.Received.Any(x => x.Channel == image.Channel && x.Kind == CookingNetworkMessageKind.BaselineAck && x.Ack == image.Identity) &&
                observer.Grants.Any(x => x.Channel == image.Channel && x.Identity == image.Identity);
        }, Pump);
        var finalState = local.Baseline; var remoteIssued = observer.Images.Last();
        var preCloseProjection = session.LatestSessionProjection;
        Check(preCloseProjection.Participants.All(x => x.ConnectedOwnerBinding && x.Ready && !x.CleanupPending), "both-connected-ready-clean-before-client-disposal");
        Check(!observer.ClosedChannels.Contains(remoteIssued.Channel), "current-remote-channel-still-live-at-freeze");
        stage = "remote-final-report-close";
        Wait(() => observer.ClosedChannels.Contains(remoteIssued.Channel), Pump);
        final = new { hash, serverSessionInstance = session.ServerSessionInstance, scope = frozen.Observation.Scope, capture = frozen,
            preCloseProjection, remoteIssued, remoteCurrentExactAckReady = true,
            localBaseline = finalState, localExactAckReady = local.Grants.Contains(finalState.Identity), ingress = observer.Received.ToArray(),
            remoteResultProof = "Actual readonly outbound reply plus mandatory paired client received results", diagnostics = session.Diagnostics };
    } else {
        using var remote = new PassivePeer(ConcurrencyFixture.Partner, "control-partner-credential", () => new LiteNetTransport("abilitykit-cooking-v3"));
        poll = () => Healthy(remote);
        remote.Open(address, port); stage = "remote-initial-full-join"; Wait(() => { Healthy(remote); return remote.HasBaseline && remote.CurrentBusinessGrant is not null; }, milliseconds: 20000);
        Check(remote.CurrentBusinessGrant?.Identity.ConnectionGeneration == remote.Binding.ConnectionGeneration, "remote-complete-baseline-exact-ack"); var firstGeneration = remote.Binding.ConnectionGeneration;
        for (var phase = 1; phase <= 21; phase++) {
            stage = "remote-marker-" + phase;
            Wait(() => { Healthy(remote); return remote.HasBaseline && ConcurrencyFixture.Phase(remote.Baseline.State) == phase; });
            var before = remote.Baseline;
            if (phase == 16) {
                remote.Dispose(); Thread.Sleep(250); // Host must separately prove actual retirement; delay is not its proof.
                remote.Reopen(address, port);
                Wait(() => remote.HasBaseline && remote.Binding.ConnectionGeneration > firstGeneration && remote.CurrentBusinessGrant?.Identity.ConnectionGeneration > firstGeneration);
                Check(remote.Binding.ConnectionGeneration == firstGeneration + 1, "live-current-generation-rebind-exact-ack");
            }
            var command = phase is 20 or 21 ? remote.Sent.Single(x => x.Correlation == "remote-" + (phase == 20 ? 1 : 4)).Wire.Command : ConcurrencyFixture.Action(phase, ConcurrencyFixture.Partner, remote.Baseline.State);
            var wire = remote.Command(command, Stable(phase), "remote-" + phase, Alter(phase, firstGeneration));
            stage = "remote-terminal-" + phase; Wait(() => { Healthy(remote); return remote.Has("remote-" + phase); });
            var result = remote.Result("remote-" + phase); Proof.RemoteOutcome(phase, result);
            if (phase is 20 or 21) {
                var original = remote.Result("remote-" + (phase == 20 ? 1 : 4));
                Check(result.DomainCommandId == original.DomainCommandId && result.Result!.Outcome == original.Result!.Outcome && result.Result.Reason == original.Result.Reason && result.Result.StateVersion == original.Result.StateVersion && result.Result.IsDuplicate, "actual-contested-cached-remote-" + phase);
            }
            if (phase == 14) Check(wire.ClientSequence < remote.Sent.Single(x => x.Correlation == "remote-11").Wire.ClientSequence && wire.ClientSequence < remote.Sent.Single(x => x.Correlation == "remote-12").Wire.ClientSequence, "lower-current-legal-sequence-after-high-reject");
            evidence.Add(new { phase, before, wire, result }); Check(true, "actual-wire-outcome-" + phase);
        }
        stage = "remote-final-paused-image";
        Wait(() => { Healthy(remote); return remote.HasBaseline && remote.Baseline.State.Observation.Lifecycle.State == CookingLevelState.Paused && remote.CurrentGranted; });
        var received = remote.Baseline; Proof.ValidateFull(received.State);
        final = new { hash = CookingNetworkWireCodec.Hash(received.State), serverSessionInstance = remote.Binding.ServerSessionInstance, scope = received.State.Observation.Scope,
            remoteBaseline = received, remoteExactAckReady = remote.Grants.Contains(received.Identity), generation = remote.Binding.ConnectionGeneration, actualResults = remote.Results.ToArray() };
        Check(received.Session.Participants.All(x => x.ConnectedOwnerBinding && x.Ready && !x.CleanupPending), "client-full-view-both-ready-clean-before-disposal");
        Check(Environment.TickCount64 + 5000 < deadline, "final-five-second-live-hold-within-endpoint-budget");
        Write(true); Thread.Sleep(5000); return 0; // durable report then bounded live hold, actual close observed by Host
    }
    Write(true); return 0;
} catch (Exception e) { Write(false, e); Console.Error.WriteLine(e); return 1; }
