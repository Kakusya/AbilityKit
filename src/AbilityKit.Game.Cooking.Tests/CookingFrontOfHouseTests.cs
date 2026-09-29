using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

/// <summary>
/// 任务 <c>09-22-cooking-front-of-house</c>：问完才开单，空闲才洗碗，
/// 营业和收尾走完且座位空了才允许成功。
/// </summary>
[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingFrontOfHouseTests
{
    private static readonly SessionId Session = new("front-session");
    private static readonly WorldId World = new("front-world");
    private static readonly MatchId Match = new("front-match");
    private static readonly PlayerId Player = new("chef-a");
    private static readonly OrderTemplateId Template = new("tomato-egg-soup-order");
    private static readonly ItemId Bowl = new("pool-bowl-1");
    private static readonly DefinitionId BowlDefinition = new("bowl");

    [Fact]
    public void F01_an_order_opens_only_after_the_inquiry_finishes()
    {
        var house = House();
        var kitchen = Kitchen();

        Assert.Equal(1, house.Step(kitchen, Template).SeatedCount);
        Assert.Empty(kitchen.Orders);

        house.Step(kitchen, Template);
        Assert.Empty(kitchen.Orders);

        house.Step(kitchen, Template);
        var order = Assert.Single(kitchen.Orders);
        Assert.Equal("customer-1-order", order.Id.Value);
        Assert.Equal(CookingOrderStatus.Open.ToString(), order.Status);
    }

    [Fact]
    public void F02_washing_waits_until_inquiry_is_finished()
    {
        var house = House();
        var kitchen = Kitchen();
        house.Step(kitchen, Template);
        Dirty(kitchen);

        house.Step(kitchen, Template);
        Assert.Contains(Bowl, kitchen.DirtyBowlsAwaitingWash());

        house.Step(kitchen, Template);
        Assert.Contains(Bowl, kitchen.DirtyBowlsAwaitingWash());

        house.Step(kitchen, Template);
        house.Step(kitchen, Template);
        Assert.DoesNotContain(Bowl, kitchen.DirtyBowlsAwaitingWash());
        Assert.Equal(2, kitchen.CleanContainerCount(BowlDefinition));
    }

    [Fact]
    public void F03_closing_admits_nobody_and_success_requires_empty_seats()
    {
        var house = new CookingFrontOfHouse(Schedule(serviceTicks: 1, waitLimitTicks: 4, diningTicks: 1));
        var kitchen = Kitchen();

        house.Step(kitchen, Template);
        Assert.True(house.IsClosing);
        Assert.Equal(1, house.Step(kitchen, Template).SeatedCount);
        Assert.False(house.CanSucceed);

        house.Step(kitchen, Template);
        var order = new OrderId("customer-1-order");
        Assert.Equal(CookingOrderStatus.Open.ToString(), Status(kitchen, order));
        kitchen.MarkOrderUnsatisfied(order);
        Assert.Empty(kitchen.SettlementHistory);

        var left = house.Step(kitchen, Template);
        Assert.Equal(0, left.SeatedCount);
        Assert.True(left.CanSucceed);
        Assert.Empty(kitchen.SettlementHistory);
    }

    [Fact]
    public void F04_a_timeout_does_not_write_a_settlement()
    {
        var house = House(waitLimitTicks: 1);
        var kitchen = Kitchen();
        Assert.Equal(1, house.Step(kitchen, Template).SeatedCount);
        var left = house.Step(kitchen, Template);
        Assert.Equal(1, left.UnsatisfiedCount);
        Assert.Empty(kitchen.SettlementHistory);
        Assert.Empty(kitchen.Orders);
        Assert.Contains(new OrderId("customer-1-order"), house.UnsatisfiedOrders);
    }

    [Fact]
    public void Q01_a_failed_retry_drops_the_front_of_house_without_touching_the_kitchen()
    {
        var house = new CookingFrontOfHouse(Schedule(serviceTicks: 2, waitLimitTicks: 1));
        var kitchen = Kitchen();
        var clean = kitchen.CleanContainerCount(BowlDefinition);
        house.Step(kitchen, Template);
        house.Step(kitchen, Template);
        var left = house.Step(kitchen, Template);
        Assert.Equal(1, left.UnsatisfiedCount);
        Dirty(kitchen);

        house.DropFailedScene();

        Assert.Equal(0, house.SeatedCount);
        Assert.False(house.IsClosing);
        Assert.Empty(house.UnsatisfiedOrders);
        Assert.Empty(kitchen.Orders);
        Assert.Equal(clean - 1, kitchen.CleanContainerCount(BowlDefinition));
        Assert.Contains(Bowl, kitchen.DirtyBowlsAwaitingWash());
    }

    [Fact]
    public void N01_the_next_level_clears_seats_and_keeps_a_dirty_bowl()
    {
        var house = new CookingFrontOfHouse(Schedule(serviceTicks: 2, waitLimitTicks: 1));
        var kitchen = Kitchen();
        house.Step(kitchen, Template);
        house.Step(kitchen, Template);
        Assert.True(house.IsClosing);
        var left = house.Step(kitchen, Template);
        Assert.Equal(1, left.UnsatisfiedCount);
        Dirty(kitchen);
        house.Step(kitchen, Template);
        Assert.Contains(Bowl, kitchen.DirtyBowlsAwaitingWash());

        house.ResetForNextLevel(kitchen, Template);

        Assert.Equal(0, house.SeatedCount);
        Assert.False(house.IsClosing);
        Assert.Empty(house.UnsatisfiedOrders);
        Assert.DoesNotContain(Bowl, kitchen.DirtyBowlsAwaitingWash());
        Assert.Equal(1, house.Step(kitchen, Template).SeatedCount);
        Assert.False(house.IsClosing);
    }

    [Fact]
    public void G01_a_served_guest_keeps_the_seat_until_dining_ends()
    {
        var house = new CookingFrontOfHouse(Schedule(serviceTicks: 1, diningTicks: 2));
        var kitchen = Kitchen();
        house.Step(kitchen, Template);
        house.Step(kitchen, Template);
        house.Step(kitchen, Template);
        var order = new OrderId("customer-1-order");
        kitchen.MarkOrderCompletedForTest(order);

        Assert.Equal(1, house.Step(kitchen, Template).SeatedCount);
        Assert.False(house.CanSucceed);
        Assert.Equal(1, house.Step(kitchen, Template).SeatedCount);

        var left = house.Step(kitchen, Template);
        Assert.Equal(0, left.SeatedCount);
        Assert.True(left.CanSucceed);
        Assert.Empty(kitchen.SettlementHistory);
    }

    [Fact]
    public void F05_success_finish_completes_the_current_inquiry_and_wash()
    {
        var house = House();
        var kitchen = Kitchen();
        house.Step(kitchen, Template);
        house.Step(kitchen, Template);

        house.FinishInProgress(kitchen, Template);

        Assert.Equal(CookingOrderStatus.Open.ToString(), Status(kitchen, new OrderId("customer-1-order")));
        Assert.True(house.CompanionIdle);

        Dirty(kitchen);
        house.Step(kitchen, Template);
        Assert.Contains(Bowl, kitchen.DirtyBowlsAwaitingWash());
        house.FinishInProgress(kitchen, Template);
        Assert.DoesNotContain(Bowl, kitchen.DirtyBowlsAwaitingWash());
        Assert.Equal(2, kitchen.CleanContainerCount(BowlDefinition));
    }

    [Fact]
    public void V01_the_same_table_serves_two_customers_with_distinct_orders()
    {
        var house = new CookingFrontOfHouse(Schedule(tableCount: 1, serviceTicks: 10, arrivalIntervalTicks: 1,
            inquiryTicks: 1, diningTicks: 1));
        var kitchen = Kitchen();

        house.Step(kitchen, Template);
        house.Step(kitchen, Template);
        var first = Assert.Single(house.Snapshot().Customers);
        Assert.Equal(new CookingCustomerId("customer-1"), first.Id);
        Assert.Equal(new OrderId("customer-1-order"), first.Order);
        kitchen.MarkOrderCompletedForTest(first.Order!.Value);

        house.Step(kitchen, Template);
        house.Step(kitchen, Template);
        var secondWaiting = Assert.Single(house.Snapshot().Customers);
        Assert.Equal(new CookingCustomerId("customer-2"), secondWaiting.Id);
        Assert.Equal(CookingTablePhase.WaitingForInquiry, secondWaiting.Phase);

        house.Step(kitchen, Template);
        var second = Assert.Single(house.Snapshot().Customers);
        Assert.Equal(new OrderId("customer-2-order"), second.Order);
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.Order, second.Order);
        Assert.Contains(kitchen.Orders, order => order.Id == first.Order);
        Assert.Contains(kitchen.Orders, order => order.Id == second.Order);
    }

    [Fact]
    public void V02_snapshot_exposes_customer_phases_and_companion_progress()
    {
        var house = new CookingFrontOfHouse(Schedule(tableCount: 1, serviceTicks: 10,
            arrivalIntervalTicks: 10, inquiryTicks: 2, diningTicks: 2));
        var kitchen = Kitchen();

        house.Step(kitchen, Template);
        var waiting = house.Snapshot();
        Assert.Equal(CookingTablePhase.WaitingForInquiry, Assert.Single(waiting.Customers).Phase);
        Assert.Equal(CookingCompanionWorkKind.Idle, waiting.Companion.Work);

        house.Step(kitchen, Template);
        var inquiring = house.Snapshot();
        var customer = Assert.Single(inquiring.Customers);
        Assert.Equal(CookingTablePhase.InquiryInProgress, customer.Phase);
        Assert.Equal(CookingCompanionWorkKind.Inquiring, inquiring.Companion.Work);
        Assert.Equal(customer.Id, inquiring.Companion.TargetCustomer);
        Assert.Equal(customer.TableId, inquiring.Companion.TargetTable);
        Assert.Equal(1, inquiring.Companion.ElapsedTicks);
        Assert.Equal(2, inquiring.Companion.RequiredTicks);

        house.Step(kitchen, Template);
        var ordered = house.Snapshot();
        customer = Assert.Single(ordered.Customers);
        Assert.Equal(CookingTablePhase.Ordered, customer.Phase);
        Assert.Equal(new OrderId("customer-1-order"), customer.Order);
        Assert.Equal(CookingCompanionWorkKind.Idle, ordered.Companion.Work);

        kitchen.MarkOrderCompletedForTest(customer.Order!.Value);
        house.Step(kitchen, Template);
        var dining = house.Snapshot();
        Assert.Equal(CookingTablePhase.Dining, Assert.Single(dining.Customers).Phase);
    }

    [Fact]
    public void V03_canonical_hash_is_deterministic_and_sensitive_to_progress()
    {
        var first = House();
        var second = House();
        var differentThreshold = new CookingFrontOfHouse(Schedule(companionGrowthTaskThreshold: 4));
        var firstKitchen = Kitchen();
        var secondKitchen = Kitchen();

        Assert.NotEqual(first.Snapshot().CanonicalText(), differentThreshold.Snapshot().CanonicalText());
        first.Step(firstKitchen, Template);
        second.Step(secondKitchen, Template);
        Assert.Equal(first.Snapshot().CanonicalText(), second.Snapshot().CanonicalText());
        Assert.Equal(first.Snapshot().Sha256(), second.Snapshot().Sha256());

        first.Step(firstKitchen, Template);
        Assert.NotEqual(first.Snapshot().CanonicalText(), second.Snapshot().CanonicalText());
        Assert.NotEqual(first.Snapshot().Sha256(), second.Snapshot().Sha256());
    }

    [Fact]
    public void V04_checkpoint_round_trip_preserves_inquiry_washing_and_dining()
    {
        AssertInquiryRoundTrip();
        AssertWashingRoundTrip();
        AssertGrowthWashingRoundTrip();
        AssertDiningRoundTrip();
    }

    [Fact]
    public void V05_invalid_checkpoint_is_rejected_without_mutation()
    {
        var house = House();
        var kitchen = Kitchen();
        house.Step(kitchen, Template);
        var checkpoint = house.ExportCheckpoint(Template);
        var before = house.Snapshot().CanonicalText();
        var customer = Assert.Single(checkpoint.State.Customers);

        AssertRejected(checkpoint with
        {
            State = checkpoint.State with
            {
                Customers = new[] { customer, customer with { TableId = "table-2" } },
            },
        }, CookingFrontOfHouseRestoreReason.CustomerInvalid);
        AssertRejected(checkpoint with
        {
            State = checkpoint.State with
            {
                NextCustomerSequence = 2,
                Customers = new[]
                {
                    customer,
                    customer with { Id = new CookingCustomerId("customer-2"), ArrivalOrder = 2 },
                },
            },
        }, CookingFrontOfHouseRestoreReason.TableInvalid);
        AssertRejected(checkpoint with
        {
            State = checkpoint.State with
            {
                Customers = new[] { customer with { Order = new OrderId("customer-99-order") } },
            },
        }, CookingFrontOfHouseRestoreReason.OrderInvalid);
        AssertRejected(checkpoint with
        {
            State = checkpoint.State with
            {
                Customers = new[] { customer with { Phase = CookingTablePhase.InquiryInProgress } },
                Companion = checkpoint.State.Companion with
                {
                    Work = CookingCompanionWorkKind.Inquiring,
                    TargetCustomer = new CookingCustomerId("customer-99"),
                    TargetTable = customer.TableId,
                    ElapsedTicks = 1,
                    RequiredTicks = checkpoint.State.Schedule.InquiryTicks,
                },
            },
        }, CookingFrontOfHouseRestoreReason.CompanionInvalid);

        Dirty(kitchen);
        AssertRejected(checkpoint with
        {
            State = checkpoint.State with { WashQueue = new[] { Bowl, Bowl } },
        }, CookingFrontOfHouseRestoreReason.WashQueueInvalid);
        AssertRejected(checkpoint with
        {
            State = checkpoint.State with { ServiceTicks = checkpoint.State.Schedule.ServiceTicks + 1 },
        }, CookingFrontOfHouseRestoreReason.CounterInvalid);
        AssertRejected(checkpoint with
        {
            State = checkpoint.State with { UnsatisfiedOrders = new[] { new OrderId("poison-order") } },
        }, CookingFrontOfHouseRestoreReason.UnsatisfiedInvalid);

        void AssertRejected(CookingFrontOfHouseCheckpoint poisoned, CookingFrontOfHouseRestoreReason reason)
        {
            var rejected = house.RestoreCheckpoint(poisoned, kitchen);
            Assert.False(rejected.Accepted);
            Assert.Equal(reason, rejected.Reason);
            Assert.Equal(before, house.Snapshot().CanonicalText());
        }
    }

    [Fact]
    public void C01_three_completed_tasks_unlock_rounded_half_washing_for_new_work()
    {
        var house = new CookingFrontOfHouse(Schedule(tableCount: 3, serviceTicks: 20,
            arrivalIntervalTicks: 1, inquiryTicks: 2, washTicks: 5, waitLimitTicks: 50));
        var kitchen = Kitchen();
        var initial = house.Snapshot();
        Assert.Equal(0, initial.Companion.CompletedTaskCount);
        Assert.False(initial.Companion.WashSpeedUnlocked);

        CompleteInquiryTasks(house, kitchen, 2);
        var beforeUnlock = house.Snapshot();
        Assert.Equal(2, beforeUnlock.Companion.CompletedTaskCount);
        Assert.False(beforeUnlock.Companion.WashSpeedUnlocked);
        Assert.NotEqual(initial.Sha256(), beforeUnlock.Sha256());

        house.Step(kitchen, Template);
        Dirty(kitchen);
        house.Step(kitchen, Template);
        Assert.Equal(CookingCompanionWorkKind.Inquiring, house.Snapshot().Companion.Work);
        house.Step(kitchen, Template);
        var accelerated = house.Snapshot().Companion;
        Assert.Equal(3, accelerated.CompletedTaskCount);
        Assert.True(accelerated.WashSpeedUnlocked);
        Assert.Equal(CookingCompanionWorkKind.Washing, accelerated.Work);
        Assert.Equal(1, accelerated.ElapsedTicks);
        Assert.Equal(3, accelerated.RequiredTicks);
        house.Step(kitchen, Template);
        house.Step(kitchen, Template);
        Assert.DoesNotContain(Bowl, kitchen.DirtyBowlsAwaitingWash());
        Assert.Equal(4, house.Snapshot().Companion.CompletedTaskCount);

        var minimum = new CookingFrontOfHouse(Schedule(tableCount: 1, serviceTicks: 10,
            arrivalIntervalTicks: 10, inquiryTicks: 1, washTicks: 1, waitLimitTicks: 20,
            companionGrowthTaskThreshold: 1));
        var minimumKitchen = Kitchen();
        minimum.Step(minimumKitchen, Template);
        minimum.Step(minimumKitchen, Template);
        Assert.True(minimum.Snapshot().Companion.WashSpeedUnlocked);
        Dirty(minimumKitchen);
        minimum.Step(minimumKitchen, Template);
        Assert.DoesNotContain(Bowl, minimumKitchen.DirtyBowlsAwaitingWash());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void C00_zero_to_two_completed_tasks_keep_base_wash_duration(int completedTasks)
    {
        var house = new CookingFrontOfHouse(Schedule(tableCount: Math.Max(1, completedTasks), serviceTicks: 20,
            arrivalIntervalTicks: 1, inquiryTicks: 20, washTicks: 5, waitLimitTicks: 50));
        var kitchen = Kitchen();
        if (completedTasks > 0)
            CompleteInquiryTasks(house, kitchen, completedTasks);

        Assert.Equal(completedTasks, house.Snapshot().Companion.CompletedTaskCount);
        Dirty(kitchen);
        house.Step(kitchen, Template);
        Assert.Equal(5, house.Snapshot().Companion.RequiredTicks);
        Assert.False(house.Snapshot().Companion.WashSpeedUnlocked);
    }

    [Fact]
    public void C02_only_successful_inquiries_and_washes_increment_growth_once()
    {
        var cancelled = new CookingFrontOfHouse(Schedule(tableCount: 1, serviceTicks: 10,
            arrivalIntervalTicks: 10, inquiryTicks: 5, washTicks: 2, waitLimitTicks: 2));
        var cancelledKitchen = Kitchen();
        cancelled.Step(cancelledKitchen, Template);
        cancelled.Step(cancelledKitchen, Template);
        cancelled.Step(cancelledKitchen, Template);
        Assert.Equal(0, cancelled.Snapshot().Companion.CompletedTaskCount);

        var failedWash = new CookingFrontOfHouse(Schedule(serviceTicks: 1, inquiryTicks: 20, washTicks: 2));
        var failedWashKitchen = Kitchen();
        Dirty(failedWashKitchen);
        failedWash.Step(failedWashKitchen, Template);
        Assert.True(failedWashKitchen.CompleteWash(Bowl).Accepted);
        failedWash.Step(failedWashKitchen, Template);
        Assert.Equal(0, failedWash.Snapshot().Companion.CompletedTaskCount);

        var successfulInquiry = new CookingFrontOfHouse(Schedule(tableCount: 1, serviceTicks: 10,
            arrivalIntervalTicks: 10, inquiryTicks: 1, waitLimitTicks: 20));
        var inquiryKitchen = Kitchen();
        successfulInquiry.Step(inquiryKitchen, Template);
        successfulInquiry.Step(inquiryKitchen, Template);
        Assert.Equal(1, successfulInquiry.Snapshot().Companion.CompletedTaskCount);
        successfulInquiry.Step(inquiryKitchen, Template);
        Assert.Equal(1, successfulInquiry.Snapshot().Companion.CompletedTaskCount);

        var successfulWash = new CookingFrontOfHouse(Schedule(serviceTicks: 1, inquiryTicks: 20, washTicks: 1));
        var washKitchen = Kitchen();
        Dirty(washKitchen);
        successfulWash.Step(washKitchen, Template);
        Assert.Equal(1, successfulWash.Snapshot().Companion.CompletedTaskCount);
        successfulWash.Step(washKitchen, Template);
        Assert.Equal(1, successfulWash.Snapshot().Companion.CompletedTaskCount);
    }

    [Fact]
    public void C03_growth_checkpoint_round_trips_and_rejects_poison_without_mutation()
    {
        var schedule = Schedule(tableCount: 3, serviceTicks: 20, arrivalIntervalTicks: 1,
            inquiryTicks: 20, washTicks: 5, waitLimitTicks: 50);
        var house = new CookingFrontOfHouse(schedule);
        var kitchen = Kitchen();
        CompleteInquiryTasks(house, kitchen, 3);
        Dirty(kitchen);
        house.Step(kitchen, Template);
        var checkpoint = house.ExportCheckpoint(Template);
        Assert.Equal(3, checkpoint.State.Companion.CompletedTaskCount);
        Assert.True(checkpoint.State.Companion.WashSpeedUnlocked);
        Assert.Equal(3, checkpoint.State.Companion.RequiredTicks);

        var restored = CookingFrontOfHouse.Restore(checkpoint, kitchen);
        Assert.True(restored.Accepted);
        Assert.Equal(house.Snapshot().CanonicalText(), restored.FrontOfHouse!.Snapshot().CanonicalText());

        var before = house.Snapshot().CanonicalText();
        AssertRejected(checkpoint with
        {
            State = checkpoint.State with
            {
                Companion = checkpoint.State.Companion with
                {
                    CompletedTaskCount = -1,
                    WashSpeedUnlocked = false,
                },
            },
        });
        AssertRejected(checkpoint with
        {
            State = checkpoint.State with
            {
                Companion = checkpoint.State.Companion with { WashSpeedUnlocked = false },
            },
        });
        AssertRejected(checkpoint with
        {
            State = checkpoint.State with
            {
                Companion = checkpoint.State.Companion with { RequiredTicks = 99 },
            },
        });

        void AssertRejected(CookingFrontOfHouseCheckpoint poisoned)
        {
            var result = house.RestoreCheckpoint(poisoned, kitchen);
            Assert.False(result.Accepted);
            Assert.Equal(CookingFrontOfHouseRestoreReason.CompanionInvalid, result.Reason);
            Assert.Equal(before, house.Snapshot().CanonicalText());
        }
    }

    [Fact]
    public void C04_failure_retry_and_next_level_reset_growth()
    {
        var failed = new CookingFrontOfHouse(Schedule(tableCount: 3, serviceTicks: 20,
            arrivalIntervalTicks: 1, inquiryTicks: 20, waitLimitTicks: 50));
        var failedKitchen = Kitchen();
        CompleteInquiryTasks(failed, failedKitchen, 3);
        Assert.True(failed.Snapshot().Companion.WashSpeedUnlocked);
        failed.DropFailedScene();
        Assert.Equal(0, failed.Snapshot().Companion.CompletedTaskCount);
        Assert.False(failed.Snapshot().Companion.WashSpeedUnlocked);

        var next = new CookingFrontOfHouse(Schedule(tableCount: 3, serviceTicks: 20,
            arrivalIntervalTicks: 1, inquiryTicks: 20, waitLimitTicks: 50));
        var nextKitchen = Kitchen();
        CompleteInquiryTasks(next, nextKitchen, 3);
        next.ResetForNextLevel(nextKitchen, Template);
        Assert.Equal(0, next.Snapshot().Companion.CompletedTaskCount);
        Assert.False(next.Snapshot().Companion.WashSpeedUnlocked);
    }

    private static void AssertInquiryRoundTrip()
    {
        var source = new CookingFrontOfHouse(Schedule(tableCount: 1, inquiryTicks: 3));
        var sourceKitchen = Kitchen();
        source.Step(sourceKitchen, Template);
        source.Step(sourceKitchen, Template);
        var checkpoint = source.ExportCheckpoint(Template);

        var restoredKitchen = Kitchen();
        var restored = CookingFrontOfHouse.Restore(checkpoint, restoredKitchen);
        Assert.True(restored.Accepted);
        Assert.Equal(source.Snapshot().CanonicalText(), restored.FrontOfHouse!.Snapshot().CanonicalText());

        source.Step(sourceKitchen, Template);
        restored.FrontOfHouse.Step(restoredKitchen, Template);
        Assert.Equal(source.Snapshot().CanonicalText(), restored.FrontOfHouse.Snapshot().CanonicalText());
    }

    private static void AssertWashingRoundTrip()
    {
        var schedule = Schedule(tableCount: 1, serviceTicks: 10, arrivalIntervalTicks: 10, washTicks: 3);
        var source = new CookingFrontOfHouse(schedule);
        var sourceKitchen = Kitchen();
        Dirty(sourceKitchen);
        source.Step(sourceKitchen, Template);
        Assert.Equal(CookingCompanionWorkKind.Washing, source.Snapshot().Companion.Work);
        var checkpoint = source.ExportCheckpoint(Template);

        var restoredKitchen = Kitchen();
        Dirty(restoredKitchen);
        var restored = CookingFrontOfHouse.Restore(checkpoint, restoredKitchen);
        Assert.True(restored.Accepted);
        Assert.Equal(source.Snapshot().CanonicalText(), restored.FrontOfHouse!.Snapshot().CanonicalText());

        source.Step(sourceKitchen, Template);
        restored.FrontOfHouse.Step(restoredKitchen, Template);
        Assert.Equal(source.Snapshot().CanonicalText(), restored.FrontOfHouse.Snapshot().CanonicalText());
    }

    private static void AssertGrowthWashingRoundTrip()
    {
        var schedule = Schedule(tableCount: 1, serviceTicks: 10, arrivalIntervalTicks: 10,
            inquiryTicks: 1, washTicks: 5, waitLimitTicks: 20, companionGrowthTaskThreshold: 1);
        var source = new CookingFrontOfHouse(schedule);
        var sourceKitchen = Kitchen();
        source.Step(sourceKitchen, Template);
        source.Step(sourceKitchen, Template);
        Dirty(sourceKitchen);
        source.Step(sourceKitchen, Template);
        var checkpoint = source.ExportCheckpoint(Template);
        Assert.Equal(1, checkpoint.State.Companion.CompletedTaskCount);
        Assert.True(checkpoint.State.Companion.WashSpeedUnlocked);
        Assert.Equal(3, checkpoint.State.Companion.RequiredTicks);

        var restoredKitchen = Kitchen();
        Assert.True(restoredKitchen.OpenOrder(new OrderId("customer-1-order"), Template).Accepted);
        Dirty(restoredKitchen);
        var restored = CookingFrontOfHouse.Restore(checkpoint, restoredKitchen);
        Assert.True(restored.Accepted);
        Assert.Equal(source.Snapshot().CanonicalText(), restored.FrontOfHouse!.Snapshot().CanonicalText());

        source.Step(sourceKitchen, Template);
        restored.FrontOfHouse.Step(restoredKitchen, Template);
        Assert.Equal(source.Snapshot().CanonicalText(), restored.FrontOfHouse.Snapshot().CanonicalText());
    }

    private static void AssertDiningRoundTrip()
    {
        var schedule = Schedule(tableCount: 1, serviceTicks: 10, arrivalIntervalTicks: 10,
            inquiryTicks: 1, diningTicks: 3);
        var source = new CookingFrontOfHouse(schedule);
        var sourceKitchen = Kitchen();
        source.Step(sourceKitchen, Template);
        source.Step(sourceKitchen, Template);
        var order = new OrderId("customer-1-order");
        sourceKitchen.MarkOrderCompletedForTest(order);
        source.Step(sourceKitchen, Template);
        Assert.Equal(CookingTablePhase.Dining, Assert.Single(source.Snapshot().Customers).Phase);
        var checkpoint = source.ExportCheckpoint(Template);

        var restoredKitchen = Kitchen();
        Assert.True(restoredKitchen.OpenOrder(order, Template).Accepted);
        restoredKitchen.MarkOrderCompletedForTest(order);
        var restored = CookingFrontOfHouse.Restore(checkpoint, restoredKitchen);
        Assert.True(restored.Accepted);
        Assert.Equal(source.Snapshot().CanonicalText(), restored.FrontOfHouse!.Snapshot().CanonicalText());

        source.Step(sourceKitchen, Template);
        restored.FrontOfHouse.Step(restoredKitchen, Template);
        Assert.Equal(source.Snapshot().CanonicalText(), restored.FrontOfHouse.Snapshot().CanonicalText());
    }

    private static CookingFrontOfHouse House(int waitLimitTicks = 9) =>
        new(Schedule(waitLimitTicks: waitLimitTicks));

    private static CookingFrontOfHouseSchedule Schedule(int tableCount = 2, int serviceTicks = 8,
        int arrivalIntervalTicks = 2, int inquiryTicks = 2, int washTicks = 2, int diningTicks = 1,
        int waitLimitTicks = 9, int companionGrowthTaskThreshold = 3) =>
        new(tableCount, serviceTicks, arrivalIntervalTicks, inquiryTicks, washTicks, diningTicks, waitLimitTicks,
            companionGrowthTaskThreshold);

    private static CookingRecipeSimulation Kitchen()
    {
        var content = CookingContentCatalog.Load(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, CookingContentCatalog.ContentFileName)));
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [Player] = new(Player, new HashSet<string> { "carry" }, new HashSet<string> { "counter-a", "stove-a" }),
        };
        var fixture = CookingContentCatalog.BuildFixture(content, new CookingScope(Session, World, Match), players);
        var simulation = new CookingRecipeSimulation(fixture);
        CookingContentCatalog.ApplyStandardInitialSupply(simulation, content);
        return simulation;
    }

    private static void Dirty(CookingRecipeSimulation kitchen) => kitchen.MarkBowlDirtyForTest(Bowl);

    private static void CompleteInquiryTasks(CookingFrontOfHouse house, CookingRecipeSimulation kitchen, int count)
    {
        for (var task = 0; task < count; task++)
            house.Step(kitchen, Template);
        house.FinishInProgress(kitchen, Template);
    }

    private static string Status(CookingRecipeSimulation kitchen, OrderId order) =>
        kitchen.Orders.Single(candidate => candidate.Id == order).Status;
}
