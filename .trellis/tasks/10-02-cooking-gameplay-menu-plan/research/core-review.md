# Research: S02 手工接续与 S03 分装恢复审议

- Query: 主分支已有语义、保留 core worktree 的实现候选与正式实施前缺口。
- Scope: internal；只读产品代码，不运行测试、不修改或合并代码。
- Date: 2026-10-02

## Findings

### 来源与边界

读取 `.trellis/workflow.md`、`.trellis/spec/cooking/index.md`、`Docs/design/CookingGame/reference/README.md` 和 S02/S03 design。当前主分支与保留 worktree 必须分别评价；以下 worktree 路径统一为 `C:/Users/Administrator/orca/workspaces/AbilityKit/cooking-core-s01-s03/`。所有 worker 改动只属于实现候选，无独立验证证据；自报 19 测试通过不能作为本报告的验收。

### 文件与代码证据

| 文件 | 作用与定位 |
|---|---|
| 主分支 `src/AbilityKit.Game.Cooking/CookingRecipeLoop.cs:1650,1703` | 原有 Pour；完成容器产出一个物件并消费容器内容，尚无单份分装与手工 worker。 |
| 主分支 `CookingRecipeLoop.cs:1511,1713` | 产品 allocator 前置递增 ID 水位，异常零变更需要补审。 |
| 主分支 `CookingConfigurationValidation.cs:70` | 正式 schema 为 cooking-definition-v2。 |
| 主分支 `CookingLevelCheckpoint.cs:85` | Level checkpoint 信封 format 3。 |
| worktree `CookingRecipeLoop.cs:57,58,144-148` | Execution/YieldPortions 和五个新操作候选。 |
| worktree `CookingSpatialInteraction.cs:167-180` | 离开释放 worker；Continue/Stop 的唯一认领校验。 |
| worktree `CookingRecipeLoop.cs:1045,1106,1444,1468` | 手工固定 Tick 门控，完成份数初始化，Start 绑定 worker。 |
| worktree `CookingPortions.cs:77-161` | 分装、清空与丢弃候选，阶段字典用于原子分装。 |
| worktree `CookingRecipeLoop.cs:1791-1794` | 完成态 Pour 改为转移所有 RemainingPortions，必须显式审议。 |
| worktree `CookingRecipeCheckpoint.cs:22,36,95,117,424` | 剩余份数、ActiveWorker、内部 schema 3、canonical、扩展恢复校验。 |
| worktree `CookingExtendedCheckpointValidation.cs:7-73` | 份数、姿态和 worker 恢复约束。 |
| worktree `CookingLevelCheckpoint.cs:85` | 信封升 format 4。 |

### S02 拟采用契约

保持 `CookingRecipeSimulation` 唯一厨房权威。`CookingRecipeConfig` 追加 `CookingRecipeExecutionKind Execution = Automatic`；手工工序显式 Manual，设备工序保持 Automatic。命令使用既有 `CookingRecipeCommand`，新增 `ContinueProcess(Process)` 与 `StopProcess(Process)`，scope/player/id/batch 保持既有 ingress 与去重，不引入第二时钟。

状态为无任务 → Start → Active(worker,elapsed=0) → 暂停(worker=null,elapsed保持) → Continue → Active → 完成并释放锁与 worker。一个工序至多一个 worker，一个玩家至多一个手工工序；不叠加加速。固定 Tick 仅在 worker 可用且达到物品和所需工位时增加手工进度；Stop/离开/换人不重置。自动设备不因玩家离开而暂停。原始 process.Player 保留开始者身份，ActiveWorker 才是当前操作者，完成不依赖开始者收尾。

错误矩阵：不存在 Process → ProcessNotFound；自动工序 Continue/Stop、他人 Stop、已被认领、操作者已有另一手工工序 → WorkerUnavailable；Continue 不可达 → TargetOutOfRange；重复同 command ID+payload → 既有缓存结果，无额外进度/事件；同 ID 不同 payload → 既有指纹冲突；关闭/暂停态遵循既有 lifecycle 拒绝。拒绝不修改进度、worker、输入锁、ID水位、canonical。

候选 ChangeWorker 与离开释放已符合主方向，但需正式测试：Stop 后同 Tick 谁可认领；转身导致不可达与离开同规则；搬动正在手工作业的容器后自动释放；开始者离开而新 worker 完成；Paused 的 fixed Tick 完全不推进。不要把硬编码站点许可当几何到达证明。

### S03 拟采用契约与必要冲突

`YieldPortions = 1` 显式配置，正整数；当前候选限制多份只用于 RetainInputs 完成容器。完成时 RemainingPortions=YieldPortions，分装 `ServePortion(Item=source,Container=target,ExpectedItemVersion)` 每次正好一份。源容器不消失；目标容量及配方成品定义兼容通过后才分配产品 ID；成功源份数减一，最后一份消费原加工输入并复位源容器。过程中的批次原料不是可额外 TakeOut 的成品，必须持续受 completed-container 规则约束。

ClearContents 保留容器、位置、identity 与 dirty 状态，删除内部可消费内容并归零 recipe/completed/portions；锁定加工输入拒绝；内部嵌套容器不能静默删除（候选明确拒绝，应记录先取出嵌套容器再清空的恢复通路）。DiscardItem 仅丢弃非容器物件，删除手持/内部索引，保留 tombstone；容器始终使用 ClearContents。

错误矩阵：源不是容器 → ContainerNotFound；版本不匹配 → ItemStale；未完成或份数0 → ProcessNotComplete；目标不存在/同源 → ContainerNotFound；两端不可达 → TargetOutOfRange；目标完成批次 → BatchCompleted；锁输入 → ItemStale；容量不足 → ContainerFull；成品类型不兼容/包含环/清空含嵌套容器/Discard容器 → ContainerRejectsItem。allocator 空 ID、重复 ID、溢出属于显式执行异常，必须证明领域状态与分配水位均未提交；不能只检验返回拒绝码。

**需要纠正的候选差异：** worktree `PourCompletedContainer` 把旧完成态 Pour 从一个成品改为整批 RemainingPortions。这与本轮“不要改变既有 Pour 语义”指令存在冲突，不能静默合入。建议保留旧完成态 Pour 的单个成品行为并让新 ServePortion 承担多份分装；完成态多份源的 Pour 可明示拒绝，避免整锅多份被旧动作消费。具体选择由主会话落为正式 design；本研究不自行修改契约。未完成混合物整体 Pour 仍按旧内容移动，不改变配方、位置或数量语义。

### 恢复与兼容审议

候选 snapshot/canonical/checkpoint 加入 RemainingPortions 与 ActiveWorker，扩展恢复检查包括0≤份数≤产量、完成容器保留完整输入多重集、worker 唯一且可用/可达。所有校验必须在 live owner 安装前完成，坏份数、坏 worker、输入缺失、姿态冲突均拒绝且原状态不变。配置身份必须包含 Execution/YieldPortions，ET 指纹必须包含新命令字段；网络编解码后置但不能遗漏这些数据。

候选内部 checkpoint schema 3 + Level envelope format 4 与主分支 format 3 不能混称同一版本。现候选只接受新 schema，旧信封拒绝，没有实现自动迁移。建议首轮明确拒绝旧格式、不猜测旧多份或 worker；如以后必须保留旧存档，在独立显式 migration 中以旧配置身份校验后映射 Automatic/Yield=1/worker=null，不能为历史完成容器补造多份。还需检查成功跨关 handoff 清 worker但保留半成品和剩余份数、失败重开按标准供应丢弃现场、同 Level 恢复则保留合法认领与进度，与固定伙伴规则相容。

### 正式验收必须补齐

1. 手工双人认领仲裁、暂停换人、自动设备离开继续、结束释放输入与worker。
2. 一锅三份分三次及最后一份争抢，产出守恒，拒绝容量/类型/版本的完整零变更。
3. 清空错误投料后可重新烹饪，丢弃手持物后可继续拿取；嵌套容器拒绝后有取出恢复通路。
4. 原有 Pour 回归和明确的多份规则，allocator故障不污染水位。
5. 导出→销毁→重建→继续与无中断 canonical 相同；篡改 worker/份数/缺字段/旧版本均原子拒绝。
6. 配置重复 Input 是多重集（饮品×2），配置校验与 runtime matcher 必须同时保留次数，不能 Distinct。

## Caveats / Not Found

- 没运行测试，没执行 git 操作；保留 worktree 代码不证明主分支具备这些能力。
- S02/S03 design 仍只有概述，签名/错误/版本/Pour冲突须由主会话同步到任务正式记录后实施。
- 不增加通用框架、Unity、价格、保质、烧焦、永久岗位或工序小游戏。
- 外部参考未用：本审议依据仓库代码、现有规范与当前 owner 决定。

## Follow-up: S01 全条款与 ET 覆盖审计

本节逐条对照主分支 S01 design 的细化要求；代码定位仍指保留 core worktree。只读，不运行测试。

| S01 条款 | 候选符合程度 | 证据和缺口 |
|---|---|---|
| 1 整数子单位、非格子移动、配置速度 | 部分 | SpatialInteraction.cs:7-17 用整数位姿和范围；Move:152-155 直接 `before + MoveX/Y`，没有速度配置，也未用 fixed Tick 方向计算位移。命令实际是受限任意位移，并非方向输入。 |
| 2 斜向归一、停止保留朝向 | 不符合 | RecipeLoop.cs:313-318 每轴限制±1000，允许(1000,1000)得到直向的√2距离。没有归一/舍入规则。Facing 必须非零并每次覆盖（SpatialInteraction.cs:158），停止时依赖调用者重新发送旧朝向，不是领域自动保留。 |
| 3 锚点、朝向/范围/障碍 | 大体符合 | SpatialInteraction.cs:79-87 缺锚点拒绝；ResolvePosition:111-129 递归容器位置，GeometryReach:132-141 用距离、正点积和视线段阻挡；配置站点锚点完整性在:27-32。朝向只有前半平面，不是更精细朝向偏差选择。 |
| 4 稳定碰撞与沿边滑动 | 部分 | Move:159-165 扫描障碍与队友矩形，阻止穿越；碰撞整体拒绝，没有沿边移动分量尝试。因此沿墙滑动不符合。角色采用轴对齐方形碰撞，PlayerRadius这个名字不能误述为圆半径。Corner/tangency 也阻挡，需明示。 |
| 5 同解析、排序、只读预览 | 部分且排序不符合 | Portions.cs:13-74 通过私有 checkpoint 克隆并执行同一命令校验筛选，原owner无状态提交；但排序是距离优先、点积其次，再target/operation（:71-74），design要求朝向偏差优先、距离其次。原点积不归一，距离越大点积也可越大，不等同角度偏差。 |
| 6 统一主交互、世界空台面 | 不完整 | Preview 枚举几乎所有操作而非唯一主操作；持物 Drop 只枚举 `_fixture.Appliances.Keys`（Portions.cs:46），没有 WorldPosition 空放置位目标。RecipeLoop shape 的 Drop 必须 Station，不能一般世界台面放置；不能用工位总数冒充普通台面能力。 |
| 7 scope/序号/版本与本地多人 | 部分符合、需真实ET证明 | 新命令复用 Submit 与固定批次；ET host.cs:86-89 指纹新增Move/Facing字段。当前测试是直接simulation Submit/SubmitBatch及AdvanceFixedTick，未发现新空间场景经ET TryEnqueue→Tick的测试。 |
| snapshot/canonical/hash | 符合数据接线候选 | RecipeLoop.cs:2123,2159 加Poses；ConfigurationValidation.cs:140-143,160 空间完整字段进入canonical；Checkpoint.cs:94,153,706 加Poses并恢复。尚未运行验证。 |
| 同 Level 原子恢复/非法位置拒绝 | 符合校验候选 | ExtendedCheckpointValidation.cs:26-38 验证所有玩家、朝向、障碍与互不重叠，缺锚点/工位双物件拒绝；恢复安装在校验以后。需要篡改位姿与边界真实用例，不能只测份数损坏。 |
| 下一Level采用新出生位 | 不符合候选 | RecipeCheckpoint.cs:331-350 ExportSuccessHandoff 清worker但保留Poses，后续restore:706安装它们，覆盖新fixture构造时InitialPoses。未发现ET handoff重写为下一关出生位，切换地图可因此恢复拒绝或携带旧位置。 |
| 旧无空间fixture仅测试兼容 | 候选没有产品强制 | LocationIsReachable:99-108 无空间WorldPosition仍无限可达；测试兼容可保留，但新产品关卡入口必须强制Spatial完整。ContentCatalog的可选spatial不能证明正式关卡要求已满足。 |

### 双端可达与预览的进一步风险

- CommandIsReachable（SpatialInteraction.cs:144-153）同时检查 Item、Container、Station，ServePortion 也检查源与目标（Portions.cs:85）。外部手持物解析到 handOwner 并拒绝他人操作；嵌套内容位置递归到父容器。此接线方向符合要求，但现 `Every_interaction_rechecks_geometry` 用一个远距离玩家和额外Station统一触发拒绝，不能证明只有源远、只有目标远、同源可达另一端隔墙的分别校验。
- Preview 的私有 simulation 共享原 `_fixture`，allocator未共享，正常调用不递增生产allocator；但 per-candidate Export/Restore/Execute 是O(候选×状态)且操作异常可能逃逸预览，不应把未跑异常测试当“预览永不会抛错”。未来只读统一解析器可避免通过试执行确定主交互。
- process Continue/Stop 候选没有Item/Container/Station payload，预览 distance/dot 得到0而不是process.Anchor坐标；因此它们会不当地抢占按距离排序的位置。主交互预览需显式解析process目标。
- 世界普通位置 `ValidateSpatialItemLocation` 只对StationSlot单槽检查，WorldPosition不检查已有占位（SpatialInteraction.cs:79-87）。因此“普通世界放置位一个物件”还不能证明；需要明确所有普通锚点单槽而非只有设备工位。

### 实际已有测试与未覆盖条款

`CookingCoreExpansionTests.cs:74-89` 覆盖旋转、远近、隔墙、扫掠阻挡和预览前后checkpoint；:92-104 覆盖SubmitBatch排序碰撞重放、预览物件被抢后重新拒绝；:107-121 使用参数化远距离拒绝；其余用例覆盖手工接续、自动设备、份数及checkpoint。这些是代码可见测试定义，非本次通过结果。

在 Cooking.Tests 的 `*Et*` / `*Host*` 文件中搜索 MoveX/FacingX/ServePortion/Spatial/Preview 没发现新增ET空间验收。需补真实ET ingress：同Tick不同输入顺序收敛；连续方向每Tick单次移动而非同Tick重复Move叠加速度；斜向等速与停止；滑墙；朝向优先预览；世界空台面拿放；双端reach；非法位姿恢复；下一关新出生位；新字段指纹冲突。无需Unity即可证明逻辑，但不得声称操作手感或高亮通过。

### S03 后续主会话裁定

2026-10-02 主会话明确：未完成 Pour 整体转移不变；完成态 YieldPortions=1 保持旧行为；多份完成容器 Pour 显式拒绝，只用 ServePortion逐份。候选 RecipeLoop.cs:1791-1794 的整批TransferPortions实现必须替换，并为三类Pour分别加回归断言。此裁定取代前节建议中的开放选择。
