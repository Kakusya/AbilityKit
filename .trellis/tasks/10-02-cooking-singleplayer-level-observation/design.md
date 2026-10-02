> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S14 初始设计

状态：draft；未实施。

## 本次审议：单机完整出口

前置 S05/S06/S07/S08 及 S09–S13 全部内容批次。关卡 Ready 前验证每条菜单供应→共享准备→工序→工作容器→出餐容器→交付闭包，包含实际数量、设备能力和可达工位；缺项报告菜品ID/节点/原因，禁止只查最终Recipe已注册。

只读提示投影来自同一authority：玩家身份/手持、材料状态、容器成分和份数、设备进度、订单编号与绑定、前厅工作认领、库存/在途、营业/收尾。提供稳定图标键，不增加UI账本。练习以配置控制有限可重放场景，不引入奖励经济。固定伙伴、评分和跨关规则保持回归。

实际出口为 ET 命令→固定Tick→可观察提示→自然收尾→成功下一关，覆盖完整87菜路线、关卡许可交集、错误恢复、旧scope拒绝、同Level销毁重建继续全态等价与失败重开无采购/认领泄漏。最终门禁必须覆盖集成后行为；各辅助类focused通过不是完整出口。

单机 ET 命令→Tick→提示数据→营业收尾可重放；checkpoint 重建等价；已解锁与本关允许取交集。

沿用 [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md) 的 owner 与数据边界，不创建平行模拟，不修改通用框架的产品职责。具体 API/数据形状、错误矩阵、版本迁移、恢复/跨关状态与测试断言在执行前补齐；本初稿不声称已达到实施就绪。


## 精确出口研究与接入顺序（2026-10-02）

完整数据复用、最小API、许可交集、受信可用性和真实营业验收方案见 [s14-runtime-exit-research.md](../10-02-cooking-gameplay-menu-plan/research/s14-runtime-exit-research.md)。Readonly observation从既有Recipe/Front/Supply/layout owner同一完整帧捕获，不再建业务owner；容器内容和订单绑定只按现有实例关系派生，当前worker使用ActiveWorker，finite库存从未移除真实物件推导。许可检查不把全部菜单原料数量求和当准备库存门，不因缺料/未满足新增正常业务失败。

分阶段先做UnknownMenu/逐菜缺项/基础授权加unlock与LevelAllowed交集，再等S06/S07/S08 stable接口接实际Prepare前验证与Observe。最终F01+D31代表经营fixture覆盖自然未满足离开/清桌/0星成功、同Level销毁恢复、成功在途承接、技术Failed基线重开，87内容另由逐菜真实生产交付fixture覆盖。观察或catalog验证辅助通过不等于S14完成。


## S14 exact Host Ready and runtime permission contract

Keep Recipe/Front/Level owners and one ET fixed tick. The trusted optional preparation factory supplies a frozen catalog and per-Level menu policy; availability comes from the actual owned Preparing kitchen. Ready rejection must leave phase, inventory, layout, process claims and clocks unchanged. Diagnostics identify menu/node/relation. Loaded ContentProvenance.SelectedMenus describes the available graph range; current SelectedMenuIds must be a subset of that range with matching catalog identity, not equal to the whole loaded range. Current Front order templates must match current operating menus.

Effective material authorization is (BaseAuthorizedMaterialDefinitions union ConfirmedMaterialUnlocks) intersect AllowedMaterialDefinitions. Unlocks are trusted factory input; commands and saved payloads cannot grant them. New supply requests/takes and new manufacturing must respect current material and selected recipe closure. Already approved in-flight reception and recovery operations remain available. Carried forbidden objects can be picked up, moved, removed and cleared; their existence does not authorize new production. Existing processes retain progress. BindingCommandsEnabled gates Bind/Unbind/Rebind without adding a physical ticket station or changing S05 command shape.

Recipe equality includes inputs, output, process, capability, carrier, execution, completion, yield and ticks. Ready checks actual sources, compatible containers and eligible reachable players; temporary occupancy or finite stock shortage is not a structural business failure. Keep pure validator separate from trusted Host wiring. Level checkpoint adds required nullable MenuConfigurationIdentity and bumps published format7 to8 only with its actual implementation. Saved identity is comparison evidence, never a policy grant; trusted per-scope reconstruction remains required.

## S14 generation transaction contract

CreateSuccessor accepts explicit nextPreparation for trusted preparation factories. Validate factories, geometry, next trusted seed poses, handoff and Front configuration in an unpublished candidate before committing. Private copies do not tick or create a second authority. Carry inventory, portions, process progress, finite supplier state, remaining delivery time and product allocator; clear order bindings, workers and generation command watermarks. Never use source live player poses as next geometry seeds.

Acquire ownership and check candidate match scope before mutating a factory-returned kitchen. Do not close or release a foreign-owned kernel. Factory/acquisition/geometry/tree publication failures leave source Observe, lifecycle, queue and actual prior ET tree disposition unchanged and permit a later retry. Successful publication alone clears old queues/dedup and releases old ownership. Retry restores trusted standard stock and confirmed choices; failed-generation purchases, pending deliveries, claims and transient layout do not leak.

## S14 typed durable baseline contract

CookingMajorCheckpointStore keeps legacy format1 APIs but adds typed format2 ReadBaseline/WriteBaseline. Payload contains source and target scopes, next preparation, configuration and preparation/front/menu identities, installed layout with seed poses, typed normalized SuccessHandoff Recipe and locked choices. Required nullable fields must be explicitly present. Hash verifies integrity, not authorization. Host must reconstruct trusted policy and validate before publication; a baseline is not an arbitrary Running save.

Write validates, stages .next, reads back, then replaces the prior file; IO failure retains the previous baseline. Do not claim fsync or power-loss guarantees. Store-level tests do not prove Host restart. Success baseline and in-memory generation publication need an explicit failure contract before connecting them; retries do not overwrite the last success baseline.

## S14 natural operating acceptance

Use actual finite supply, catalog-driven F01+D31 preparation, two players, manual handoff, automatic processing, unbound finished cup and independent binding. Front opens real orders; do not use OpenOrder/BeginEnd shortcuts as natural operating proof. Unmet customers leave naturally, new orders stop, tables clear and normal zero-star service may succeed. Zero stars use ScoreThresholds, not an invented delivery count rule. Compare uninterrupted, replay and mid-run dispose/restore frame state and canonical results.

Long manual test chains must change a trusted test catalog copy and recompute provenance/content identity, not change runtime ticks alone or weaken Ready equality. Original 87-menu source artifacts remain unchanged. Full S14, durable Host restart and scope-narrowed permission acceptance remain open after the reviewed transaction and validator increments.

### Trusted menu configuration installation window

ConfigureMenuPolicy is construction/configuration, not an ordinary gameplay action. Use the existing geometry installation boundary: not lifecycle-closed, owner authority mutation open, no reentry, and lifecycle absent or IsLayoutInstallationOpen. Thus a bound Created or Preparing kernel can receive its frozen trusted policy before publication; Running/Paused/Ended cannot configure it. Ordinary gameplay admission remains unchanged. Once installed, a different policy cannot replace it; current-scope factory reconstruction installs policy before saved state is restored. This resolves the actual Host Created gate conflict without publishing an unconfigured kitchen or advancing the lifecycle to evade validation.

## Durable Host publication refinement (candidate, 2026-10-03)

The historical typed format2 store proof above does not save the monotonic Host frame sequence. The current candidate requires HostFrameSequence in the payload and canonical integrity text and bumps typed baseline2 to3; formats1 and2 reject explicitly through ReadBaseline. Definition3, Recipe5 and Level8 are unaffected. Cold restart must equal live successor Created Observe and subsequent complete checkpoint, including the Host clock.

The durable success overload accepts explicit next preparation, locked confirmed progress and the existing store. Validate the actual next factory kitchen, ownership, match, frozen policy/Front/geometry, normalized handoff, trusted choices and lifecycle counter/event publication before IO. Install the candidate ET tree with rollback and exercise all ordinary failure injectors before the file replacement. Successful WriteBaseline replacement is the commit point. Before that point, rejection retains source lifecycle/tree/queue and old baseline bytes and releases only the private acquired kernel. Immediately after accepted replacement set baselineCommitted; publish the prevalidated lifecycle and Host bindings. Do not convert a subsequent exceptional failure into an ordinary rejected rollback or close the published authority. Prepared lifecycle event capacity is reserved before IO; this does not claim all subsequent Host bookkeeping is allocation-free. No journal, new authority, fsync or power-loss guarantee is introduced. Retry never writes the success baseline.

Load reads typed format3, then reconstructs and freezes trusted current configuration, preparation, scoped Front/menu and confirmed choices once. ICookingConfirmedMajorChoicesGameplayFactory supplies confirmed choices independently of the saved payload. Without that provider only locked empty decoration/unlocks and CookFaster=false are accepted; rehashed saved data cannot grant a buff or unlock. Ever-unlocked materials may persist outside current LevelAllowed; actual production still uses the established authorization intersection. Current trusted menu confirmed unlocks cannot exceed independently confirmed choices.

Created baseline poses are normalized next factory InitialPoses projected through the saved layout and current trusted geometry. Both saved installed seeds and normalized handoff poses must equal that projection, including facing and initial movement watermark. A different legal position with a recomputed hash is not an authorized spawn. This rule does not apply to same-Level Running checkpoints, whose saved live poses intentionally differ from initial seeds. Install geometry before accepting the handoff. Acquire before mutation; foreign-owned kernels remain untouched. Publish a Created host with the saved HostFrameSequence, never implicitly Ready or Running. Ordinary prepublication factory failure returns structured InitializationFailed with cleanup; valid loading remains possible afterward.

Producer focused tests and independent review are incremental evidence. Actual natural Ended success to durable next-generation cold restart, scope-narrowed carry and full integration/master gates are still required before S14 completion.
