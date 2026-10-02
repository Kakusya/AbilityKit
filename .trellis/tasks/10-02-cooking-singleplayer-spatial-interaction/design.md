> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S01 初始设计

状态：draft；未实施。

## 本次审议：现有事实与规划边界

核对对象是根工作树的主分支代码，不是已停止 worker 的未合并代码。`CookingPlayerConfig` 当前记录能力及静态 `ReachableStations`；`CookingRecipeSimulation.Pickup` 对工位物品检查该名单，对普通 WorldPosition 物品没有角色几何距离检查；容器可达性也沿用静态位置类型判断。现有拿放及容器转移具有版本、scope、手槽、占位和原子提交校验，但不能据此宣称已有连续移动、朝向、障碍或交互目标选择。

代码证据：`src/AbilityKit.Game.Cooking/CookingDomain.cs`、`CookingRecipeLoop.cs`；宿主入口是 `src/AbilityKit.Game.Cooking.EtRuntime/CookingLevelEtHost.cs`。S01 扩展既有 RecipeSimulation 的状态和校验，复用宿主固定 Tick，不新建第二个移动或厨房 authority。

## 细节规划（owner 已授权助手补足；仍未批准实施）

1. 放置位保持单槽，角色位置不吸附到台面格子。逻辑坐标使用整数子单位，固定 Tick 按配置速度推进；这支持自由走位，不要求一次移动一个格子。坐标尺度、速度、半径和距离均是可调参数，不当作正式平衡数值。
2. 记录每名逻辑玩家的位置与非零朝向。停止移动保留最后朝向；初始朝向由配置提供。移动指令表达方向，禁止客户端直接写最终位置。斜向速度须与直向一致，整数舍入规则固定。
3. 世界放置位和工位具有逻辑锚点，设备实体区域提供阻挡。面对目标、距离范围及中间障碍共同决定可操作性；配置了空间却缺少目标锚点时拒绝，不退回无限可达。
4. 同 Tick 沿用现有稳定命令排序；角色与障碍、角色与角色不能重叠。遇到阻挡保留最后合法位置，允许确定性的沿边移动；不自动推开队友。窄道先后通过可以成立，但冲刺、投掷、接取均不进入 S01。
5. 只读预览与执行使用同一目标解析规则：先剔除不可达目标，再按朝向偏差、距离、稳定目标 ID 排序。执行时重新解析和校验，不能信任较早的预览。预览不修改物品、进度、命令序号或 allocator。
6. 统一主交互：空手面对物件优先拿取；持物面对空普通台面放下；面对兼容容器尝试添加。满台面、不兼容容器、其他玩家手中的物件均给出拒绝原因，不默默换到另一个台面。盛装和分装的动作方向由 S03 收敛，不在 S01 偷加。
7. 每次移动和交互经既有 scope、序号、幂等与版本检查。交互拒绝不得改动任何状态；合法移动产生的位姿变化是明确提交结果。多人指多个本地逻辑参与者，测试不需要网络。

## 跨层与恢复约束

位姿进入 snapshot/canonical/hash；空间配置进入 configuration identity。新增命令字段须进入领域及 ET 指纹，不能只有 DTO 能读取。恢复 checkpoint 应重建位置、朝向、锚点及障碍配置并校验身份一致；不合法的重建一次拒绝，不留下半恢复状态。成功进入下一小关使用下一关出生位，物品与经营成果仍遵循既有成功交接规则；失败重开采用当前关标准初始状态，不引入任意时刻存档。

旧无空间 fixture 的兼容路径必须显式标为测试兼容，不能用于新产品关卡的验收。旧行为回归保留；新关卡必须有完整空间配置。具体 schema/checkpoint 版本和迁移诊断待实施前统一审议，不采纳未合并分支自报的 v3/v4 作为主分支事实。

## 可观察验收矩阵

| 场景 | 预期结果 |
|---|---|
| 不吸附格子的移动、斜向移动、停止 | 位置连续细分；斜向不加速；停止保留朝向 |
| 面前/身后、范围内/外、隔墙目标 | 预览与实际执行一致；不可达时拒绝且不搬动物品 |
| 空台面与已占用台面 | 普通台面一个物件；满槽无覆盖 |
| 多名本地逻辑玩家拿同一物件 | 仅一人成功；无复制、消失或双持 |
| 同 Tick 两人走向同一空隙 | 按既有稳定排序提交；无角色重叠；可确定性重放 |
| 预览后物件被队友拿走 | 执行重新校验；明确 stale/不可达拒绝 |
| 容器换人搬运 | 成分及加工状态不变；目标可达性重新计算 |
| 非法输入、重复命令、旧 scope | 拒绝或幂等返回；snapshot/hash/allocator 无意外变化 |
| 销毁重建与同序列重放 | 同 Level 位姿与状态一致；下一 Level 使用新出生位 |

这些规则的操作手感仍需 U01 场景验证。无界面 .NET 验收只能证明逻辑和确定性，不能宣称高亮、画面可读性或移动体感已经达标。

朝向/距离/障碍改变候选，空手/持物解析稳定；拒绝零变更；普通手槽与台面单物件。

沿用 [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md) 的 owner 与数据边界，不创建平行模拟，不修改通用框架的产品职责。具体 API/数据形状、错误矩阵、版本迁移、恢复/跨关状态与测试断言在执行前补齐；本初稿不声称已达到实施就绪。

## Worker implementation refinement 2026-10-02
Current dispatch and execution.md authorize implementation; reviewed core-review/final-review contracts control. One coordinated definition-v3, recipe schema3 and Level format4; no implicit old-format migration. Ownership remains existing authority partials, configuration/checkpoint, ET ingress fingerprint and behavioral tests only. Movement direction uses max 1000 components, configured speed, integer floor Euclidean normalization; zero direction keeps position and zero facing keeps last facing. At most one translational movement per player per logical tick; blocked diagonal tries X then Y deterministically. Square player footprints and closed obstacle tangency are explicit. World and Station anchors are single slots; Drop gains WorldAnchor and ET fingerprint. Preview chooses one main operation per target with facing cosine priority then distance and ordinal ID. Success handoff installs destination spawn poses, same-Level restore preserves validated poses. Manual worker and portion/error matrices follow core-review, and completed multi-yield Pour rejects even after serving down to one.

### Concrete payload / error contract
Move(Scope,Batch,Player,Command,MoveX/MoveY in ?1000,FacingX/FacingY in ?1) rejects MovementBlocked for absent Spatial, swept/boundary/player collision or an already accepted translation in this logical tick; zero facing preserves prior facing. Drop adds WorldAnchor:string? mutually exclusive with Station; a configured unoccupied reachable World anchor is required. Missing/configured anchors reject rather than granting reach. ContinueProcess/StopProcess identify Process; absent process ? ProcessNotFound, conflicting/automatic/non-owner claims ? WorkerUnavailable, unreachable Continue ? TargetOutOfRange. ServePortion identifies source Item, target Container and ExpectedItemVersion; stale/locked ? ItemStale, absent container ? ContainerNotFound, incomplete/empty source ? ProcessNotComplete, complete target/source edit ? BatchCompleted, insufficient capacity ? ContainerFull, incompatible product ? ContainerRejectsItem; every endpoint revalidates. ClearContents/DiscardItem identify Item/version; active inputs reject ItemStale, nested clear/non-container discard distinctions use ContainerRejectsItem. Allocator empty/duplicate or arithmetic overflow throws before commit, preserving complete checkpoint and counters. Shape/scope/lifecycle/dedup errors remain existing shared admission semantics.

Ordinary world tables are single-object slots; existing clean-container pool is a managed dispenser with separate stock semantics, mandatory spatial anchor and no ordinary Drop entry. Preview isolates execution exceptions inside its private sandbox and never consumes the production allocator. Successful preparation migration clears all manual workers, including unaffected non-station tasks, while preserving progress. Reviewed old Drop lock semantics are retained (only active container Pickup is exempt), so this task does not invent direct hand-to-hand exchange.
