using System.Text.Json;
using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;

namespace AbilityKit.ET.Runtime.Tests;

// Acceptance fixture configuration derives from the accepted S14 natural fixture.
// It owns no secondary gameplay simulation; all actions use the framed Session path.
internal sealed class CookingRichRecoveryFixture : ICookingMenuGameplayFactory, ICookingScopedFrontOfHouseGameplayFactory
{
    public static readonly PlayerId Chef = new("natural-chef"), Partner = new("natural-partner");
    private readonly Dictionary<DefinitionId, CookingMenuStep> producers;
    private readonly Dictionary<string, ItemId> working = new(StringComparer.Ordinal);
    private readonly Dictionary<RecipeId, ItemId> storage = new();
    private readonly Dictionary<ItemId, string> homes = new();
    private readonly Dictionary<string, ItemId> servingVessels = new(StringComparer.Ordinal);
    private readonly List<(ItemId Id, DefinitionId Definition, string Home)> initial = new();
    private readonly Dictionary<string, CookingLayoutCell> cells = new(StringComparer.Ordinal);
    private readonly Dictionary<StationSlotId, DefinitionId> bindings = new();
    private readonly CookingRestaurantLayout layout;
    private readonly int floorHeight;
    public CookingMenuCatalog Catalog { get; }
    public CookingContent Content { get; }
    public CookingRecipeFixture RecipeFixture { get; }
    public static readonly CookingLevelScope InitialScope = new(new(new("natural-session"), new("world"), new("match")), new(1), new("service"), 1);
    public CookingLevelScope Scope => InitialScope;
    public CookingFrontOfHouseConfiguration FrontOfHouseConfiguration { get; }
    public DefinitionId? DisallowedRetryUnlock { get; }
    public CookingRichRecoveryFixture(bool fullCatalog = false, bool disallowedRetryUnlock = false)
    {
        var bundled = CookingMenuCatalog.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, CookingMenuCatalog.ContentFileName)));
        var document = bundled.Document;
        Catalog = CookingMenuCatalog.Load(document with { Steps = document.Steps.Select(s => s with { RequiredTicks = 6 }).ToArray() });
        // Trusted test timing for visible manual handoff and unattended equipment;
        // the catalog hash and projected provenance both derive from this copy.
        var selected = new[] { "F01", "D31" };
        var loaded = fullCatalog ? Catalog.Document.Menus.Select(m => m.SourceId).ToArray() : selected;
        var requirements = Catalog.Requirements(loaded);
        producers = Catalog.Document.Steps.Where(s => requirements.Recipes.Contains(s.Id)).ToDictionary(s => s.Output);
        var baseline = JsonSerializer.Deserialize<CookingContentDocument>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            CookingContentCatalog.ContentFileName)), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var projected = Catalog.ToContentDocument(baseline, loaded);
        var targets = new List<CookingLayoutTarget>();
        var equipment = new List<CookingEquipmentPlacement>();
        var anchors = new List<CookingSpatialAnchor>();
        var supplies = new List<CookingContentSupplyEntry>();
        var supplierDefinitions = new List<CookingSupplierDefinition>();
        int nextCell = 0;
        CookingLayoutCell AllocateCell() { var index = nextCell++; return new(2 + index % 8, index / 8 * 2); }
        string AddTarget(string id, CookingLayoutTargetKind kind = CookingLayoutTargetKind.Storage)
        {
            var cell = AllocateCell(); cells.Add(id, cell);
            targets.Add(new(id, kind, cell)); anchors.Add(new(LocationKind.WorldPosition, id, cell.X * 1000 + 500, cell.Y * 1000 + 500)); return id;
        }
        ItemId Empty(DefinitionId definition, string label)
        {
            // Instance/anchor IDs are compact stable fixture ordinals; catalog
            // definition/recipe IDs, all receipts and all item state remain intact.
            var id = new ItemId("v" + initial.Count); var home = AddTarget("h" + initial.Count);
            initial.Add((id, definition, home)); homes.Add(id, home); supplies.Add(new(definition.Value, 1, "world:" + home)); return id;
        }
        foreach (var step in producers.Values.OrderBy(s => s.Id.Value, StringComparer.Ordinal))
        {
            var key = step.SourceId + "|" + step.Carrier.Value;
            if (!working.ContainsKey(key)) working.Add(key, Empty(step.Carrier, key.Replace('|', '-')));
            if (step.OutputStorageContainer is { } store) storage.Add(step.Id, Empty(store, "storage-" + step.Id.Value));
        }
        foreach (var menu in Catalog.Document.Menus.Where(m => m.SourceId is "F01" or "D31"))
        {
            var container = Catalog.Document.Containers.Single(c => c.Id == menu.ServingContainer);
            if (container.Disposable) servingVessels.Add(menu.SourceId, Empty(menu.ServingContainer, "serving-" + menu.SourceId));
            else
            {
                supplies.Add(new(menu.ServingContainer.Value, 1, CookingContentCatalog.CleanPoolLocation));
                var pooled = new ItemId("pool-" + menu.ServingContainer.Value + "-1");
                servingVessels.Add(menu.SourceId, pooled); homes.Add(pooled, "clean-pool");
            }
        }
        AddTarget("clean-pool");
        if (disallowedRetryUnlock)
        {
            DisallowedRetryUnlock = requirements.Supplies.Except(Catalog.Requirements(selected).Supplies).OrderBy(d => d.Value, StringComparer.Ordinal).First();
            supplies.Add(new(DisallowedRetryUnlock.Value.Value, 1, "world:" + AddTarget("global-unlock")));
        }
        var sharedSource = AddTarget("purchase-source");
        var sharedReceiving = AddTarget("delivery-receiving");
        foreach (var raw in requirements.Supplies)
        {
            var package = "natural-package-" + raw.Value;
            projected = projected with { Items = projected.Items.Append(new CookingContentItem(package, new[] { "cook" }, new(8, new[] { raw.Value }))).ToArray() };
            AddTarget("stock-" + raw.Value);
            supplierDefinitions.Add(new(raw.Value, sharedSource, sharedReceiving, raw, new(package), 8, 3, 16));
        }
        foreach (var appliance in projected.Appliances.OrderBy(a => a.Station, StringComparer.Ordinal))
        {
            var interaction = AllocateCell(); var cell = new CookingLayoutCell(interaction.X, interaction.Y + 1);
            var station = new StationSlotId(appliance.Station); var definition = new DefinitionId("natural-equipment-" + appliance.Station);
            bindings.Add(station, definition); equipment.Add(new(station, definition, cell, 1, 1, 0, 0, -1));
            cells.Add(appliance.Station, interaction); anchors.Add(new(LocationKind.StationSlot, appliance.Station, interaction.X * 1000 + 500, interaction.Y * 1000 + 500));
        }
        AddTarget("washing"); AddTarget("service");
        AddTarget("customer-entrance", CookingLayoutTargetKind.CustomerEntrance); AddTarget("queue", CookingLayoutTargetKind.Queue);
        AddTarget("exit", CookingLayoutTargetKind.Exit);
        for (var table = 1; table <= 3; table++) AddTarget("table-" + table, CookingLayoutTargetKind.Table);
        floorHeight = (nextCell / 8 + 2) * 2;
        cells.Add("chef-parking", new(0, floorHeight - 1)); targets.Add(new("chef-parking", CookingLayoutTargetKind.PlayerEntrance, cells["chef-parking"]));
        anchors.Add(new(LocationKind.WorldPosition, "chef-parking", 500, (floorHeight - 1) * 1000 + 500));
        cells.Add("partner-parking", new(1, floorHeight - 1)); targets.Add(new("partner-parking", CookingLayoutTargetKind.Storage, cells["partner-parking"]));
        anchors.Add(new(LocationKind.WorldPosition, "partner-parking", 1500, (floorHeight - 1) * 1000 + 500));
        layout = new(new("natural-layout"), new[] { new CookingFloorRegion("floor", 0, 0, 12, floorHeight) }, equipment, Array.Empty<CookingLayoutCell>(), targets) { ActorRadius = 40 };
        var spatial = new CookingSpatialConfiguration(0, 0, 12000, floorHeight * 1000, 40, 800,
            new[] { new CookingPlayerPose(Chef, 500, (floorHeight - 1) * 1000 + 500, 1, 0), new CookingPlayerPose(Partner, 1500, (floorHeight - 1) * 1000 + 500, 1, 0) }, anchors,
            Array.Empty<CookingSpatialObstacle>(), 1000);
        Content = CookingContentCatalog.Load(projected with { StandardInitialSupply = supplies, Spatial = spatial, Supply = new(supplierDefinitions) });
        var players = new[] { Chef, Partner }.ToDictionary(p => p, p => new CookingPlayerConfig(p, new HashSet<string> { "cook" }, Content.Appliances.Keys.Select(s => s.Value).ToHashSet()));
        RecipeFixture = CookingContentCatalog.BuildFixture(Content, Scope.MatchScope, players, "clean-pool") with
        { ScoreThresholds = new(1000, 2000, 3000) };
        FrontOfHouseConfiguration = new(new(3, 160, 60, 2, 2, 3, 500),
            new[] { Catalog.Document.Menus.Single(m => m.SourceId == "D31").OrderTemplate, Catalog.Document.Menus.Single(m => m.SourceId == "F01").OrderTemplate },
            ManualPolicyIdentity: "natural-front-v1", DeliveryPolicy: new(CookingFrontDeliveryMode.ServingAnchor, "service"));
    }

    public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
    {
        if (scope.MatchScope != InitialScope.MatchScope || scope.RestaurantRuntime != InitialScope.RestaurantRuntime || Content.Identity != configuration.Identity)
            throw new InvalidOperationException("Untrusted process-fixture configuration.");
        var simulation = new CookingRecipeSimulation(RecipeFixture);
        foreach (var entry in initial) simulation.AddItem(entry.Id, entry.Definition, ItemLocation.World(entry.Home));
        if (simulation.Snapshot().Items.Any(x => Content.Items[x.Definition].Container is null))
            throw new InvalidOperationException("Factory seeds must only be empty configured tools.");
        return simulation;
    }
    public CookingPreparationConfiguration CreatePreparationConfiguration(CookingLevelScope scope, CookingConfigurationSnapshot configuration) =>
        new(layout, bindings.Values.ToDictionary(d => d, d => new CookingEquipmentFootprint(d, 1, 1, 0, -1)), new(40, 800, 1000),
            bindings.Values.ToHashSet(), bindings.Values.ToHashSet(), bindings, 3, 2);
    public CookingMenuCatalog CreateMenuCatalog(CookingLevelScope scope, CookingConfigurationSnapshot configuration) => Catalog;
    public CookingLevelMenuConfiguration CreateMenuConfiguration(CookingLevelScope scope, CookingConfigurationSnapshot configuration) =>
        new(Catalog.Sha256, new[] { "F01", "D31" }, Content.Items.Keys.ToHashSet(), new HashSet<DefinitionId>(),
            Content.Items.Keys.Where(d => scope.LevelEpoch == 1 || d != DisallowedRetryUnlock).ToHashSet(), true);
    public CookingFrontOfHouseConfiguration CreateFrontOfHouseConfiguration(CookingLevelScope scope, CookingConfigurationSnapshot configuration) => FrontOfHouseConfiguration;
    public CookingLevelPreparation Preparation(CookingLevelScope scope) => new(scope.Level, new("natural-map"),
        new(layout.Id, Content.Appliances.Keys.ToArray(), Content.Items.Values.Where(i => i.Container is not null).Select(i => i.Id).ToArray()), Content.Identity);
    public IReadOnlyDictionary<string, CookingLayoutCell> Cells => cells;
    public CookingRestaurantLayout Layout => layout;
    public IReadOnlyDictionary<string, ItemId> WorkingVessels => working;
    public IReadOnlyDictionary<RecipeId, ItemId> StorageVessels => storage;
    public IReadOnlyDictionary<string, ItemId> ServingVessels => servingVessels;
    public IReadOnlyDictionary<ItemId, string> Homes => homes;
}
