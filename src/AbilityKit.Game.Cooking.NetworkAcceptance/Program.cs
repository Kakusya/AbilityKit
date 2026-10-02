using System.Diagnostics;
using System.Net;
using System.Text.Json;
using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using AbilityKit.Game.Cooking.NetworkAcceptance;
using AbilityKit.Game.Cooking.Session;
using AbilityKit.Network.Transport.LiteNet;

var role = args.FirstOrDefault() ?? "help";
string Option(string key, string fallback)
{
    var index = Array.IndexOf(args, key);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
}
var address = Option("--ip", "127.0.0.1");
var port = int.Parse(Option("--port", "0"));
var reportPath = Path.GetFullPath(Option("--report", "network-" + role + ".json"));
var topology = Option("--topology", "SameMachineIndependentProcessesUdp");
var watch = Stopwatch.StartNew();
var checks = new List<string>();
var responseMs = new List<double>();
object? detail = null;
const string fixtureId = "cooking-et-s14-finite-service-process-v3";
const string hostCredential = "process-host-credential";
const string remoteCredential = "process-remote-credential";
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException(name);
    checks.Add(name);
}
void WriteReport(bool passed, Exception? error = null)
{
    Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
    File.WriteAllText(reportPath, JsonSerializer.Serialize(new
    {
        fixture = fixtureId, role, passed, failure = error?.ToString(), topology,
        machine = Environment.MachineName, pid = Environment.ProcessId, address, port,
        protocol = CookingNetworkWireCodec.ProtocolVersion, elapsedMs = watch.Elapsed.TotalMilliseconds,
        runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        buildIdentity = typeof(CookingLevelEtHost).Assembly.ManifestModule.ModuleVersionId,
        sessionBuildIdentity = typeof(CookingNetworkSessionHost).Assembly.ManifestModule.ModuleVersionId,
        formalPerformanceTarget = "UNSET", movementProjectionCheckInterval = 8, physicalTwoPc = "NOT_VERIFIED", status = passed ? "passed" : error is null ? "in_progress" : "failed",
        checks, commandResponseMs = responseMs, detail
    }, new JsonSerializerOptions { WriteIndented = true }));
}
return SingleThreadOwner.Run(RunScenario);

async Task<int> RunScenario()
{
try
{
    var fixture = new ProcessServiceFixture();
    if (role == "host")
    {
        using var authority = new CookingLevelEtHost(new CookingLevelLifecycle(ProcessServiceFixture.InitialScope, fixture.Content.Snapshot, fixture));
        Check(authority.BeginPreparation(fixture.Preparation(ProcessServiceFixture.InitialScope)).Accepted, "prepared-existing-et-authority");
        var adapter = new CookingNetworkAuthorityAdapter(authority);
        using var session = new CookingNetworkSessionHost(adapter,
            new LiteNetChannelListener(IPAddress.Parse(address), port, "abilitykit-cooking-v3"),
            new Dictionary<PlayerId, string> { [ProcessServiceFixture.Chef] = hostCredential, [ProcessServiceFixture.Partner] = remoteCredential });
        session.Start(); port = session.Port; Console.WriteLine("TRACE listener-started");
        using var local = new CookingNetworkSessionClient(ProcessServiceFixture.Chef, hostCredential, session.CreateLocalClientTransport);
        long ownerAllocatedBytes = 0;
        long ownerTimestampTicks = 0;
        long ownerFrames = 0;
        async Task Pump()
        {
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            session.ProcessOwnerFrame();
            ownerTimestampTicks += Stopwatch.GetTimestamp() - started;
            ownerAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            ownerFrames++;
            await Task.Delay(10);
        }
        async Task<T> PumpTask<T>(Task<T> task)
        {
            var deadline = Environment.TickCount64 + 30000;
            while (!task.IsCompleted && Environment.TickCount64 < deadline) await Pump();
            if (!task.IsCompleted) throw new TimeoutException("Host-local framed operation deadline.");
            return await task;
        }
        var connecting = local.ConnectAsync("inprocess", 1);
        var connectDeadline = Environment.TickCount64 + 20000;
        while (!connecting.IsCompleted && Environment.TickCount64 < connectDeadline) await Pump();
        if (!connecting.IsCompleted) throw new TimeoutException("Local baseline deadline: " + JsonSerializer.Serialize(session.Diagnostics));
        await connecting;
        Check(local.IsSynchronized, "host-local-full-framed-baseline");
        async Task<CookingRecipeCommandResult> SendLocal(CookingRecipeCommand command)
        {
            var result = await PumpTask(local.SendCommandAsync(command.Command.Value, command));
            return result.Result ?? throw new InvalidOperationException("Local command failed: " + result.Reason);
        }
        var planner = new NetworkActionPlanner(fixture, ProcessServiceFixture.Chef,
            () => local.LatestBaseline!.State, SendLocal, Pump, async commands => {
                var results = new List<CookingRecipeCommandResult>();
                foreach (var command in commands) results.Add(await SendLocal(command));
                return results;
            });
        Console.WriteLine("TRACE local-ready"); await planner.Procure(); Console.WriteLine("TRACE procurement-finished");
        await planner.PrepareManualHandoff(); Console.WriteLine("TRACE handoff-finished");
        Check(authority.FrontOfHouseSnapshot!.ServiceTicks == 0, "finite-stock-and-handoff-during-preparation");
        Console.WriteLine($"READY {port} {Environment.ProcessId}");
        Console.Out.Flush();
        var running = false;
        var successor = false; var drinkDeliveredByOtherPlayer = false;
        var finalHold = 0L;
        var deadline = Environment.TickCount64 + 540000;
        while (Environment.TickCount64 < deadline)
        {
            await Pump();
            var state = session.LatestCapture!;
            if (!running && new[] { "F01", "D31" }.All(id =>
                state.Observation.Recipe!.Items.Any(item =>
                    item.Definition == fixture.Catalog.Document.Menus.Single(m => m.SourceId == id).Product &&
                    item.Location.Kind == LocationKind.ContainerSlot && item.Location.OwnerId == fixture.ServingVessels[id].Value)))
            {
                // Owner lifecycle calls retain the existing singleplayer application authority.
                Check(authority.CompletePreparation().Accepted, "complete-preparation-after-shared-products");
                Check(authority.Start().Accepted, "start-service-after-preparation");
                running = true;
            }
            if (running && !drinkDeliveredByOtherPlayer) {
                var drink = local.LatestBaseline!.State.Observation.Recipe!.Items.Single(i =>
                    i.Definition == fixture.Catalog.Document.Menus.Single(m => m.SourceId == "D31").Product &&
                    i.Location.Kind == LocationKind.ContainerSlot && i.Location.OwnerId == fixture.ServingVessels["D31"].Value);
                Check(drink.BoundOrder is null, "local-received-remote-unbound-finished-drink");
                await planner.Deliver(drink.Id, "D31");
                drinkDeliveredByOtherPlayer = true;
                checks.Add("different-player-bound-and-delivered-remote-finished-drink");
            }
            // The scenario includes return-route commands after each submission.
            // Do not retire their scope while either passive client is still walking.
            bool Returned(PlayerId participant, string anchor) {
                var cell = fixture.Cells[anchor];
                var pose = state.Observation.Recipe!.Poses!.Single(p => p.Player == participant);
                return pose.X == cell.X * 1000 + 500 && pose.Y == cell.Y * 1000 + 500 &&
                    pose.FacingX == 1 && pose.FacingY == 1;
            }
            if (running && drinkDeliveredByOtherPlayer && !successor &&
                state.Observation.Recipe!.AcceptedOrders.Count == 2 &&
                Returned(ProcessServiceFixture.Chef, "chef-parking") &&
                Returned(ProcessServiceFixture.Partner, "partner-parking") && authority.TryFinishService().Accepted)
            {
                checks.Add("natural-service-success");
                Check(authority.CompleteEnd().Accepted, "natural-service-ended");
                Check(authority.Lifecycle.Outcome == CookingLevelOutcome.Success, "unmet-orders-do-not-fail-normal-service");
                Check(adapter.CaptureFullState().State!.Observation.Recipe!.AcceptedOrders.Count == 2, "two-catalog-orders-delivered-once");
                Check(authority.FrontOfHouseSnapshot!.UnsatisfiedOrders.Count == 1, "one-unmet-order-naturally-departed");
                var completed = adapter.CaptureFullState().State!;
                Check(completed.Observation.Recipe!.Stars == 0 && completed.Observation.Recipe.IsCompleted,
                    "zero-star-completed-service-success");
                Check(completed.FullFront!.State.Closing && completed.FullFront.State.WashQueue.Count == 0 &&
                    completed.FullFront.State.Tables.All(t => t.State == CookingFrontTableState.Free) &&
                    completed.FullFront.State.Work.All(w => w.Player is null && w.Status != CookingFrontWorkStatus.Working),
                    "closed-cleared-tables-empty-wash-released-work");
                var target = new CookingLevelScope(ProcessServiceFixture.InitialScope.MatchScope,
                    ProcessServiceFixture.InitialScope.RestaurantRuntime, new("service-2"), 2);
                var progress = new CookingMajorProgress(); progress.Lock();
                var store = new CookingMajorCheckpointStore(Path.Combine(Path.GetDirectoryName(reportPath)!, "checkpoint"));
                Check(authority.CreateSuccessor(target.Level, target.LevelEpoch, fixture.Preparation(target), progress, store).Accepted,
                    "actual-durable-et-successor-created");
                successor = true;
                finalHold = Environment.TickCount64 + 7000;
            }
            if (successor && Environment.TickCount64 >= finalHold) break;
        }
        if (!successor) detail = new { stage = "scenario-deadline", lifecycle = session.LatestCapture!.Observation.Lifecycle,
            diagnostics = session.Diagnostics, recipeVersion = session.LatestCapture.Observation.Recipe!.Version,
            processes = session.LatestCapture.Observation.Recipe.Processes,
            acceptedOrders = session.LatestCapture.Observation.Recipe.AcceptedOrders.Count };
        Check(successor, "complete-finite-catalog-service-and-successor");
        var final = adapter.CaptureFullState();
        Check(final.Accepted && final.State!.Observation.Scope.LevelEpoch == 2, "final-current-full-capture");
        var diagnostics = session.Diagnostics;
        double Percentile(double[] values, double p) => values.Length == 0 ? 0 : values[(int)Math.Ceiling(p * values.Length) - 1];
        var receiveConsume = diagnostics.Timings.Select(t => (t.ConsumedTimestamp - t.ReceivedTimestamp) * 1000.0 / Stopwatch.Frequency).Order().ToArray();
        var consumeCommit = diagnostics.Timings.Select(t => (t.CommittedTimestamp - t.ConsumedTimestamp) * 1000.0 / Stopwatch.Frequency).Order().ToArray();
        detail = new
        {
            hash = CookingNetworkWireCodec.Hash(final.State), scope = final.State!.Observation.Scope,
            baselineHash = CookingNetworkWireCodec.BaselineHash(final.State, session.LatestSessionProjection!),
            serverSessionInstance = session.ServerSessionInstance,
            snapshotBytes = CookingNetworkWireCodec.Encode(CookingNetworkMessageKind.Baseline, "measure",
                new CookingNetworkBaseline(new(session.ServerSessionInstance, ProcessServiceFixture.Chef, 1,
                    final.State.Observation.Scope, 2, 1,
                    CookingNetworkWireCodec.BaselineHash(final.State, session.LatestSessionProjection!), "diagnostic-issue"),
                    CookingLevelCheckpointCodec.CurrentFormatVersion, 5, final.State, session.LatestSessionProjection!)).Length,
            sessionProjection = session.LatestSessionProjection,
            diagnostics.QueueHighWater, diagnostics.Rejected, diagnostics.ReceivedBytes, diagnostics.SentBytes,
            samples = diagnostics.Timings.Count,
            ownerFrames, ownerAllocatedBytes,
            ownerFrameMilliseconds = ownerTimestampTicks * 1000.0 / Stopwatch.Frequency,
            committedResponsesPerSecond = diagnostics.Timings.Count / watch.Elapsed.TotalSeconds,
            receiveToConsumeMs = new { p50 = Percentile(receiveConsume, .5), p95 = Percentile(receiveConsume, .95), p99 = Percentile(receiveConsume, .99) },
            consumeToCommitMs = new { p50 = Percentile(consumeCommit, .5), p95 = Percentile(consumeCommit, .95), p99 = Percentile(consumeCommit, .99) },
            metricDefinitions = "Same-process monotonic Host timestamps at frozen ingress, owner mapping and committed result delivery. Bytes are Session payload bytes. Allocation sums all managed allocations on the owner thread within each synchronous owner call, including inline InProcess delivery callbacks; excludes await time, allocations on other threads and native allocations. Throughput uses total scenario wall time including final hold; diagnostic only. No cross-machine clock subtraction or approved performance thresholds."
        };
    }
    else if (role == "client")
    {
        using var client = new CookingNetworkSessionClient(ProcessServiceFixture.Partner, remoteCredential);
        await client.ConnectAsync(address, port);
        Check(client.IsSynchronized, "remote-real-udp-issued-baseline-ack");
        Check(client.LatestBaseline!.State.FullRecipe!.Supply!.Deliveries.All(d => d.Phase == CookingDeliveryPhase.Received),
            "finite-receipts-in-full-typed-baseline");
        Check(client.LatestBaseline.State.Observation.Recipe!.Processes.Any(p => p.ActiveWorker is null), "remote-visible-paused-manual-handoff");
        async Task<CookingRecipeCommandResult> SendRemote(CookingRecipeCommand command)
        {
            var timing = Stopwatch.StartNew();
            var result = await client.SendCommandAsync(command.Command.Value, command);
            responseMs.Add(timing.Elapsed.TotalMilliseconds);
            if (responseMs.Count % 10 == 0) {
                detail = new { stage = "remote-active", command = command.Command.Value, operation = command.Operation,
                    visibleVersion = client.LatestBaseline?.State.Observation.Recipe?.Version };
                WriteReport(false);
            }
            return result.Result ?? throw new InvalidOperationException("Remote command failed: " + result.Reason);
        }
        var planner = new NetworkActionPlanner(fixture, ProcessServiceFixture.Partner,
            () => client.LatestBaseline!.State, SendRemote, () => Task.Delay(10), async commands => {
                var results = new List<CookingRecipeCommandResult>();
                foreach (var command in commands) results.Add(await SendRemote(command));
                return results;
            });
        await planner.CompleteAvailableManualHandoff(); Console.WriteLine("TRACE remote-handoff-completed");
        checks.Add("remote-resumed-original-manual-work-and-released-station");
        var drink = await planner.ProduceAndPlate("D31"); Console.WriteLine("TRACE remote-drink-completed");
        var salad = await planner.ProduceAndPlate("F01"); Console.WriteLine("TRACE remote-salad-completed");
        Check(client.LatestBaseline!.State.Observation.Recipe!.Items.Single(i => i.Id == drink).BoundOrder is null,
            "completed-drink-remains-unbound-before-service");
        await planner.WaitUntil(() => client.LatestBaseline!.State.Observation.Lifecycle.State == CookingLevelState.Running, "owner service start");
        await planner.Deliver(salad, "F01");
        await planner.WaitUntil(() => client.LatestBaseline!.State.Observation.Recipe!.AcceptedOrders.Count == 2, "other-player-drink-delivery");
        Check(client.LatestBaseline!.State.Observation.Recipe!.AcceptedOrders.Count == 2, "remote-full-two-order-commit");
        await planner.WaitUntil(() => client.LatestBaseline!.Identity.Scope.LevelEpoch == 2 && client.IsSynchronized, "durable successor baseline ack");
        var instance = client.ServerSessionInstance;
        var token = client.RebindToken;
        await client.ReconnectAsync(address, port);
        Check(client.IsSynchronized && client.ServerSessionInstance == instance && client.RebindToken != token,
            "live-instance-token-rebind-full-baseline");
        var receivedBaseline = client.LatestBaseline!;
        var state = receivedBaseline.State;
        Check(receivedBaseline.Identity.StateHash == CookingNetworkWireCodec.BaselineHash(state, receivedBaseline.Session),
            "received-complete-baseline-identity-hash-verified");
        Check(receivedBaseline.Session.Participants.Select(p => p.Participant).SequenceEqual(
            new[] { ProcessServiceFixture.Chef, ProcessServiceFixture.Partner }) &&
            receivedBaseline.Session.Participants.All(p => p.ConnectionGeneration > 0 && p.ConnectedOwnerBinding),
            "received-configured-two-participant-owner-bindings");
        Check(state.Observation.Lifecycle.State == CookingLevelState.Created && state.Observation.Scope.LevelEpoch == 2,
            "client-passive-created-successor");
        Check(state.ResumableCheckpoint is null && state.CheckpointUnavailableReason != CookingNetworkCheckpointUnavailableReason.None,
            "created-unavailable-resumable-explicit");
        Check(state.FullRecipe is not null || state.CheckpointUnavailableReason == CookingNetworkCheckpointUnavailableReason.NotInitialized,
            "created-readonly-initialization-state-explicit");
        detail = new { hash = CookingNetworkWireCodec.Hash(state), scope = state.Observation.Scope,
            baselineHash = CookingNetworkWireCodec.BaselineHash(state, receivedBaseline.Session),
            serverSessionInstance = client.ServerSessionInstance,
            sessionProjection = receivedBaseline.Session, commandCount = responseMs.Count, projectionOnly = true };
    }
    else throw new ArgumentException("Expected host or client role.");
    WriteReport(true);
    return 0;
}
catch (Exception error)
{
    WriteReport(false, error);
    Console.Error.WriteLine(error);
    return 1;
}
}
