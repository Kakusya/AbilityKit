using System.Text.Json;
using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingMenuRuntimePolicyTests
{
    private static readonly CookingScope Scope = new(new("runtime-menu"), new("world"), new("match"));
    private static readonly CookingLevelScope Level = new(Scope, new(1), new("level"), 1);
    private static readonly PlayerId Chef = new("chef");
    private sealed class Allocator : ICookingProductIdAllocator {
        public int Calls;
        public Action? OnAllocate;
        public ItemId GetProductId(long sequence) { Calls++; OnAllocate?.Invoke(); return new($"product-{sequence}"); }
    }
    private static (CookingMenuCatalog Catalog, CookingRecipeSimulation Sim, CookingLevelMenuConfiguration Policy, Allocator Allocator) Kitchen()
    {
        var catalog = CookingMenuCatalog.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, CookingMenuCatalog.ContentFileName)));
        var baseline = JsonSerializer.Deserialize<CookingContentDocument>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, CookingContentCatalog.ContentFileName)),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var content = catalog.LoadContent(baseline, new[] { "F01", "F04", "D31" });
        var player = new CookingPlayerConfig(Chef, content.Items.Values.SelectMany(i => i.AllowedPlayerCapabilities).ToHashSet(), content.Appliances.Keys.Select(s => s.Value).ToHashSet());
        var allocator = new Allocator(); var sim = new CookingRecipeSimulation(CookingContentCatalog.BuildFixture(content, Scope,
            new Dictionary<PlayerId, CookingPlayerConfig> { [Chef] = player }), allocator);
        var policy = new CookingLevelMenuConfiguration(catalog.Sha256, new[] { "F01", "F04", "D31" }, content.Items.Keys.ToHashSet(), new HashSet<DefinitionId>(), content.Items.Keys.ToHashSet(), true);
        return (catalog, sim, policy, allocator);
    }
    private static CookingRecipeCommand C(CookingRecipeSimulation sim, CookingRecipeOperation op, string key,
        ItemId? item = null, ItemId? container = null, RecipeId? recipe = null, StationSlotId? station = null, OrderId? order = null) =>
        new(Scope, 1, Chef, new(key), op, Recipe: recipe, Item: item, Container: container, Station: station, Order: order,
            ExpectedItemVersion: item is null ? 0 : sim.Snapshot().Items.Single(i => i.Id == item).Version);
    private static CookingRecipeCommandResult Accept(CookingRecipeSimulation sim, CookingRecipeCommand command) {
        var result = sim.Submit(command); Assert.True(result.Outcome == CookingRecipeOutcome.Accepted, result.Reason.ToString()); return result;
    }
    private static (ItemId Carrier, StationSlotId Station) Seed(CookingRecipeSimulation sim, CookingRecipeDefinition recipe, string suffix)
    {
        var carrier = new ItemId("carrier-" + suffix); var view = sim.DescribeManufacturingAvailability();
        sim.AddItem(carrier, recipe.RequiredProcessingContainerDefinition!.Value, ItemLocation.World(carrier.Value));
        for (var i = 0; i < recipe.Inputs.Count; i++) sim.AddItem(new("input-" + suffix + "-" + i), recipe.Inputs[i], ItemLocation.Container(carrier, "slot-" + i));
        return (carrier, view.Appliances.Values.First(a => a.Capabilities.Contains(recipe.RequiredApplianceCapability)).Station);
    }
    private static CookingRecipeDefinition Final(CookingMenuCatalog catalog, CookingRecipeSimulation sim, string menu) =>
        sim.DescribeManufacturingAvailability().Recipes[catalog.Document.Menus.Single(m => m.SourceId == menu).FinalRecipe];

    [Fact]
    public void Configuration_is_frozen_once_idempotent_and_not_a_Ready_or_saved_grant_check()
    {
        var (catalog, sim, policy, _) = Kitchen(); var allowed = policy.AllowedMaterialDefinitions.ToHashSet();
        var configured = policy with { AllowedMaterialDefinitions = allowed }; var before = sim.ExportCheckpoint().CanonicalText();
        sim.ConfigureMenuPolicy(catalog, configured); allowed.Clear(); sim.ConfigureMenuPolicy(catalog, policy);
        Assert.Equal(before, sim.ExportCheckpoint().CanonicalText());
        Assert.Throws<InvalidOperationException>(() => sim.ConfigureMenuPolicy(catalog, policy with { BindingCommandsEnabled = false }));
        var (_, empty, emptyPolicy, _) = Kitchen(); empty.ConfigureMenuPolicy(catalog, emptyPolicy with { AllowedMaterialDefinitions = new HashSet<DefinitionId>() });
        var (_, mismatch, _, _) = Kitchen(); Assert.Throws<ArgumentException>(() => mismatch.ConfigureMenuPolicy(catalog, policy with { CatalogIdentity = "foreign" }));
        mismatch.ConfigureMenuPolicy(catalog, policy); // Failed configuration left no partial policy behind.
    }
    [Theory][InlineData("recipe")][InlineData("input")][InlineData("carrier")][InlineData("output")]
    public void Unauthorized_new_process_preserves_objects_allocator_and_watermarks(string restriction)
    {
        var (catalog, sim, policy, allocator) = Kitchen(); var recipe = Final(catalog, sim, "F01"); var (carrier, station) = Seed(sim, recipe, restriction);
        var definition = restriction switch { "input" => recipe.Inputs.First(), "carrier" => recipe.RequiredProcessingContainerDefinition!.Value, _ => recipe.ProductDefinition };
        sim.ConfigureMenuPolicy(catalog, restriction == "recipe" ? policy with { SelectedMenuIds = new[] { "D31" } }
            : policy with { AllowedMaterialDefinitions = policy.AllowedMaterialDefinitions.Where(d => d != definition).ToHashSet() });
        var before = sim.Snapshot().CanonicalText(); var water = sim.ExportCheckpoint().NextProcessId;
        var command = C(sim, CookingRecipeOperation.StartProcess, "start", carrier, recipe: recipe.Id, station: station);
        var result = sim.Submit(command); Assert.Equal(CookingRecipeRejectionReason.MenuNotAuthorized, result.Reason);
        Assert.Equal(before, sim.Snapshot().CanonicalText()); Assert.Equal(water, sim.ExportCheckpoint().NextProcessId); Assert.Equal(0, allocator.Calls);
        var repeat = sim.Submit(command); Assert.True(repeat.IsDuplicate); Assert.Equal(result.Reason, repeat.Reason);
    }
    [Fact]
    public void Bare_forbidden_hand_input_is_not_moved_to_station_on_rejected_start()
    {
        var (catalog, sim, policy, _) = Kitchen(); var recipe = sim.DescribeManufacturingAvailability().Recipes[new("bake-bread")];
        var raw = new ItemId("hand-raw"); sim.AddItem(raw, recipe.Inputs.Single(), ItemLocation.Hand(Chef));
        var station = sim.DescribeManufacturingAvailability().Appliances.Values.First(a => a.Capabilities.Contains(recipe.RequiredApplianceCapability)).Station;
        sim.ConfigureMenuPolicy(catalog, policy);
        var before = sim.Snapshot().CanonicalText(); var result = sim.Submit(C(sim, CookingRecipeOperation.StartProcess, "bare", raw, recipe: recipe.Id, station: station));
        Assert.Equal(CookingRecipeRejectionReason.MenuNotAuthorized, result.Reason); Assert.Equal(before, sim.Snapshot().CanonicalText());
    }
    [Fact]
    public void Legacy_and_authorized_new_process_work_while_existing_processing_finishes_after_narrowing()
    {
        var (catalog, sim, policy, _) = Kitchen(); var recipe = Final(catalog, sim, "F01"); var (carrier, station) = Seed(sim, recipe, "old");
        var start = Accept(sim, C(sim, CookingRecipeOperation.StartProcess, "start", carrier, recipe: recipe.Id, station: station));
        sim.ConfigureMenuPolicy(catalog, policy with { SelectedMenuIds = new[] { "D31" }, AllowedMaterialDefinitions = new HashSet<DefinitionId>() });
        if (recipe.Execution == CookingRecipeExecutionKind.Manual) {
            Accept(sim, new(Scope, 1, Chef, new("stop"), CookingRecipeOperation.StopProcess, Process: start.Events.Single().Process));
            Accept(sim, new(Scope, 1, Chef, new("continue"), CookingRecipeOperation.ContinueProcess, Process: start.Events.Single().Process));
        }
        for (var i = 0; i < recipe.RequiredTicks; i++) sim.AdvanceFixedTick(Level, sim.LogicalTick + 1);
        Assert.Empty(sim.Snapshot().Processes); Assert.Contains(sim.Snapshot().Items, i => i.Definition == recipe.ProductDefinition);
        var (_, allowed, _, _) = Kitchen(); var seeded = Seed(allowed, recipe, "new"); allowed.ConfigureMenuPolicy(catalog, policy);
        Accept(allowed, C(allowed, CookingRecipeOperation.StartProcess, "start", seeded.Carrier, recipe: recipe.Id, station: seeded.Station));
    }
    [Fact]
    public void Forbidden_carry_can_be_taken_out_moved_cleared_and_discarded_but_not_newly_added()
    {
        var (catalog, sim, policy, _) = Kitchen(); var recipe = Final(catalog, sim, "F01"); var (carrier, station) = Seed(sim, recipe, "recovery");
        var raw = sim.Snapshot().Containers.Single(c => c.Id == carrier).ItemIds.First(); var definition = sim.Snapshot().Items.Single(i => i.Id == raw).Definition;
        sim.ConfigureMenuPolicy(catalog, policy with { AllowedMaterialDefinitions = policy.AllowedMaterialDefinitions.Where(d => d != definition).ToHashSet() });
        Accept(sim, C(sim, CookingRecipeOperation.TakeOut, "take", raw, carrier)); var before = sim.Snapshot().CanonicalText();
        Assert.Equal(CookingRecipeRejectionReason.MenuNotAuthorized, sim.Submit(C(sim, CookingRecipeOperation.PutIn, "put", raw, carrier)).Reason);
        Assert.Equal(before, sim.Snapshot().CanonicalText());
        Accept(sim, C(sim, CookingRecipeOperation.Drop, "drop", raw, station: station));
        Accept(sim, C(sim, CookingRecipeOperation.Pickup, "pickup", raw));
        Accept(sim, C(sim, CookingRecipeOperation.DiscardItem, "discard", raw));
        Accept(sim, C(sim, CookingRecipeOperation.ClearContents, "clear", carrier));
        Assert.Empty(sim.Snapshot().Containers.Single(c => c.Id == carrier).ItemIds);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void New_portion_instances_require_recipe_closure_and_permissions_before_allocator(bool usePour)
    {
        var (catalog, original, policy, allocator) = Kitchen(); var view = original.DescribeManufacturingAvailability();
        var recipe = Final(catalog, original, "F01") with { Completion = CookingRecipeCompletionKind.RetainInputs, YieldPortions = 1 };
        var recipes = view.Recipes.ToDictionary(); recipes[recipe.Id] = recipe;
        var sim = new CookingRecipeSimulation(new(Scope, view.Players, view.ItemDefinitions, view.Appliances, recipes, orderTemplates: view.OrderTemplates), allocator);
        var (carrier, station) = Seed(sim, recipe, "portion"); Accept(sim, C(sim, CookingRecipeOperation.StartProcess, "start", carrier, recipe: recipe.Id, station: station));
        for (var i = 0; i < recipe.RequiredTicks; i++) sim.AdvanceFixedTick(Level, sim.LogicalTick + 1);
        var targetDef = sim.DescribeManufacturingAvailability().ItemDefinitions.Values.First(i => i.Id != recipe.RequiredProcessingContainerDefinition && i.Container?.AcceptedDefinitions.Contains(recipe.ProductDefinition) == true).Id;
        var target = new ItemId("target"); sim.AddItem(target, targetDef, ItemLocation.World("target"));
        sim.ConfigureMenuPolicy(catalog, policy with { SelectedMenuIds = Array.Empty<string>() });
        var before = sim.Snapshot().CanonicalText(); var water = sim.ExportCheckpoint().NextProductId; var calls = allocator.Calls;
        var result = sim.Submit(C(sim, usePour ? CookingRecipeOperation.Pour : CookingRecipeOperation.ServePortion, "portion", carrier, target));
        Assert.Equal(CookingRecipeRejectionReason.MenuNotAuthorized, result.Reason); Assert.Equal(before, sim.Snapshot().CanonicalText());
        Assert.Equal(water, sim.ExportCheckpoint().NextProductId); Assert.Equal(calls, allocator.Calls);
    }

    private static (CookingMenuCatalog Catalog, CookingRecipeSimulation Sim, CookingLevelMenuConfiguration Policy, Allocator Allocator, DefinitionId Raw, DefinitionId Package) SupplyKernel(bool infinite)
    {
        var (catalog, original, policy, allocator) = Kitchen(); var view = original.DescribeManufacturingAvailability();
        var raw = catalog.Requirements(new[] { "F01" }).Supplies.First(); var package = new DefinitionId("supply-box");
        var items = view.ItemDefinitions.ToDictionary(); items[package] = new(package, view.Players[Chef].Capabilities, new(3, new HashSet<DefinitionId> { raw }));
        var anchors = view.Appliances.Keys.Select((s, i) => new CookingSpatialAnchor(LocationKind.StationSlot, s.Value, 500 + i * 100, 0))
            .Concat(new[] { new CookingSpatialAnchor(LocationKind.WorldPosition, "source", 200, 0), new CookingSpatialAnchor(LocationKind.WorldPosition, "receiving", 300, 0) }).ToArray();
        var spatial = new CookingSpatialConfiguration(-5000, -5000, 10000, 10000, 100, 2000, new[] { new CookingPlayerPose(Chef, 0, 0, 1, 0) }, anchors, Array.Empty<CookingSpatialObstacle>());
        var sim = new CookingRecipeSimulation(new(Scope, view.Players, items, view.Appliances, view.Recipes, orderTemplates: view.OrderTemplates, spatial: spatial,
            supply: new(new[] { new CookingSupplierDefinition("supplier", "source", "receiving", raw, package, 2, 0, infinite ? 0 : 4, infinite) })), allocator);
        policy = policy with { BaseAuthorizedMaterialDefinitions = policy.BaseAuthorizedMaterialDefinitions.Append(package).ToHashSet(),
            AllowedMaterialDefinitions = policy.AllowedMaterialDefinitions.Append(package).ToHashSet() };
        return (catalog, sim, policy, allocator, raw, package);
    }
    [Theory][InlineData(false, "unit")][InlineData(false, "package")][InlineData(true, "unit")]
    public void Unauthorized_new_supply_reserves_no_stock_and_calls_no_allocator(bool infinite, string restriction)
    {
        var (catalog, sim, policy, allocator, raw, package) = SupplyKernel(infinite); var denied = restriction == "unit" ? raw : package;
        sim.ConfigureMenuPolicy(catalog, policy with { AllowedMaterialDefinitions = policy.AllowedMaterialDefinitions.Where(d => d != denied).ToHashSet() });
        var before = sim.SupplySnapshot()!.Ledger; var snapshot = sim.Snapshot().CanonicalText();
        var command = new CookingRecipeCommand(Scope, 1, Chef, new("request"), infinite ? CookingRecipeOperation.TakeSupply : CookingRecipeOperation.RequestSupply,
            SupplierId: "supplier", SupplyRequestId: "request");
        Assert.Equal(CookingRecipeRejectionReason.MenuNotAuthorized, sim.Submit(command).Reason);
        Assert.Equal(snapshot, sim.Snapshot().CanonicalText()); Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(sim.SupplySnapshot()!.Ledger)); Assert.Equal(0, allocator.Calls);
        Assert.True(sim.Submit(command).IsDuplicate);
    }
    [Fact]
    public void Infinite_new_take_does_not_require_unmaterialized_package_permission()
    {
        var (catalog, sim, policy, allocator, _, package) = SupplyKernel(true);
        sim.ConfigureMenuPolicy(catalog, policy with { AllowedMaterialDefinitions = policy.AllowedMaterialDefinitions.Where(d => d != package).ToHashSet() });
        var taken = Accept(sim, new(Scope, 1, Chef, new("take"), CookingRecipeOperation.TakeSupply, SupplierId: "supplier", SupplyRequestId: "take"));
        Assert.Null(taken.Supply!.Package); Assert.Single(taken.Supply.Units); Assert.Equal(1, allocator.Calls);
    }
    [Fact]
    public void Approved_inflight_supply_reception_survives_narrowed_permissions()
    {
        var (catalog, sim, policy, allocator, raw, package) = SupplyKernel(false);
        var approved = Accept(sim, new(Scope, 1, Chef, new("request"), CookingRecipeOperation.RequestSupply, SupplierId: "supplier", SupplyRequestId: "request"));
        var restored = SupplyKernel(false);
        Assert.True(restored.Sim.RestoreCheckpoint(sim.ExportCheckpoint()).Accepted);
        restored.Sim.ConfigureMenuPolicy(catalog, policy with { AllowedMaterialDefinitions = new HashSet<DefinitionId>() });
        restored.Sim.AdvanceFixedTick(Level, 1);
        var received = Accept(restored.Sim, new(Scope, 1, Chef, new("receive"), CookingRecipeOperation.ReceiveSupply, DeliveryId: approved.Supply!.DeliveryId));
        Assert.NotNull(received.Supply!.Package); Assert.Equal(2, received.Supply.Units.Count);
        Assert.Equal(3, restored.Allocator.Calls); Assert.Equal(2, restored.Sim.SupplySnapshot()!.Ledger.Balances.Single().AvailableUnits);
    }
    private static ItemId MakeFinal(CookingMenuCatalog catalog, CookingRecipeSimulation sim, string suffix)
    {
        var recipe = Final(catalog, sim, "F01"); var (carrier, station) = Seed(sim, recipe, suffix);
        Accept(sim, C(sim, CookingRecipeOperation.StartProcess, "start-" + suffix, carrier, recipe: recipe.Id, station: station));
        for (var i = 0; i < recipe.RequiredTicks; i++) sim.AdvanceFixedTick(Level, sim.LogicalTick + 1);
        return carrier;
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void Ordinary_pour_moves_authorized_formed_goods_from_an_excluded_recipe_but_rejects_forbidden_material(bool forbidProduct)
    {
        var (catalog, sim, policy, allocator) = Kitchen(); var carrier = MakeFinal(catalog, sim, "pour");
        var menu = catalog.Document.Menus.Single(m => m.SourceId == "F01"); var target = new ItemId("serving");
        sim.AddItem(target, menu.ServingContainer, ItemLocation.World("serving"));
        sim.ConfigureMenuPolicy(catalog, policy with { SelectedMenuIds = new[] { "D31" }, AllowedMaterialDefinitions = policy.AllowedMaterialDefinitions.Where(d => !forbidProduct || d != menu.Product).ToHashSet() });
        var before = sim.Snapshot().CanonicalText(); var calls = allocator.Calls;
        var result = sim.Submit(C(sim, CookingRecipeOperation.Pour, "pour", carrier, target));
        if (forbidProduct) { Assert.Equal(CookingRecipeRejectionReason.MenuNotAuthorized, result.Reason); Assert.Equal(before, sim.Snapshot().CanonicalText()); }
        else { Assert.Equal(CookingRecipeOutcome.Accepted, result.Outcome); Assert.Single(sim.Snapshot().Containers.Single(c => c.Id == target).ItemIds); }
        Assert.Equal(calls, allocator.Calls);
    }
    [Theory][InlineData(CookingRecipeOperation.BindOrder)][InlineData(CookingRecipeOperation.UnbindOrder)][InlineData(CookingRecipeOperation.RebindOrder)]
    public void Disabled_binding_commands_reject_without_mutating_existing_binding(CookingRecipeOperation operation)
    {
        var (catalog, sim, policy, _) = Kitchen(); var carrier = MakeFinal(catalog, sim, "binding");
        var menu = catalog.Document.Menus.Single(m => m.SourceId == "F01"); var target = new ItemId("serving"); sim.AddItem(target, menu.ServingContainer, ItemLocation.World("serving"));
        Accept(sim, C(sim, CookingRecipeOperation.Pour, "plate", carrier, target));
        var product = sim.Snapshot().Containers.Single(c => c.Id == target).ItemIds.Single(); var order = new OrderId("order");
        Assert.True(sim.OpenOrder(order, menu.OrderTemplate).Accepted);
        if (operation != CookingRecipeOperation.BindOrder) Accept(sim, C(sim, CookingRecipeOperation.BindOrder, "old-bind", product, order: order));
        sim.ConfigureMenuPolicy(catalog, policy with { BindingCommandsEnabled = false }); var before = sim.Snapshot().CanonicalText();
        var result = sim.Submit(C(sim, operation, "binding", product, order: order));
        Assert.Equal(CookingRecipeRejectionReason.MenuNotAuthorized, result.Reason); Assert.Equal(before, sim.Snapshot().CanonicalText());
        var stale = sim.Submit(C(sim, operation, "stale", product, order: order) with { ExpectedItemVersion = CookingRecipeCommandValidation.MaximumExpectedItemVersion });
        Assert.Equal(CookingRecipeRejectionReason.ItemStale, stale.Reason);
    }
    [Fact]
    public void Preview_respects_policy_and_allocator_callbacks_cannot_reconfigure()
    {
        var (catalog, sim, policy, allocator) = Kitchen(); var recipe = Final(catalog, sim, "F01"); var (carrier, station) = Seed(sim, recipe, "preview");
        sim.ConfigureMenuPolicy(catalog, policy with { SelectedMenuIds = new[] { "D31" } });
        Assert.DoesNotContain(sim.PreviewInteraction(Chef, 1, new("preview")), p => p.Command.Operation == CookingRecipeOperation.StartProcess && p.Command.Item == carrier);
        var actual = Kitchen(); var actualRecipe = Final(actual.Catalog, actual.Sim, "F01"); var seeded = Seed(actual.Sim, actualRecipe, "callback");
        actual.Sim.ConfigureMenuPolicy(actual.Catalog, actual.Policy);
        actual.Allocator.OnAllocate = () => Assert.Throws<InvalidOperationException>(() => actual.Sim.ConfigureMenuPolicy(actual.Catalog, actual.Policy));
        Accept(actual.Sim, C(actual.Sim, CookingRecipeOperation.StartProcess, "start", seeded.Carrier, recipe: actualRecipe.Id, station: seeded.Station));
        for (var i = 0; i < actualRecipe.RequiredTicks; i++) actual.Sim.AdvanceFixedTick(Level, actual.Sim.LogicalTick + 1);
        Assert.Equal(1, actual.Allocator.Calls);
    }

    private sealed class KernelFactory(CookingRecipeSimulation kernel) : ICookingLevelGameplayFactory {
        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration) => kernel;
    }
    private sealed class PublicationGuard : ICookingLevelGameplayPublicationGuard {
        public bool TryAcquire(CookingRecipeSimulation kernel) => true;
        public void Release(CookingRecipeSimulation kernel) { }
    }
    private sealed class Authority : ICookingRecipeAuthorityGate { public bool IsAuthorityMutationOpen { get; set; } }
    [Fact]
    public void Closed_authority_cannot_grant_policy_and_confirmed_unlocks_never_bypass_allowed()
    {
        var (catalog, sim, policy, _) = Kitchen(); var authority = new Authority(); sim.BindAuthorityGate(authority);
        var before = sim.ExportCheckpoint().CanonicalText(); Assert.Throws<InvalidOperationException>(() => sim.ConfigureMenuPolicy(catalog, policy));
        Assert.Equal(before, sim.ExportCheckpoint().CanonicalText()); authority.IsAuthorityMutationOpen = true;
        var recipe = Final(catalog, sim, "F01"); var definition = recipe.ProductDefinition; var seeded = Seed(sim, recipe, "unlock");
        sim.ConfigureMenuPolicy(catalog, policy with { BaseAuthorizedMaterialDefinitions = policy.BaseAuthorizedMaterialDefinitions.Where(d => d != definition).ToHashSet(),
            ConfirmedMaterialUnlocks = new HashSet<DefinitionId> { definition }, AllowedMaterialDefinitions = policy.AllowedMaterialDefinitions.Where(d => d != definition).ToHashSet() });
        Assert.Equal(CookingRecipeRejectionReason.MenuNotAuthorized, sim.Submit(C(sim, CookingRecipeOperation.StartProcess, "start", seeded.Carrier, recipe: recipe.Id, station: seeded.Station)).Reason);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void Actual_bound_created_allows_configuration_and_bound_running_denies_first_or_repeat(bool configureCreated)
    {
        var (catalog, sim, policy, _) = Kitchen(); var view = sim.DescribeManufacturingAvailability();
        var baseline = JsonSerializer.Deserialize<CookingContentDocument>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, CookingContentCatalog.ContentFileName)),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var configuration = catalog.LoadContent(baseline, policy.SelectedMenuIds).Snapshot!;
        var lifecycle = new CookingLevelLifecycle(Level, configuration, new KernelFactory(sim));
        Assert.True(lifecycle.InitializePreparationKitchen(new PublicationGuard()).Accepted);
        var beforeCreated = sim.ExportCheckpoint().CanonicalText();
        if (configureCreated) { sim.ConfigureMenuPolicy(catalog, policy); Assert.Equal(beforeCreated, sim.ExportCheckpoint().CanonicalText()); }
        var preparation = new CookingLevelPreparation(Level.Level, new("map"), new(new("layout"), view.Appliances.Keys.ToArray(),
            view.ItemDefinitions.Values.Where(i => i.Container is not null).Select(i => i.Id).ToArray()), configuration.Identity);
        Assert.True(lifecycle.BeginPreparation(preparation).Accepted); Assert.True(lifecycle.CompletePreparation().Accepted); Assert.True(lifecycle.Start().Accepted);
        var beforeRunning = sim.ExportCheckpoint().CanonicalText(); var beforeLevel = lifecycle.Snapshot();
        Assert.Throws<InvalidOperationException>(() => sim.ConfigureMenuPolicy(catalog, policy));
        Assert.Equal(beforeRunning, sim.ExportCheckpoint().CanonicalText()); Assert.Equal(beforeLevel, lifecycle.Snapshot());
    }
}
