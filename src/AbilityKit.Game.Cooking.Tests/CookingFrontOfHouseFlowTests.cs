using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingFrontOfHouseFlowTests
{
    private static readonly PlayerId Player = new("chef-a");
    private static readonly PlayerId Other = new("chef-b");
    private static readonly OrderTemplateId Template = new("tomato-egg-soup-order");
    private static CookingFrontOfHouseSchedule Schedule(int service = 4, int inquiry = 4, int wait = 20) =>
        new(1, service, 1, inquiry, 3, 2, wait);
    private static CookingRecipeSimulation Kitchen()
    {
        var content = CookingContentCatalog.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, CookingContentCatalog.ContentFileName)));
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        { [Player] = new(Player, new HashSet<string> { "carry" }, new HashSet<string> { "counter-a", "stove-a" }),
          [Other] = new(Other, new HashSet<string> { "carry" }, new HashSet<string> { "counter-a", "stove-a" }) };
        var fixture = CookingContentCatalog.BuildFixture(content, new(new("front-session"), new("front-world"), new("front-match")), players);
        var kitchen = new CookingRecipeSimulation(fixture);
        CookingContentCatalog.ApplyStandardInitialSupply(kitchen, content);
        return kitchen;
    }
    private static CookingFrontOfHouseFlow Flow(int clear = 3)
    {
        var spatial = new CookingSpatialConfiguration(0, 0, 6000, 1000, 100, 1000,
            Array.Empty<CookingPlayerPose>(), Array.Empty<CookingSpatialAnchor>(), Array.Empty<CookingSpatialObstacle>());
        return new(CookingFrontOfHouseFlow.SpatialIdentity(spatial), Enumerable.Range(0, 6).Select(x => new CookingFrontPoint(x, 0)).ToArray(),
            new[] { new CookingFrontPoint(0, 0), new(1, 0) },
            new[] { new CookingFrontPoint(1, 0), new(2, 0), new(3, 0), new(4, 0), new(5, 0) },
            new[] { new CookingFrontTableRoute("table-1", new[] { new CookingFrontPoint(1, 0), new(2, 0), new(3, 0) },
                new[] { new CookingFrontPoint(3, 0), new(4, 0), new(5, 0) }) }, 2, clear) { Spatial = spatial };
    }

    [Fact]
    public void Bounded_queue_is_fifo_and_closing_drains_paths_and_real_dirty_table_work()
    {
        var kitchen = Kitchen(); var house = new CookingFrontOfHouse(Schedule(inquiry: 1, wait: 6)); house.ConfigureFlow(Flow());
        house.Step(kitchen, Template);
        Assert.Equal(CookingTablePhase.Arriving, Assert.Single(house.Snapshot().Customers).Phase);
        house.Step(kitchen, Template);
        Assert.Equal(CookingFrontTableState.Reserved, house.Snapshot().Tables[0].State);
        house.Step(kitchen, Template);
        var state = house.Snapshot();
        Assert.Contains(state.Customers, x => x.Id.Value == "customer-2" && x.Phase == CookingTablePhase.Queued);
        Assert.Equal("customer-1", state.Customers.Single(x => x.TableId == "table-1").Id.Value);
        while (!house.IsClosing) house.Step(kitchen, Template);
        var admitted = house.Snapshot().NextCustomerSequence;
        Assert.InRange(admitted, 1, 4);
        var sawDirty = false; var sawLeaving = false; var sawSecondReserved = false;
        for (var i = 0; i < 80 && !house.CanSucceed; i++)
        {
            house.Step(kitchen, Template);
            state = house.Snapshot();
            Assert.Equal(admitted, state.NextCustomerSequence);
            sawDirty |= state.Tables[0].State == CookingFrontTableState.Dirty;
            sawLeaving |= state.Customers.Any(x => x.Phase == CookingTablePhase.Leaving);
            sawSecondReserved |= state.Customers.Any(x => x.TableId == "table-1" && x.Id.Value == "customer-2");
        }
        Assert.True(sawDirty); Assert.True(sawLeaving);
        Assert.True(house.CanSucceed);
        Assert.Equal(CookingFrontTableState.Free, house.Snapshot().Tables[0].State);
        Assert.Empty(kitchen.SettlementHistory);
        Assert.NotEmpty(house.UnsatisfiedOrders);
        Assert.False(sawSecondReserved); // Customer-2 times out in FIFO while the sole table is occupied.
    }

    [Fact]
    public void Manual_inquiry_is_unique_progresses_on_ticks_and_does_not_grow_the_companion()
    {
        var kitchen = Kitchen(); var house = new CookingFrontOfHouse(Schedule(service: 1, inquiry: 2));
        house.ConfigureManualWork("reach-v1", (_, _) => true);
        house.Step(kitchen, Template);
        Assert.True(house.ClaimFrontWork(kitchen, Player, "inquiry:customer-1").Accepted);
        var before = house.Snapshot().CanonicalText();
        Assert.Equal(CookingFrontWorkRejection.Occupied, house.ClaimFrontWork(kitchen, Other, "inquiry:customer-1").Reason);
        Assert.True(house.ContinueFrontWork(Player, "inquiry:customer-1").Accepted);
        Assert.Equal(before, house.Snapshot().CanonicalText());
        house.Step(kitchen, Template); Assert.Empty(kitchen.Orders);
        house.Step(kitchen, Template); Assert.Single(kitchen.Orders);
        Assert.Equal(0, house.Snapshot().Companion.CompletedTaskCount);
        Assert.Equal(CookingFrontWorkStatus.Completed, house.Snapshot().Work.Single().Status);
    }

    [Fact]
    public void Stop_or_move_away_retains_manual_progress_and_companion_resumes_without_acceleration()
    {
        var kitchen = Kitchen(); var house = new CookingFrontOfHouse(Schedule(service: 1, inquiry: 4));
        var reachable = true; house.ConfigureManualWork("reach-v1", (_, _) => reachable);
        house.Step(kitchen, Template); house.ClaimFrontWork(kitchen, Player, "inquiry:customer-1");
        house.Step(kitchen, Template);
        Assert.Equal(1, house.Snapshot().Work.Single().ElapsedTicks);
        Assert.True(house.StopFrontWork(Player, "inquiry:customer-1").Accepted);
        Assert.Equal(1, house.Snapshot().Work.Single().ElapsedTicks);
        house.ClaimFrontWork(kitchen, Other, "inquiry:customer-1");
        reachable = false;
        house.Step(kitchen, Template);
        Assert.Null(house.Snapshot().Work.Single().Player);
        Assert.True(house.Snapshot().Work.Single().Companion);
        Assert.Equal(2, house.Snapshot().Work.Single().ElapsedTicks);
        house.Step(kitchen, Template); Assert.Empty(kitchen.Orders);
        house.Step(kitchen, Template); Assert.Single(kitchen.Orders);
        Assert.Equal(1, house.Snapshot().Companion.CompletedTaskCount);
    }

    [Fact]
    public void Customer_departure_cancels_manual_inquiry_and_finish_hook_cannot_create_ghost_orders()
    {
        var kitchen = Kitchen(); var house = new CookingFrontOfHouse(Schedule(service: 1, inquiry: 20, wait: 2));
        house.ConfigureManualWork("reach-v1", (_, _) => true);
        house.Step(kitchen, Template); house.ClaimFrontWork(kitchen, Player, "inquiry:customer-1");
        house.FinishInProgress(kitchen, Template); Assert.Empty(kitchen.Orders);
        house.Step(kitchen, Template); house.Step(kitchen, Template);
        Assert.True(house.CanSucceed); Assert.Empty(kitchen.Orders);
        Assert.Equal(CookingFrontWorkStatus.Cancelled, house.Snapshot().Work.Single().Status);
        Assert.Equal(0, house.Snapshot().Companion.CompletedTaskCount);
    }

    [Fact]
    public void Flow_finish_hook_cannot_skip_walking_or_open_inquiries()
    {
        var kitchen = Kitchen(); var house = new CookingFrontOfHouse(Schedule(service: 1)); house.ConfigureFlow(Flow());
        house.Step(kitchen, Template);
        var before = house.Snapshot().CanonicalText();
        house.FinishInProgress(kitchen, Template);
        Assert.Empty(kitchen.Orders); Assert.Equal(before, house.Snapshot().CanonicalText()); Assert.False(house.CanSucceed);
    }

    [Fact]
    public void Paths_and_manual_work_restore_then_continue_and_poison_is_rejected_without_mutation()
    {
        var kitchen = Kitchen(); var house = new CookingFrontOfHouse(Schedule(service: 3)); house.ConfigureFlow(Flow());
        for (var i = 0; i < 3; i++) house.Step(kitchen, Template);
        var checkpoint = house.ExportCheckpoint(Template);
        var restored = CookingFrontOfHouse.Restore(checkpoint, kitchen);
        Assert.True(restored.Accepted);
        Assert.Equal(checkpoint.State.CanonicalText(), restored.FrontOfHouse!.Snapshot().CanonicalText());
        var before = house.Snapshot().CanonicalText();
        var poisoned = checkpoint with { State = checkpoint.State with { Customers = checkpoint.State.Customers.Select((x, i) => i == 0 ? x with { PathIndex = 999 } : x).ToArray() } };
        Assert.False(house.RestoreCheckpoint(poisoned, kitchen).Accepted);
        Assert.Equal(before, house.Snapshot().CanonicalText());
        var manual = new CookingFrontOfHouse(Schedule(service: 1)); manual.ConfigureManualWork("reach-v1", (_, _) => true);
        var secondKitchen = Kitchen(); manual.Step(secondKitchen, Template); manual.ClaimFrontWork(secondKitchen, Player, "inquiry:customer-1");
        manual.Step(secondKitchen, Template);
        var manualCheckpoint = manual.ExportCheckpoint(Template);
        var manualRestored = CookingFrontOfHouse.Restore(manualCheckpoint, secondKitchen);
        Assert.True(manualRestored.Accepted);
        manualRestored.FrontOfHouse!.ConfigureManualWork("reach-v1", (_, _) => true);
        Assert.Equal(manual.Snapshot().CanonicalText(), manualRestored.FrontOfHouse.Snapshot().CanonicalText());
        var duplicateOwner = manualCheckpoint with { State = manualCheckpoint.State with { Work = manualCheckpoint.State.Work.Concat(manualCheckpoint.State.Work).ToArray() } };
        Assert.False(manual.RestoreCheckpoint(duplicateOwner, secondKitchen).Accepted);
        var poisonedGeometry = checkpoint with { State = checkpoint.State with { Flow = checkpoint.State.Flow! with { GeometryIdentity = "different" } } };
        Assert.False(house.RestoreCheckpoint(poisonedGeometry, kitchen).Accepted);
    }

    [Fact]
    public void Immutable_geometry_rejects_walls_and_nonadjacent_path_edges()
    {
        var house = new CookingFrontOfHouse(Schedule()); var flow = Flow();
        var blocked = flow.Spatial! with { Obstacles = new[] { new CookingSpatialObstacle(1800, 0, 2200, 1000) } };
        Assert.Throws<ArgumentException>(() => house.ConfigureFlow(flow with { Spatial = blocked, GeometryIdentity = CookingFrontOfHouseFlow.SpatialIdentity(blocked) }));
        Assert.Throws<ArgumentException>(() => house.ConfigureFlow(flow with { EntranceToQueue = new[] { new CookingFrontPoint(0, 0), new(2, 0) } }));
        Assert.Null(house.Snapshot().Flow);
        house.ConfigureFlow(flow);
        Assert.IsAssignableFrom<System.Collections.ObjectModel.ReadOnlyCollection<CookingFrontPoint>>(house.Snapshot().Flow!.Walkable);
    }

    [Fact]
    public void New_arrival_at_table_has_a_real_manual_claim_window_before_partner_takes_it()
    {
        var kitchen = Kitchen(); var house = new CookingFrontOfHouse(Schedule(service: 1, inquiry: 2)); house.ConfigureFlow(Flow());
        house.ConfigureManualWork("reach-v1", (_, _) => true);
        for (var i = 0; i < 4; i++) house.Step(kitchen, Template);
        Assert.Equal(CookingTablePhase.WaitingForInquiry, Assert.Single(house.Snapshot().Customers).Phase);
        Assert.True(house.CompanionIdle);
        Assert.True(house.ClaimFrontWork(kitchen, Player, "inquiry:customer-1").Accepted);
        house.Step(kitchen, Template); house.Step(kitchen, Template);
        Assert.Single(kitchen.Orders); Assert.Equal(0, house.Snapshot().Companion.CompletedTaskCount);
    }

    [Fact]
    public void FIFO_second_customer_takes_the_table_only_after_clear_work_completes()
    {
        var kitchen = Kitchen(); var house = new CookingFrontOfHouse(Schedule(service: 3, inquiry: 1, wait: 50)); house.ConfigureFlow(Flow(clear: 3));
        for (var i = 0; i < 5; i++) house.Step(kitchen, Template);
        Assert.True(kitchen.MarkOrderUnsatisfied(new("customer-1-order")).Accepted);
        house.Step(kitchen, Template);
        Assert.Equal(CookingFrontTableState.Dirty, house.Snapshot().Tables[0].State);
        Assert.Contains(house.Snapshot().Customers, x => x.Id.Value == "customer-2" && x.Phase == CookingTablePhase.Queued);
        for (var i = 0; i < 2; i++)
        {
            house.Step(kitchen, Template);
            Assert.Equal(CookingFrontTableState.Dirty, house.Snapshot().Tables[0].State);
        }
        house.Step(kitchen, Template);
        Assert.Equal(CookingFrontTableState.Reserved, house.Snapshot().Tables[0].State);
        Assert.Equal("customer-2", house.Snapshot().Customers.Single(x => x.TableId == "table-1").Id.Value);
        Assert.Equal(1, house.Snapshot().Companion.CompletedTaskCount); // Clearing adds no inquiry/wash growth.
    }

    [Fact]
    public void Manual_wash_removes_the_real_queue_entry_and_checkpoint_stays_restorable()
    {
        var kitchen = Kitchen(); var house = new CookingFrontOfHouse(Schedule(service: 1, inquiry: 20, wait: 50));
        house.ConfigureManualWork("reach-v1", (_, _) => true);
        house.Step(kitchen, Template);
        var bowl = new ItemId("pool-bowl-1"); kitchen.MarkBowlDirtyForTest(bowl);
        house.Step(kitchen, Template); // Companion is inquiring; wash is available.
        Assert.True(house.ClaimFrontWork(kitchen, Player, "wash:pool-bowl-1").Accepted);
        for (var i = 0; i < 3; i++) house.Step(kitchen, Template);
        Assert.DoesNotContain(bowl, kitchen.DirtyBowlsAwaitingWash());
        Assert.DoesNotContain(bowl, house.Snapshot().WashQueue);
        Assert.Equal(0, house.Snapshot().Companion.CompletedTaskCount);
        Assert.True(CookingFrontOfHouse.Restore(house.ExportCheckpoint(Template), kitchen).Accepted);
    }

    [Fact]
    public void Restored_walking_and_manual_progress_continue_with_equivalent_end_states()
    {
        var originalKitchen = Kitchen(); var original = new CookingFrontOfHouse(Schedule(service: 1, inquiry: 3)); original.ConfigureFlow(Flow());
        original.ConfigureManualWork("reach-v1", (_, _) => true);
        for (var i = 0; i < 4; i++) original.Step(originalKitchen, Template);
        original.ClaimFrontWork(originalKitchen, Player, "inquiry:customer-1"); original.Step(originalKitchen, Template);
        var checkpoint = original.ExportCheckpoint(Template);
        var restoredKitchen = Kitchen(); var result = CookingFrontOfHouse.Restore(checkpoint, restoredKitchen);
        Assert.True(result.Accepted); var restored = result.FrontOfHouse!;
        restored.ConfigureManualWork("reach-v1", (_, _) => true);
        for (var i = 0; i < 40 && !original.CanSucceed; i++)
        {
            original.Step(originalKitchen, Template); restored.Step(restoredKitchen, Template);
            Assert.Equal(original.Snapshot().CanonicalText(), restored.Snapshot().CanonicalText());
            Assert.Equal(originalKitchen.Snapshot().CanonicalText(), restoredKitchen.Snapshot().CanonicalText());
        }
        Assert.True(original.CanSucceed); Assert.True(restored.CanSucceed);
    }

    [Fact]
    public void Old_wash_work_id_cannot_claim_a_new_dirty_cycle_for_the_same_bowl()
    {
        var kitchen = Kitchen(); var house = new CookingFrontOfHouse(Schedule(service: 1, inquiry: 40, wait: 50));
        house.ConfigureManualWork("reach-v1", (_, _) => true);
        house.Step(kitchen, Template);
        var bowl = new ItemId("pool-bowl-1"); kitchen.MarkBowlDirtyForTest(bowl); house.Step(kitchen, Template);
        house.ClaimFrontWork(kitchen, Player, "wash:pool-bowl-1");
        for (var i = 0; i < 3; i++) house.Step(kitchen, Template);
        kitchen.MarkBowlDirtyForTest(bowl); house.Step(kitchen, Template);
        var before = house.Snapshot().CanonicalText();
        Assert.Equal(CookingFrontWorkRejection.Occupied, house.ClaimFrontWork(kitchen, Player, "wash:pool-bowl-1").Reason);
        Assert.Equal(before, house.Snapshot().CanonicalText());
        Assert.True(house.ClaimFrontWork(kitchen, Player, "wash:pool-bowl-1:2").Accepted);
        Assert.True(CookingFrontOfHouse.Restore(house.ExportCheckpoint(Template), kitchen).Accepted);
    }

    [Fact]
    public void Restore_rejects_two_unfinished_wash_cycles_or_an_unfinished_old_cycle_without_mutation()
    {
        var kitchen = Kitchen(); var house = new CookingFrontOfHouse(Schedule(service: 1, inquiry: 40, wait: 50));
        house.ConfigureManualWork("reach-v1", (_, _) => true);
        house.Step(kitchen, Template);
        var bowl = new ItemId("pool-bowl-1");
        kitchen.MarkBowlDirtyForTest(bowl); house.Step(kitchen, Template);
        Assert.True(house.ClaimFrontWork(kitchen, Player, "wash:pool-bowl-1").Accepted);
        for (var i = 0; i < 3; i++) house.Step(kitchen, Template);
        kitchen.MarkBowlDirtyForTest(bowl); house.Step(kitchen, Template);
        Assert.True(house.ClaimFrontWork(kitchen, Player, "wash:pool-bowl-1:2").Accepted);
        var checkpoint = house.ExportCheckpoint(Template);
        Assert.True(CookingFrontOfHouse.Restore(checkpoint, kitchen).Accepted);
        Assert.Equal(CookingFrontWorkStatus.Completed, checkpoint.State.Work.Single(x => x.Bowl == bowl && x.Cycle == 1).Status);
        Assert.Equal(CookingFrontWorkStatus.Working, checkpoint.State.Work.Single(x => x.Bowl == bowl && x.Cycle == 2).Status);
        var before = house.Snapshot().CanonicalText();
        var twoUnfinished = checkpoint with { State = checkpoint.State with
        {
            Work = checkpoint.State.Work.Select(x => x.Bowl == bowl && x.Cycle == 1
                ? x with { Status = CookingFrontWorkStatus.Paused, ElapsedTicks = 0 } : x).ToArray()
        } };
        var oldUnfinished = checkpoint with { State = checkpoint.State with
        {
            Work = checkpoint.State.Work.Select(x => x.Bowl != bowl ? x : x.Cycle == 1
                ? x with { Status = CookingFrontWorkStatus.Paused, ElapsedTicks = 0 }
                : x with { Status = CookingFrontWorkStatus.Cancelled, Player = null }).ToArray()
        } };
        foreach (var poison in new[] { twoUnfinished, oldUnfinished })
        {
            Assert.False(CookingFrontOfHouse.Restore(poison, kitchen).Accepted);
            Assert.False(house.RestoreCheckpoint(poison, kitchen).Accepted);
            Assert.Equal(before, house.Snapshot().CanonicalText());
        }
        Assert.True(CookingFrontOfHouse.Restore(checkpoint, kitchen).Accepted);
    }

    [Fact]
    public void Missing_or_null_extended_fields_reject_cleanly_without_replacing_the_house()
    {
        var kitchen = Kitchen(); var house = new CookingFrontOfHouse(Schedule(service: 1)); house.ConfigureFlow(Flow());
        house.Step(kitchen, Template); var checkpoint = house.ExportCheckpoint(Template);
        var before = house.Snapshot().CanonicalText();
        var poisons = new[]
        {
            checkpoint with { State = checkpoint.State with { Tables = Array.Empty<CookingFrontTableSnapshot>() } },
            checkpoint with { State = checkpoint.State with { Tables = new CookingFrontTableSnapshot[] { null! } } },
            checkpoint with { State = checkpoint.State with { Work = new CookingFrontWorkSnapshot[] { null! } } },
            checkpoint with { State = checkpoint.State with { Flow = checkpoint.State.Flow! with { Spatial = null } } },
            checkpoint with { State = checkpoint.State with { Flow = checkpoint.State.Flow! with { Spatial = checkpoint.State.Flow!.Spatial! with { Obstacles = null! } } } }
        };
        foreach (var poison in poisons)
        {
            Assert.False(house.RestoreCheckpoint(poison, kitchen).Accepted);
            Assert.False(CookingFrontOfHouse.Restore(poison, kitchen).Accepted);
            Assert.Equal(before, house.Snapshot().CanonicalText());
        }
    }

    [Fact]
    public void Partial_wash_retains_its_duration_when_the_partner_unlocks_speed_during_manual_work()
    {
        var kitchen = Kitchen();
        var house = new CookingFrontOfHouse(new CookingFrontOfHouseSchedule(1, 1, 1, 2, 3, 2, 50, 1));
        house.ConfigureManualWork("reach-v1", (_, _) => true);
        house.Step(kitchen, Template);
        kitchen.MarkBowlDirtyForTest(new("pool-bowl-1")); house.Step(kitchen, Template);
        house.ClaimFrontWork(kitchen, Player, "wash:pool-bowl-1"); house.Step(kitchen, Template);
        Assert.True(house.Snapshot().Companion.WashSpeedUnlocked);
        house.StopFrontWork(Player, "wash:pool-bowl-1"); house.Step(kitchen, Template);
        Assert.Equal(2, house.Snapshot().Companion.ElapsedTicks);
        Assert.Equal(3, house.Snapshot().Companion.RequiredTicks);
        Assert.True(CookingFrontOfHouse.Restore(house.ExportCheckpoint(Template), kitchen).Accepted);
        house.Step(kitchen, Template);
        Assert.DoesNotContain(new ItemId("pool-bowl-1"), kitchen.DirtyBowlsAwaitingWash());
        Assert.Equal(2, house.Snapshot().Companion.CompletedTaskCount);
    }

    [Fact]
    public void Successful_reset_cannot_bypass_the_new_flow_before_natural_completion()
    {
        var kitchen = Kitchen(); var house = new CookingFrontOfHouse(Schedule(service: 1)); house.ConfigureFlow(Flow());
        house.Step(kitchen, Template); var before = house.Snapshot().CanonicalText();
        Assert.Throws<InvalidOperationException>(() => house.ResetForNextLevel(kitchen, Template));
        Assert.Equal(before, house.Snapshot().CanonicalText()); Assert.Empty(kitchen.Orders);
    }
}
