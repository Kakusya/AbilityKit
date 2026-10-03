using System.Collections.Concurrent;
using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using AbilityKit.Game.Cooking.Session;
using AbilityKit.Network.Host.InProcess;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

[Trait("Gate", "CookingLevelRuntime")]
public sealed class CookingNetworkRichLiveRecoveryEtTests
{
    private sealed class Peer : IDisposable
    {
        private readonly ConnectionManager _connection;
        private readonly ConcurrentDictionary<string, CookingNetworkWireResult> _results = new();
        private readonly ConcurrentQueue<string> _ready = new();
        private long _sequence;
        public PlayerId Player { get; }
        public CookingNetworkJoined? Binding { get; private set; }
        public CookingNetworkBaseline? Latest { get; private set; }
        public string? DropResult { get; set; }
        public int Dropped { get; private set; }
        public ConcurrentQueue<string> Rejections { get; } = new();
        public Peer(CookingNetworkSessionHost session, PlayerId player)
        {
            Player = player; _connection = new(session.CreateLocalClientTransport, new ConnectionOptions { EnableReconnect = false });
            _connection.ServerPushReceived += (_, bytes) => {
                Assert.True(CookingNetworkWireCodec.TryDecode(bytes.AsSpan(), new(), out var envelope));
                switch (envelope!.Kind) {
                    case CookingNetworkMessageKind.Joined: Binding = CookingNetworkWireCodec.Read<CookingNetworkJoined>(envelope)!; break;
                    case CookingNetworkMessageKind.Baseline:
                        Latest = CookingNetworkWireCodec.Read<CookingNetworkBaseline>(envelope)!;
                        Send(CookingNetworkMessageKind.BaselineAck, "ack-" + Latest.Identity.SnapshotSequence, Latest.Identity); break;
                    case CookingNetworkMessageKind.Ready: _ready.Enqueue(envelope.CorrelationId); break;
                    case CookingNetworkMessageKind.Rejected:
                        Rejections.Enqueue(envelope.CorrelationId + ":" + CookingNetworkWireCodec.Read<CookingNetworkWireResult>(envelope)!.Reason); break;
                    case CookingNetworkMessageKind.CommandResult:
                        if (DropResult == envelope.CorrelationId) { Dropped++; return; }
                        _results[envelope.CorrelationId] = CookingNetworkWireCodec.Read<CookingNetworkWireResult>(envelope)!; break;
                }
            };
            _connection.Open("inprocess", 1);
        }
        private void Send<T>(CookingNetworkMessageKind kind, string correlation, T value) => _connection.Send(CookingNetworkWireCodec.OpCode,
            new ArraySegment<byte>(CookingNetworkWireCodec.Encode(kind, correlation, value)), (ushort)NetworkPacketFlags.ServerPush);
        public void Join(CookingNetworkSessionHost session, CookingNetworkJoined? prior = null)
        {
            Send(CookingNetworkMessageKind.Join, "join", new CookingNetworkJoin(Player, "rich-" + Player.Value,
                prior?.ServerSessionInstance, prior?.RebindToken));
            Pump(session, () => _ready.Count != 0);
            Assert.NotNull(Binding); Assert.NotNull(Latest);
            if (prior is not null) { Assert.Equal(prior.ConnectionGeneration + 1, Binding!.ConnectionGeneration); Assert.NotEqual(prior.RebindToken, Binding.RebindToken); }
        }
        public void Queue(CookingNetworkSessionHost session, CookingRecipeCommand command, string stable)
        {
            Send(CookingNetworkMessageKind.Command, stable, new CookingNetworkWireCommand(Binding!.ServerSessionInstance,
                Binding.ConnectionGeneration, ++_sequence, stable, session.LatestCapture!.Observation.Scope, command));
        }
        public bool Has(string stable) => _results.ContainsKey(stable);
        public CookingNetworkWireResult Result(string stable) => _results[stable];
        public void Dispose() => _connection.Dispose();
    }
    private static void Pump(CookingNetworkSessionHost session, Func<bool> ready)
    {
        var deadline = Environment.TickCount64 + 15000;
        while (!ready() && Environment.TickCount64 < deadline) { session.ProcessOwnerFrame(); Thread.Sleep(1); }
        Assert.True(ready(), "Rich framed recovery response did not complete within bounded owner pumping.");
    }

    [Theory]
    [InlineData("manual-paused")]
    [InlineData("automatic-active")]
    [InlineData("unbound-cup")]
    [InlineData("submitted-reply-lost")]
    public async Task Real_F01_D31_finite_chain_recovers_cutpoint_naturally_closes_and_creates_durable_successor(string cutpoint)
    {
        var started = Environment.TickCount64;
        var phase = "initializing";
        var commandCount = 0;
        var activityDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "local", "Logs", "cooking-execution", "network-rich-recovery"));
        Directory.CreateDirectory(activityDirectory);
        var activity = Path.Combine(activityDirectory, "rich-stage-" + cutpoint + "-" + Guid.NewGuid().ToString("N") + ".log");
        void Mark(string next) {
            phase = next;
            File.AppendAllText(activity, $"{DateTimeOffset.UtcNow:O} elapsed={Environment.TickCount64 - started} phase={phase} commands={commandCount}{Environment.NewLine}");
        }
        void CheckBudget() => Assert.True(Environment.TickCount64 - started < 600000,
            $"Rich whole-case 600s fixture budget exceeded: cutpoint={cutpoint}, phase={phase}, commands={commandCount}, activity={activity}");
        Mark(phase);
        var fixture = new CookingRichRecoveryFixture();
        using var host = new CookingLevelEtHost(new CookingLevelLifecycle(CookingRichRecoveryFixture.InitialScope, fixture.Content.Snapshot, fixture));
        Assert.True(host.BeginPreparation(fixture.Preparation(host.Binding.LevelScope)).Accepted);
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "rich-network-recovery-" + Guid.NewGuid().ToString("N")));
        var progress = new CookingMajorProgress(); progress.Lock();
        var store = new CookingMajorCheckpointStore(root);
        var captured = new List<CookingNetworkAuthorityCapture>();
        using var session = new CookingNetworkSessionHost(new CookingNetworkAuthorityAdapter(host, request => {
            if (request == "start") { var prepared = host.CompletePreparation(); return prepared.Accepted ? host.Start() : prepared; }
            if (request == "successor") {
                var finished = host.TryFinishService(); if (!finished.Accepted) return finished;
                var ended = host.CompleteEnd(); if (!ended.Accepted) return ended;
                captured.Add(new CookingNetworkAuthorityAdapter(host).CaptureFullState().State!);
                var target = new CookingLevelScope(host.Binding.LevelScope.MatchScope, host.Binding.LevelScope.RestaurantRuntime, new("service-2"), 2);
                var next = host.CreateSuccessor(target.Level, target.LevelEpoch, fixture.Preparation(target), progress, store);
                return new(next.Accepted, next.Reason.ToString(), host.Lifecycle.State.ToString(), host.Lifecycle.Version, Array.Empty<CookingLevelPendingDisposition>());
            }
            return new(false, "Unauthorized", host.Lifecycle.State.ToString(), host.Lifecycle.Version, Array.Empty<CookingLevelPendingDisposition>());
        }), new InProcessChannelListener(), new Dictionary<PlayerId, string> {
            [CookingRichRecoveryFixture.Chef] = "rich-" + CookingRichRecoveryFixture.Chef.Value,
            [CookingRichRecoveryFixture.Partner] = "rich-" + CookingRichRecoveryFixture.Partner.Value });
        session.Start();
        var peers = new Dictionary<PlayerId, Peer>();
        foreach (var player in new[] { CookingRichRecoveryFixture.Chef, CookingRichRecoveryFixture.Partner }) {
            var peer = new Peer(session, player); peers.Add(player, peer); peer.Join(session);
        }
        var injected = false;
        var planners = new Dictionary<PlayerId, CookingRichRecoveryPlanner>();
        long manualElapsed = -1; ProcessId? manualProcess = null;
        var servedPortions = 0;
        void Rebind(PlayerId player, bool paused)
        {
            var prior = peers[player].Binding!;
            captured.Add(session.LatestCapture!);
            if (paused) Assert.True(session.ApplyControl(new(CookingNetworkControlKind.Pause, "cutpoint-pause")).Accepted);
            var pausedRecipe = host.Observe().CanonicalText(); var frame = host.HostFrameSequence;
            peers[player].Dispose();
            session.ProcessOwnerFrame();
            if (paused) {
                for (var i = 0; i < 2; i++) session.ProcessOwnerFrame();
                Assert.Equal(pausedRecipe, host.Observe().CanonicalText()); Assert.Equal(frame, host.HostFrameSequence);
                Assert.True(session.ApplyControl(new(CookingNetworkControlKind.Resume, "cutpoint-resume")).Accepted);
                session.ProcessOwnerFrame();
                var remaining = host.Observe().Recipe!.Processes.Single(p => p.Id == manualProcess);
                Assert.Null(remaining.ActiveWorker); Assert.Equal(manualElapsed, remaining.ElapsedTicks);
            }
            var fresh = new Peer(session, player); peers[player] = fresh; fresh.Join(session, prior);
            captured.Add(session.LatestCapture!);
            Assert.True(session.LatestSessionProjection.Participants.Single(p => p.Participant == player).Ready);
        }
        async Task<CookingRecipeCommandResult> Send(PlayerId player, CookingRecipeCommand command)
        {
            CheckBudget(); commandCount++;
            if (commandCount % 50 == 0) Mark(phase);
            var peer = peers[player]; var stable = command.Command.Value;
            var beforePortion = command.Operation == CookingRecipeOperation.ServePortion ? session.LatestCapture!.FullRecipe : null;
            var sourcePortions = beforePortion?.Items.Single(i => i.Id == command.Item).RemainingPortions;
            var targetUnits = beforePortion?.Items.Count(i => !i.Removed && i.Location.Kind == LocationKind.ContainerSlot && i.Location.OwnerId == command.Container!.Value.Value);
            var lost = !injected && cutpoint == "submitted-reply-lost" && command.Operation == CookingRecipeOperation.SubmitOrder;
            if (lost) peer.DropResult = stable;
            peer.Queue(session, command, stable);
            if (lost) {
                session.ProcessOwnerFrame(); Assert.Equal(1, peer.Dropped); Assert.False(peer.Has(stable));
                Assert.Single(peer.Latest!.State.FullRecipe!.Settlements); // Complete baseline reached the application despite the dropped reply.
                var committed = session.LatestCapture!.FullRecipe!;
                var tombstones = committed.Items.Where(i => i.Removed).ToArray();
                var originalId = CookingNetworkWireCodec.DomainId(session.ServerSessionInstance, host.Binding.LevelScope, player, stable);
                Rebind(player, false);
                peer = peers[player]; peer.Queue(session, command, stable); Pump(session, () => peer.Has(stable));
                var replay = peer.Result(stable); Assert.Equal(originalId, replay.DomainCommandId!.Value);
                Assert.True(replay.Result!.IsDuplicate); Assert.Equal(CookingRecipeOutcome.Accepted, replay.Result.Outcome);
                Assert.Equal(committed.Settlements.ToArray(), session.LatestCapture!.FullRecipe!.Settlements.ToArray());
                Assert.Equal(tombstones, session.LatestCapture.FullRecipe.Items.Where(i => i.Removed).ToArray());
                injected = true; return replay.Result;
            }
            Pump(session, () => peer.Has(stable)); var result = peer.Result(stable);
            Assert.True(result.Result?.Outcome == CookingRecipeOutcome.Accepted,
                $"{player}/{command.Operation}/{command.Item}/{command.Recipe}: wire={result.Reason}, domain={result.Result?.Reason}; poses={System.Text.Json.JsonSerializer.Serialize(host.Observe().Recipe!.Poses)}");
            if (beforePortion is not null) {
                var afterPortion = session.LatestCapture!.FullRecipe!;
                Assert.True(sourcePortions > 0);
                Assert.Equal(sourcePortions - 1, afterPortion.Items.Single(i => i.Id == command.Item).RemainingPortions);
                Assert.Equal(targetUnits + 1, afterPortion.Items.Count(i => !i.Removed && i.Location.Kind == LocationKind.ContainerSlot && i.Location.OwnerId == command.Container!.Value.Value));
                Assert.True(afterPortion.NextProductId > beforePortion.NextProductId); servedPortions++;
            }
            if (!injected && command.Operation == CookingRecipeOperation.StartProcess) {
                var recipe = fixture.Content.Recipes[command.Recipe!.Value];
                if (cutpoint == "manual-paused" && host.Lifecycle.State == CookingLevelState.Running && recipe.Execution == CookingRecipeExecutionKind.Manual) {
                    var active = host.Observe().Recipe!.Processes.Single(p => p.Anchor == command.Item);
                    Assert.Equal(player, active.ActiveWorker); manualProcess = active.Id; manualElapsed = active.ElapsedTicks;
                    Rebind(player, true); injected = true;
                    // Move the old worker out through real framed commands before another actor approaches the station.
                    await planners[player].Go("chef-parking");
                    SyncBoth();
                    await planners[CookingRichRecoveryFixture.Partner].CompleteAvailableManualHandoff();
                    await planners[CookingRichRecoveryFixture.Partner].Go("partner-parking");
                    SyncBoth();
                } else if (cutpoint == "automatic-active" && recipe.Execution == CookingRecipeExecutionKind.Automatic) {
                    var active = host.Observe().Recipe!.Processes.Single(p => p.Anchor == command.Item);
                    Assert.True(active.ElapsedTicks < active.RequiredTicks); Assert.Null(active.ActiveWorker);
                    Rebind(player, false); injected = true;
                }
            }
            if (!injected && cutpoint == "unbound-cup" && command.Operation == CookingRecipeOperation.PutIn &&
                command.Container == fixture.ServingVessels["D31"]) {
                var product = host.Observe().Recipe!.Items.Single(i => i.Id == command.Item); Assert.Null(product.BoundOrder);
                Assert.Equal(fixture.ServingVessels["D31"].Value, product.Location.OwnerId);
                Rebind(player, false);
                Assert.Equal(product, host.Observe().Recipe!.Items.Single(i => i.Id == command.Item)); injected = true;
            }
            return result.Result!;
        }
        void SyncBoth()
        {
            CheckBudget();
            var required = host.Observe().Recipe!.Version;
            try { Pump(session, () => peers.Values.All(p => p.Latest!.State.Observation.Recipe!.Version >= required)); }
            catch (Exception error) { throw new InvalidOperationException($"Fixed barrier required={required}; owner={host.Observe().Recipe!.Version}; view={System.Text.Json.JsonSerializer.Serialize(session.LatestSessionProjection)}; peers={string.Join(" | ", peers.Values.Select(p => $"{p.Player}:seq={p.Latest?.Identity.SnapshotSequence}/ver={p.Latest?.State.Observation.Recipe?.Version}/reject={string.Join(",", p.Rejections.TakeLast(6))}"))}", error); }
        }
        CookingRichRecoveryPlanner Planner(PlayerId player) => new(fixture, player,
            () => peers[player].Latest!.State, command => Send(player, command),
            () => { CheckBudget(); session.ProcessOwnerFrame(); Thread.Sleep(1); return Task.CompletedTask; }, async commands => {
                var results = new List<CookingRecipeCommandResult>();
                foreach (var command in commands) results.Add(await Send(player, command));
                return results;
            });
        try {
            var chef = Planner(CookingRichRecoveryFixture.Chef); var partner = Planner(CookingRichRecoveryFixture.Partner);
            planners.Add(CookingRichRecoveryFixture.Chef, chef); planners.Add(CookingRichRecoveryFixture.Partner, partner);
            Mark("procurement-and-shared-manual-preparation");
            await chef.Procure(); await chef.PrepareManualHandoff(); SyncBoth();
            await partner.CompleteAvailableManualHandoff();
            Mark("drink-preparation");
            var drink = await partner.ProduceAndPlate("D31"); await partner.Go("partner-parking"); SyncBoth();
            await chef.PrepareComponents("F01"); await chef.Go("chef-parking"); SyncBoth();
            Assert.Equal(0, host.FrontOfHouseSnapshot!.ServiceTicks);
            Assert.NotEmpty(session.LatestCapture!.FullRecipe!.Supply!.Deliveries);
            Assert.True(session.ApplyControl(new(CookingNetworkControlKind.ExecuteAuthorizedTransition, "start")).Accepted);
            Mark("running-drink-delivery");
            SyncBoth(); await chef.Deliver(drink, "D31"); SyncBoth();
            Mark("running-salad-assembly-and-delivery");
            var salad = await chef.ProduceAndPlate("F01"); await chef.Go("chef-parking"); SyncBoth();
            if (cutpoint == "manual-paused") Assert.DoesNotContain(host.Observe().Recipe!.Processes, p => p.Id == manualProcess);
            await partner.Deliver(salad, "F01");
            Assert.True(injected, "Required rich recovery cutpoint was not observed.");
            Assert.True(servedPortions > 0, "Rich preparation must exercise actual portion extraction and conservation.");
            Mark("natural-close-and-durable-successor");
            CookingNetworkControlResult? transitioned = null;
            for (var i = 0; i < 1000 && transitioned?.Accepted != true; i++) {
                CheckBudget();
                transitioned = session.ApplyControl(new(CookingNetworkControlKind.ExecuteAuthorizedTransition, "successor"));
                if (!transitioned.Accepted) session.ProcessOwnerFrame();
            }
            Assert.True(transitioned?.Accepted == true, "Rich service did not naturally close within its fixture budget.");
            Mark("successor-full-baseline-verification");
            var ended = captured.Last();
            Assert.Equal(2, ended.FullRecipe!.Settlements.Count); Assert.Single(ended.FullFront!.State.UnsatisfiedOrders);
            Assert.Equal(0, ended.Observation.Recipe!.Stars); Assert.True(ended.Observation.Recipe.IsCompleted);
            Assert.True(ended.FullFront.State.Closing); Assert.Empty(ended.FullFront.State.WashQueue);
            Assert.All(ended.FullFront.State.Tables, t => Assert.Equal(CookingFrontTableState.Free, t.State));
            Assert.Equal(CookingLevelState.Created, host.Lifecycle.State); Assert.Equal(2, host.Binding.LevelScope.LevelEpoch);
            var saved = store.ReadBaseline(host.Binding.LevelScope.MatchScope); Assert.True(saved.Accepted);
            Assert.Equal(ended.FullRecipe.NextProductId, saved.Payload!.Kitchen.NextProductId);
            Assert.Equal(ended.FullRecipe.Supply!.Balances, saved.Payload.Kitchen.Supply!.Balances);
            Assert.Equal(ended.FullRecipe.Supply.Deliveries, saved.Payload.Kitchen.Supply.Deliveries);
            // Owner sends the new complete scope baseline; exact automatic ACK must make both existing participants Ready.
            Pump(session, () => session.LatestSessionProjection.Participants.All(p => p.Ready));
            foreach (var peer in peers.Values) {
                Assert.Equal(host.Binding.LevelScope, peer.Latest!.Identity.Scope);
                Assert.Equal(session.LatestCapture!.FullRecipe!.CanonicalText(), peer.Latest.State.FullRecipe!.CanonicalText());
                Assert.Equal(session.LatestCapture.FullFront!.CanonicalText(), peer.Latest.State.FullFront!.CanonicalText());
                Assert.NotEmpty(peer.Latest.State.FullRecipe.Supply!.Deliveries);
            }
            Mark("completed");
            Assert.True(captured.Count >= 3); Assert.All(captured, c => {
                Assert.NotNull(c.FullRecipe); Assert.NotNull(c.FullFront); Assert.NotNull(c.InstalledLayout);
                Assert.NotNull(c.FullRecipe!.Supply);
            });
        }
        finally {
            foreach (var peer in peers.Values) peer.Dispose();
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.StartsWith(temp, root, StringComparison.OrdinalIgnoreCase); Assert.StartsWith("rich-network-recovery-", Path.GetFileName(root), StringComparison.Ordinal);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}

