> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S08 初始设计

状态：执行设计收敛，等待 S01/S06/S07 接口；不是已交付。

## 本次审议：准备态原子安装

沿现有 Level preparation owner，Created/Preparing 可提交完整布局，Ready/Running/Paused/Ending/Ended 拒绝。设备许可为默认基础授权加已解锁，与本关允许集取交集；占地与交互面来自定义，不信任调用方缩小设备大小。

布局包含地板区域/墙/设备旋转和占地/入口/队列/桌位/出餐/收货/仓储/清洗目标；扩建是可用地板集合，首轮不引入购买价格。一次候选中校验占位、角色半径净空、连通、权限、物件与工位迁移、工作引用及玩家保留或确定性重定位，再原子安装。失败厨房/选择/位姿/allocator/hash均不变，修正现有装修空旧选择回滚和解锁先写厨房风险。

成功更新真实S01障碍与锚点，后续旧位置操作失败、新位置成功。路径用相同尺度、半径和稳定邻居顺序；顾客入口→桌→出口及工作入口→必需设备均可达，不以格中心BFS代替半径净空。保留分支静态辅助尚未接入，不算功能已完成。

几何全量及hash进入checkpoint，先验证候选再安装和恢复对象引用。同Level恢复保持有效几何；跨关允许集变化需要合法新布局或明确拒绝，不偷偷删带食材容器。版本与核心/供应/前厅由同一协调者统一；每次正式字段集合变化显式版本化，不复用历史版本隐藏差异。

准备态移动/旋转/区域扩建；占位/交互面/顾客与玩家通路验证后提交；营业中拒绝修改。

沿用 [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md) 的 owner 与数据边界，不创建平行模拟，不修改通用框架的产品职责。具体 API/数据形状、错误矩阵、版本迁移、恢复/跨关状态与测试断言在执行前补齐；本初稿不声称已达到实施就绪。

## Scope / Trigger

在既有准备态与逻辑layout内表达房屋可用区域、设备footprint/旋转/交互面、入口/排队/桌位/出餐/供应/回收锚点。连续玩家移动复用S01，路径检查的离散格子只用于布局验证，不强制玩家棋盘步进。只在现有 Created/Preparing 阶段允许，不能新造独立关间准备产品对象。

## Signatures / Contracts

`CookingRestaurantLayout`：稳定layout identity、已启用floor区域、设备instance/definition、基准格与0/90/180/270朝向、墙障碍、顾客/玩家入口与必须可达交互锚点。`TryApplyLayout(candidate)`先全量验证占位、引用、权限和路径，再原子安装同一模拟空间/工位映射，不先搬真实物件后再发现非法布局。扩建只增加声明区域，不绑定未经批准价格和关号。

工作/顾客路由是同一有效地形的各自目标集合；BFS邻接序明确，角色实际pose/半径通路约束与S01一致。设备移动保留其物件和合法加工状态；manual worker清，station/id迁移保持snapshot/checkpoint索引一致。解锁可用内容与本关允许列表取交集，解锁不保证每关都可摆。

## Validation & Error Matrix

非准备态→InvalidState；非正面积/非法rotation/越界→InvalidLayout；definition未知/未解锁/本关不允许→DefinitionUnavailable；footprint重叠→OccupancyConflict；玩家不可达工位或顾客不可达桌/出口→UnreachableTarget；已有物件无可映射目标→拒绝，不删除或落到任意位置。所有失败保留旧layout/pose/物件/进度/hash。

## Good / Base / Bad

Good：准备态旋转设备、扩一块合法区域，保留备料且改善通路，之后原厨房继续营业。Base：同layout重复应用无重复搬物。Bad：设备堵住唯一出口/桌边，必须拒绝整次修改，不能只给提示但保留无路布局。

## Tests Required

非方设备四方向footprint与交互面、狭道/玩家半径、厨房分隔交接、入口队列桌到出口、进货到回收动线。多设备批量调换位置、带容器/剩余份的工位迁移、权限交集、营业拒绝、非法布局零变更、同Level恢复及成功承接。ET host准备操作改变真实后续Move/交互结果，不能仅验证独立编辑器数据。

## Wrong vs Correct

Wrong：layout编辑只更改一张grid，实际模拟继续用旧ReachableStations；顾客路径另用忽略设备的地图。Correct：验证后安装一份权威逻辑空间，各用途查询同一障碍与锚点，后续命令重新校验。

## Reviewed installation implementation contract (2026-10-02)

This section specifies the next executable increment; it is not a delivery claim. Existing static validation is already merged. S06 currently owns Level/host/checkpoint edits, so installation waits for its stable reviewed source; no parallel edit of those files.

- Existing fixture/configuration is immutable trusted content. Dynamic restaurant geometry must be a simulation-owned frozen override, separately identified by full layout canonical hash. Do not rewrite definition identity or treat a LayoutId alone as geometry. Initial override is the trusted factory geometry. All reach/movement/preview/restore checks read the same effective geometry.
- The existing factory Create(scope, configuration) does not receive preparation; merely storing CookingLogicalLayout cannot install geometry. Preparation must stage one kitchen and publish that same kitchen at Start. A trusted preparation/factory extension supplies equipment footprints, allowed definitions, geometry policy and targets; a caller cannot reduce actor radius or invent usable appliances.
- Project validated floor union, rotated rectangular equipment and walls into S01 bounds/closed obstacles. Floor holes and disconnected outer regions are actual obstacles: bounding rectangle alone would allow walking through unavailable area. Construct complement obstacles by occupied row intervals and gap slabs, bounded by MaximumCells, rather than enumerating a huge bounding rectangle between remote regions. Station anchors are the validated external interaction-cell centres, not inside blocked equipment; world targets are centres of validated target cells. Scale/radius/movement/interaction policy remains trusted.
- Candidate must contain every live station/required world-location reference. Moving a station keeps its StationSlotId and contents. Unknown definition-to-station mappings reject before mutation. Removing a referenced anchor, deleting occupied station or losing a dirty/filled container rejects; no implicit disposal. Explicit station replacement remains a distinct existing preparation action and must preserve its own atomic contract.
- Preserve valid current poses. Relocate only invalid poses deterministically using stable player order and validated reachable entrance-connected cell centres, checking S01 radius and mutual collision. If no placement exists, reject the whole candidate. Preserve movement watermark and clear manual-worker claims only after accepted installation.
- Validate every live item location/slot/hand/container index, process station/carrier and front-house table/queue/exit/wash routing against staged geometry. Front-house rebind uses S06 trusted policy, not checkpoint self-reported geometry. Layout changes only Created/Preparing; Ready/Running/Paused/Ending/Ended reject and remain unchanged.
- Commit kitchen effective geometry/poses, front-house geometry/routing and immutable preparation layout in one owner transaction. Configuration identity, slot/item/process allocator, orders and supplies remain unchanged unless their explicitly validated migration is part of that transaction. Repeating the same full layout is idempotent.
- Checkpoint includes required nullable full installed layout plus effective geometry identity; verify against trusted equipment/radius/permissions, and reconstruct before applying stored poses or front routes. Same-Level restore preserves; successful cross-Level admission intersects unlocked with next-level allowed definitions and rejects incompatible content without losing stock/in-flight supply. No new prices/unlock schedule.

Existing preparation atomicity defects to fix with this increment: ChooseDecoration changes progress before MigrateStations and cannot roll back an empty old selection; Unlock places the item before progress.Unlock can reject a locked progress object. Both need complete preflight/candidate staging and zero-change failure tests; do not merely hide the failure result.

Required proof: non-square rotations and holes/large sparse bounds; loaded station relocation changes actual old/new interaction outcome; invalid/no-room candidate leaves complete checkpoint and allocator unchanged; old decoration empty and locked unlock reject atomically; ET preparation then same-kitchen Start; customer and player route parity; same-Level export/dispose/rebuild and success carrying filled containers/in-flight stock. Static geometry tests alone do not close S08.
## 2026-10-02 preparation integration decisions

Preparation supports actual movement, material handling, manual and automatic preparation, and supply requests/arrival/receipt using the same ET fixed tick and the same kitchen instance later adopted by Start. It does not start customer arrivals, inquiry, ordering, binding or submission. This implements the previously approved preparation/stocking capabilities rather than an independent simulation or timer. Record the kitchen logical tick at service start; restore validates the front service clock against that offset, not against preparation time.

Candidate layout installation is temporary scene state until an explicit accepted major-decoration choice records a durable preference. Failure retries restore the existing successful baseline or standard supply and then accepted major preferences; they do not inherit arbitrary failed scene layouts, supply purchases or manual claims. Same-Level recovery preserves the effective installed layout and contents. Successful next-Level handoff retains the approved shared preparation subject to next-Level permissions; incompatible permissions block readiness rather than silently deleting objects.

Trusted equipment footprints and station capabilities remain application content. Requests choose positions and rotations of authorized instances; requests cannot invent equipment sizes, capabilities or permissions. Layout, projected poses and derived customer routes must pass preflight against all live item/process/front/supply references before a single installation commits. Evidence and detailed proposed interfaces: ../10-02-cooking-gameplay-menu-plan/research/s08-preparation-installation-research.md. This decision records intended behavior; it does not claim the installation APIs or preparing runtime are implemented.
