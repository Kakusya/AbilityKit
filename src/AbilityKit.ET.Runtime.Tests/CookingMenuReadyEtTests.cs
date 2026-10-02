using System.Text.Json;
using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

[Trait("Gate", "CookingLevelRuntime")]
public sealed class CookingMenuReadyEtTests
{
    private static readonly CookingScope Scope = new(new("menu-ready"), new("world"), new("match"));
    private static readonly CookingLevelScope Level = new(Scope, new(1), new("first"), 1);
    private static readonly PlayerId Chef = new("chef");
    private sealed class Factory : ICookingMenuGameplayFactory, ICookingScopedFrontOfHouseGameplayFactory
    {
        public CookingMenuCatalog Catalog { get; }
        public CookingContent Content { get; }
        public CookingLevelMenuConfiguration Policy { get; set; }
        public bool FrontMismatch { get; set; }
        public bool NullCatalog { get; set; }
        public bool AlternatingMenu { get; set; }
        public int MenuReads { get; private set; }
        public bool MissingAppliances { get; set; }
        public CookingRecipeSimulation Simulation { get; private set; } = null!;
        public int CreateCount { get; private set; }
        public Factory()
        {
            Catalog = CookingMenuCatalog.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, CookingMenuCatalog.ContentFileName)));
            var baseline = JsonSerializer.Deserialize<CookingContentDocument>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                CookingContentCatalog.ContentFileName)), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            var loaded = Catalog.LoadContent(baseline, new[] { "F01", "D31" });
            var supplies = loaded.StandardInitialSupply.Select(e => e with { Count = 1,
                Location = e.Location.StartsWith("world:") ? "world:seed-" + e.Definition.Value : e.Location }).ToArray();
            var candidate = loaded.Candidate with { StandardInitialSupply = supplies };
            var registry = new CookingConfigurationRegistry(); Assert.True(registry.Submit(candidate).Accepted);
            Content = loaded with { Candidate = candidate, StandardInitialSupply = supplies, Identity = registry.Current!.Identity, Snapshot = registry.Current };
            Policy = new(Catalog.Sha256, new[] { "F01" }, Content.Items.Keys.ToHashSet(), new HashSet<DefinitionId>(), Content.Items.Keys.ToHashSet(), true);
        }
        public CookingMenuCatalog CreateMenuCatalog(CookingLevelScope scope, CookingConfigurationSnapshot configuration) => NullCatalog ? null! : Catalog;
        public CookingLevelMenuConfiguration CreateMenuConfiguration(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            MenuReads++;
            var policy = scope.Level == Level.Level ? Policy : Policy with { SelectedMenuIds = new[] { "D31" } };
            return AlternatingMenu && MenuReads > 1 ? policy with { BindingCommandsEnabled = false } : policy;
        }
        private OrderTemplateId[] Templates(CookingLevelScope scope) => Catalog.Document.Menus
            .Where(m => (FrontMismatch ? new[] { "F01", "D31" } : scope.Level == Level.Level ? Policy.SelectedMenuIds : new[] { "D31" }).Contains(m.SourceId))
            .Select(m => m.OrderTemplate).ToArray();
        public CookingFrontOfHouseConfiguration FrontOfHouseConfiguration => CreateFrontOfHouseConfiguration(Level, Content.Snapshot);
        public CookingFrontOfHouseConfiguration CreateFrontOfHouseConfiguration(CookingLevelScope scope, CookingConfigurationSnapshot configuration) =>
            new(new(1, 20, 3, 1, 2, 2, 100), Templates(scope));
        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            CreateCount++;
            var player = new CookingPlayerConfig(Chef, Content.Items.Values.SelectMany(i => i.AllowedPlayerCapabilities).ToHashSet(), Content.Appliances.Keys.Select(s => s.Value).ToHashSet());
            var preparation = CreatePreparationConfiguration(scope, configuration);
            var projected = preparation.Project(preparation.InitialLayout, new[] { new CookingPlayerPose(Chef, 500, 1500, 1, 0) }, Content.Appliances);
            Assert.True(projected.Accepted);
            var fixture = CookingContentCatalog.BuildFixture(Content, Scope, new Dictionary<PlayerId, CookingPlayerConfig> { [Chef] = player }, spatial: projected.Geometry);
            if (MissingAppliances) fixture = new(Scope, fixture.Players, fixture.Items,
                fixture.Appliances.ToDictionary(p => p.Key, p => p.Value with { IsAvailable = false }), fixture.Recipes,
                fixture.WashableContainerDefinitions, fixture.CleanContainerSupply, fixture.CleanPoolLocation, fixture.OrderTemplates, spatial: fixture.Spatial, supply: fixture.Supply);
            Simulation = new(fixture);
            if (scope.LevelEpoch == 1 || scope.Level != Level.Level) CookingContentCatalog.ApplyStandardInitialSupply(Simulation, Content);
            return Simulation;
        }
        public CookingPreparationConfiguration CreatePreparationConfiguration(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            var placements = Content.Appliances.Keys.OrderBy(s => s.Value).Select((s, i) => new CookingEquipmentPlacement(s, new("machine-" + s.Value),
                new(i % 20 + 2, i / 20 * 3 + 5), 1, 1, 0, 0, -1)).ToArray();
            var targets = Content.StandardInitialSupply.Where(s => s.Location.StartsWith("world:")).Select(s => s.Location[6..])
                .Append(CookingContentCatalog.CleanPoolLocation).Distinct().Select((id, i) => new CookingLayoutTarget(id, CookingLayoutTargetKind.Storage, new(i % 20 + 2, 1))).ToList();
            targets.AddRange(new[] { new CookingLayoutTarget("player", CookingLayoutTargetKind.PlayerEntrance, new(0, 1)),
                new("customer-entrance", CookingLayoutTargetKind.CustomerEntrance, new(0, 15)), new("exit", CookingLayoutTargetKind.Exit, new(29, 15)),
                new("queue", CookingLayoutTargetKind.Queue, new(0, 16)), new("table-1", CookingLayoutTargetKind.Table, new(2, 16)) });
            var layout = new CookingRestaurantLayout(new("menu-layout"), new[] { new CookingFloorRegion("floor", 0, 0, 30, 20) }, placements,
                Array.Empty<CookingLayoutCell>(), targets);
            var footprints = placements.ToDictionary(p => p.Definition, p => new CookingEquipmentFootprint(p.Definition, 1, 1, 0, -1));
            return new(layout, footprints, new(250, 50000, 1000), footprints.Keys.ToHashSet(), footprints.Keys.ToHashSet(),
                placements.ToDictionary(p => p.Station, p => p.Definition), 2, 3);
        }
        public CookingLevelPreparation Preparation(LevelId level) => new(level, new("map"), new(new("logical"), Content.Appliances.Keys.ToArray(),
            Content.Items.Values.Where(i => i.Container is not null).Select(i => i.Id).ToArray()), Content.Snapshot.Identity);
        public CookingLevelEtHost Host() => new(new CookingLevelLifecycle(Level, Content.Snapshot, this));
    }
    [Fact]
    public void Current_menu_subset_of_loaded_graph_reaches_ready_using_one_real_preparing_kitchen()
    {
        var factory = new Factory(); using var host = factory.Host(); var begun = host.BeginPreparation(factory.Preparation(Level.Level)); Assert.True(begun.Accepted, begun.ToString());
        var kitchen = host.Driver.Simulation; var result = host.CompletePreparation();
        Assert.True(result.Accepted, string.Join(" | ", result.MenuDiagnostics ?? Array.Empty<CookingMenuReadyDiagnostic>()));
        Assert.Same(kitchen, host.Driver.Simulation); Assert.Equal(1, factory.CreateCount);
        Assert.Equal(new[] { factory.Catalog.Document.Menus.Single(m => m.SourceId == "F01").OrderTemplate }, host.Observe().Front!.OrderMenu);
    }
    [Theory]
    [InlineData("appliance")][InlineData("material")][InlineData("front")]
    public void Ready_rejection_has_structured_diagnostics_and_changes_no_owner_state(string defect)
    {
        var factory = new Factory();
        if (defect == "appliance") factory.MissingAppliances = true;
        if (defect == "material") factory.Policy = factory.Policy with { AllowedMaterialDefinitions = new HashSet<DefinitionId>() };
        if (defect == "front") factory.FrontMismatch = true;
        using var host = factory.Host(); var begun = host.BeginPreparation(factory.Preparation(Level.Level)); Assert.True(begun.Accepted, begun.ToString());
        var before = host.Observe().CanonicalText(); var result = host.CompletePreparation();
        Assert.False(result.Accepted); Assert.Equal("MenuNotReady", result.Reason); Assert.NotEmpty(result.MenuDiagnostics!);
        Assert.Equal(before, host.Observe().CanonicalText()); Assert.Equal(1, factory.CreateCount);
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void Preparing_and_running_restore_reconstruct_trusted_menu_identity_and_same_owned_state(bool running)
    {
        var factory = new Factory(); var host = factory.Host();
        Assert.True(host.BeginPreparation(factory.Preparation(Level.Level)).Accepted); host.Tick();
        if (running) { Assert.True(host.CompletePreparation().Accepted); Assert.True(host.Start().Accepted); host.Tick(); }
        var saved = host.ExportCheckpoint().Checkpoint!; var observation = host.Observe().CanonicalText(); host.Dispose();
        var changed = new Factory(); changed.Policy = changed.Policy with { BindingCommandsEnabled = false };
        Assert.False(CookingLevelEtHost.Restore(saved, changed.Content.Snapshot, changed).Accepted); Assert.Equal(0, changed.CreateCount);
        var restored = CookingLevelEtHost.Restore(saved, factory.Content.Snapshot, new Factory());
        Assert.True(restored.Accepted, restored.ToString()); using var recovered = restored.Host!;
        Assert.Equal(saved.CanonicalText(), recovered.ExportCheckpoint().Checkpoint!.CanonicalText());
        Assert.Equal(observation, recovered.Observe().CanonicalText());
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void Successor_and_retry_use_the_current_scope_front_menu_not_loaded_superset(bool retry)
    {
        var factory = new Factory(); using var host = factory.Host(); Assert.True(host.Prepare(factory.Preparation(Level.Level)).Accepted);
        Assert.True(host.Start().Accepted);
        if (!retry) for (var i = 0; i < 400; i++) host.Tick();
        Assert.True(host.BeginEnd(retry ? CookingLevelOutcome.Failed : CookingLevelOutcome.Success).Accepted);
        Assert.True(host.CompleteEnd().Accepted);
        var next = new LevelId("next");
        var result = retry ? host.CreateRetry(2, factory.Content) : host.CreateSuccessor(next, 2, factory.Preparation(next));
        Assert.True(result.Accepted, result.ToString());
        var expectedMenu = retry ? "F01" : "D31";
        Assert.Equal(new[] { factory.Catalog.Document.Menus.Single(m => m.SourceId == expectedMenu).OrderTemplate }, host.Observe().Front!.OrderMenu);
        Assert.True(host.BeginPreparation(factory.Preparation(retry ? Level.Level : next)).Accepted);
        var ready = host.CompletePreparation(); Assert.True(ready.Accepted, string.Join(" | ", ready.MenuDiagnostics ?? Array.Empty<CookingMenuReadyDiagnostic>()));
        Assert.True(host.Start().Accepted);
    }

    [Fact]
    public void Menu_factory_null_catalog_cannot_fall_back_to_legacy_or_publish_authority()
    {
        var factory = new Factory { NullCatalog = true };
        Assert.Throws<ArgumentException>(() => factory.Host()); Assert.Equal(0, factory.CreateCount);
        factory.NullCatalog = false;
        using var host = factory.Host(); Assert.True(host.Prepare(factory.Preparation(Level.Level)).Accepted);
        Assert.True(host.Start().Accepted); Assert.Equal(1, factory.CreateCount);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void Restored_policy_rejects_new_forbidden_content_but_keeps_carried_cleanup_available(bool running)
    {
        var factory = new Factory();
        var meal = factory.Catalog.Requirements(new[] { "F01" }); var drinks = factory.Catalog.Requirements(new[] { "D31" });
        var step = factory.Catalog.Document.Steps.First(s => drinks.Recipes.Contains(s.Id) && !meal.Recipes.Contains(s.Id)
            && s.Inputs.Any(i => drinks.Supplies.Contains(i.Definition)));
        var forbidden = step.Inputs.First(i => drinks.Supplies.Contains(i.Definition)).Definition;
        factory.Policy = factory.Policy with { AllowedMaterialDefinitions = factory.Policy.AllowedMaterialDefinitions.Where(d => d != forbidden).ToHashSet() };
        var host = factory.Host(); Assert.True(host.BeginPreparation(factory.Preparation(Level.Level)).Accepted); host.Tick();
        if (running) { Assert.True(host.CompletePreparation().Accepted); Assert.True(host.Start().Accepted); host.Tick(); }
        var saved = host.ExportCheckpoint().Checkpoint!; host.Dispose();
        var restored = CookingLevelEtHost.Restore(saved, factory.Content.Snapshot, factory); Assert.True(restored.Accepted, restored.ToString());
        using var recovered = restored.Host!;
        var raw = recovered.Observe().Recipe!.Items.Single(i => i.Definition == forbidden && i.Location.Kind == LocationKind.WorldPosition);
        var carrier = recovered.Observe().Recipe!.Items.First(i => i.Definition == step.Carrier && i.Location.Kind == LocationKind.WorldPosition);
        CookingRecipeCommandResult Execute(CookingRecipeOperation operation, ItemId item, ItemId? container = null, string? world = null)
        {
            var command = new CookingRecipeCommand(Scope, recovered.HostFrameSequence + 1, Chef, new("policy-" + recovered.HostFrameSequence), operation,
                Item: item, Container: container, WorldAnchor: world, ExpectedItemVersion: recovered.Observe().Recipe!.Items.Single(i => i.Id == item).Version);
            Assert.True(recovered.TryEnqueue(new(recovered.Binding.LevelScope, command, "local", command.Command.Value)).Accepted);
            return Assert.Single(recovered.Tick().Dispositions).Result!;
        }
        Assert.Equal(CookingRecipeOutcome.Accepted, Execute(CookingRecipeOperation.Pickup, raw.Id).Outcome);
        var denied = Execute(CookingRecipeOperation.PutIn, raw.Id, carrier.Id);
        Assert.Equal(CookingRecipeRejectionReason.MenuNotAuthorized, denied.Reason);
        Assert.Equal(CookingRecipeOutcome.Accepted, Execute(CookingRecipeOperation.Drop, raw.Id, world: raw.Location.SlotId).Outcome);
    }

    [Fact]
    public void Restore_freezes_trusted_menu_once_and_never_installs_a_later_factory_policy()
    {
        var factory = new Factory(); var host = factory.Host(); Assert.True(host.BeginPreparation(factory.Preparation(Level.Level)).Accepted); host.Tick();
        var saved = host.ExportCheckpoint().Checkpoint!; host.Dispose();
        var alternating = new Factory { AlternatingMenu = true };
        var restored = CookingLevelEtHost.Restore(saved, alternating.Content.Snapshot, alternating);
        Assert.True(restored.Accepted, restored.ToString()); Assert.Equal(1, alternating.MenuReads);
        Assert.Equal(saved.MenuConfigurationIdentity, restored.Host!.ExportCheckpoint().Checkpoint!.MenuConfigurationIdentity);
        restored.Host.Dispose();
        var stable = new Factory(); var control = CookingLevelEtHost.Restore(saved, stable.Content.Snapshot, stable);
        Assert.True(control.Accepted); control.Host!.Dispose();
    }

}
