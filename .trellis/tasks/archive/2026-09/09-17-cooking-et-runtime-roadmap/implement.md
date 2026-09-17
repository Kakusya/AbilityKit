# Implementation checklist

## 批准与当前状态

用户已批准四阶段计划并要求继续施工；task 为 in_progress。原先“只规划、不 start”的说明已过期。八项业务决定与未决语义见 prd.md；下列计划不代表已完成能力。

## 已完成

- [x] 保存业务地图、八项决定、未决问题、设计、依赖研究、Proposed ADR 和 context manifests。
- [x] 既有 Cooking 基线 57/57。
- [x] Demo 宿主 ET 生命周期探针 1/1（不是独立 ET 内核验证）。
- [x] CookingSnapshot.Items → Scene/Registry/Item 投影探针 4/4：Pickup 位置与版本、移除/EntityRef 失效、重放稳定性、Scene 递归销毁。
- [x] 修正新探针缺少项目引用、虚构 fixture/命令签名、AddChild 与 EntityRef API 不匹配。

## 短期施工顺序与出口

1. [x] 独立内核提炼（2026-09-17 完成）：`Unity/Packages/com.abilitykit.et.runtime`（原 ET core Share 70 文件 + sourcegenerator 标注，剥离 MongoDB/MemoryPack/CommandLineParser 标注，ET License 内部使用）+ `src/AbilityKit.ET.Runtime`（net10，Compile Include，无外部依赖）+ `EtRuntimeHost`（单宿主/单 owner 线程/显式 Tick/显式销毁）。测试 8/8（显式 Tick、销毁停止、队列隔离与递归销毁、池代际、阶段延续、上下文作用域、重启、拒绝重复宿主/重 id/重入/跨线程、ETTask 帧末恢复）。验证见 research/validation.md 第五轮；Unity 侧编译未验证（本机无 Editor，跳过不计通过）。
2. [~] Cooking 纵切最小接点（2026-09-17）：新增独立应用项目 `AbilityKit.Game.Cooking.EtRuntime`；`CookingRecipeTickHost.Enqueue` 只入队，ET `UpdateSystem` 在显式 Tick 内调用既有 `CookingRecipeSimulation.Submit`。已覆盖拾取、开始加工、推进加工、产物、装盘、订单与重复命令幂等。尚未实现完整 MatchLifecycle、自动固定处理时钟、UDP 身份绑定、checkpoint 恢复、成功延续/失败供应、工位升级迁移和成功结算持久化，因此不记阶段三完成。
3. 回归：修改 UDP 时运行 cooking-udp；核心/同步改动运行 core-stability；宿主装配改动运行 runtime-contracts；大范围迁移运行 regression。两物理 PC LAN 单独人工验收。
4. 清退：验证完成、依赖清零、测试迁移后分批提交删除范围。Moba/Shooter/Samples 后续单独处置，不作为当前探针 blocker；不删测试换通过，不在本轮删除 ECS。

## 当前交付边界

- `CookingRecipeSimulation` 仍是唯一权威 owner；ET Entity/System 只负责调度，不复制 Cooking 规则状态。
- 当前 host 是 owner-thread FIFO 队列，不是可从网络回调线程直接调用的并发入口，也没有实现 stable-batch 排序。
- 加工时钟仍由既有 `AdvanceTicks` 命令推进；空 ET Tick 不会自动推进加工。
- 未提供通用 checkpoint restore；当前 snapshot 不覆盖全部去重账本、tombstone、计数器与 lifecycle closed 状态。
- Unity asmdef 已与 unsafe 源码配置对齐，但本轮没有 Unity Editor 编译证据。

## 实际验证

命令、TRX 指针、通过/失败/跳过以及依赖告警统一见 research/validation.md。之前 regression 在 HFSM DefinitionJsonTests 的 2/47 失败保持失败，不以本轮聚焦测试替代。

## Rollback and scope

本轮已按用户授权提交代码；构建触碰的三个既有生成 DLL 已恢复，没有进入提交。任务归档仅表示本轮 ET runtime 提炼和最小 Tick 调度接点收口，不表示完整四阶段路线完成；后续阶段三/四应以新任务或 successor task 继续。
