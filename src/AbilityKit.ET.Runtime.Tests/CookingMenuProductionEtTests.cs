using System.Text.Json;
using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.Tests;
using AbilityKit.Game.Cooking.EtRuntime;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

[Trait("Gate", "CookingLevelRuntime")]
public sealed class CookingMenuProductionEtTests
{
    private static CookingMenuCatalog Catalog() => CookingMenuCatalog.Load(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, CookingMenuCatalog.ContentFileName)));
    private static CookingContentDocument Baseline() => JsonSerializer.Deserialize<CookingContentDocument>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, CookingContentCatalog.ContentFileName)),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    public static IEnumerable<object[]> Candidates() => Enumerable.Range(1, 44).Select(i => new object[] { $"F{i:00}" })
        .Concat(Enumerable.Range(1, 12).Select(i => new object[] { $"S{i:00}" }))
        .Concat(Enumerable.Range(1, 31).Select(i => new object[] { $"D{i:00}" }));

    private sealed class Factory(CookingMenuProductionFixture driver, bool initial) : ICookingLevelGameplayFactory
    {
        public CookingRecipeSimulation Simulation { get; private set; } = null!;
        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            Assert.Equal(driver.Level, scope);
            Assert.Equal(driver.Content.Snapshot.Identity, configuration.Identity);
            return Simulation = initial ? driver.Simulation : new(driver.RecipeFixture);
        }
    }

    [Theory]
    [MemberData(nameof(Candidates))]
    public void Every_candidate_uses_ET_ingress_and_continues_after_real_checkpoint_restore(string sourceId)
    {
        var uninterrupted = Run(sourceId, false);
        var recovered = Run(sourceId, true);
        Assert.Equal(uninterrupted, recovered);
    }

    private static string Run(string sourceId, bool recover)
    {
        var driver = new CookingMenuProductionFixture(Catalog(), Baseline(), sourceId);
        var factory = new Factory(driver, true);
        var host = new CookingLevelEtHost(new CookingLevelLifecycle(driver.Level, driver.Content.Snapshot, factory));
        var recovered = false;
        try
        {
            Assert.True(host.Prepare(new(driver.Level.Level, new("menu-fixture-map"),
                new(new("menu-fixture-layout"), driver.Content.Appliances.Keys.ToArray(),
                    driver.Content.Items.Values.Where(x => x.Container is not null).Select(x => x.Id).ToArray()),
                driver.Content.Snapshot.Identity)).Accepted);
            Assert.True(host.Start().Accepted);
            driver.CommandDispatcher = command =>
            {
                Assert.True(host.TryEnqueue(new(driver.Level, command, "menu-connection", command.Command.Value)).Accepted);
                var frame = host.Tick();
                Assert.True(frame.Accepted);
                return Assert.Single(frame.Dispositions).Result!;
            };
            driver.FrameAdvance = () => Assert.True(host.Tick().Accepted);
            driver.AfterFrame = () =>
            {
                if (!recover || recovered || !driver.Items.Any(x => x.IsProduct || x.ContainerCompleted)) return;
                var checkpoint = host.ExportCheckpoint().Checkpoint!;
                var decoded = CookingLevelCheckpointCodec.Deserialize(CookingLevelCheckpointCodec.Serialize(
                    CookingLevelCheckpointCodec.CreateEnvelope(checkpoint)));
                Assert.True(decoded.Accepted);
                host.Dispose();
                var restoredFactory = new Factory(driver, false);
                var restored = CookingLevelEtHost.Restore(decoded.Checkpoint!, driver.Content.Snapshot, restoredFactory);
                Assert.True(restored.Accepted);
                host = restored.Host!;
                driver.UseRestoredSimulation(restoredFactory.Simulation);
                Assert.Equal(checkpoint.CanonicalText(), host.ExportCheckpoint().Checkpoint!.CanonicalText());
                recovered = true;
            };
            var product = driver.ProduceAndPlate();
            if (!driver.Menu.RequiresBinding) driver.SubmitMeal(product);
            Assert.Equal(recover, recovered);
            // Drinks are produced and plated here; S05 owns binding and drink settlement.
            return host.ExportCheckpoint().Checkpoint!.CanonicalText();
        }
        finally { host.Dispose(); }
    }
}
