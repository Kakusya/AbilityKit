using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

[Trait("Gate", "CookingLevelRuntime")]
public sealed class CookingPreparedExpansionEtTests
{
    [Fact]
    public void Preparing_floor_expansion_opens_actual_movement_and_restores_inside_new_area_before_start()
    {
        var live = Run(false);
        var restored = Run(true);
        Assert.Equal(live, restored);
    }

    private static IReadOnlyList<string> Run(bool recover)
    {
        var factory = new CookingNaturalOperatingFixture(); factory.Begin();
        var host = factory.Host;
        var trace = new List<string>();
        long sequence = 0;
        try
        {
            var scope = factory.Scope;
            var initial = factory.CreatePreparationConfiguration(scope, factory.Content.Snapshot).InitialLayout;
            var floor = Assert.Single(initial.Floors);
            Assert.Equal(0, floor.X); Assert.Equal(0, floor.Y);
            string Canonical() => host.ExportCheckpoint().Checkpoint!.CanonicalText() + "\n" + host.Observe().CanonicalText();
            CookingPlayerPose Pose() => host.Driver.Simulation!.Snapshot().Poses!.Single(p => p.Player == CookingNaturalOperatingFixture.Chef);
            void Move(int x, int y, bool accepted = true)
            {
                var command = new CookingRecipeCommand(scope.MatchScope, ++sequence, CookingNaturalOperatingFixture.Chef,
                    new("expansion-" + sequence), CookingRecipeOperation.Move, MoveX: x, MoveY: y,
                    FacingX: Math.Sign(x), FacingY: Math.Sign(y));
                Assert.True(host.TryEnqueue(new(scope, command, "expansion-local", command.Command.Value)).Accepted);
                var frame = host.Tick(); Assert.True(frame.Accepted);
                var result = Assert.Single(frame.Dispositions).Result!;
                Assert.Equal(accepted ? CookingRecipeOutcome.Accepted : CookingRecipeOutcome.Rejected, result.Outcome);
                if (!accepted) Assert.Equal(CookingRecipeRejectionReason.MovementBlocked, result.Reason);
                trace.Add(Canonical());
            }
            trace.Add(Canonical());
            // This corridor is empty in the trusted initial layout; movement still
            // goes through the continuous production Move command and collision rules.
            Move(0, -1000);
            for (var step = 1; step < floor.Width; step++) Move(1000, 0);
            var oldEdge = Pose();
            Assert.Equal(floor.Width * 1000 - 500, oldEdge.X);
            Move(1000, 0, accepted: false);
            Assert.Equal(oldEdge.X, Pose().X); Assert.Equal(oldEdge.Y, Pose().Y);

            var expanded = initial with { Id = new("natural-expanded-layout"),
                Floors = new[] { floor with { Width = floor.Width + 4 } } };
            Assert.True(host.TryInstallPreparedLayout(expanded));
            Assert.Equal((floor.Width + 4) * 1000, host.Driver.Simulation!.SpatialConfiguration!.MaxX);
            trace.Add(Canonical());
            Move(1000, 0); Move(1000, 0);
            Assert.True(Pose().X > floor.Width * 1000);
            Assert.Equal(CookingLevelState.Preparing, host.Lifecycle.State);
            var saved = host.ExportCheckpoint().Checkpoint!;
            var savedObservation = host.Observe().CanonicalText();
            var serialized = CookingLevelCheckpointCodec.Serialize(CookingLevelCheckpointCodec.CreateEnvelope(saved));
            var decoded = CookingLevelCheckpointCodec.Deserialize(serialized);
            Assert.True(decoded.Accepted, decoded.ToString());
            if (recover)
            {
                host.Dispose();
                var fresh = new CookingNaturalOperatingFixture();
                var loaded = CookingLevelEtHost.Restore(decoded.Checkpoint!, fresh.Content.Snapshot, fresh);
                Assert.True(loaded.Accepted, loaded.ToString()); host = loaded.Host!;
                Assert.Equal(saved.CanonicalText(), host.ExportCheckpoint().Checkpoint!.CanonicalText());
                Assert.Equal(savedObservation, host.Observe().CanonicalText());
                Assert.True(Pose().X > floor.Width * 1000);
            }
            Move(1000, 0); Move(-1000, 0);
            Assert.True(host.CompletePreparation().Accepted); Assert.True(host.Start().Accepted);
            Assert.Equal(CookingLevelState.Running, host.Lifecycle.State);
            Assert.True(host.Tick().Accepted); trace.Add(Canonical());
            return trace;
        }
        finally { host.Dispose(); }
    }
}
