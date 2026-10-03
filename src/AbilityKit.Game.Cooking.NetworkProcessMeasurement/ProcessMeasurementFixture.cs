using AbilityKit.Game.Cooking.EtRuntime;
namespace AbilityKit.Game.Cooking.NetworkProcessMeasurement;
internal sealed class ProcessMeasurementFixture : ICookingLevelGameplayFactory
{
    internal static readonly PlayerId Local = new("load-local"), Remote = new("load-remote");
    internal static readonly ItemId[] Tools = { new("local-tool"), new("remote-tool") };
    internal static readonly ItemId Input = new("input"), Cup = new("cup");
    internal static readonly StationSlotId Board = new("board"), Counter = new("counter");
    internal static readonly OrderId Order = new("fault-order");
    internal static readonly CookingScope Match = new(new("process-load"), new("world"), new("match"));
    internal static readonly CookingLevelScope Scope = new(Match, new(1), new("load"), 1);
    private readonly CookingRecipeFixture _fixture;
    internal CookingConfigurationSnapshot Configuration { get; }
    internal int CreateCount { get; private set; }
    internal ProcessMeasurementFixture()
    {
        var caps = new HashSet<string> { "cook" };
        var raw = new DefinitionId("raw"); var drink = new DefinitionId("drink"); var box = new DefinitionId("box"); var cup = new DefinitionId("cup-type");
        var recipe = new RecipeId("fault-recipe"); var template = new OrderTemplateId("fault-template");
        var items = new[] { new CookingItemDefinition(raw, caps), new(drink, caps), new(box, caps, new(1, new HashSet<DefinitionId>{raw})),
            new(cup, caps, new(1, new HashSet<DefinitionId>{drink}, DisposableOnSubmission:true)) };
        var stations = new[] { new CookingApplianceDefinition(Board, new HashSet<string>{"mix"}), new(Counter,new HashSet<string>()) };
        var recipes = new[] { new CookingRecipeDefinition(recipe,new[]{raw},drink,new("mix"),"mix",8) };
        var orders = new[] { new CookingOrderTemplateDefinition(template,recipe,cup,RequiresBinding:true) };
        var spatial = new CookingSpatialConfiguration(0,0,6000,6000,40,800,
            new[]{new CookingPlayerPose(Local,1500,1500,1,0),new CookingPlayerPose(Remote,4500,1500,1,0)},
            new[]{new CookingSpatialAnchor(LocationKind.WorldPosition,Tools[0].Value,1500,1500),new(LocationKind.WorldPosition,Tools[1].Value,4500,1500),
                new(LocationKind.StationSlot,Board.Value,4500,1500),new(LocationKind.StationSlot,Counter.Value,4500,1500)},Array.Empty<CookingSpatialObstacle>(),1000);
        var registry = new CookingConfigurationRegistry();
        if (!registry.Submit(new(new[]{"mix"},items,stations,recipes,OrderTemplates:orders,Spatial:spatial)).Accepted) throw new InvalidOperationException("Invalid process measurement config.");
        Configuration=registry.Current!;
        _fixture=new(Match,new[]{Local,Remote}.ToDictionary(p=>p,p=>new CookingPlayerConfig(p,caps,
            new HashSet<string>{Board.Value,Counter.Value,Tools[0].Value,Tools[1].Value})),items.ToDictionary(i=>i.Id),stations.ToDictionary(s=>s.Station),recipes.ToDictionary(r=>r.Id),orderTemplates:orders.ToDictionary(o=>o.Id),spatial:spatial);
    }
    public CookingRecipeSimulation Create(CookingLevelScope scope,CookingConfigurationSnapshot configuration)
    {
        if(scope!=Scope || configuration.Identity!=Configuration.Identity || ++CreateCount!=1) throw new InvalidOperationException("Repeated/untrusted authority creation.");
        var kitchen=new CookingRecipeSimulation(_fixture);
        kitchen.AddItem(Input,new("raw"),ItemLocation.Station(Board)); kitchen.AddItem(Cup,new("cup-type"),ItemLocation.Station(Counter));
        foreach(var tool in Tools) kitchen.AddItem(tool,new("box"),ItemLocation.World(tool.Value));
        if(!kitchen.OpenOrder(Order,new("fault-template")).Accepted) throw new InvalidOperationException("Cannot open fixture order.");
        return kitchen;
    }
    internal CookingLevelEtHost CreateHost()
    {
        var host=new CookingLevelEtHost(new CookingLevelLifecycle(Scope,Configuration,this));
        try {
            if(!host.Prepare(new(Scope.Level,new("map"),new(new("layout"),new[]{Board,Counter},new[]{new DefinitionId("cup-type")}),Configuration.Identity)).Accepted || !host.Start().Accepted)
                throw new InvalidOperationException("Cannot start trusted fixture.");
            return host;
        } catch { host.Dispose(); throw; }
    }
}
