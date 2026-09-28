# Cooking 可见顾客与固定伙伴运行时纵切：技术设计

更新：2026-09-28。状态：implemented / verified。

## 1. 设计原则

1. `CookingFrontOfHouse` 继续是前厅唯一权威 owner；快照和 checkpoint 都从该对象导出，不复制一套投影状态机。
2. 不重写已经验证的前厅调度顺序，只把内部匿名桌位占用升级为具有稳定顾客身份的状态。
3. snapshot 服务当前状态投影，checkpoint 服务同一 Level 的销毁重建；两者字段相近但职责不同。
4. 任何恢复先校验、后整册替换；不能逐字段写入后再发现错误。

## 2. 领域模型

### 2.1 顾客身份与桌位

- 新增强类型 `CookingCustomerId`，由前厅的 Level-local 单调计数器分配。
- 每个非空桌位持有一个顾客运行态，包含：顾客 ID、到店序号、阶段、阶段计时和可选订单 ID。
- 订单 ID 从顾客 ID 派生，避免同一桌重复接待时复用 `table-N-order`。
- 餐桌 ID 继续稳定存在；桌位只是顾客当前占用位置，不再充当顾客身份。

### 2.2 固定伙伴

- `CookingFrontOfHouse` 构造时接受稳定 `CookingCompanionId`；现有调用点可使用明确的 fixture 默认值迁移。
- 内部工作记录继续只有 `Idle`、`Inquiring`、`Washing`，但目标改为稳定 ID，而不是暴露内部桌对象引用。
- 当前工作记录保存 elapsed；required 由日程配置和工作类型确定并写入快照。

## 3. Snapshot 合同

新增不可变记录：

- `CookingFrontOfHouseSnapshot`
- `CookingCustomerSnapshot`
- `CookingCompanionSnapshot`
- 必要的桌位/队列只读记录

`CookingFrontOfHouse.Snapshot()` 返回深拷贝并按餐桌 ID、顾客 ID和队列原顺序规范化。快照 canonical 至少覆盖：

- 日程与营业/到店时钟；
- 下一顾客身份水位；
- 当前顾客及桌位占用；
- 伙伴身份、工作、目标和进度；
- 洗碗队列与未满足订单列表。

`CanonicalText()` 使用稳定字段顺序和 ordinal 排序；`Sha256()` 对 UTF-8 canonical 文本计算 SHA-256。离席顾客不保留历史实体，表现层通过相邻快照 diff 观察离场。

## 4. Checkpoint 合同

新增 `CookingFrontOfHouseCheckpoint`，字段覆盖 snapshot 所需状态及恢复所需配置。提供：

- `ExportCheckpoint()`：导出深拷贝；
- `RestoreCheckpoint(checkpoint)`：先验证完整性，再整册替换；
- `Restore(checkpoint)` 或等价工厂：供 ET 宿主恢复时创建新的前厅 owner。

验证至少覆盖：

- schedule 所有 Tick 和桌数为正；
- 顾客/餐桌/到店序号唯一，空桌不得带顾客；
- 顾客阶段与订单是否存在的组合合法；
- 伙伴询问目标必须是当前询问中的顾客，洗碗目标必须与队列/厨房恢复状态一致；
- 洗碗队列和未满足订单无重复；
- elapsed、service tick、arrival countdown 和身份水位非负且不倒退。

`CookingLevelCheckpoint` 增加可选 `FrontOfHouse` 载荷。`CookingLevelCheckpointCodec.CurrentFormatVersion` 升级到 2；不迁移 v1。

## 5. ET Host 接入

- `CookingLevelEtHost.ExportCheckpoint()` 在启用前厅时连同活动模板导出前厅 checkpoint。
- `CookingLevelEtHost.Restore(...)` 在厨房恢复成功后创建并验证前厅，再通过 `UseFrontOfHouse` 安装；任一步失败释放新宿主并返回结构化 reason。
- `AdoptRecoveredCheckpoint` 只在厨房和前厅都恢复成功后采用宿主帧水位。
- 暂停、Tick、`TryFinishService`、失败重开和下一 Level 的现有调用顺序不变。

## 6. 状态流

```text
固定 Tick
  -> 厨房推进
  -> 前厅读取厨房订单/脏碗
  -> 顾客与伙伴状态推进
  -> 导出厨房 snapshot + 前厅 snapshot

同 Level 恢复
  -> 读取 v2 Level checkpoint
  -> 校验 scope/config/厨房载荷
  -> 创建并恢复厨房
  -> 校验并创建前厅
  -> 一次性采用 host/frame/command 水位
```

## 7. 兼容性与迁移

- 订单 ID 从桌位派生改为顾客派生是有意的行为修复；测试和仅依赖 fixture ID 的调用点同步更新。
- checkpoint wire 格式升 v2；仓库当前无产品 durable save 兼容承诺，因此旧格式明确拒绝优于隐式猜测迁移。
- LAN session snapshot/protocol 不在本任务修改范围；本任务先建立宿主无关的前厅 snapshot/checkpoint 合同。

## 8. 风险与控制

- **风险：前厅状态与厨房订单脱节。** 通过恢复校验、跨对象一致性测试和同一 Tick 单 owner 顺序控制。
- **风险：snapshot 与 checkpoint 字段重复漂移。** 从同一内部状态构造，并以 round-trip canonical 测试约束。
- **风险：扩大为完整 NPC 系统。** 明确不加入位置、寻路、动画、成长、岗位、情绪或长期存档。
- **风险：破坏跨关清理语义。** 保留现有 `FinishInProgress`、`DropFailedScene`、`ResetForNextLevel` 入口并增加可观察断言。

## 9. 预计修改面

- `src/AbilityKit.Game.Cooking/CookingFrontOfHouse.cs`
- 新增或就近放置前厅 snapshot/checkpoint 类型
- `src/AbilityKit.Game.Cooking/CookingLevelCheckpoint.cs`
- `src/AbilityKit.Game.Cooking.EtRuntime/CookingLevelEtHost.cs`
- `src/AbilityKit.Game.Cooking.Tests/CookingFrontOfHouseTests.cs`
- ET checkpoint/recovery 与可复用闭环验收测试
- `.trellis/spec/cooking/`、`Docs/design/CookingGame/progress.md`、`Docs/Todo.md`
