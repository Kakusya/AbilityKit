using AbilityKit.Game.Cooking.EtRuntime;
namespace AbilityKit.Game.Cooking.NetworkConcurrencyAcceptance;

internal sealed class ConcurrencyFixture : ICookingLevelGameplayFactory
{
    public static readonly PlayerId Chef = new("control-chef"), Partner = new("control-partner");
    public static readonly CookingLevelScope Scope = new(new(new("concurrency-session"), new("world"), new("match")), new(1), new("controls"), 1);
    public static readonly StationSlotId Slot = new("single-slot");
    public static readonly ItemId Shared = new("shared"), ChefFood = new("chef-food"), PartnerFood = new("partner-food"), Bad = new("incompatible"), Good = new("compatible"), Vessel = new("vessel");
    public static readonly ItemId[] ItemIds = [Shared, ChefFood, PartnerFood, Bad, Good, Vessel];
    private readonly CookingRecipeFixture _fixture;
    public CookingConfigurationSnapshot Configuration { get; }
    public ConcurrencyFixture()
    {
        var tags = new HashSet<string> { "cook" };
        DefinitionId a = new("raw-a"), b = new("raw-b"), bowl = new("bowl");
        var items = new[] { new CookingItemDefinition(a, tags), new CookingItemDefinition(b, tags), new CookingItemDefinition(bowl, tags, new(2, new HashSet<DefinitionId> { a })) };
        var station = new CookingApplianceDefinition(Slot, new HashSet<string> { "storage" });
        var registry = new CookingConfigurationRegistry();

        var anchors = ItemIds.Select((id, i) => new CookingSpatialAnchor(LocationKind.WorldPosition, "home-" + id.Value, 1500, 400 + i * 100)).ToList();
        anchors.Add(new(LocationKind.WorldPosition, "clear-chef", 1500, 1100));
        anchors.Add(new(LocationKind.WorldPosition, "clear-partner", 1500, 1200));
        anchors.Add(new(LocationKind.WorldPosition, "clear2-chef", 1500, 1500));
        anchors.Add(new(LocationKind.WorldPosition, "clear2-partner", 1500, 1600));
        anchors.Add(new(LocationKind.WorldPosition, "bad-recovery", 1500, 1300));
        anchors.Add(new(LocationKind.StationSlot, Slot.Value, 1500, 1400));
        var spatial = new CookingSpatialConfiguration(0, 0, 6000, 6000, 20, 5000,
            new[] { new CookingPlayerPose(Chef, 500, 500, 1, 0), new CookingPlayerPose(Partner, 700, 500, 1, 0) }, anchors, Array.Empty<CookingSpatialObstacle>(), MovementSpeed: 10);
        Require(registry.Submit(new(new[] { "storage" }, items, new[] { station }, Array.Empty<CookingRecipeDefinition>(), Spatial: spatial)).Accepted, "configuration");
        Configuration = registry.Current!;
        _fixture = new(Scope.MatchScope, new[] { Chef, Partner }.ToDictionary(p => p, p => new CookingPlayerConfig(p, tags, anchors.Select(x => x.Id).ToHashSet())),
            items.ToDictionary(x => x.Id), new Dictionary<StationSlotId, CookingApplianceDefinition> { [Slot] = station }, new Dictionary<RecipeId, CookingRecipeDefinition>(), spatial: spatial);
    }
    public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
    {
        var kitchen = new CookingRecipeSimulation(_fixture);
        foreach (var id in ItemIds) kitchen.AddWorldIngredient(id, new(id == Vessel ? "bowl" : id == Bad ? "raw-b" : "raw-a"), "home-" + id.Value);
        return kitchen;
    }
    public CookingLevelEtHost Host()
    {
        var host = new CookingLevelEtHost(new CookingLevelLifecycle(Scope, Configuration, this));
        Require(host.Prepare(new(Scope.Level, new("control-map"), new(new("control-layout"), new[] { Slot }, Array.Empty<DefinitionId>()), Configuration.Identity)).Accepted, "prepare");
        Require(host.Start().Accepted, "start"); return host;
    }
    public static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    public static int Phase(CookingNetworkAuthorityCapture state) => (state.FullRecipe!.Poses!.Single(p => p.Player == Chef).Y - 500) / 10;
    public static CookingRecipeCheckpointItem Item(CookingNetworkAuthorityCapture state, ItemId id) => state.FullRecipe!.Items.Single(x => x.Id == id);
    public static CookingRecipeCommand Move(PlayerId player, bool marker = false, int facingY = 0) => new(Scope.MatchScope, 0, player, new("unused"), CookingRecipeOperation.Move, MoveY: marker ? 1 : 0, FacingX: 1, FacingY: facingY);
    public static CookingRecipeCommand Action(int phase, PlayerId player, CookingNetworkAuthorityCapture state)
    {
        var remote = player == Partner;
        CookingRecipeCommand On(CookingRecipeOperation op, ItemId id, StationSlotId? station = null, ItemId? container = null, string? home = null) =>
            new(Scope.MatchScope, 0, player, new("unused"), op, Item: id, Station: station, Container: container, ExpectedItemVersion: Item(state, id).Version, WorldAnchor: home);
        var held = state.FullRecipe!.Items.SingleOrDefault(i => !i.Removed && i.Location == ItemLocation.Hand(player));
        return phase switch {
            1 => On(CookingRecipeOperation.Pickup, Shared),
            2 or 5 => held is null ? Move(player) : On(CookingRecipeOperation.Drop, held.Id, home: phase == 5 ? (remote ? "clear2-partner" : "clear2-chef") : (remote ? "clear-partner" : "clear-chef")),
            3 => On(CookingRecipeOperation.Pickup, remote ? PartnerFood : ChefFood),
            4 => On(CookingRecipeOperation.Drop, held!.Id, station: Slot),
            6 when remote => On(CookingRecipeOperation.Pickup, Bad),
            7 when remote => On(CookingRecipeOperation.PutIn, Bad, container: Vessel),
            8 when remote => On(CookingRecipeOperation.Drop, Bad, home: "bad-recovery"),
            9 when remote => On(CookingRecipeOperation.Pickup, Good),
            10 when remote => On(CookingRecipeOperation.PutIn, Good, container: Vessel),
            _ => Move(player, facingY: phase == 15 && remote ? 1 : 0)
        };
    }
}
