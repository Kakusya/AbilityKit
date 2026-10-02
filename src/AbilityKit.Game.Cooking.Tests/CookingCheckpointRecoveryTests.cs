using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

/// <summary>
/// 任务 <c>09-22-cooking-checkpoint-recovery</c>：领域侧恢复 checkpoint 契约。
/// 与同步快照（<c>CookingRecipeSimulation.Snapshot</c>，过滤墓碑、不含去重账本/历史/计数器）分工不同：
/// checkpoint 覆盖继续运行所需的全部权威状态，可序列化脱离宿主自包含存在，并能在 fresh 仿真上整册恢复。
/// </summary>
[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingCheckpointRecoveryTests
{
    [Fact]
    public void Menu_policy_identity_is_required_nullable_integrity_data_and_format7_is_rejected()
    {
        var checkpoint = Wrap(CreateFixture().Simulation.ExportCheckpoint());
        var encoded = CookingLevelCheckpointCodec.Serialize(CookingLevelCheckpointCodec.CreateEnvelope(checkpoint));
        var legacy = CookingLevelCheckpointCodec.Deserialize(encoded);
        Assert.True(legacy.Accepted);
        Assert.Null(legacy.Checkpoint!.MenuConfigurationIdentity);
        var json = System.Text.Json.Nodes.JsonNode.Parse(encoded)!;
        Assert.True(json["checkpoint"]!.AsObject().Remove("menuConfigurationIdentity"));
        Assert.Equal(CookingCheckpointReadReason.RecordTruncated,
            CookingLevelCheckpointCodec.Deserialize(json.ToJsonString()).Reason);
        Assert.Equal(CookingCheckpointReadReason.UnknownFormatVersion,
            CookingLevelCheckpointCodec.Deserialize(encoded.Replace("\"formatVersion\":8", "\"formatVersion\":7")).Reason);
        var configured = checkpoint with { MenuConfigurationIdentity = "trusted-menu-policy" };
        Assert.NotEqual(checkpoint.Sha256(), configured.Sha256());
        var configuredText = CookingLevelCheckpointCodec.Serialize(CookingLevelCheckpointCodec.CreateEnvelope(configured));
        Assert.Equal("trusted-menu-policy", CookingLevelCheckpointCodec.Deserialize(configuredText).Checkpoint!.MenuConfigurationIdentity);
        Assert.Equal(CookingCheckpointReadReason.IntegrityFailure,
            CookingLevelCheckpointCodec.Deserialize(configuredText.Replace("trusted-menu-policy", "other-policy")).Reason);
    }

    [Theory]
    [InlineData("geometrySeedPoses")]
    [InlineData("floors")]
    [InlineData("equipment")]
    [InlineData("targets")]
    public void Malformed_installed_layout_entry_is_rejected_without_throwing_from_integrity_validation(string field)
    {
        var checkpoint = Wrap(CreateFixture().Simulation.ExportCheckpoint()) with
        {
            InstalledLayout = new(new(new("layout"), new[] { new CookingFloorRegion("floor", 0, 0, 2, 2) },
                Array.Empty<CookingEquipmentPlacement>(), Array.Empty<CookingLayoutCell>(), Array.Empty<CookingLayoutTarget>()),
                new[] { new CookingPlayerPose(new("chef-a"), 500, 500, 1, 0) })
        };
        var serialized = CookingLevelCheckpointCodec.Serialize(CookingLevelCheckpointCodec.CreateEnvelope(checkpoint));
        var json = System.Text.Json.Nodes.JsonNode.Parse(serialized)!;
        var installed = json["checkpoint"]!["installedLayout"]!;
        var target = field == "geometrySeedPoses" ? installed : installed["layout"]!;
        target[field] = new System.Text.Json.Nodes.JsonArray((System.Text.Json.Nodes.JsonNode?)null);
        Assert.Equal(CookingCheckpointReadReason.RecordTruncated, CookingLevelCheckpointCodec.Deserialize(json.ToJsonString()).Reason);
    }

    private static readonly SessionId Session = new("checkpoint-session");
    private static readonly WorldId World = new("checkpoint-world");
    private static readonly MatchId Match = new("checkpoint-match");
    private static readonly PlayerId Player = new("chef-a");
    private static readonly StationSlotId Board = new("board-a");
    private static readonly StationSlotId Stove = new("stove-a");
    private static readonly StationSlotId Oven = new("oven-a");
    private static readonly StationSlotId Counter = new("counter-a");
    private static readonly DefinitionId ChoppedTomato = new("chopped-tomato");
    private static readonly DefinitionId BeatenEgg = new("beaten-egg");
    private static readonly DefinitionId Soup = new("tomato-egg-soup");
    private static readonly DefinitionId ToastedBread = new("toasted-bread");
    private static readonly DefinitionId BowlDefinition = new("bowl");
    private static readonly DefinitionId PotDefinition = new("pot");
    private static readonly RecipeId ChopRecipe = new("chop-tomato");
    private static readonly RecipeId BeatRecipe = new("beat-egg");
    private static readonly RecipeId SoupRecipe = new("tomato-egg-soup");
    private static readonly RecipeId BakeRecipe = new("bake-bread");
    private static readonly OrderTemplateId SoupOrderTemplate = new("tomato-egg-soup-order");
    private static readonly OrderId SoupOrder = new("order-soup-1");
    private static readonly ItemId Pot = new("pot-1");
    private static readonly ItemId Bowl = new("pool-bowl-1");
    private static readonly ItemId Tomato = new("tomato-1");
    private static readonly ItemId Egg = new("egg-1");
    private static readonly ItemId BreadSlice = new("bread-slice-2");
    private const int ChopTicks = 2;
    private const int BeatTicks = 2;
    private const int SoupTicks = 6;
    private const int SoupTicksBeforeCheckpoint = 3;
    private const int BakeTicks = 2;
    private const string Runner = "dotnet test AbilityKit.Game.Cooking.Tests";

    [Fact]
    public void C01_export_covers_every_checkpoint_field_mid_cooking()
    {
        var fixture = CreateFixture();
        var loop = RunToSoupCooking(fixture, null, null, SoupTicksBeforeCheckpoint);
        var checkpoint = fixture.Simulation.ExportCheckpoint();

        // 身份与水位：14 条命令 + 7 个 fixed tick。
        Assert.Equal(new CookingScope(Session, World, Match), checkpoint.Scope);
        Assert.Equal(7, checkpoint.LogicalTick);
        Assert.Equal(21, checkpoint.StateVersion);
        Assert.Equal(21, checkpoint.EventSequence);

        // 物品/tombstone：7 在册 + 2 墓碑（切番茄消耗的 tomato-1、打蛋消耗的 egg-1）。
        Assert.Equal(13, checkpoint.Items.Count);
        Assert.True(Assert.Single(checkpoint.Items, item => item.Id == Tomato).Removed);
        Assert.True(Assert.Single(checkpoint.Items, item => item.Id == Egg).Removed);

        // 加工：活动中的煮制进程（elapsed 3/6、RetainInputs、容器锚定、锁输入含锚点与内容物）。
        var process = Assert.Single(checkpoint.Processes);
        Assert.Equal(new ProcessId("process-3"), process.Id);
        Assert.Equal(SoupRecipe, process.Recipe);
        Assert.Equal(Player, process.Player);
        Assert.Equal(Pot, process.Anchor);
        Assert.Equal(Stove, process.Station);
        Assert.Equal(SoupTicksBeforeCheckpoint, process.ElapsedTicks);
        Assert.Equal(SoupTicks, process.RequiredTicks);
        Assert.Equal(CookingRecipeCompletionKind.RetainInputs, process.Completion);
        Assert.Equal(Pot, process.Container);
        Assert.Equal(new[] { Pot, new ItemId("product-1"), new ItemId("product-2") }, process.LockedInputs);

        // 容器：有序内容物（番茄块、蛋液按入锅顺序）+ 两只空碗 + 两只空盘。
        Assert.Equal(5, checkpoint.Containers.Count);
        Assert.Equal(new[] { new ItemId("product-1"), new ItemId("product-2") },
            Assert.Single(checkpoint.Containers, container => container.Id == Pot).ItemIds);
        Assert.Empty(Assert.Single(checkpoint.Containers, container => container.Id == Bowl).ItemIds);

        // 订单与结算：尚未开单、零结算。
        Assert.Empty(checkpoint.Orders);
        Assert.Empty(checkpoint.Settlements);

        // 消耗产物账与干净池：无消耗；在册干净碗仍为 2——一只已上台面备用、一只池中，
        // 取出干净碗不改变在册计数（该计数与“在册干净容器”不是同一集合，必须显式入账）。
        Assert.Empty(checkpoint.ConsumedProducts);
        Assert.Equal(2, checkpoint.CleanContainerCounts.Count);
        Assert.Equal(2, Assert.Single(checkpoint.CleanContainerCounts,
            pool => pool.Definition == BowlDefinition).Count);
        Assert.Equal(2, Assert.Single(checkpoint.CleanContainerCounts,
            pool => pool.Definition == new DefinitionId("plate")).Count);

        // 去重账本与事件/tick 历史：14 条命令逐条入账；tick 也各消耗一个事件序列号。
        Assert.Equal(14, checkpoint.Deduplication.Count);
        Assert.Equal(14, checkpoint.Events.Count);
        Assert.Equal(7, checkpoint.TickEvents.Count);
        Assert.Equal(14, loop.Commands.Count);

        // ID 计数器：两个已完成预处理（process-1/2、product-1/2），煮制进程占用 process-3。
        Assert.Equal(3, checkpoint.NextProcessId);
        Assert.Equal(2, checkpoint.NextProductId);
        Assert.Equal(0, checkpoint.NextSettlementSequence);
    }

    [Fact]
    public void C02_restored_simulation_reproduces_the_same_trajectory()
    {
        // 臂一：源仿真直接续跑；臂二：导出（经序列化信封往返）后整册恢复到 fresh 仿真再续跑。
        using var sourceEvidence = CreateEvidence("C02-source");
        using var restoredEvidence = CreateEvidence("C02-restored");
        var source = CreateFixture();
        var loop = RunToSoupCooking(source, sourceEvidence, "C02-source", SoupTicksBeforeCheckpoint);
        var checkpoint = source.Simulation.ExportCheckpoint();
        var exportedCanonical = source.Simulation.Snapshot().CanonicalText();
        var envelope = CookingLevelCheckpointCodec.CreateEnvelope(Wrap(checkpoint));
        var read = CookingLevelCheckpointCodec.Deserialize(CookingLevelCheckpointCodec.Serialize(envelope));
        Assert.True(read.Accepted);
        Assert.Equal(envelope.Checkpoint!.CanonicalText(), read.Checkpoint!.CanonicalText());

        var sourceFinal = FinishLoop(source, loop, "C02-source", sourceEvidence);

        var restoredFixture = CreateFixture();
        var restore = restoredFixture.Simulation.RestoreCheckpoint(read.Checkpoint!.Recipe);
        Assert.True(restore.Accepted);
        Assert.Equal(exportedCanonical, restoredFixture.Simulation.Snapshot().CanonicalText());
        var restoredFinal = FinishLoop(restoredFixture, loop, "C02-restored", restoredEvidence);

        Assert.Equal(sourceFinal.Canonical, restoredFinal.Canonical);
        Assert.Equal(sourceFinal.Hash, restoredFinal.Hash);
        Assert.Equal(sourceFinal.NextProductId, restoredFinal.NextProductId);
        Assert.Equal("product-4", sourceFinal.NextProductId.Value);
    }
    [Fact]
    public void C03_restore_rejects_foreign_or_corrupt_payloads_without_mutation()
    {
        var fixture = CreateFixture();
        RunToSoupCooking(fixture, null, null, SoupTicksBeforeCheckpoint);
        var checkpoint = fixture.Simulation.ExportCheckpoint();
        var simulation = fixture.Simulation;

        AssertRejected(simulation, checkpoint with
        {
            Scope = new CookingScope(Session, World, new MatchId("foreign-match")),
        }, CookingCheckpointRestoreReason.ScopeMismatch);

        AssertRejected(simulation, checkpoint with
        {
            Items = checkpoint.Items
                .Select(item => item.Id == Pot ? item with { Definition = new DefinitionId("no-such-pot") } : item)
                .ToArray(),
        }, CookingCheckpointRestoreReason.ItemDefinitionNotFound);

        AssertRejected(simulation, checkpoint with
        {
            Processes = checkpoint.Processes
                .Select(process => process with { LockedInputs = process.LockedInputs.Skip(1).ToArray() })
                .ToArray(),
        }, CookingCheckpointRestoreReason.ProcessInputUnavailable);

        AssertRejected(simulation, checkpoint with { NextSettlementSequence = 5 },
            CookingCheckpointRestoreReason.SettlementSequenceInvalid);

        AssertRejected(simulation, checkpoint with { LogicalTick = -1 },
            CookingCheckpointRestoreReason.CounterInvalid);

        // 容器内容顺序进 canonical：同槽位、列表顺序不同必须打出不同哈希。
        var pot = Assert.Single(checkpoint.Containers, container => container.Id == Pot);
        var reordered = checkpoint with
        {
            Containers = checkpoint.Containers
                .Select(container => container.Id == Pot
                    ? container with { ItemIds = container.ItemIds.Reverse().ToArray() }
                    : container)
                .ToArray(),
        };
        Assert.NotEqual(pot.ItemIds, pot.ItemIds.Reverse().ToArray());
        Assert.NotEqual(checkpoint.CanonicalText(), reordered.CanonicalText());
        Assert.NotEqual(checkpoint.Sha256(), reordered.Sha256());

        // 事件序号错位：命令事件与 tick 事件序号重复时结构化拒绝，且零变更。
        AssertRejected(simulation, checkpoint with
        {
            Events = checkpoint.Events
                .Select((entry, index) => index == 0 ? entry with { Sequence = checkpoint.TickEvents[0].Sequence } : entry)
                .ToArray(),
        }, CookingCheckpointRestoreReason.EventSequenceInvalid);

        // 去掉代际绑定：已经推进过的载荷不能再当成“尚未绑定”恢复。
        AssertRejected(simulation, checkpoint with { LevelScope = null },
            CookingCheckpointRestoreReason.EventSequenceInvalid);

        // 信封层：截断、格式版本篡改与载荷篡改分别结构化拒绝。
        var serialized = CookingLevelCheckpointCodec.Serialize(CookingLevelCheckpointCodec.CreateEnvelope(Wrap(checkpoint)));
        Assert.Equal(CookingCheckpointReadReason.RecordTruncated,
            CookingLevelCheckpointCodec.Deserialize(serialized[..(serialized.Length / 2)]).Reason);
        Assert.Equal(CookingCheckpointReadReason.UnknownFormatVersion,
            CookingLevelCheckpointCodec.Deserialize(serialized.Replace("\"formatVersion\":8", "\"formatVersion\":99",
                StringComparison.Ordinal)).Reason);
        Assert.Equal(CookingCheckpointReadReason.UnknownFormatVersion,
            CookingLevelCheckpointCodec.Deserialize(serialized.Replace("\"formatVersion\":8", "\"formatVersion\":5",
                StringComparison.Ordinal)).Reason);
        Assert.Equal(CookingCheckpointReadReason.IntegrityFailure,
            CookingLevelCheckpointCodec.Deserialize(serialized.Replace("\"stateVersion\":21", "\"stateVersion\":22",
                StringComparison.Ordinal)).Reason);
    }

    [Fact]
    public void C04_restored_simulation_deduplicates_replayed_commands()
    {
        var fixture = CreateFixture();
        var loop = RunToSoupCooking(fixture, null, null, SoupTicksBeforeCheckpoint);
        var checkpoint = fixture.Simulation.ExportCheckpoint();
        var soupStart = Assert.Single(loop.Commands, command => command.Command.Value == "soup-start");

        var restoredFixture = CreateFixture();
        Assert.True(restoredFixture.Simulation.RestoreCheckpoint(checkpoint).Accepted);
        var restored = restoredFixture.Simulation;

        // 同 identity 重放：返回缓存结果并标记重复，不二次变更、版本不回退。
        var original = Assert.Single(checkpoint.Deduplication, entry => entry.Command.Value == "soup-start");
        var replay = restored.Submit(soupStart);
        Assert.True(replay.IsDuplicate);
        Assert.Equal(CookingRecipeOutcome.Accepted, replay.Outcome);
        Assert.Equal(CookingRecipeRejectionReason.None, replay.Reason);
        Assert.Empty(replay.Events);
        Assert.Equal(original.StateVersion, replay.StateVersion);
        Assert.Equal(checkpoint.StateVersion, restored.Snapshot().Version);

        // 同 identity 不同内容：身份冲突，不拿缓存结果冒充。
        var conflict = restored.Submit(soupStart with { Operation = CookingRecipeOperation.Drop, Station = Counter });
        Assert.Equal(CookingRecipeOutcome.Rejected, conflict.Outcome);
        Assert.Equal(CookingRecipeRejectionReason.CommandIdentityConflict, conflict.Reason);
        Assert.Equal(checkpoint.StateVersion, restored.Snapshot().Version);
    }

    private static void AssertRejected(CookingRecipeSimulation simulation, CookingRecipeCheckpoint payload,
        CookingCheckpointRestoreReason reason)
    {
        var before = simulation.Snapshot().CanonicalText();
        var result = simulation.RestoreCheckpoint(payload);
        Assert.False(result.Accepted);
        Assert.Equal(reason, result.Reason);
        Assert.Equal(before, simulation.Snapshot().CanonicalText());
    }

    private static CookingLevelCheckpoint Wrap(CookingRecipeCheckpoint recipe) => new(
        new CookingLevelScope(new CookingScope(Session, World, Match), new RestaurantRuntimeId(1),
            new LevelId("loop-level"), 1),
        new CookingConfigurationIdentity("cooking-definition-v2", "checkpoint-test"),
        new CookingLevelPreparation(new LevelId("loop-level"), new MapId("map"),
            new CookingLogicalLayout(new LayoutId("layout"),
                new[] { Board, Stove, Oven, Counter }, new[] { BowlDefinition, PotDefinition }),
            new CookingConfigurationIdentity("cooking-definition-v2", "checkpoint-test")),
        CookingLevelState.Running,
        null,
        3,
        7,
        0,
        recipe);
    private sealed record LoopResult(List<CookingRecipeCommand> Commands);

    private sealed record FinalState(string Canonical, string Hash, ItemId NextProductId);

    /// <summary>
    /// 跑到“煮制进行中”：标准初始供应 → 碗上台面 → 切番茄（2 tick）→ 番茄块入锅 → 打蛋（2 tick）→
    /// 蛋液入锅 → 启动煮制并推进 <paramref name="soupTicks"/> 个 fixed tick。返回已执行命令。
    /// </summary>
    private static LoopResult RunToSoupCooking(Fixture fixture, EvidenceScope? evidence, string? testId, int soupTicks)
    {
        var simulation = fixture.Simulation;
        var commands = new List<CookingRecipeCommand>();
        var frame = 0;

        Submit(simulation, commands, evidence, testId, Command(CookingRecipeOperation.Pickup, "pickup-pool-bowl",
            item: Bowl, expectedVersion: VersionOf(simulation, Bowl)), "pick up a clean bowl from the pool");
        Submit(simulation, commands, evidence, testId, Command(CookingRecipeOperation.Drop, "drop-bowl",
            item: Bowl, station: Counter, expectedVersion: VersionOf(simulation, Bowl)),
            "put the bowl on the counter for reuse");

        Submit(simulation, commands, evidence, testId, Command(CookingRecipeOperation.Pickup, "pickup-tomato-1",
            item: Tomato, expectedVersion: VersionOf(simulation, Tomato)), "pick up the tomato from the pantry");
        Submit(simulation, commands, evidence, testId, Command(CookingRecipeOperation.Drop, "drop-tomato-1",
            item: Tomato, station: Board, expectedVersion: VersionOf(simulation, Tomato)),
            "drop the tomato onto the board");
        Submit(simulation, commands, evidence, testId, Command(CookingRecipeOperation.StartProcess, "start-chop",
            recipe: ChopRecipe, item: Tomato, station: Board, expectedVersion: VersionOf(simulation, Tomato)),
            "start chopping on the board");
        frame = AdvanceClock(simulation, frame, ChopTicks);
        var chopped = Assert.Single(simulation.Snapshot().Items, item => item.IsProduct);
        Assert.Equal(ChoppedTomato, chopped.Definition);

        Submit(simulation, commands, evidence, testId, Command(CookingRecipeOperation.Pickup, "pickup-chopped",
            item: chopped.Id, expectedVersion: chopped.Version), "pick up the chopped tomato");
        Submit(simulation, commands, evidence, testId, Command(CookingRecipeOperation.PutIn, "chopped-into-pot",
            item: chopped.Id, container: Pot, expectedVersion: VersionOf(simulation, chopped.Id)),
            "put the chopped tomato into the pot");

        Submit(simulation, commands, evidence, testId, Command(CookingRecipeOperation.Pickup, "pickup-egg",
            item: Egg, expectedVersion: VersionOf(simulation, Egg)), "pick up the egg from the pantry");
        Submit(simulation, commands, evidence, testId, Command(CookingRecipeOperation.PutIn, "egg-into-bowl",
            item: Egg, container: Bowl, expectedVersion: VersionOf(simulation, Egg)),
            "put the egg into the bowl");
        Submit(simulation, commands, evidence, testId, Command(CookingRecipeOperation.Pickup, "pickup-bowl",
            item: Bowl, expectedVersion: VersionOf(simulation, Bowl)), "pick up the bowl holding the egg");
        Submit(simulation, commands, evidence, testId, Command(CookingRecipeOperation.StartProcess, "beat-start",
            recipe: BeatRecipe, item: Bowl, expectedVersion: VersionOf(simulation, Bowl)),
            "beating eggs needs no station");
        frame = AdvanceClock(simulation, frame, BeatTicks);
        var liquid = Assert.Single(simulation.Snapshot().Items, item => item.Definition == BeatenEgg);
        Assert.Equal(ItemLocation.Container(Bowl, "slot-0"), liquid.Location);

        Submit(simulation, commands, evidence, testId, Command(CookingRecipeOperation.Pour, "pour-egg",
            item: Bowl, container: Pot, expectedVersion: VersionOf(simulation, Bowl)),
            "pour the egg liquid into the pot and free the bowl");
        Submit(simulation, commands, evidence, testId, Command(CookingRecipeOperation.Drop, "bowl-back",
            item: Bowl, station: Counter, expectedVersion: VersionOf(simulation, Bowl)),
            "put the emptied bowl back on the counter");

        Submit(simulation, commands, evidence, testId, Command(CookingRecipeOperation.StartProcess, "soup-start",
            recipe: SoupRecipe, item: Pot, station: Stove, expectedVersion: VersionOf(simulation, Pot)),
            "start the multi-input cooking");
        frame = AdvanceClock(simulation, frame, soupTicks);

        return new LoopResult(commands);
    }
    /// <summary>
    /// 续跑闭环剩余部分：煮满 6 tick → 端走锅 → 倒汤入碗 → 前厅开单（注入）→ 提交 → 洗碗回池（注入），
    /// 再以“烤面包片”探针验证下一产物 ID 的连续性。返回终态 canonical、哈希与探针产物 ID。
    /// </summary>
    private static FinalState FinishLoop(Fixture fixture, LoopResult loop, string testId, EvidenceScope? evidence)
    {
        var simulation = fixture.Simulation;
        var frame = ChopTicks + BeatTicks + SoupTicksBeforeCheckpoint;

        frame = AdvanceClock(simulation, frame, SoupTicks - SoupTicksBeforeCheckpoint);
        Assert.True(ItemState(simulation, Pot).ContainerCompleted);

        Submit(simulation, loop.Commands, evidence, testId, Command(CookingRecipeOperation.Pickup, "carry-pot",
            item: Pot, expectedVersion: VersionOf(simulation, Pot)),
            "carry the completed pot away from the stove");
        Submit(simulation, loop.Commands, evidence, testId, Command(CookingRecipeOperation.Pour, "pour-soup",
            item: Pot, container: Bowl, expectedVersion: VersionOf(simulation, Pot)),
            "pour the soup into the bowl");
        var soup = Assert.Single(simulation.Snapshot().Items, item => item.IsProduct);
        Assert.Equal(Soup, soup.Definition);
        Assert.Equal(ItemLocation.Container(Bowl, "slot-0"), soup.Location);

        Assert.True(simulation.OpenOrder(SoupOrder, SoupOrderTemplate).Accepted);
        Submit(simulation, loop.Commands, evidence, testId, Command(CookingRecipeOperation.SubmitOrder, "submit-soup",
            item: soup.Id, order: SoupOrder, expectedVersion: soup.Version),
            "submit the bowl of soup to the order");
        Assert.Single(simulation.SettlementHistory);
        Assert.True(simulation.CompleteWash(Bowl).Accepted);

        // 提交后玩家仍手持锅：先放回灶台，腾出手再做烤面包探针。
        Submit(simulation, loop.Commands, evidence, testId, Command(CookingRecipeOperation.Drop, "drop-pot",
            item: Pot, station: Stove, expectedVersion: VersionOf(simulation, Pot)),
            "put the emptied pot back on the stove");

        // 探针：正式内容的烤面包配方（单输入消耗生成），两臂必须分配到同一下一产物 ID。
        Submit(simulation, loop.Commands, evidence, testId, Command(CookingRecipeOperation.Pickup, "pickup-slice",
            item: BreadSlice, expectedVersion: VersionOf(simulation, BreadSlice)),
            "pick up the bread slice from the standard supply");
        Submit(simulation, loop.Commands, evidence, testId, Command(CookingRecipeOperation.Drop, "drop-slice",
            item: BreadSlice, station: Oven, expectedVersion: VersionOf(simulation, BreadSlice)),
            "drop the bread slice onto the oven");
        Submit(simulation, loop.Commands, evidence, testId, Command(CookingRecipeOperation.StartProcess, "bake-start",
            recipe: BakeRecipe, item: BreadSlice, station: Oven, expectedVersion: VersionOf(simulation, BreadSlice)),
            "the oven bakes with a single input");
        frame = AdvanceClock(simulation, frame, BakeTicks);
        var bread = Assert.Single(simulation.Snapshot().Items, item => item.Definition == ToastedBread);
        Assert.Equal(ItemLocation.Station(Oven), bread.Location);

        return new FinalState(simulation.Snapshot().CanonicalText(), simulation.Snapshot().Sha256(), bread.Id);
    }

    private static int AdvanceClock(CookingRecipeSimulation simulation, int frame, int ticks)
    {
        for (var tick = 1; tick <= ticks; tick++)
        {
            var before = simulation.LogicalTick;
            var result = simulation.AdvanceFixedTick(LevelScope(), frame + tick);
            Assert.Equal(before + 1, result.AfterLogicalTick);
        }

        return frame + ticks;
    }

    private static int VersionOf(CookingRecipeSimulation simulation, ItemId item) =>
        ItemState(simulation, item).Version;

    private static CookingRecipeSnapshotItem ItemState(CookingRecipeSimulation simulation, ItemId id) =>
        simulation.Snapshot().Items.Single(item => item.Id == id);

    private static CookingLevelScope LevelScope() => new(
        new CookingScope(Session, World, Match), new RestaurantRuntimeId(1), new LevelId("loop-level"), 1);
    private static CookingRecipeCommand Command(CookingRecipeOperation operation, string commandId, RecipeId? recipe = null,
        ProcessId? process = null, ItemId? item = null, StationSlotId? station = null, ItemId? container = null,
        OrderId? order = null, int expectedVersion = 0, int ticks = 0) =>
        new(new CookingScope(Session, World, Match), 10, Player, new RecipeCommandId(commandId), operation,
            recipe, process, item, station, container, order, expectedVersion, ticks);

    private static CookingRecipeCommandResult Submit(CookingRecipeSimulation simulation,
        List<CookingRecipeCommand>? commands, EvidenceScope? evidence, string? testId,
        CookingRecipeCommand command, string assertionSummary)
    {
        commands?.Add(command);
        var before = simulation.Snapshot();
        var result = simulation.Submit(command);
        Assert.Equal(CookingRecipeOutcome.Accepted, result.Outcome);
        Assert.Equal(CookingRecipeRejectionReason.None, result.Reason);
        Assert.Single(result.Events);
        if (evidence is not null)
        {
            CookingRecipeAcceptanceEvidenceWriter.Append(evidence.Path, new CookingRecipeAcceptanceEvidence(
                testId!, "domain-checkpoint-recovery", command, simulation.LogicalTick, result.Outcome.ToString(),
                result.Reason.ToString(), result.IsDuplicate, result.Events, before.Sha256(),
                simulation.Snapshot().Sha256(), assertionSummary, Runner, DateTimeOffset.UtcNow.ToString("O")));
        }

        if (result.Outcome == CookingRecipeOutcome.Rejected)
            Assert.Equal(before.CanonicalText(), simulation.Snapshot().CanonicalText());
        return result;
    }

    private static Fixture CreateFixture()
    {
        var scope = new CookingScope(Session, World, Match);
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [Player] = new(Player, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new HashSet<string>(StringComparer.Ordinal) { Board.Value, Stove.Value, Oven.Value, Counter.Value }),
        };
        var content = CookingContentCatalog.Load(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, CookingContentCatalog.ContentFileName)));
        var washPort = new RecordingWashPort();
        var simulation = new CookingRecipeSimulation(
            CookingContentCatalog.BuildFixture(content, scope, players, "clean-pool"), null, washPort);
        CookingContentCatalog.ApplyStandardInitialSupply(simulation, content);
        return new Fixture(simulation, washPort);
    }

    private sealed class Fixture
    {
        public Fixture(CookingRecipeSimulation simulation, RecordingWashPort washPort)
        {
            Simulation = simulation;
            WashPort = washPort;
        }

        public CookingRecipeSimulation Simulation { get; }
        public RecordingWashPort WashPort { get; }
    }

    private sealed class RecordingWashPort : ICookingBowlWashingPort
    {
        public List<ItemId> Requests { get; } = new();

        public void RequestWash(ItemId bowl, DefinitionId definition) => Requests.Add(bowl);
    }

    private static EvidenceScope CreateEvidence(string testId) => new(testId);

    private sealed class EvidenceScope : IDisposable
    {
        private readonly string _directory;
        private readonly bool _keepArtifacts;

        public EvidenceScope(string testId)
        {
            var requestedRoot = Environment.GetEnvironmentVariable("COOKING_RECIPE_EVIDENCE_DIRECTORY");
            _keepArtifacts = !string.IsNullOrWhiteSpace(requestedRoot);
            var root = _keepArtifacts
                ? System.IO.Path.GetFullPath(requestedRoot!)
                : System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AbilityKit.Game.Cooking.Tests", "checkpoint");
            _directory = System.IO.Path.Combine(root, testId, Guid.NewGuid().ToString("N"));
            Path = System.IO.Path.Combine(_directory, "checkpoint-acceptance.jsonl");
        }

        public string Path { get; }

        public void Dispose()
        {
            if (!_keepArtifacts && Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }
    }
}
