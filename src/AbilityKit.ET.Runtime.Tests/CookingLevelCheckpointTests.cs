using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

/// <summary>
/// 任务 <c>09-22-cooking-checkpoint-recovery</c>：ET fixed-tick Level 宿主边界的 checkpoint 契约。
/// 与同步快照（<c>CookingLevelLifecycleSnapshot</c>）分工不同：checkpoint 携带一代 Level 的
/// scope/epoch、preparation、lifecycle 水位、HostFrameSequence、命令水位与整册仿真载荷，
/// 证明“导出 → 销毁 host → 重建 → 继续运行”与不中断基线不可区分。
/// </summary>
[Trait("Gate", "CookingLevelRuntime")]
public sealed class CookingLevelCheckpointTests
{
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
    private const int PartialLoopCommandCount = 14;
    private const int FinishCommandCount = 7;
    // 两臂各执行同 21 条命令：恢复臂的 evidence 覆盖导出前 14 条与重建后续跑 7 条。
    private const int BaselineCommandCount = PartialLoopCommandCount + FinishCommandCount;
    private const int RecoveryCommandCount = BaselineCommandCount;
    private const long ReplayBatch = 100;
    private const string Runner = "dotnet test AbilityKit.ET.Runtime.Tests";

    [Fact]
    public void R01_recovered_host_continues_the_loop_like_the_uninterrupted_baseline()
    {
        var content = LoadContent();

        // 基线臂：同一条番茄蛋花汤闭环不中断跑完。ET 宿主是进程级单例，两臂顺序执行。
        FinalState baselineFinal;
        string baselineCheckpointCanonical;
        ReplayProbe baselineReplay;
        long baselineSettlements;
        {
            using var baselineEvidence = CreateEvidence("R01-baseline");
            using var baseline = CreateFixture(content);
            using var baselineHost = baseline.CreateStartedHost(state =>
                CookingContentCatalog.ApplyStandardInitialSupply(state, content));
            var baselineCommands = RunToSoupCooking(baseline, baselineEvidence, "R01-baseline");
            baselineFinal = FinishLoop(baseline, baselineCommands, baselineEvidence, "R01-baseline");
            var baselineCheckpoint = baseline.Host.ExportCheckpoint();
            Assert.True(baselineCheckpoint.Accepted);
            baselineCheckpointCanonical = baselineCheckpoint.Checkpoint!.CanonicalText();
            var soupStart = Assert.Single(baselineCommands, command => command.Command.Value == "soup-start");
            baselineReplay = ProbeReplay(baseline, soupStart, expectTerminalDuplicate: true);
            baselineSettlements = baseline.Simulation.SettlementHistory.Count;
            Assert.Equal(baselineFinal.StateVersion, baseline.Simulation.Snapshot().Version);
            AssertEvidence(baselineEvidence.Path, "R01-baseline", BaselineCommandCount);
        }

        // 恢复臂：同一序列，在煮制进行中导出（经序列化信封往返）→ 销毁宿主 → 按 checkpoint 重建 → 继续。
        FinalState recoveredFinal;
        string recoveredCheckpointCanonical;
        ReplayProbe recoveredReplay;
        long recoveredSettlements;
        {
            using var recoveryEvidence = CreateEvidence("R01-recovery");
            using var recovery = CreateFixture(content);
            using var recoveryHost = recovery.CreateStartedHost(state =>
                CookingContentCatalog.ApplyStandardInitialSupply(state, content));
            var recoveryCommands = RunToSoupCooking(recovery, recoveryEvidence, "R01-recovery");

            var exported = recovery.Host.ExportCheckpoint();
            Assert.True(exported.Accepted);
            var exportedCheckpoint = exported.Checkpoint!;
            // 宿主每帧推进一个 fixed tick：14 个命令帧 + 7 个纯时钟帧 = LogicalTick/HostFrameSequence 均为 21。
            var expectedTick = PartialLoopCommandCount + ChopTicks + BeatTicks + SoupTicksBeforeCheckpoint;
            Assert.Equal(expectedTick, exportedCheckpoint.Recipe.LogicalTick);
            Assert.Equal(expectedTick, exportedCheckpoint.HostFrameSequence);
            Assert.Equal(PartialLoopCommandCount, exportedCheckpoint.LastCommittedSimulationBatch);

            var envelope = CookingLevelCheckpointCodec.CreateEnvelope(exportedCheckpoint);
            var read = CookingLevelCheckpointCodec.Deserialize(CookingLevelCheckpointCodec.Serialize(envelope));
            Assert.True(read.Accepted);
            var checkpoint = read.Checkpoint!;
            Assert.Equal(envelope.Checkpoint!.CanonicalText(), checkpoint.CanonicalText());

            recoveryHost.Dispose();

            using var recovered = CreateFixture(content, checkpoint.LastCommittedSimulationBatch);
            var restored = CookingLevelEtHost.Restore(checkpoint, content.Snapshot, recovered.Factory);
            Assert.True(restored.Accepted);
            using var restoredHost = restored.Host!;
            recovered.AdoptHost(restoredHost);
            recoveredFinal = FinishLoop(recovered, recoveryCommands, recoveryEvidence, "R01-recovery");
            var recoveredCheckpoint = recovered.Host.ExportCheckpoint();
            Assert.True(recoveredCheckpoint.Accepted);
            recoveredCheckpointCanonical = recoveredCheckpoint.Checkpoint!.CanonicalText();
            var soupStart = Assert.Single(recoveryCommands, command => command.Command.Value == "soup-start");
            recoveredReplay = ProbeReplay(recovered, soupStart, expectTerminalDuplicate: false);
            recoveredSettlements = recovered.Simulation.SettlementHistory.Count;
            Assert.Equal(recoveredFinal.StateVersion, recovered.Simulation.Snapshot().Version);
            AssertEvidence(recoveryEvidence.Path, "R01-recovery", RecoveryCommandCount);
        }

        // 终态等价：最终 hash、state version、logical tick 一致。
        Assert.Equal(baselineFinal.Hash, recoveredFinal.Hash);
        Assert.Equal(baselineFinal.Canonical, recoveredFinal.Canonical);
        Assert.Equal(baselineFinal.StateVersion, recoveredFinal.StateVersion);
        Assert.Equal(baselineFinal.LogicalTick, recoveredFinal.LogicalTick);
        // ID 连续性：烤面包探针在两臂分配到同下一产物 ID。
        Assert.Equal(baselineFinal.NextProductId, recoveredFinal.NextProductId);
        Assert.Equal("product-4", baselineFinal.NextProductId.Value);
        // 复合证明：两份终态 checkpoint 的 canonical 逐字节相等（水位、计数器、账本、历史全部一致）。
        Assert.Equal(baselineCheckpointCanonical, recoveredCheckpointCanonical);

        // 过期命令不得二次推进：基线臂命中宿主终态簿记（Duplicate），恢复臂在 admission 被
        // BatchStale 拒（命令水位已随 checkpoint 恢复）。标签不同是宿主簿记口径（design §7.1），
        // 两臂都不得产生第二次执行：状态版本与结算次数均不变。
        Assert.True(baselineReplay.Accepted);
        Assert.Equal(CookingLevelDispositionKind.Duplicate, baselineReplay.TerminalKind);
        Assert.True(baselineReplay.IsDuplicate);
        Assert.False(recoveredReplay.Accepted);
        Assert.Equal(CookingLevelAdmissionReason.BatchStale, recoveredReplay.Reason);
        Assert.Equal(1, baselineSettlements);
        Assert.Equal(1, recoveredSettlements);
    }

    /// <summary>
    /// 去重探针：以原批量重放已执行命令。基线臂应命中宿主终态簿记（admission 即返回 Duplicate）；
    /// 恢复臂的批次水位已随 checkpoint 恢复，同一条命令在 admission 被判 BatchStale。
    /// 两者都证明“过期命令不二次推进”，disciplinary 口径见 design §7.1。
    /// </summary>
    private static ReplayProbe ProbeReplay(Fixture fixture, CookingRecipeCommand command, bool expectTerminalDuplicate)
    {
        var admission = fixture.Host.TryEnqueue(Envelope(fixture, command, command.Command.Value + "-replay"));
        if (expectTerminalDuplicate)
        {
            Assert.True(admission.Accepted);
            var terminal = admission.TerminalDisposition!;
            Assert.Equal(CookingLevelDispositionKind.Duplicate, terminal.Kind);
            return new ReplayProbe(true, terminal.Kind, terminal.Result!.IsDuplicate, admission.Reason);
        }

        Assert.False(admission.Accepted);
        Assert.Equal(CookingLevelAdmissionReason.BatchStale, admission.Reason);
        return new ReplayProbe(false, null, false, admission.Reason);
    }

    private sealed record ReplayProbe(bool Accepted, CookingLevelDispositionKind? TerminalKind, bool IsDuplicate,
        CookingLevelAdmissionReason Reason);

    [Fact]
    public void R02_export_requires_a_running_quiescent_generation()
    {
        var content = LoadContent();
        using var fixture = CreateFixture(content);
        using var host = fixture.CreateStartedHost(state =>
            CookingContentCatalog.ApplyStandardInitialSupply(state, content));
        var simulation = fixture.Simulation;

        // 未决命令（入队但不推进帧）：导出被结构化拒绝，不产出半个 checkpoint。
        Execute(fixture, null, null, "pickup-pool-bowl", CookingRecipeOperation.Pickup,
            item: Bowl, expectedVersion: ItemState(simulation, Bowl).Version,
            summary: "pick up a clean bowl from the pool", tick: false);
        var pending = host.ExportCheckpoint();
        Assert.False(pending.Accepted);
        Assert.Equal(CookingLevelCheckpointExportReason.PendingCommands, pending.Reason);

        // 推进该帧后 pending 清空，导出恢复可用。
        host.Tick();
        var exported = host.ExportCheckpoint();
        Assert.True(exported.Accepted);
        Assert.NotNull(exported.Checkpoint);

        // Paused 代际同样拒绝：暂停语义（保留 live queue）不在本契约范围。
        Assert.True(host.Pause().Accepted);
        var paused = host.ExportCheckpoint();
        Assert.False(paused.Accepted);
        Assert.Equal(CookingLevelCheckpointExportReason.LevelPaused, paused.Reason);

        // 拒绝不改变可观察状态。
        Assert.True(host.Resume().Accepted);
        Assert.Equal(exported.Checkpoint!.Recipe.CanonicalText(), host.ExportCheckpoint().Checkpoint!.Recipe.CanonicalText());
    }

    [Fact]
    public void R03_restore_rejects_checkpoints_from_another_generation()
    {
        var content = LoadContent();
        CookingLevelCheckpoint checkpoint;
        {
            using var fixture = CreateFixture(content);
            using var host = fixture.CreateStartedHost(state =>
                CookingContentCatalog.ApplyStandardInitialSupply(state, content));
            var exported = host.ExportCheckpoint();
            Assert.True(exported.Accepted);
            checkpoint = exported.Checkpoint!;
            // 恢复入口要新建进程级单例宿主，先显式释放本宿主。
            host.Dispose();
        }

        // 载荷属于别的 match：创建宿主前即拒绝，不残留半恢复宿主。
        var foreignPayload = checkpoint with
        {
            Recipe = checkpoint.Recipe with { Scope = new CookingScope(Session, World, new MatchId("foreign-match")) },
        };
        var payloadResult = CookingLevelEtHost.Restore(foreignPayload, content.Snapshot, CreateFactory(content));
        Assert.False(payloadResult.Accepted);
        Assert.Equal(CookingLevelCheckpointRestoreReason.CheckpointPayloadScopeMismatch, payloadResult.Reason);
        Assert.Null(payloadResult.Host);

        // 只改 epoch：载荷 match 不变，也不能绕过代际绑定被建成另一代宿主。
        var foreignEpoch = checkpoint with
        {
            Scope = new CookingLevelScope(checkpoint.Scope.MatchScope, checkpoint.Scope.RestaurantRuntime,
                checkpoint.Scope.Level, checkpoint.Scope.LevelEpoch + 1),
        };
        var epochResult = CookingLevelEtHost.Restore(foreignEpoch, content.Snapshot, CreateFactory(content));
        Assert.False(epochResult.Accepted);
        Assert.Equal(CookingLevelCheckpointRestoreReason.TickHistoryScopeMismatch, epochResult.Reason);
        Assert.Null(epochResult.Host);

        // 另一配置身份：拒绝，不得把 foreign 内容插进本代际。
        var foreignConfig = checkpoint with
        {
            ConfigIdentity = new CookingConfigurationIdentity(checkpoint.ConfigIdentity.Schema, "foreign-config"),
        };
        var configResult = CookingLevelEtHost.Restore(foreignConfig, content.Snapshot, CreateFactory(content));
        Assert.False(configResult.Accepted);
        Assert.Equal(CookingLevelCheckpointRestoreReason.ConfigurationIdentityMismatch, configResult.Reason);
        Assert.Null(configResult.Host);

        // 同一代际、layout 引用了不存在的工位：代际绑定不变，lifecycle 校验拒绝（宿主创建后立即释放）。
        var foreignPreparation = checkpoint with
        {
            Preparation = checkpoint.Preparation with
            {
                Layout = checkpoint.Preparation.Layout with
                {
                    ApplianceStations = checkpoint.Preparation.Layout.ApplianceStations
                        .Append(new StationSlotId("no-such-station"))
                        .ToArray(),
                },
            },
        };
        var preparationResult = CookingLevelEtHost.Restore(foreignPreparation, content.Snapshot, CreateFactory(content));
        Assert.False(preparationResult.Accepted);
        Assert.Equal(CookingLevelCheckpointRestoreReason.PreparationRejected, preparationResult.Reason);
        Assert.Null(preparationResult.Host);

        // 载荷被篡改（外键缺失）：仿真恢复拒绝，宿主创建后释放，结构化 reason 透传。
        var foreignItem = checkpoint with
        {
            Recipe = checkpoint.Recipe with
            {
                Items = checkpoint.Recipe.Items
                    .Select(item => item.Id == new ItemId("pot-1")
                        ? item with { Definition = new DefinitionId("no-such-pot") }
                        : item)
                    .ToArray(),
            },
        };
        var itemResult = CookingLevelEtHost.Restore(foreignItem, content.Snapshot, CreateFactory(content));
        Assert.False(itemResult.Accepted);
        Assert.Equal(CookingLevelCheckpointRestoreReason.GameplayRestoreRejected, itemResult.Reason);
        Assert.Equal(CookingCheckpointRestoreReason.ItemDefinitionNotFound, itemResult.RecipeRestoreReason);
        Assert.Null(itemResult.Host);

        // 载荷缺失（内存态损坏）：与 scope 不匹配同一处理，不抛裸异常。
        var missingPayload = checkpoint with { Recipe = null! };
        var missingResult = CookingLevelEtHost.Restore(missingPayload, content.Snapshot, CreateFactory(content));
        Assert.False(missingResult.Accepted);
        Assert.Equal(CookingLevelCheckpointRestoreReason.CheckpointPayloadScopeMismatch, missingResult.Reason);
        Assert.Null(missingResult.Host);

        // 同一内容正常恢复仍可用（上述拒绝没有污染进程级单例）。
        using var recovered = CreateFixture(content, checkpoint.LastCommittedSimulationBatch);
        var accepted = CookingLevelEtHost.Restore(checkpoint, content.Snapshot, recovered.Factory);
        Assert.True(accepted.Accepted);
        using var restoredHost = accepted.Host!;
        Assert.Equal(checkpoint.HostFrameSequence, restoredHost.HostFrameSequence);
    }

    private static Factory CreateFactory(CookingContent content) => CreateFixture(content).Factory;

    [Fact]
    public void R04_restored_host_keeps_host_frame_sequence_monotonic()
    {
        var content = LoadContent();
        using var fixture = CreateFixture(content);
        using var host = fixture.CreateStartedHost(state =>
            CookingContentCatalog.ApplyStandardInitialSupply(state, content));
        RunToSoupCooking(fixture, null, null);
        var exported = host.ExportCheckpoint();
        Assert.True(exported.Accepted);
        var checkpoint = exported.Checkpoint!;

        host.Dispose();

        using var recovered = CreateFixture(content, checkpoint.LastCommittedSimulationBatch);
        var restored = CookingLevelEtHost.Restore(checkpoint, content.Snapshot, recovered.Factory);
        Assert.True(restored.Accepted);
        using var restoredHost = restored.Host!;
        recovered.AdoptHost(restoredHost);

        // HostFrameSequence 单调不重置：恢复后即 checkpoint 值，下一帧严格 +1，LogicalTick 连续。
        Assert.Equal(checkpoint.HostFrameSequence, restoredHost.HostFrameSequence);
        Assert.Equal(checkpoint.Recipe.LogicalTick, recovered.Simulation.LogicalTick);
        Assert.Equal(checkpoint.LifecycleVersion, restoredHost.Lifecycle.Version);
        var frame = restoredHost.Tick();
        Assert.True(frame.Accepted);
        Assert.Equal(checkpoint.HostFrameSequence + 1, frame.HostFrameSequence);
        Assert.Equal(checkpoint.Recipe.LogicalTick + 1, frame.Tick!.AfterLogicalTick);
        Assert.Equal(checkpoint.Recipe.StateVersion + 1, frame.Tick!.AfterStateVersion);
    }

    [Fact]
    public void R05_recovered_host_restores_front_of_house_projection_and_progress()
    {
        var content = LoadContent();
        var schedule = new CookingFrontOfHouseSchedule(1, 10, 10, 3, 3, 3, 9);

        string baselineFrontCanonical;
        string baselineKitchenCanonical;
        long baselineFrame;
        {
            using var baseline = CreateFixture(content);
            using var baselineHost = baseline.CreateStartedHost(state =>
                CookingContentCatalog.ApplyStandardInitialSupply(state, content));
            baselineHost.UseFrontOfHouse(new CookingFrontOfHouse(schedule), SoupOrderTemplate);
            AdvanceClock(baseline, 4);
            baselineFrontCanonical = baselineHost.FrontOfHouseSnapshot!.CanonicalText();
            baselineKitchenCanonical = baseline.Simulation.Snapshot().CanonicalText();
            baselineFrame = baselineHost.HostFrameSequence;
            Assert.Equal(CookingTablePhase.Ordered,
                Assert.Single(baselineHost.FrontOfHouseSnapshot.Customers).Phase);
        }

        string recoveredFrontCanonical;
        string recoveredKitchenCanonical;
        long recoveredFrame;
        {
            using var source = CreateFixture(content);
            using var sourceHost = source.CreateStartedHost(state =>
                CookingContentCatalog.ApplyStandardInitialSupply(state, content));
            sourceHost.UseFrontOfHouse(new CookingFrontOfHouse(schedule), SoupOrderTemplate);
            AdvanceClock(source, 2);
            Assert.Equal(CookingCompanionWorkKind.Inquiring, sourceHost.FrontOfHouseSnapshot!.Companion.Work);

            var exported = sourceHost.ExportCheckpoint();
            Assert.True(exported.Accepted);
            Assert.NotNull(exported.Checkpoint!.FrontOfHouse);
            var checkpoint = CookingLevelCheckpointCodec.Deserialize(CookingLevelCheckpointCodec.Serialize(
                CookingLevelCheckpointCodec.CreateEnvelope(exported.Checkpoint))).Checkpoint!;
            sourceHost.Dispose();

            using var recovered = CreateFixture(content, checkpoint.LastCommittedSimulationBatch);
            var restored = CookingLevelEtHost.Restore(checkpoint, content.Snapshot, recovered.Factory);
            Assert.True(restored.Accepted);
            using var restoredHost = restored.Host!;
            recovered.AdoptHost(restoredHost);
            Assert.Equal(checkpoint.FrontOfHouse!.State.CanonicalText(),
                restoredHost.FrontOfHouseSnapshot!.CanonicalText());

            AdvanceClock(recovered, 2);
            recoveredFrontCanonical = restoredHost.FrontOfHouseSnapshot!.CanonicalText();
            recoveredKitchenCanonical = recovered.Simulation.Snapshot().CanonicalText();
            recoveredFrame = restoredHost.HostFrameSequence;
        }

        Assert.Equal(baselineFrontCanonical, recoveredFrontCanonical);
        Assert.Equal(baselineKitchenCanonical, recoveredKitchenCanonical);
        Assert.Equal(baselineFrame, recoveredFrame);
    }

    [Fact]
    public void R06_restore_rejects_a_corrupt_front_of_house_checkpoint()
    {
        var content = LoadContent();
        var schedule = new CookingFrontOfHouseSchedule(1, 10, 10, 3, 3, 3, 9);
        CookingLevelCheckpoint checkpoint;
        {
            using var source = CreateFixture(content);
            using var sourceHost = source.CreateStartedHost(state =>
                CookingContentCatalog.ApplyStandardInitialSupply(state, content));
            sourceHost.UseFrontOfHouse(new CookingFrontOfHouse(schedule), SoupOrderTemplate);
            AdvanceClock(source, 2);
            checkpoint = sourceHost.ExportCheckpoint().Checkpoint!;
            sourceHost.Dispose();
        }

        var front = checkpoint.FrontOfHouse!;
        var corrupt = checkpoint with
        {
            FrontOfHouse = front with
            {
                State = front.State with
                {
                    Companion = front.State.Companion with { RequiredTicks = 99 },
                },
            },
        };

        var rejected = CookingLevelEtHost.Restore(corrupt, content.Snapshot, CreateFactory(content));

        Assert.False(rejected.Accepted);
        Assert.Equal(CookingLevelCheckpointRestoreReason.FrontOfHouseRestoreRejected, rejected.Reason);
        Assert.Equal(CookingFrontOfHouseRestoreReason.CompanionInvalid, rejected.FrontOfHouseRestoreReason);
        Assert.Null(rejected.Host);

        using var recovered = CreateFixture(content, checkpoint.LastCommittedSimulationBatch);
        var accepted = CookingLevelEtHost.Restore(checkpoint, content.Snapshot, recovered.Factory);
        Assert.True(accepted.Accepted);
        using var restoredHost = accepted.Host!;
    }

    private sealed record FinalState(string Canonical, string Hash, long StateVersion, long LogicalTick, ItemId NextProductId);

    /// <summary>
    /// 跑到“煮制进行中”：标准初始供应 → 碗上台面 → 切番茄（2 帧）→ 番茄块入锅 → 打蛋（2 帧）→
    /// 蛋液入锅 → 启动煮制并推进 3 个纯时钟帧。返回已执行命令。
    /// </summary>
    private static List<CookingRecipeCommand> RunToSoupCooking(Fixture fixture, EvidenceScope? evidence, string? testId)
    {
        var simulation = fixture.Simulation;
        var commands = new List<CookingRecipeCommand>();

        commands.Add(Execute(fixture, evidence, testId, "pickup-pool-bowl", CookingRecipeOperation.Pickup,
            item: Bowl, expectedVersion: ItemState(simulation, Bowl).Version,
            summary: "pick up a clean bowl from the pool"));
        commands.Add(Execute(fixture, evidence, testId, "drop-bowl", CookingRecipeOperation.Drop,
            item: Bowl, station: Counter, expectedVersion: ItemState(simulation, Bowl).Version,
            summary: "put the bowl on the counter for reuse"));

        commands.Add(Execute(fixture, evidence, testId, "pickup-tomato-1", CookingRecipeOperation.Pickup,
            item: Tomato, expectedVersion: ItemState(simulation, Tomato).Version,
            summary: "pick up the tomato from the pantry"));
        commands.Add(Execute(fixture, evidence, testId, "drop-tomato-1", CookingRecipeOperation.Drop,
            item: Tomato, station: Board, expectedVersion: ItemState(simulation, Tomato).Version,
            summary: "drop the tomato onto the board"));
        commands.Add(Execute(fixture, evidence, testId, "start-chop", CookingRecipeOperation.StartProcess,
            recipe: ChopRecipe, item: Tomato, station: Board, expectedVersion: ItemState(simulation, Tomato).Version,
            summary: "start chopping on the board"));
        AdvanceClock(fixture, ChopTicks);
        var chopped = Assert.Single(simulation.Snapshot().Items, item => item.IsProduct);
        Assert.Equal(ChoppedTomato, chopped.Definition);

        commands.Add(Execute(fixture, evidence, testId, "pickup-chopped", CookingRecipeOperation.Pickup,
            item: chopped.Id, expectedVersion: chopped.Version, summary: "pick up the chopped tomato"));
        commands.Add(Execute(fixture, evidence, testId, "chopped-into-pot", CookingRecipeOperation.PutIn,
            item: chopped.Id, container: Pot, expectedVersion: ItemState(simulation, chopped.Id).Version,
            summary: "put the chopped tomato into the pot"));

        commands.Add(Execute(fixture, evidence, testId, "pickup-egg", CookingRecipeOperation.Pickup,
            item: Egg, expectedVersion: ItemState(simulation, Egg).Version,
            summary: "pick up the egg from the pantry"));
        commands.Add(Execute(fixture, evidence, testId, "egg-into-bowl", CookingRecipeOperation.PutIn,
            item: Egg, container: Bowl, expectedVersion: ItemState(simulation, Egg).Version,
            summary: "put the egg into the bowl"));
        commands.Add(Execute(fixture, evidence, testId, "pickup-bowl", CookingRecipeOperation.Pickup,
            item: Bowl, expectedVersion: ItemState(simulation, Bowl).Version,
            summary: "pick up the bowl holding the egg"));
        commands.Add(Execute(fixture, evidence, testId, "beat-start", CookingRecipeOperation.StartProcess,
            recipe: BeatRecipe, item: Bowl, expectedVersion: ItemState(simulation, Bowl).Version,
            summary: "beating eggs needs no station"));
        AdvanceClock(fixture, BeatTicks);
        var liquid = Assert.Single(simulation.Snapshot().Items, item => item.Definition == BeatenEgg);
        Assert.Equal(ItemLocation.Container(Bowl, "slot-0"), liquid.Location);

        commands.Add(Execute(fixture, evidence, testId, "pour-egg", CookingRecipeOperation.Pour,
            item: Bowl, container: Pot, expectedVersion: ItemState(simulation, Bowl).Version,
            summary: "pour the egg liquid into the pot and free the bowl"));
        commands.Add(Execute(fixture, evidence, testId, "bowl-back", CookingRecipeOperation.Drop,
            item: Bowl, station: Counter, expectedVersion: ItemState(simulation, Bowl).Version,
            summary: "put the emptied bowl back on the counter"));

        commands.Add(Execute(fixture, evidence, testId, "soup-start", CookingRecipeOperation.StartProcess,
            recipe: SoupRecipe, item: Pot, station: Stove, expectedVersion: ItemState(simulation, Pot).Version,
            summary: "start the multi-input cooking"));
        AdvanceClock(fixture, SoupTicksBeforeCheckpoint);

        return commands;
    }
    /// <summary>
    /// 续跑闭环剩余部分：煮满 6 tick → 端走锅 → 倒汤入碗 → 前厅开单（注入）→ 提交 → 洗碗回池（注入）
    /// → 锅放回灶台 → 烤面包探针（验证下一产物 ID 连续性）。返回终态观测值。
    /// </summary>
    private static FinalState FinishLoop(Fixture fixture, List<CookingRecipeCommand> commands,
        EvidenceScope? evidence, string? testId)
    {
        var simulation = fixture.Simulation;
        AdvanceClock(fixture, SoupTicks - SoupTicksBeforeCheckpoint);
        Assert.True(ItemState(simulation, Pot).ContainerCompleted);

        commands.Add(Execute(fixture, evidence, testId, "carry-pot", CookingRecipeOperation.Pickup,
            item: Pot, expectedVersion: ItemState(simulation, Pot).Version,
            summary: "carry the completed pot away from the stove"));
        commands.Add(Execute(fixture, evidence, testId, "pour-soup", CookingRecipeOperation.Pour,
            item: Pot, container: Bowl, expectedVersion: ItemState(simulation, Pot).Version,
            summary: "pour the soup into the bowl"));
        var soup = Assert.Single(simulation.Snapshot().Items, item => item.IsProduct);
        Assert.Equal(Soup, soup.Definition);
        Assert.Equal(ItemLocation.Container(Bowl, "slot-0"), soup.Location);

        Assert.True(simulation.OpenOrder(SoupOrder, SoupOrderTemplate).Accepted);
        commands.Add(Execute(fixture, evidence, testId, "submit-soup", CookingRecipeOperation.SubmitOrder,
            item: soup.Id, order: SoupOrder, expectedVersion: soup.Version,
            summary: "submit the bowl of soup to the order"));
        Assert.Equal(CookingOrderStatus.Completed.ToString(),
            simulation.Snapshot().Orders.Single(order => order.Id == SoupOrder).Status);
        Assert.True(simulation.CompleteWash(Bowl).Accepted);

        // 提交后玩家仍手持锅：先放回灶台，腾出手再做探针。
        commands.Add(Execute(fixture, evidence, testId, "drop-pot", CookingRecipeOperation.Drop,
            item: Pot, station: Stove, expectedVersion: ItemState(simulation, Pot).Version,
            summary: "put the emptied pot back on the stove"));

        var slice = ItemState(simulation, BreadSlice);
        commands.Add(Execute(fixture, evidence, testId, "pickup-slice", CookingRecipeOperation.Pickup,
            item: BreadSlice, expectedVersion: slice.Version,
            summary: "pick up the bread slice from the standard supply"));
        commands.Add(Execute(fixture, evidence, testId, "drop-slice", CookingRecipeOperation.Drop,
            item: BreadSlice, station: Oven, expectedVersion: ItemState(simulation, BreadSlice).Version,
            summary: "drop the bread slice onto the oven"));
        commands.Add(Execute(fixture, evidence, testId, "bake-start", CookingRecipeOperation.StartProcess,
            recipe: BakeRecipe, item: BreadSlice, station: Oven, expectedVersion: ItemState(simulation, BreadSlice).Version,
            summary: "the oven bakes with a single input"));
        AdvanceClock(fixture, BakeTicks);
        var bread = Assert.Single(simulation.Snapshot().Items, item => item.Definition == ToastedBread);
        Assert.Equal(ItemLocation.Station(Oven), bread.Location);

        var snapshot = simulation.Snapshot();
        return new FinalState(snapshot.CanonicalText(), snapshot.Sha256(), snapshot.Version,
            snapshot.LogicalTick, bread.Id);
    }

    private static CookingLevelCommandEnvelope Envelope(Fixture fixture, CookingRecipeCommand command, string correlationId) =>
        new(fixture.LevelScope, command, "loop-connection", correlationId);
    /// <summary>经宿主执行一条玩家命令：入队被接受、一帧执行恰好一个 disposition、该帧推进一个 fixed tick。</summary>
    private static CookingRecipeCommand Execute(
        Fixture fixture,
        EvidenceScope? evidence,
        string? testId,
        string commandId,
        CookingRecipeOperation operation,
        string summary,
        RecipeId? recipe = null,
        ItemId? item = null,
        StationSlotId? station = null,
        ItemId? container = null,
        OrderId? order = null,
        int expectedVersion = 0,
        bool tick = true)
    {
        var host = fixture.Host;
        var simulation = fixture.Simulation;
        var batch = fixture.NextBatch();
        var command = new CookingRecipeCommand(fixture.LevelScope.MatchScope, batch, Player,
            new RecipeCommandId(commandId), operation, recipe, null, item, station, container, order,
            expectedVersion, 0);
        var before = simulation.Snapshot();
        Assert.True(host.TryEnqueue(Envelope(fixture, command, commandId)).Accepted);

        if (tick)
        {
            var frame = host.Tick();
            Assert.True(frame.Accepted);
            Assert.Equal(batch, frame.SimulationBatch);
            Assert.Equal(simulation.LogicalTick, frame.HostFrameSequence);
            Assert.Equal(before.LogicalTick + 1, frame.Tick!.AfterLogicalTick);
            var disposition = Assert.Single(frame.Dispositions);
            Assert.Equal(CookingLevelDispositionKind.Executed, disposition.Kind);
            var result = disposition.Result!;
            Assert.Equal(CookingRecipeOutcome.Accepted, result.Outcome);
            Assert.Equal(CookingRecipeRejectionReason.None, result.Reason);
        }

        if (evidence is not null)
        {
            CookingRecipeAcceptanceEvidenceWriter.Append(evidence.Path, new CookingRecipeAcceptanceEvidence(
                testId!, "et-level-checkpoint", command, simulation.LogicalTick,
                CookingRecipeOutcome.Accepted.ToString(), CookingRecipeRejectionReason.None.ToString(), false,
                Array.Empty<CookingRecipeEvent>(), before.Sha256(), simulation.Snapshot().Sha256(), summary,
                Runner, DateTimeOffset.UtcNow.ToString("O")));
        }

        return command;
    }

    /// <summary>纯时钟帧：无命令入队，只推进一个 fixed tick，不产生 disposition。</summary>
    private static void AdvanceClock(Fixture fixture, int frames)
    {
        for (var frame = 1; frame <= frames; frame++)
        {
            var before = fixture.Simulation.LogicalTick;
            var result = fixture.Host.Tick();
            Assert.True(result.Accepted);
            Assert.Empty(result.Dispositions);
            Assert.Equal(before + 1, result.Tick!.AfterLogicalTick);
            Assert.Equal(before + 1, fixture.Simulation.LogicalTick);
        }
    }

    private static CookingRecipeSnapshotItem ItemState(CookingRecipeSimulation simulation, ItemId id) =>
        simulation.Snapshot().Items.Single(item => item.Id == id);

    private static CookingContent LoadContent() => CookingContentCatalog.Load(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, CookingContentCatalog.ContentFileName)));
    private static Fixture CreateFixture(CookingContent content, long startingBatch = 0)
    {
        var matchScope = new CookingScope(Session, World, Match);
        var levelScope = new CookingLevelScope(matchScope, new RestaurantRuntimeId(1),
            new LevelId("loop-level"), 1);
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [Player] = new(Player, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new HashSet<string>(StringComparer.Ordinal)
                {
                    Board.Value, Stove.Value, Oven.Value, Counter.Value,
                }),
        };
        var simulationFixture = CookingContentCatalog.BuildFixture(content, matchScope, players, "clean-pool");
        var washPort = new RecordingWashPort();
        var factory = new Factory(simulationFixture, washPort, startingBatch);
        var lifecycle = new CookingLevelLifecycle(levelScope, content.Snapshot, factory);
        var preparation = new CookingLevelPreparation(levelScope.Level, new MapId("map"),
            new CookingLogicalLayout(new LayoutId("layout"),
                new[] { Board, Stove, Oven, Counter },
                new[] { BowlDefinition, PotDefinition }),
            content.Identity);
        return new Fixture(levelScope, lifecycle, preparation, factory);
    }

    private static void AssertEvidence(string path, string testId, int recordCount)
    {
        var records = CookingRecipeAcceptanceEvidenceWriter.ReadAll(path);
        Assert.Equal(recordCount, records.Count);
        Assert.All(records, record =>
        {
            Assert.Equal(testId, record.TestId);
            Assert.Equal("et-level-checkpoint", record.FixtureId);
            Assert.False(string.IsNullOrWhiteSpace(record.BeforeStateHash));
            Assert.False(string.IsNullOrWhiteSpace(record.AfterStateHash));
            Assert.False(string.IsNullOrWhiteSpace(record.AssertionSummary));
            Assert.Equal(Runner, record.Runner);
        });
    }

    private static EvidenceScope CreateEvidence(string testId) => new(testId);

    private sealed class Fixture : IDisposable
    {
        private readonly Factory _factory;
        private CookingLevelEtHost? _host;
        private long _batch;

        public Fixture(
            CookingLevelScope levelScope,
            CookingLevelLifecycle lifecycle,
            CookingLevelPreparation preparation,
            Factory factory)
        {
            LevelScope = levelScope;
            Lifecycle = lifecycle;
            Preparation = preparation;
            _factory = factory;
            _batch = factory.StartingBatch;
        }

        public CookingLevelScope LevelScope { get; }
        public CookingLevelLifecycle Lifecycle { get; }
        public CookingLevelPreparation Preparation { get; }
        public Factory Factory => _factory;
        public CookingRecipeSimulation Simulation => _factory.Simulation!;
        public CookingLevelEtHost Host =>
            _host ?? throw new InvalidOperationException("The level host is not started.");

        public long NextBatch() => ++_batch;

        public CookingLevelEtHost CreateStartedHost(Action<CookingRecipeSimulation> initialize)
        {
            var host = new CookingLevelEtHost(Lifecycle);
            try
            {
                Assert.True(host.Prepare(Preparation).Accepted);
                initialize(_factory.EnsureSimulation(LevelScope));
                Assert.True(host.Start().Accepted);
                return _host = host;
            }
            catch
            {
                // 初始化失败同样释放宿主：ET 宿主是进程级单例，泄漏会污染同进程后续测试。
                host.Dispose();
                throw;
            }
        }

        /// <summary>接管由静态恢复入口创建的宿主，使释放路径统一。</summary>
        public void AdoptHost(CookingLevelEtHost host)
        {
            if (_host is not null)
                throw new InvalidOperationException("The fixture already owns a started host.");
            _host = host;
        }

        public void Dispose() => _host?.Dispose();
    }
    private sealed class Factory : ICookingLevelGameplayFactory
    {
        private readonly CookingRecipeFixture _fixture;
        private readonly RecordingWashPort _washPort;
        private CookingRecipeSimulation? _precreated;

        public Factory(CookingRecipeFixture fixture, RecordingWashPort washPort, long startingBatch)
        {
            _fixture = fixture;
            _washPort = washPort;
            StartingBatch = startingBatch;
        }

        public long StartingBatch { get; }
        public RecordingWashPort WashPort => _washPort;
        public CookingRecipeSimulation? Simulation { get; private set; }

        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            Assert.Equal(scope.MatchScope, _fixture.Scope);
            if (_precreated is not null)
            {
                Simulation = _precreated;
                _precreated = null;
                return Simulation;
            }

            return Simulation = NewSimulation();
        }

        public CookingRecipeSimulation EnsureSimulation(CookingLevelScope scope)
        {
            Assert.Equal(scope.MatchScope, _fixture.Scope);
            return _precreated ??= NewSimulation();
        }

        private CookingRecipeSimulation NewSimulation() => new(_fixture, null, _washPort);
    }

    private sealed class RecordingWashPort : ICookingBowlWashingPort
    {
        public List<ItemId> Requests { get; } = new();

        public void RequestWash(ItemId bowl, DefinitionId definition) => Requests.Add(bowl);
    }

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
                : System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AbilityKit.ET.Runtime.Tests", "checkpoint");
            _directory = System.IO.Path.Combine(root, testId, Guid.NewGuid().ToString("N"));
            Path = System.IO.Path.Combine(_directory, "et-checkpoint.jsonl");
        }

        public string Path { get; }

        public void Dispose()
        {
            if (!_keepArtifacts && Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }
    }
}
