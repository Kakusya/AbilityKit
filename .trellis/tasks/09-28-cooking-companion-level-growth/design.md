# Cooking 固定伙伴小关内成长 — Technical Design

## 1. Architecture And Ownership

成长状态继续归 `CookingFrontOfHouse` 所有：

```text
CookingFrontOfHouseSchedule
  └─ CompanionGrowthTaskThreshold (configuration, default 3)

CookingFrontOfHouse
  ├─ _completedCompanionTasks (single mutable source of truth)
  ├─ derived WashSpeedUnlocked
  ├─ CookingCompanionWork.RequiredTicks (captured at claim time)
  └─ Snapshot / Checkpoint / Canonical

CookingLevelEtHost
  └─ existing FrontOfHouseSnapshot and FrontOfHouseCheckpoint passthrough
```

不在 ET host、session、transport 或 Unity 建立第二份伙伴成长状态。ET host 继续从领域对象读取 snapshot，并把领域 checkpoint 放入现有 `CookingLevelCheckpoint`。

## 2. Configuration Contract

在 `CookingFrontOfHouseSchedule` 尾部增加：

```csharp
int CompanionGrowthTaskThreshold = 3
```

选择尾部可选参数的原因：

- 保持当前 7 参数构造调用源代码兼容；
- 允许测试用例用小阈值构造最短场景，但产品默认仍为 3；
- 阈值属于 Level 前厅配置，不是 Match 或 Profile 长期数据。

`ValidateSchedule` 要求阈值大于 0。洗碗加速公式不新增倍率配置，第一版固定为：

```csharp
Math.Max(1, (schedule.WashTicks + 1) / 2)
```

## 3. Domain State And Data Flow

### 3.1 Mutable Source Of Truth

增加字段：

```csharp
private int _completedCompanionTasks;
```

解锁状态为纯派生属性：

```csharp
private bool WashSpeedUnlocked =>
    _completedCompanionTasks >= _schedule.CompanionGrowthTaskThreshold;
```

不保存第二个可变 `_washSpeedUnlocked`，避免计数、snapshot 和 checkpoint 之间漂移。

### 3.2 Completion Accounting

两个完成终点只在真实成功后计数：

- `CompleteInquiry(...)`：仅在 `kitchen.OpenOrder(...)` accepted 后更新顾客状态并增加计数。
- `CompleteWash(...)`：仅在 `kitchen.CompleteWash(...)` accepted 后移除队列身份并增加计数；失败仍重新入队但不计数。

计数更新集中在这两个成功终点，普通 Tick、认领、取消、重排和 snapshot 不得修改计数。

### 3.3 Claim-Time Required Ticks

扩展 `CookingCompanionWork`：

```csharp
public int RequiredTicks { get; private set; }
```

构造规则：

- `Idle()`：`RequiredTicks = 0`
- `Inquiry(...)`：`RequiredTicks = schedule.InquiryTicks`
- `Wash(...)`：认领时根据当前 `WashSpeedUnlocked` 选择基础或加速值
- `From(snapshot)`：恢复 snapshot 中已经冻结的值

`AdvanceCompanion` 只比较 `_work.Elapsed` 与 `_work.RequiredTicks`。`Snapshot()` 直接投影 `_work.RequiredTicks`，不再按工作类型从 schedule 重算。

### 3.4 Fixed-Tick Ordering

保留现有 `Step` 顺序：

1. 发现脏碗；
2. 推进顾客；
3. 推进当前伙伴工作；
4. 当前工作完成后，如伙伴空闲则认领下一任务；
5. 认领后沿用现有行为立即推进 1 Tick；
6. 处理来客与营业关闭。

因此当前任务若在步骤 3 成为第三次成功完成，步骤 4 认领的洗碗会读取更新后的计数并立即使用加速耗时。已经开始的任务使用自身保存的 `RequiredTicks`，不会被追溯修改。

## 4. Snapshot And Canonical Contract

扩展 `CookingCompanionSnapshot`：

```text
CompletedTaskCount: int
WashSpeedUnlocked: bool
RequiredTicks: int
```

字段属于伙伴投影，因为它们描述固定伙伴在当前 Level 的成长和当前工作。`CookingFrontOfHouseSnapshot.CanonicalText()` 同时把 `Schedule.CompanionGrowthTaskThreshold` 加入 schedule canonical，并在 `CanonicalCompanion` 加入计数与解锁状态，保持固定字段顺序；SHA-256 自动覆盖新增配置与运行态内容。

兼容性决定：本任务不承诺旧 snapshot/checkpoint JSON 的跨版本迁移。当前范围只验证同版本导出与恢复；LAN wire 未使用该新增字段，故不改协议版本。

## 5. Checkpoint Validation And Atomicity

`CookingFrontOfHouseCheckpoint` 继续包裹完整 snapshot。恢复顺序保持：

1. 校验 schedule、身份、计数、伙伴工作、顾客、洗碗队列和厨房外键；
2. 全部通过后构造新 `CookingFrontOfHouse` 并 `Apply`；
3. 实例恢复入口只在候选对象成功后 `CopyFrom`。

新增校验：

- `CompletedTaskCount >= 0`；
- `WashSpeedUnlocked == (CompletedTaskCount >= CompanionGrowthTaskThreshold)`；
- Idle：`RequiredTicks == 0`；
- Inquiring：`RequiredTicks == Schedule.InquiryTicks`；
- Washing：`RequiredTicks` 必须等于按当前完成计数可得的基础或加速洗碗耗时；
- 活动工作继续满足 `1 <= ElapsedTicks < RequiredTicks`。

洗碗进行时没有其他伙伴并发完成任务，因此 checkpoint 中当前计数就是该洗碗认领时的计数，能够唯一确定其合法 `RequiredTicks`。

结构化原因优先复用 `CookingFrontOfHouseRestoreReason.CompanionInvalid`，避免仅为字段细分扩大公共枚举；若实现时现有测试可读性明显不足，再新增单一 `GrowthInvalid`，但不得改变原子恢复路径。

## 6. Lifecycle Semantics

### Failure Retry

`DropFailedScene()` 清空 `_completedCompanionTasks`，与座位、营业时钟、队列和当前工作一起重置。失败厨房现场的任务完成不进入新代际。

### Next Level

`ResetForNextLevel(...)` 先沿用 `FinishInProgress(...)` 完成既有收口语义，再清空 `_completedCompanionTasks`。这样本关最终收口仍语义正确，但新 Level 观察到的成长从 0 开始。

### Same-Level Checkpoint

同 Level 恢复保留计数和当前工作耗时，不触发重置。ET host 恢复继续要求厨房和前厅共同成功，否则释放候选 host 并返回失败。

## 7. ET Host Impact

预期不修改 `CookingLevelEtHost` 生产逻辑：

- `FrontOfHouseSnapshot` 已直接返回领域 snapshot；
- `ExportCheckpoint()` 已包含 `_frontOfHouse?.ExportCheckpoint(...)`；
- `Restore(...)` 已先恢复厨房，再恢复前厅，并在失败时释放候选 host。

只新增或扩展 `AbilityKit.ET.Runtime.Tests` 的 checkpoint 测试，证明成长字段经过宿主信封往返，并继续执行后与不中断基线一致。只有测试暴露透传缺口时才做最小宿主修复。

## 8. Test Strategy

### Domain Tests

在 `CookingFrontOfHouseTests` 增加聚焦用例：

- 0–2 次完成仍使用基础洗碗耗时；
- 询问/洗碗混合完成到第 3 次解锁；
- 奇数耗时向上取整和 1 Tick 下限；
- 失败洗碗重排、取消询问、未完成工作不计数；
- 进行中洗碗 `RequiredTicks` 固定；
- 同 Tick 第三次完成后新洗碗立即加速；
- canonical/hash 对计数、解锁和 required ticks 敏感；
- checkpoint 在解锁前后、进行中状态往返；
- 负计数、解锁不一致和非法 required ticks 原子拒绝；
- 失败重开、下一 Level 清零。

### ET Runtime Tests

在 `CookingLevelCheckpointTests` 或相邻宿主测试中验证：

- host 导出的前厅 snapshot/checkpoint 含成长状态；
- 恢复后 `FrontOfHouseSnapshot` 与源 canonical/SHA-256 一致；
- 恢复后继续 Tick 与不中断基线一致；
- 前厅成长投毒使整个 host restore 失败，不留下半恢复宿主。

## 9. Rollback And Scope Control

- 生产改动应集中在 `CookingFrontOfHouse.cs`；若 ET host 无透传缺口，不修改其生产代码。
- 若位置参数兼容性出现问题，优先把调用点改为命名参数，不改变阈值产品决定。
- 不修改 Cooking LAN/session/UDP、Unity package、内容 schema 或长期存档。
- 回滚时可整体撤销新增字段和测试；不存在数据迁移或外部协议清理步骤。
