using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using System.Text.Json.Nodes;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

[Trait("Gate", "CookingLevelRuntime")]
public sealed class CookingFrontOfHouseEtTests
{
    private static readonly PlayerId A = new("a"), B = new("b");
    private static readonly StationSlotId Board = new("board");
    private static readonly ItemId Input = new("input");
    private static readonly DefinitionId Raw = new("raw"), Product = new("product"), Plate = new("plate");
    private static readonly RecipeId Recipe = new("recipe");
    private static readonly OrderTemplateId Template = new("order-template");
    private static readonly CookingScope Scope = new(new("front-et"), new("world"), new("match"));
    private static readonly CookingLevelScope Level = new(Scope, new(1), new("level"), 1);

    private sealed class Factory : ICookingFrontOfHouseGameplayFactory
    {
        public CookingConfigurationSnapshot Config { get; }
        public CookingRecipeFixture Fixture { get; }
        public CookingRecipeSimulation Simulation { get; private set; } = null!;
        public CookingFrontOfHouseConfiguration FrontOfHouseConfiguration { get; }
        public Factory(bool flow = true, string policy = "front-policy-v1", int inquiry = 6, int wait = 100)
        {
            var items = new[] { new CookingItemDefinition(Raw, new HashSet<string> { "cook" }),
                new CookingItemDefinition(Product, new HashSet<string> { "cook" }),
                new CookingItemDefinition(Plate, new HashSet<string> { "cook" }, new(1, new HashSet<DefinitionId> { Product })) };
            var appliances = new[] { new CookingApplianceDefinition(Board, new HashSet<string> { "work" }) };
            var recipes = new[] { new CookingRecipeDefinition(Recipe, new[] { Raw }, Product, new("work"), "work", 100, Execution: CookingRecipeExecutionKind.Manual) };
            var orders = new[] { new CookingOrderTemplateDefinition(Template, Recipe, Plate) };
            var spatial = new CookingSpatialConfiguration(0, 0, 7000, 3000, 100, 2200,
                new[] { new CookingPlayerPose(A, 2500, 1500, 1, 0), new CookingPlayerPose(B, 2500, 1900, 1, 0) },
                new[] { new CookingSpatialAnchor(LocationKind.StationSlot, "board", 3500, 1500),
                    new CookingSpatialAnchor(LocationKind.WorldPosition, "table-1", 3500, 1500),
                    new CookingSpatialAnchor(LocationKind.WorldPosition, "washing", 3500, 1500),
                    new CookingSpatialAnchor(LocationKind.WorldPosition, "customer-entrance", 500, 1500),
                    new CookingSpatialAnchor(LocationKind.WorldPosition, "queue", 1500, 1500),
                    new CookingSpatialAnchor(LocationKind.WorldPosition, "exit", 6500, 1500) }, Array.Empty<CookingSpatialObstacle>());
            var registry = new CookingConfigurationRegistry();
            Assert.True(registry.Submit(new(new[] { "work" }, items, appliances, recipes, OrderTemplates: orders, Spatial: spatial)).Accepted);
            Config = registry.Current!;
            Fixture = new(Scope, new Dictionary<PlayerId, CookingPlayerConfig>
                { [A] = new(A, new HashSet<string> { "cook" }, new HashSet<string> { "board" }),
                  [B] = new(B, new HashSet<string> { "cook" }, new HashSet<string> { "board" }) },
                items.ToDictionary(x => x.Id), appliances.ToDictionary(x => x.Station), recipes.ToDictionary(x => x.Id),
                orderTemplates: orders.ToDictionary(x => x.Id), spatial: spatial);
            static CookingFrontPoint[] Path(int start, int end) => Enumerable.Range(start, end - start + 1).Select(x => new CookingFrontPoint(x, 1)).ToArray();
            var paths = new CookingFrontOfHouseFlow(CookingFrontOfHouseFlow.SpatialIdentity(spatial), Path(0, 6), Path(0, 1), Path(1, 6),
                new[] { new CookingFrontTableRoute("table-1", Path(1, 3), Path(3, 6)) }, 2, 3) { Spatial = spatial };
            FrontOfHouseConfiguration = new(new(1, 3, 1, inquiry, 3, 2, wait), new[] { Template }, flow ? paths : null, policy);
        }
        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            Simulation = new(Fixture); Simulation.AddItem(Input, Raw, ItemLocation.Station(Board)); return Simulation;
        }
        public CookingLevelEtHost Start()
        {
            var host = new CookingLevelEtHost(new CookingLevelLifecycle(Level, Config, this));
            Assert.True(host.Prepare(new(Level.Level, new("map"), new(new("layout"), new[] { Board }, new[] { Plate }), Config.Identity)).Accepted);
            Assert.True(host.Start().Accepted); return host;
        }
    }
    private static CookingRecipeCommand Command(Factory factory, CookingLevelEtHost host, CookingRecipeOperation op, string id, PlayerId? player = null,
        string? work = "inquiry:customer-1", ProcessId? process = null) => new(Scope, host.HostFrameSequence + 1, player ?? A, new(id), op,
            Item: op == CookingRecipeOperation.StartProcess ? Input : null,
            Station: op == CookingRecipeOperation.StartProcess ? Board : null,
            ExpectedItemVersion: op == CookingRecipeOperation.StartProcess ? factory.Simulation.Snapshot().Items.Single(x => x.Id == Input).Version : 0,
            Process: process, WorldAnchor: op is CookingRecipeOperation.ClaimFrontWork or CookingRecipeOperation.ContinueFrontWork or CookingRecipeOperation.StopFrontWork ? work : null);
    private static void Ready(CookingLevelEtHost host)
    {
        for (var i = 0; i < 4; i++) Assert.True(host.Tick().Accepted);
        Assert.Equal(CookingFrontWorkStatus.Available, host.FrontOfHouseSnapshot!.Work.Single().Status);
    }
    private static CookingRecipeCommandResult Execute(CookingLevelEtHost host, CookingRecipeCommand command)
    {
        Assert.True(host.TryEnqueue(new(Level, command, "local", command.Command.Value)).Accepted);
        return Assert.Single(host.Tick().Dispositions).Result!;
    }

    [Fact]
    public void Front_claim_uses_the_real_ingress_stable_arbitration_and_duplicate_lane()
    {
        var f = new Factory(); using var host = f.Start(); Ready(host);
        var a = Command(f, host, CookingRecipeOperation.ClaimFrontWork, "claim"); var b = a with { Player = B };
        Assert.True(host.TryEnqueue(new(Level, b, "b", "1")).Accepted);
        Assert.True(host.TryEnqueue(new(Level, a, "a", "1")).Accepted);
        Assert.True(host.TryEnqueue(new(Level, a, "a", "2")).Accepted);
        var frame = host.Tick();
        Assert.Single(frame.Dispositions, x => x.Kind == CookingLevelDispositionKind.Executed && x.Result!.Outcome == CookingRecipeOutcome.Accepted);
        Assert.Single(frame.Dispositions, x => x.Kind == CookingLevelDispositionKind.Duplicate);
        Assert.Equal(A, host.FrontOfHouseSnapshot!.Work.Single().Player);
        Assert.Equal(1, host.FrontOfHouseSnapshot.Work.Single().ElapsedTicks);
        Assert.Empty(f.Simulation.Orders);
        var before = host.FrontOfHouseSnapshot.CanonicalText();
        var replay = host.TryEnqueue(new(Level, a, "again", "again"));
        Assert.True(replay.Accepted); Assert.True(replay.TerminalDisposition!.Result!.IsDuplicate);
        Assert.Equal(before, host.FrontOfHouseSnapshot.CanonicalText());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Kitchen_and_front_manual_claims_are_mutually_exclusive_in_both_command_orders(bool frontFirst)
    {
        var f = new Factory(); using var host = f.Start(); Ready(host);
        var front = Command(f, host, CookingRecipeOperation.ClaimFrontWork, frontFirst ? "a-front" : "b-front");
        var kitchen = Command(f, host, CookingRecipeOperation.StartProcess, frontFirst ? "b-kitchen" : "a-kitchen");
        Assert.True(host.TryEnqueue(new(Level, kitchen, "local", "k")).Accepted);
        Assert.True(host.TryEnqueue(new(Level, front, "local", "f")).Accepted);
        var frame = host.Tick();
        Assert.Single(frame.Dispositions, x => x.Result!.Outcome == CookingRecipeOutcome.Accepted);
        Assert.Equal(CookingRecipeRejectionReason.WorkerUnavailable, Assert.Single(frame.Dispositions, x => x.Result!.Outcome == CookingRecipeOutcome.Rejected).Result!.Reason);
        if (frontFirst) Assert.Empty(f.Simulation.Snapshot().Processes);
        else Assert.Equal(A, Assert.Single(f.Simulation.Snapshot().Processes).ActiveWorker);
    }

    [Fact]
    public void Pause_preserves_claim_and_every_clock_then_continue_and_stop_use_the_same_lane()
    {
        var f = new Factory(); using var host = f.Start(); Ready(host);
        Assert.Equal(CookingRecipeOutcome.Accepted, Execute(host, Command(f, host, CookingRecipeOperation.ClaimFrontWork, "claim")).Outcome);
        var front = host.FrontOfHouseSnapshot!.CanonicalText(); var kitchen = f.Simulation.ExportCheckpoint().CanonicalText(); var frame = host.HostFrameSequence;
        Assert.True(host.Pause().Accepted);
        Assert.Equal(CookingLevelAdmissionReason.LevelPaused, host.TryEnqueue(new(Level, Command(f, host, CookingRecipeOperation.ContinueFrontWork, "continue"), "local", "c")).Reason);
        for (var i = 0; i < 3; i++) Assert.Equal(CookingLevelFrameReason.LevelPaused, host.Tick().Reason);
        Assert.Equal(frame, host.HostFrameSequence); Assert.Equal(front, host.FrontOfHouseSnapshot.CanonicalText()); Assert.Equal(kitchen, f.Simulation.ExportCheckpoint().CanonicalText());
        Assert.True(host.Resume().Accepted);
        Assert.Equal(CookingRecipeOutcome.Accepted, Execute(host, Command(f, host, CookingRecipeOperation.ContinueFrontWork, "continue")).Outcome);
        Assert.Equal(2, host.FrontOfHouseSnapshot.Work.Single().ElapsedTicks);
        Assert.Equal(CookingRecipeOutcome.Accepted, Execute(host, Command(f, host, CookingRecipeOperation.StopFrontWork, "stop")).Outcome);
        Assert.Null(host.FrontOfHouseSnapshot.Work.Single().Player); Assert.True(host.FrontOfHouseSnapshot.Work.Single().Companion);
        Assert.Equal(3, host.FrontOfHouseSnapshot.Work.Single().ElapsedTicks);
    }

    [Fact]
    public void Work_ids_participate_in_fingerprint_and_shape_and_scope_validation()
    {
        var f = new Factory(); using var host = f.Start(); Ready(host);
        var c = Command(f, host, CookingRecipeOperation.ClaimFrontWork, "claim"); var envelope = new CookingLevelCommandEnvelope(Level, c, "local", "c");
        Assert.NotEqual(CookingCommandFingerprint.Create(envelope), CookingCommandFingerprint.Create(envelope with { Command = c with { WorldAnchor = "inquiry:customer-2" } }));
        Assert.Equal(CookingLevelAdmissionReason.MalformedCommand, host.TryEnqueue(envelope with { Command = c with { Item = Input } }).Reason);
        Assert.Equal(CookingLevelAdmissionReason.MalformedCommand, host.TryEnqueue(envelope with { Command = c with { WorldAnchor = null } }).Reason);
        Assert.Equal(CookingLevelAdmissionReason.ScopeMismatch, host.TryEnqueue(envelope with { LevelScope = new(Scope, new(1), new("level"), 2) }).Reason);
        Assert.True(host.TryEnqueue(envelope).Accepted);
        Assert.Equal(CookingLevelAdmissionReason.CommandIdentityConflict, host.TryEnqueue(envelope with { Command = c with { WorldAnchor = "different" } }).Reason);
        Assert.Null(host.FrontOfHouseSnapshot!.Work.Single().Player);
    }

    [Fact]
    public void Turning_away_releases_manual_work_using_current_authoritative_pose()
    {
        var f = new Factory(); using var host = f.Start(); Ready(host);
        Execute(host, Command(f, host, CookingRecipeOperation.ClaimFrontWork, "claim"));
        var turn = new CookingRecipeCommand(Scope, host.HostFrameSequence + 1, A, new("turn"), CookingRecipeOperation.Move, FacingX: -1);
        Assert.Equal(CookingRecipeOutcome.Accepted, Execute(host, turn).Outcome);
        Assert.Null(host.FrontOfHouseSnapshot!.Work.Single().Player);
        Assert.True(host.FrontOfHouseSnapshot.Work.Single().Companion);
    }

    [Fact]
    public void Codec_six_requires_front_fields_and_destroy_restore_rebinds_the_new_kitchen()
    {
        var f = new Factory(); CookingLevelCheckpoint saved; string finalFront; string finalKitchen;
        using (var host = f.Start())
        {
            Ready(host); Execute(host, Command(f, host, CookingRecipeOperation.ClaimFrontWork, "claim"));
            saved = host.ExportCheckpoint().Checkpoint!;
            var text = CookingLevelCheckpointCodec.Serialize(CookingLevelCheckpointCodec.CreateEnvelope(saved));
            Assert.Equal(8, CookingLevelCheckpointCodec.CurrentFormatVersion);
            Assert.False(CookingLevelCheckpointCodec.Deserialize(text.Replace("\"formatVersion\":8", "\"formatVersion\":5")).Accepted);
            var missing = JsonNode.Parse(text)!; missing["checkpoint"]!.AsObject().Remove("frontOfHouseConfigurationIdentity");
            Assert.False(CookingLevelCheckpointCodec.Deserialize(missing.ToJsonString()).Accepted);
            missing = JsonNode.Parse(text)!; missing["checkpoint"]!["frontOfHouse"]!["state"]!.AsObject().Remove("manualPolicyIdentity");
            Assert.False(CookingLevelCheckpointCodec.Deserialize(missing.ToJsonString()).Accepted);
            saved = CookingLevelCheckpointCodec.Deserialize(text).Checkpoint!;
            for (var i = 0; i < 4; i++) host.Tick();
            finalFront = host.FrontOfHouseSnapshot!.CanonicalText(); finalKitchen = f.Simulation.Snapshot().CanonicalText();
        }
        var newFactory = new Factory(); var restored = CookingLevelEtHost.Restore(saved, newFactory.Config, newFactory);
        Assert.True(restored.Accepted, restored.ToString()); using var result = restored.Host!;
        for (var i = 0; i < 4; i++) result.Tick();
        Assert.Equal(finalFront, result.FrontOfHouseSnapshot!.CanonicalText()); Assert.Equal(finalKitchen, newFactory.Simulation.Snapshot().CanonicalText());
        Assert.Equal(0, result.FrontOfHouseSnapshot.Companion.CompletedTaskCount);
    }

    [Fact]
    public void Trusted_factory_rejects_forged_policy_geometry_and_dual_owner_restore()
    {
        var f = new Factory(); CookingLevelCheckpoint saved;
        using (var host = f.Start())
        {
            Ready(host); Execute(host, Command(f, host, CookingRecipeOperation.ClaimFrontWork, "claim"));
            Execute(host, Command(f, host, CookingRecipeOperation.StartProcess, "kitchen-b", B));
            saved = host.ExportCheckpoint().Checkpoint!;
        }
        var changed = new Factory(policy: "forged-policy"); Assert.False(CookingLevelEtHost.Restore(saved, changed.Config, changed).Accepted);
        var original = new Factory();
        var poison = saved with { FrontOfHouse = saved.FrontOfHouse! with { State = saved.FrontOfHouse.State with
            { Work = saved.FrontOfHouse.State.Work.Select(x => x.Player == A ? x with { Player = B } : x).ToArray() } } };
        Assert.False(CookingLevelEtHost.Restore(poison, original.Config, original).Accepted);
        poison = saved with { FrontOfHouse = saved.FrontOfHouse! with { State = saved.FrontOfHouse.State with { ManualPolicyIdentity = null } } };
        Assert.False(CookingLevelEtHost.Restore(poison, original.Config, original).Accepted);
        poison = saved with { FrontOfHouse = saved.FrontOfHouse! with { State = saved.FrontOfHouse.State with
            { Flow = saved.FrontOfHouse.State.Flow! with { QueueCapacity = 999 } } } };
        Assert.False(CookingLevelEtHost.Restore(poison, original.Config, original).Accepted);
        poison = saved with { FrontOfHouse = saved.FrontOfHouse! with { State = saved.FrontOfHouse.State with { ServiceTicks = 2, Closing = false } } };
        Assert.False(CookingLevelEtHost.Restore(poison, original.Config, original).Accepted);
        var restored = CookingLevelEtHost.Restore(saved, original.Config, original); Assert.True(restored.Accepted, restored.ToString()); restored.Host!.Dispose();
    }

    [Fact]
    public void Initial_flow_drains_real_paths_and_dirty_tables_to_natural_success_with_unmet_orders()
    {
        var f = new Factory(inquiry: 2, wait: 8); using var host = f.Start();
        Assert.False(host.BeginEnd(CookingLevelOutcome.Success).Accepted);
        var sawQueue = false; var sawDirty = false;
        for (var i = 0; i < 100 && !(host.FrontOfHouseSnapshot?.Closing == true && f.Simulation.Snapshot().IsCompleted); i++)
        {
            host.Tick(); var state = host.FrontOfHouseSnapshot!;
            sawQueue |= state.Customers.Any(x => x.Phase == CookingTablePhase.Queued);
            sawDirty |= state.Tables.Any(x => x.State == CookingFrontTableState.Dirty);
        }
        Assert.True(sawQueue); Assert.True(sawDirty); Assert.True(f.Simulation.Snapshot().IsCompleted);
        Assert.NotEmpty(host.FrontOfHouseSnapshot!.UnsatisfiedOrders); Assert.Empty(f.Simulation.SettlementHistory);
        Assert.Equal(0, f.Simulation.CalculateStars());
        Assert.True(host.TryFinishService().Accepted); Assert.True(host.CompleteEnd().Accepted);
        Assert.True(host.CreateSuccessor(new("next-level"), 2).Accepted);
        Assert.Equal(CookingLevelState.Created, host.Lifecycle.State);
        Assert.Empty(host.FrontOfHouseSnapshot.Customers);
    }
}
