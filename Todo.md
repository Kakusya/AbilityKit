# 当前 ET 接入状态与后续工作

> 文档定位：本文件是当前 ET/Cooking 工程工作的交接与待办入口，不替代架构决策、Cooking 行为规范或测试证据。
>
> 权威来源：长期路线见 [`Docs/design/CookingGame/technical-roadmap.md`](Docs/design/CookingGame/technical-roadmap.md)，跨阶段状态见 [`Docs/design/CookingGame/progress.md`](Docs/design/CookingGame/progress.md)，稳定行为合同见 [`.trellis/spec/cooking/`](.trellis/spec/cooking/index.md)，实际验证证据见对应 Trellis task 的 `check.jsonl` 与归档研究记录。

## 当前结论

ET 已从旧 Demo 依赖中提炼为独立内部运行时，并通过真实 `UpdateSystem` 接入 Cooking 的权威命令执行路径。目前 ET 负责实体生命周期、owner-thread 约束和显式 Tick 调度；`CookingRecipeSimulation` 仍是唯一的游戏规则与权威状态 owner。

这不是完整 Cooking 迁移：Match 生命周期、统一网络入口、自动固定加工时钟、checkpoint 恢复、跨小关延续、失败重开、工位升级迁移、成功结算持久化和 ECS 清退仍未完成。

## 已完成

### 独立 ET runtime

- 内部 UPM 包：[`Unity/Packages/com.abilitykit.et.runtime/`](Unity/Packages/com.abilitykit.et.runtime/)
- .NET 编译入口：[`src/AbilityKit.ET.Runtime/AbilityKit.ET.Runtime.csproj`](src/AbilityKit.ET.Runtime/AbilityKit.ET.Runtime.csproj)
- 显式宿主：[`EtRuntimeHost.cs`](Unity/Packages/com.abilitykit.et.runtime/Runtime/EtRuntimeHost.cs)
- 已覆盖 Entity 所有权树、EntitySystem、Scene/Fiber、显式 Tick、递归销毁、EntityRef 代际、对象池复用、ETTask 帧末恢复、宿主销毁与重启。
- 当前限制：同一进程只允许一个 live host；宿主操作要求 owner thread；禁止 Tick 重入和跨线程直接操作。
- ET 派生代码受仓库内 ET License 约束，仅限内部使用，不能按一般开源 UPM 包传播。

### Cooking 最小 ET Tick 接点

- 应用项目：[`src/AbilityKit.Game.Cooking.EtRuntime/`](src/AbilityKit.Game.Cooking.EtRuntime/)
- 当前宿主：[`CookingRecipeTickHost.cs`](src/AbilityKit.Game.Cooking.EtRuntime/CookingRecipeTickHost.cs)
- 命令路径：

```text
Enqueue(CookingRecipeCommand)
    -> owner-thread FIFO queue
    -> EtRuntimeHost.Tick()
    -> CookingRecipeDriverUpdate.Update()
    -> CookingRecipeSimulation.Submit(command)
```

- `Enqueue` 不提前修改权威状态。
- ET `UpdateSystem` 在显式 Tick 内执行既有 Cooking 命令。
- 既有版本校验、命令 fingerprint 和幂等结果仍由 Cooking 领域层负责。
- 已验证拾取、开始加工、推进加工、产物生成、装盘、提交订单、重复命令不重复执行、空 Tick 无结果和 Dispose 后拒绝调用。
- 对应工作提交：`38d822271 feat(cooking): dispatch recipe commands through ET tick`。

## 当前架构边界

### ET 应负责

- Entity/Scene 的所有权和生命周期。
- 单线程应用宿主和固定 Tick 编排。
- 从稳定命令批次驱动领域权威入口。
- 阶段实体、工位实体和加工实体的生命周期投影。
- ET System 调度错误的显式上报。

### Cooking 领域层应继续负责

- 命令合法性和原子提交。
- 物品唯一位置、工位、容器、配方和订单规则。
- Item/Match/Player 等稳定领域身份。
- 版本、幂等、事件、快照和 settlement 语义。
- 成功延续、失败重开、升级迁移等业务规则。

### 禁止形成的双重权威

- 不在 ET Entity 与 `CookingRecipeSimulation` 中各保存一份可独立修改的物品/加工/订单状态。
- ET EntityId 不作为网络身份或存档稳定身份。
- 不把 Cooking 规则塞入通用 `AbilityKit.ET.Runtime` 包。
- 网络线程、Unity `Update` 或表现层不得直接修改权威模拟。

## 后续执行顺序

### 1. 建立正式 Cooking Match ET 宿主

- [ ] 在 `AbilityKit.Game.Cooking.EtRuntime` 中建立应用层 Match host，而不是继续扩展通用 ET runtime。
- [ ] 由宿主持有并编排 `CookingMatchLifecycle`、当前 `CookingRecipeSimulation`、ET Scene 和命令结果。
- [ ] 把 Preparing、Ready、Started、Ended 映射为明确的应用生命周期，不重复实现领域状态机。
- [ ] 明确 Restaurant 长生命周期与 Stage/Match 短生命周期的 ET 所有权结构。
- [ ] 保持本地玩家和远端玩家最终进入同一权威命令入口。

建议结构：

```text
CookingMatchEtHost
├── CookingMatchLifecycle
├── CookingRecipeSimulation
├── Restaurant Entity          # 跨成功小关保留
├── Stage/Match Entity         # 每个小关替换
├── Station/Process projection
└── Command ingress/results
```

### 2. 收敛为单一固定加工时钟

- [ ] 不再依赖“每个 Process 各提交一次 `AdvanceTicks`”推进全局时间。
- [ ] 为 Cooking 领域增加一次固定帧只推进一次全局 `LogicalTick` 的批量接口。
- [ ] 同一 Tick 内多个加工完成时使用明确、稳定的排序。
- [ ] Preparing 阶段暂停加工，Started 后从原进度继续。
- [ ] 验证空 Tick、暂停 Tick、恢复 Tick 和同时完成的确定性结果。

注意：当前 `AdvanceTicks` 每执行一次都会增加全局 `LogicalTick`；不能直接让多个 ET Process System 在同一帧分别调用它。

### 3. 建立可继续运行的 checkpoint

- [ ] 将网络/显示用 snapshot 与恢复用 checkpoint 分开建模。
- [ ] checkpoint 至少覆盖活动和已移除物品、加工、容器、订单、逻辑 Tick、命令水位、去重账本、事件序列、ID 计数器、Match/epoch/config identity 和 lifecycle 状态。
- [ ] 支持“导出 -> 销毁 host -> 重建 -> 继续运行”。
- [ ] 与不中断执行的基线比较最终 hash、生成 ID、版本、去重结果和订单提交次数。

当前 `CookingRecipeSnapshot` 不能单独证明恢复后可继续运行。

### 4. 落实已确认的小关规则

#### 成功进入下一小关

- [ ] 保留食材、半成品和未完成加工进度。
- [ ] 清理本关订单和 Stage 临时状态。
- [ ] 准备阶段暂停加工，新小关 Started 后恢复。

#### 失败重开

- [ ] 关闭失败 Match，创建新的 MatchId 和更高 epoch。
- [ ] 清空失败现场，不保存失败现场状态。
- [ ] 按关卡定义重新生成标准初始供应。
- [ ] 从上一次成功 settlement 后的长期进度重新开始。

#### 工位升级

- [ ] 使用显式领域迁移操作把未完成加工从旧工位迁移到升级后的工位。
- [ ] 保留 elapsed/progress，不通过销毁父 Entity 隐式丢失加工。
- [ ] 先提交领域关系迁移，再调整 ET Entity 所有权或投影。

#### 持久化

- [ ] 只在小关成功完成时应用 confirmed settlement。
- [ ] 失败重开和准备阶段修改不写盘。
- [ ] durable storage、进程崩溃恢复和磁盘原子性必须有真实存储实现与测试后才能宣称完成。

### 5. 接入 UDP 统一 ingress

- [ ] LiteNetLib 回调线程只复制、解码、验证并写入线程安全 ingress，不直接调用 ET host 或 Cooking simulation。
- [ ] ET owner thread 在固定 Tick 边界取出稳定批次。
- [ ] 保留 connection -> PlayerId 身份绑定，并校验 scope、MatchId、epoch 和 config identity。
- [ ] 本地主机玩家与远端客户端共用同一批处理和权威提交路径。
- [ ] 重复、乱序、过期和断开连接后的命令不得重复推进加工或重复提交订单。

当前 `cooking-udp` 回归通过不等于 ET 与 UDP 已经接合，也不等于两台物理 PC LAN 已通过。

### 6. Unity 验证与 ECS 清退

- [ ] 在允许的范围内先运行 Unity compile check；缺少 Unity 环境或被其他 Editor 占用时必须记录为跳过/受阻，不能记为通过。
- [ ] 若将来重新授权 Cooking Unity，Unity 只负责输入、资源、场景和表现，不重新实现权威规则。
- [ ] 盘点 `world.entitas` 与 `world.ecs` 的全部消费者、`.csproj`、`.asmdef`、manifest 和门禁。
- [ ] 只有消费者迁移、依赖清零、等价测试迁移和适用门禁通过后，才按批次退役旧 ECS。
- [ ] 不删除测试换取通过。

## 验证出口

每个后续 Trellis task 至少要区分：

- 实际通过的聚焦构建和测试。
- 实际失败及其是否为既有失败。
- 因环境受阻或未运行的 Unity、跨进程、两 PC LAN 和 durable-store 验证。
- 只证明既有 UDP 能力的回归，与 ET/UDP 集成证据。
- 路线和计划，不得表述为已实现能力。

当前已知证据与限制见：

- [ET/Cooking 路线归档验证记录](.trellis/tasks/archive/2026-09/09-17-cooking-et-runtime-roadmap/research/validation.md)
- [Cooking 当前进度](Docs/design/CookingGame/progress.md)
- [Cooking 技术路线](Docs/design/CookingGame/technical-roadmap.md)

## 当前不应宣称

在对应实现和真实验证完成前，不得宣称：

- 完整 Cooking 已迁移到 ET。
- ET Tick 已经是唯一自动加工时钟。
- UDP 网络输入已进入 ET owner-thread 调度。
- checkpoint 可重建后继续运行。
- 成功延续、失败标准供应或工位升级迁移已经实现。
- 小关成功 settlement 已经持久化到 durable storage。
- Unity 2022.3 编译已经通过。
- 两台物理 PC LAN 已通过。
- `world.entitas` 或 `world.ecs` 已满足删除条件。
