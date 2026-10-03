using System.Diagnostics;
using System.Net;
using System.Text.Json;
using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using AbilityKit.Game.Cooking.NetworkAcceptance;
using AbilityKit.Game.Cooking.NetworkMeasurement;
using AbilityKit.Game.Cooking.Session;
using AbilityKit.Network.Transport.LiteNet;

var output = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.GetFullPath("measurement.json");
var topology = args.Length > 1 ? args[1] : "InProcess";
var controlOnly = args.Contains("--control-only");
var reports = new List<object>();
Func<object>? activeEvidence = null;
return SingleThreadOwner.Run(Run);

async Task<int> Run()
{
    try {
        if (topology is not ("InProcess" or "SameMachineUdp")) throw new ArgumentException("Unsupported topology.");
        reports.Add(await Execute(0, true));
        if (!controlOnly) for (var repeat = 1; repeat <= 3; repeat++) reports.Add(await Execute(repeat, false));
        Save(true, null); return 0;
    } catch (Exception error) { Save(false, error.ToString()); Console.Error.WriteLine(error); return 1; }
}

void Save(bool passed, string? failure)
{
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    File.WriteAllText(output, JsonSerializer.Serialize(new { passed, failure, topology, physicalTwoPc = "NOT_VERIFIED",
        performanceTarget = "UNSET", controlOnly, machine = Environment.MachineName, pid = Environment.ProcessId,
        sdkRuntime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        etBuild = typeof(CookingLevelEtHost).Assembly.ManifestModule.ModuleVersionId,
        sessionBuild = typeof(CookingNetworkSessionHost).Assembly.ManifestModule.ModuleVersionId, reports,
        activeFailureEvidence = passed ? null : activeEvidence?.Invoke() },
        new JsonSerializerOptions { WriteIndented = true }));
}

async Task<object> Execute(int repeat, bool control)
{
    var fixture = new MeasurementFixture(); using var host = fixture.CreateHost();
    var adapter = new CookingNetworkAuthorityAdapter(host);
    var options = control ? new CookingNetworkSessionOptions(ReceiptCapacity: 16) : new CookingNetworkSessionOptions();
    using var session = new CookingNetworkSessionHost(adapter, new LiteNetChannelListener(IPAddress.Loopback, 0, "abilitykit-cooking-v3"),
        MeasurementFixture.Players.ToDictionary(p => p, p => "credential-" + p.Value), options);
    session.Start();
    activeEvidence = () => new { repeat, control, diagnostics = session.Diagnostics, session.LatestCapture };
    using var first = new CookingNetworkSessionClient(MeasurementFixture.Players[0], "credential-" + MeasurementFixture.Players[0].Value,
        topology == "InProcess" ? session.CreateLocalClientTransport : null, options);
    using var second = new CookingNetworkSessionClient(MeasurementFixture.Players[1], "credential-" + MeasurementFixture.Players[1].Value,
        topology == "InProcess" ? session.CreateLocalClientTransport : null, options);
    var clients = new[] { first, second };
    var profile = new MeasurementProfile();
    var admitted = new HashSet<RecipeCommandId>();
    long ownerCalls = 0, ownerAllocated = 0, ownerTimestamp = 0;
    var runWatch = Stopwatch.StartNew();
    async Task Pump(bool measured = false)
    {
        var bytes = GC.GetAllocatedBytesForCurrentThread(); var timestamp = Stopwatch.GetTimestamp();
        var frame = session.ProcessOwnerFrame();
        var ownerElapsedMs = (Stopwatch.GetTimestamp() - timestamp) * 1000.0 / Stopwatch.Frequency;
        if (measured) { ownerCalls++; ownerAllocated += GC.GetAllocatedBytesForCurrentThread() - bytes; ownerTimestamp += Stopwatch.GetTimestamp() - timestamp; }
        if (frame is not null) foreach (var admission in frame.Admissions.Where(a => a.Accepted)) admitted.Add(admission.CommandId);
        profile.Observe(clients, measured, ownerElapsedMs);
        await Task.Delay(10);
    }
    async Task<T> PumpUntil<T>(Task<T> task)
    {
        var deadline = Stopwatch.StartNew();
        while (!task.IsCompleted && deadline.Elapsed.TotalSeconds < 30) await Pump();
        if (!task.IsCompleted) throw new TimeoutException("Measurement operation deadline.");
        return await task;
    }
    var connects = Task.WhenAll(clients.Select(c => c.ConnectAsync("127.0.0.1", topology == "InProcess" ? 1 : session.Port)));
    while (!connects.IsCompleted && runWatch.Elapsed.TotalSeconds < 30) await Pump();
    if (!connects.IsCompleted) throw new TimeoutException("Measurement baseline deadline.");
    await connects;
    Require(fixture.CreateCount == 1 && clients.All(c => c.IsSynchronized), "One real ET authority and two acknowledged clients.");
    Require(adapter.CaptureFullState().State!.Observation.Lifecycle.State == CookingLevelState.Preparing, "Preparing authority.");
    CookingRecipeCommand Command(int player, int index) {
        var state = clients[player].LatestBaseline!.State;
        var item = state.Observation.Recipe!.Items.Single(i => i.Id == MeasurementFixture.Tools[player]);
        return new(MeasurementFixture.Match, 0, MeasurementFixture.Players[player], new("wire-unused"),
            item.Location.Kind == LocationKind.PlayerHand ? CookingRecipeOperation.Drop : CookingRecipeOperation.Pickup,
            Item: item.Id, ExpectedItemVersion: item.Version,
            WorldAnchor: item.Location.Kind == LocationKind.PlayerHand ? item.Id.Value : null);
    }
    async Task ObserveResult(int player, CookingNetworkWireResult result)
    {
        Require(result.Result?.Outcome == CookingRecipeOutcome.Accepted, "Legal empty-tool operation: " + result.Reason);
        var deadline = Stopwatch.StartNew();
        while (clients[player].LatestBaseline!.State.Observation.Recipe!.Version < result.Result!.StateVersion && deadline.Elapsed.TotalSeconds < 30)
            await Task.Delay(1);
        if (clients[player].LatestBaseline!.State.Observation.Recipe!.Version < result.Result!.StateVersion)
            throw new TimeoutException("Committed full projection deadline.");
        var state = clients[player].LatestBaseline!.State.Observation.Recipe!;
        Require(state.Items.Count == 2 && state.Containers.Count == 2 && state.Containers.All(i => i.ItemIds.Count == 0), "Two empty physical tools preserved.");
    }
    object Readiness(CookingNetworkAuthorityCapture final)
    {
        var participantView = session.LatestSessionProjection.Participants;
        Require(participantView.Count == 2 && participantView.Select(p => p.Participant).ToHashSet().SetEquals(MeasurementFixture.Players),
            "Exactly two configured participants at exit.");
        Require(participantView.All(p => p.ConnectedOwnerBinding && p.Ready && !p.CleanupPending && p.ConnectionGeneration > 0),
            "Server participants remain connected Ready without cleanup.");
        for (var player = 0; player < clients.Length; player++) {
            var client = clients[player]; var identity = client.LatestBaseline!.Identity;
            var server = participantView.Single(p => p.Participant == MeasurementFixture.Players[player]);
            Require(client.IsSynchronized && client.ServerSessionInstance == session.ServerSessionInstance &&
                identity.ServerSessionInstance == session.ServerSessionInstance && identity.Participant == server.Participant &&
                identity.ConnectionGeneration == server.ConnectionGeneration && identity.Scope == final.Observation.Scope,
                "Client exit binding is synchronized to current server instance/scope/generation.");
        }
        Require(session.LatestCapture is not null && CookingNetworkWireCodec.Hash(final) == CookingNetworkWireCodec.Hash(session.LatestCapture),
            "Session capture matches current same-frame real authority.");
            return new { asserted = true, session.LatestSessionProjection, clients = clients.Select(c => new { c.IsSynchronized, c.ServerSessionInstance, c.LatestBaseline!.Identity }).ToArray(), sameFrameAuthorityHash = CookingNetworkWireCodec.Hash(final) };
    }
    if (control) {
        CookingRecipeCommand? original = null;
        for (var i = 0; i < 16; i++) {
            var command = Command(0, i); original ??= command;
            var result = await PumpUntil(first.SendCommandAsync("capacity-" + i, command));
            var projection = ObserveResult(0, result);
            while (!projection.IsCompleted) await Pump(); await projection;
        }
        var before = adapter.CaptureFullState().State!.FullRecipe!;
        var rejected = await PumpUntil(first.SendCommandAsync("capacity-17", Command(0, 16)));
        Require(rejected.Reason == "ReceiptCapacityExceeded" && rejected.Result is null, "Real seventeenth mapping rejected.");
        AssertOnlyIdleTicks(before, adapter.CaptureFullState().State!.FullRecipe!);
        var duplicate = await PumpUntil(first.SendCommandAsync("capacity-0", original!));
        Require(duplicate.Result is { Outcome: CookingRecipeOutcome.Accepted, IsDuplicate: true }, "Original terminal survives capacity.");
        var after = adapter.CaptureFullState().State!.FullRecipe!;
        AssertOnlyIdleTicks(before, after);
        return new { repeat, kind = "ReceiptCapacity16", accepted = 16, rejected = 1, cachedDuplicate = true,
            idleLogicalTickDelta = after.LogicalTick - before.LogicalTick, fixture.Configuration.Identity, finalReadiness = Readiness(adapter.CaptureFullState().State!) };
    }

    Console.WriteLine($"MEASURE repeat={repeat} topology={topology} warmup=10 sample=60 offeredPerParticipant=5");
    var samples = new List<Sample>(); var offered = new int[2]; var skipped = new int[2]; var schedulerSkipped = new int[2];
    var warmupOffered = new int[2]; var warmupSkipped = new int[2];
    activeEvidence = () => new { repeat, offered, skippedBackpressure = skipped, schedulerSkipped, warmupOffered, warmupSkipped,
        issued = samples.Count(s => s.Sampled), completed = samples.Count(s => s.Sampled && s.Result is not null),
        admitted = samples.Count(s => s.Sampled && admitted.Contains(s.ExpectedDomainId)),
        accepted = samples.Count(s => s.Sampled && s.Result?.Result?.Outcome == CookingRecipeOutcome.Accepted),
        rejected = samples.Count(s => s.Sampled && s.Result is not null && s.Result.Result?.Outcome != CookingRecipeOutcome.Accepted),
        pending = samples.Count(s => s.Sampled && s.Result is null), results = samples.Select(s => new { s.Player, s.Index, s.Sampled, s.Result }),
        projectionCompleted = samples.Count(s => s.Sampled && s.ProjectionCompleted),
        terminalTimeouts = samples.Count(s => s.Sampled && s.TimedOut && s.FailureStage == "AwaitTerminal"),
        projectionTimeouts = samples.Count(s => s.Sampled && s.TimedOut && s.FailureStage == "AwaitCommittedProjection"),
        failedSamples = samples.Where(s => s.Failure is not null).Select(s => new { s.Player, s.Index, s.Sampled, s.FailureStage, s.TimedOut, s.Failure }),
        diagnosticWindow = profile.FailureEvidence(), diagnostics = session.Diagnostics, session.LatestCapture };
    var clock = Stopwatch.StartNew(); var sampleStartTimestamp = Stopwatch.GetTimestamp() + 10L * Stopwatch.Frequency;
    var sampleEndTimestamp = sampleStartTimestamp + 60L * Stopwatch.Frequency;
    CookingNetworkSessionDiagnostics? sampleStart = null, sampleEnd = null;
    using var process = Process.GetCurrentProcess();
    TimeSpan cpuStart = default, cpuEnd = default; long peakWorking = 0, peakPrivate = 0;
    async Task Worker(int player)
    {
        Task? flight = null;
        for (var index = 0; index < 350; index++) {
            var due = index * 200;
            while (clock.ElapsedMilliseconds < due) await Task.Delay(1);
            var sampled = index >= 50;
            if (sampled) offered[player]++; else warmupOffered[player]++;
            if (clock.ElapsedMilliseconds >= due + 200) { if (sampled) schedulerSkipped[player]++; else warmupSkipped[player]++; continue; }
            if (flight is { IsCompleted: false }) { if (sampled) skipped[player]++; else warmupSkipped[player]++; continue; }
            if (flight is not null) await flight;
            if (!clients[player].IsSynchronized) throw new InvalidOperationException("Measurement authority became unavailable.");
            var stableId = $"run-{repeat}-p-{player}-i-{index}";
            var command = Command(player, index); var sample = new Sample(player, index, sampled, Stopwatch.GetTimestamp(),
                CookingNetworkWireCodec.DomainId(session.ServerSessionInstance, MeasurementFixture.Scope, MeasurementFixture.Players[player], stableId)); samples.Add(sample);
            flight = Finish();
            async Task Finish() {
                var stage = "AwaitTerminal";
                try {
                    var result = await clients[player].SendCommandAsync(stableId, command).WaitAsync(TimeSpan.FromSeconds(30));
                    var terminalObserved = Stopwatch.GetTimestamp();
                    sample.Result = result; sample.RttMs = (terminalObserved - sample.StartTimestamp) * 1000.0 / Stopwatch.Frequency;
                    stage = "AwaitCommittedProjection";
                    await ObserveResult(player, result);
                    var projected = Stopwatch.GetTimestamp();
                    sample.TerminalToProjectionMs = (projected - terminalObserved) * 1000.0 / Stopwatch.Frequency;
                    sample.SendToProjectionMs = (projected - sample.StartTimestamp) * 1000.0 / Stopwatch.Frequency;
                    sample.ProjectionCompleted = true;
                } catch (Exception error) {
                    sample.FailureStage = stage; sample.TimedOut = error is TimeoutException; sample.Failure = error.ToString(); throw;
                }
            }
        }
        if (flight is not null) await flight;
    }
    var workers = Task.WhenAll(Worker(0), Worker(1));
    while (!workers.IsCompleted && clock.Elapsed.TotalSeconds < 180) {
        var measured = clock.Elapsed.TotalSeconds >= 10 && clock.Elapsed.TotalSeconds < 70;
        if (measured && sampleStart is null) { sampleStart = session.Diagnostics; cpuStart = process.TotalProcessorTime; profile.Begin(); }
        if (!measured && sampleStart is not null && sampleEnd is null) { profile.End(); sampleEnd = session.Diagnostics; cpuEnd = process.TotalProcessorTime; }
        if (measured) { process.Refresh(); peakWorking = Math.Max(peakWorking, process.WorkingSet64); peakPrivate = Math.Max(peakPrivate, process.PrivateMemorySize64); }
        await Pump(measured);
    }
    if (!workers.IsCompleted) throw new TimeoutException("Measurement run deadline.");
    await workers;
    while (clock.Elapsed.TotalSeconds < 70) {
        process.Refresh(); peakWorking = Math.Max(peakWorking, process.WorkingSet64); peakPrivate = Math.Max(peakPrivate, process.PrivateMemorySize64);
        await Pump(clock.Elapsed.TotalSeconds >= 10);
    }
    sampleStart ??= session.Diagnostics;
    if (sampleEnd is null) { profile.End(); sampleEnd = session.Diagnostics; cpuEnd = process.TotalProcessorTime; }
    var measuredSamples = samples.Where(s => s.Sampled).ToArray();
    Require(offered.All(n => n == 300) && warmupOffered.All(n => n == 50), "Offered schedule complete.");
    Require(measuredSamples.All(s => s.Result?.Result?.Outcome == CookingRecipeOutcome.Accepted), "All issued legal operations completed accepted.");
    Require(measuredSamples.All(s => s.ProjectionCompleted && s.Failure is null), "Every completed terminal has a committed continuation projection.");
    Require(measuredSamples.All(s => s.Result!.DomainCommandId == s.ExpectedDomainId && admitted.Contains(s.ExpectedDomainId)), "Issued operations really admitted by ET.");
    var timings = session.Diagnostics.Timings.Where(t => t.ReceivedTimestamp >= sampleStartTimestamp && t.ReceivedTimestamp < sampleEndTimestamp).ToArray();
    var final = adapter.CaptureFullState().State!;
    Require(final.Observation.Lifecycle.State == CookingLevelState.Preparing && fixture.CreateCount == 1, "No service/second authority.");
    var readiness = Readiness(final);
    foreach (var client in clients) Require(client.LatestBaseline!.Identity.StateHash ==
        CookingNetworkWireCodec.BaselineHash(client.LatestBaseline.State, client.LatestBaseline.Session), "Issued full baseline hash valid.");
    Require(measuredSamples.Length + skipped.Sum() + schedulerSkipped.Sum() == offered.Sum(), "Every sample opportunity accounted.");
    var baselineBytes = CookingNetworkWireCodec.Encode(CookingNetworkMessageKind.Baseline, "measure", first.LatestBaseline!);
    var tokenCount = CountTokens(baselineBytes);
    Console.WriteLine($"COMPLETE repeat={repeat} issued={measuredSamples.Length} skipped={skipped.Sum()+schedulerSkipped.Sum()} tokens={tokenCount}");
    return new { repeat, kind = "BaselineMeasurement", warmupSeconds = 10, sampleSeconds = 60, offeredPerParticipantPerSecond = 5,
        offered, skippedBackpressure = skipped, schedulerSkipped, warmupOffered, warmupSkipped,
        issued = measuredSamples.Length, admitted = measuredSamples.Length, accepted = measuredSamples.Length, rejected = 0, cancelled = 0,
        completed = measuredSamples.Count(s => s.Result is not null), pending = 0,
        projectionCompleted = measuredSamples.Count(s => s.ProjectionCompleted), terminalTimeouts = 0, projectionTimeouts = 0,
        acceptedPerSecond = measuredSamples.Length / 60.0,
        rttMs = Distribution(measuredSamples.Select(s => s.RttMs)),
        terminalObservedToCommittedProjectionMs = Distribution(measuredSamples.Select(s => s.TerminalToProjectionMs)),
        sendToCommittedProjectionMs = Distribution(measuredSamples.Select(s => s.SendToProjectionMs)),
        externalProfile = profile.Summary(),
        hostReceiveConsumeMs = Distribution(timings.Select(t => (t.ConsumedTimestamp - t.ReceivedTimestamp) * 1000.0 / Stopwatch.Frequency)),
        hostConsumeSendMs = Distribution(timings.Select(t => (t.CommittedTimestamp - t.ConsumedTimestamp) * 1000.0 / Stopwatch.Frequency)),
        ownerCalls, ownerAllocatedBytes = ownerAllocated, ownerCallMilliseconds = ownerTimestamp * 1000.0 / Stopwatch.Frequency,
        processCpuSampleMilliseconds = (cpuEnd - cpuStart).TotalMilliseconds, sampledPeakWorkingBytes = peakWorking, sampledPeakPrivateBytes = peakPrivate,
        receivedPayloadBytes = sampleEnd.ReceivedBytes - sampleStart.ReceivedBytes, sentPayloadBytes = sampleEnd.SentBytes - sampleStart.SentBytes,
        sampleRejectedIngress = sampleEnd.Rejected - sampleStart.Rejected,
        cumulativeRunQueueHighWater = session.Diagnostics.QueueHighWater,
        timingSamples = timings.Length, baselineBytes = baselineBytes.Length, baselineTokens = tokenCount,
        fullStateHash = CookingNetworkWireCodec.Hash(final), configurationIdentity = fixture.Configuration.Identity,
        clientBaselineHashes = clients.Select(c => c.LatestBaseline!.Identity.StateHash).ToArray(),
        finalReadiness = readiness,
        serverInstance = session.ServerSessionInstance,
        metricDefinitions = "Closed loop: one inflight per participant; scheduled busy opportunities skipped, never catch-up burst. RTT send-to-terminal includes transport. Host timing same-process monotonic receive/map/result-send, not client receipt. GC sums all managed allocations on the owner thread during synchronous owner calls, including inlined InProcess client callbacks; excludes allocations on other threads, native allocation and awaits. CPU and memory include both clients and real authority in this process; memory is sampled, not OS lifetime peak. Bytes are Session payload, queue peak whole run including warmup. Timings selected by Host receipt within sample interval; results selected by offered sample index; counts therefore have distinct cohorts. No performance threshold." };
}

static object Distribution(IEnumerable<double> values)
{
    var sorted = values.Order().ToArray();
    double Quantile(double q) => sorted.Length == 0 ? 0 : sorted[(int)Math.Ceiling(sorted.Length * q) - 1];
    return new { samples = sorted.Length, p50 = Quantile(.5), p95 = Quantile(.95), p99 = Quantile(.99) };
}
static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static int CountTokens(byte[] bytes)
{
    var reader = new Utf8JsonReader(bytes); var count = 0;
    while (reader.Read()) count++;
    return count;
}
static void AssertOnlyIdleTicks(CookingRecipeCheckpoint before, CookingRecipeCheckpoint after)
{
    var ticks = after.LogicalTick - before.LogicalTick;
    Require(ticks >= 0 && after.StateVersion - before.StateVersion == ticks && after.EventSequence - before.EventSequence == ticks,
        "Only real idle frame clocks advanced.");
    Require(after.TickEvents.Count - before.TickEvents.Count == ticks, "Idle tick audit retained.");
    Require(before.CanonicalText() == (after with { LogicalTick = before.LogicalTick, StateVersion = before.StateVersion,
        EventSequence = before.EventSequence, TickEvents = before.TickEvents }).CanonicalText(),
        "Rejection/duplicate preserve all non-clock domain fields and receipts.");
}
internal sealed class Sample(int player, int index, bool sampled, long timestamp, RecipeCommandId expectedDomainId)
{
    internal int Player { get; } = player;
    internal int Index { get; } = index;
    internal bool Sampled { get; } = sampled;
    internal long StartTimestamp { get; } = timestamp;
    internal RecipeCommandId ExpectedDomainId { get; } = expectedDomainId;
    internal CookingNetworkWireResult? Result { get; set; }
    internal double RttMs { get; set; }
    internal double TerminalToProjectionMs { get; set; }
    internal double SendToProjectionMs { get; set; }
    internal bool ProjectionCompleted { get; set; }
    internal string? FailureStage { get; set; }
    internal bool TimedOut { get; set; }
    internal string? Failure { get; set; }
}
