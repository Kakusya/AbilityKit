using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
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
    public static IEnumerable<object[]> Drinks() => Enumerable.Range(1, 31).Select(i => new object[] { $"D{i:00}" });

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
        var recovered = Run(sourceId, true, expectedCanonical: uninterrupted);
        Assert.Equal(uninterrupted, recovered);
    }

    [Theory]
    [InlineData("F01")]
    [InlineData("D31")]
    public void Menu_manual_pause_change_player_resume_survives_ET_checkpoint(string sourceId)
        => Compare(sourceId, handoff: true);

    [Theory]
    [InlineData("D31")]
    public void Partially_served_menu_batch_survives_ET_checkpoint(string sourceId)
        => Compare(sourceId, partialBatch: true);

    [Theory]
    [MemberData(nameof(Drinks))]
    public void Every_drink_restores_actual_bound_cup_then_submits_and_consumes_it(string sourceId)
        => Compare(sourceId, boundCup: true);

    private static void Compare(string sourceId, bool handoff = false, bool partialBatch = false, bool boundCup = false)
    {
        var expected = Run(sourceId, false, handoff, partialBatch, boundCup);
        Assert.Equal(expected, Run(sourceId, true, handoff, partialBatch, boundCup, expected));
    }

    private static string Run(string sourceId, bool recover, bool handoff = false, bool partialBatch = false, bool boundCup = false,
        string? expectedCanonical = null)
    {
        var driver = new CookingMenuProductionFixture(Catalog(), Baseline(), sourceId, manualHandoff: handoff);
        var factory = new Factory(driver, true);
        var host = new CookingLevelEtHost(new CookingLevelLifecycle(driver.Level, driver.Content.Snapshot, factory));
        var recovered = false;
        var commandCount = 0;
        string? intermediateHash = null;
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
                commandCount++;
                var frame = host.Tick();
                Assert.True(frame.Accepted);
                return Assert.Single(frame.Dispositions).Result!;
            };
            driver.FrameAdvance = () => Assert.True(host.Tick().Accepted);
            driver.AfterFrame = () =>
            {
                var snapshot = driver.Simulation.Snapshot();
                var checkpointPoint = boundCup ? driver.Items.Any(x => x.BoundOrder is not null)
                    : handoff ? snapshot.Processes.Any(x => x.ActiveWorker is null)
                    : partialBatch ? driver.Items.Any(x => x.ContainerCompleted && x.RemainingPortions == 1)
                    : driver.Items.Any(x => x.IsProduct || x.ContainerCompleted);
                if (!recover || recovered || !checkpointPoint) return;
                var checkpoint = host.ExportCheckpoint().Checkpoint!;
                intermediateHash = Hash(checkpoint.CanonicalText());
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
            Assert.Equal(handoff, driver.HandedOff);
            driver.SubmitDelivery(product);
            Assert.Equal(recover, recovered);
            Assert.Single(driver.Simulation.SettlementHistory);
            var final = host.ExportCheckpoint().Checkpoint!;
            var canonical = final.CanonicalText();
            if (recover) Assert.Equal(expectedCanonical, canonical);
            var evidenceDirectory = Environment.GetEnvironmentVariable("COOKING_MENU_ET_EVIDENCE_DIRECTORY");
            if (recover && !string.IsNullOrWhiteSpace(evidenceDirectory))
            {
                Directory.CreateDirectory(evidenceDirectory);
                var point = boundCup ? "bound-cup" : handoff ? "paused-manual" : partialBatch ? "partial-batch" : "produced-intermediate";
                File.WriteAllText(Path.Combine(evidenceDirectory, sourceId + "-" + point + ".json"), JsonSerializer.Serialize(new
                {
                    schema = "cooking-menu-et-production-evidence-v1", menu = sourceId, checkpointPoint = point,
                    driver.Content.Identity, provenance = driver.Content.Snapshot.ContentProvenance, driver.Level,
                    recovery = "actual-host-export-codec-dispose-empty-factory-restore",
                    intermediateCheckpointSha256 = intermediateHash, finalCheckpointSha256 = Hash(canonical),
                    finalSnapshotSha256 = driver.Simulation.Snapshot().Sha256(), commandIngressCount = commandCount,
                    recipes = driver.ExecutedRecipes.Select(x => x.Value), settlements = driver.Simulation.SettlementHistory,
                    servingVessel = final.Recipe.Items.Single(x => x.Id == driver.ServingVessel),
                    fixtureBoundary = "finite validated raw/empty-vessel setup and direct OpenOrder fixture; not S07 procurement or S14 full front lifecycle",
                    finalCanonicalEqualsUninterrupted = true,
                }, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) + "\n");
            }
            return canonical;
        }
        finally { host.Dispose(); }
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
