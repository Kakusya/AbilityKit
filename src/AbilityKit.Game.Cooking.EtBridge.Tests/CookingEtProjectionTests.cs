using System.Reflection;
using Xunit;

namespace AbilityKit.Game.Cooking.EtBridge.Tests;

public class CookingEtProjectionTests
{
    private static global::ET.Scene CreateScene()
    {
        var launchOptions = global::ET.Logic.ETDemoProcessLaunchOptions.CreateLocalSmokeDefaults();
        global::ET.AbilityKit.Demo.ET.App.DemoEntry.Init(launchOptions);
        var ctor = typeof(global::ET.Fiber).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance, null,
            new[] { typeof(int), typeof(int), typeof(int), typeof(string) }, null);
        Assert.NotNull(ctor);
        var fiber = (global::ET.Fiber)ctor.Invoke(new object[] { 2, 0, (int)global::ET.SceneType.Main, "cooking-bridge-test" });
        return new global::ET.Scene(fiber, 10, 10, (int)global::ET.SceneType.Main, "cooking-test");
    }

    private static CookingSimulation CreateSimulation()
    {
        var player = new PlayerId("p1");
        var definition = new DefinitionId("tomato");
        var capabilities = new HashSet<string>(StringComparer.Ordinal) { "cook" };
        return new CookingSimulation(new CookingFixture(
            new CookingScope(new SessionId("session"), new WorldId("restaurant"), new MatchId("service")),
            new Dictionary<PlayerId, CookingPlayerConfig>
            {
                [player] = new(player, capabilities, new HashSet<string>()),
            },
            new Dictionary<StationSlotId, CookingStationConfig>(),
            new Dictionary<DefinitionId, CookingItemDefinition>
            {
                [definition] = new(definition, capabilities),
            }));
    }

    [Fact]
    public void Snapshot_Items_Project_To_Entity_Tree_And_Sync()
    {
        var sim = CreateSimulation();
        sim.AddItem(new ItemId("tomato-1"), new DefinitionId("tomato"), ItemLocation.World("counter-1"));

        using var scene = CreateScene();
        var registry = CookingEtProjection.ApplySnapshot(scene, sim.Snapshot());
        var entity = registry.Find(new ItemId("tomato-1"));
        Assert.NotNull(entity);
        Assert.Equal(new DefinitionId("tomato"), entity.Definition);
        Assert.Equal(ItemLocation.World("counter-1"), entity.Location);
        Assert.Same(registry, entity.Parent);

        var result = sim.Submit(new CookingCommand(
            sim.Snapshot().Scope, 1, new PlayerId("p1"), new CommandId("c1"),
            CookingOperation.Pickup, new ItemId("tomato-1"), 1));
        Assert.Equal(CommandOutcome.Accepted, result.Outcome);
        CookingEtProjection.ApplySnapshot(scene, sim.Snapshot());
        Assert.Same(entity, registry.Find(new ItemId("tomato-1")));
        Assert.Equal(ItemLocation.Hand(new PlayerId("p1")), entity.Location);
        Assert.Equal(2, entity.Version);
    }

    [Fact]
    public void Reapplying_Snapshot_Preserves_Entity_And_Authoritative_State()
    {
        var sim = CreateSimulation();
        sim.AddItem(new ItemId("tomato-1"), new DefinitionId("tomato"), ItemLocation.World("counter-1"));
        var snapshot = sim.Snapshot();
        var before = snapshot.CanonicalText();
        using var scene = CreateScene();
        var registry = CookingEtProjection.ApplySnapshot(scene, snapshot);
        var entity = registry.Find(new ItemId("tomato-1"));

        Assert.Same(registry, CookingEtProjection.ApplySnapshot(scene, snapshot));
        Assert.Same(entity, registry.Find(new ItemId("tomato-1")));
        Assert.Single(registry.Items);
        Assert.Equal(before, sim.Snapshot().CanonicalText());
    }

    [Fact]
    public void Scene_Disposal_Recursively_Disposes_Projected_Items()
    {
        var sim = CreateSimulation();
        sim.AddItem(new ItemId("tomato-1"), new DefinitionId("tomato"), ItemLocation.World("counter-1"));
        using var scene = CreateScene();
        var registry = CookingEtProjection.ApplySnapshot(scene, sim.Snapshot());
        var entity = registry.Find(new ItemId("tomato-1"))!;
        global::ET.EntityRef<CookingItemEntity> reference = entity;

        scene.Dispose();

        Assert.True(registry.IsDisposed);
        Assert.True(entity.IsDisposed);
        Assert.Null((CookingItemEntity)reference);
        Assert.Null(registry.Find(new ItemId("tomato-1")));
    }

    [Fact]
    public void Removed_Item_Is_Destroyed_And_EntityRef_Invalidated()
    {
        var sim = CreateSimulation();
        sim.AddItem(new ItemId("tomato-1"), new DefinitionId("tomato"), ItemLocation.World("counter-1"));

        using var scene = CreateScene();
        var registry = CookingEtProjection.ApplySnapshot(scene, sim.Snapshot());
        var entity = registry.Find(new ItemId("tomato-1"))!;
        global::ET.EntityRef<CookingItemEntity> weakRef = entity;

        sim.RemoveItem(new ItemId("tomato-1"));
        CookingEtProjection.ApplySnapshot(scene, sim.Snapshot());

        Assert.True(entity.IsDisposed);
        Assert.Null((CookingItemEntity)weakRef);
        Assert.Null(registry.Find(new ItemId("tomato-1")));
        Assert.Empty(registry.Items);
    }
}
