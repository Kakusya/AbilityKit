# Cooking 双人局域网网络纵切实现 PRD

> 状态：`in_progress`。本任务基于 `09-19-cooking-productization-network-slice-planning` 确认的架构决策，利用 `09-24-cooking-reusable-testing-harness` 落地的高层测试 Harness，实现 Cooking 首个真正的双人网络纵切（Host + Client 架构），完成双端命令 Ingress、固定 Tick 权威推进、状态快照/增量同步，并通过 `TomatoEggSoupScenario` 进行双端 SHA-256 哈希一致性验证。

## 1. 目标与背景

根据项目规划与架构决策：
1. **单一 Cooking 产品主线**：聚焦于 Cooking 做菜闭环的双人网络互通，不引入非 Cooking 特性。
2. **两名玩家模型**：包含 1 个 Listen Host（同时作为本地玩家参与）和 1 个 Remote Client。
3. **权威玩法模型**：Host 持有唯一的 `CookingRecipeSimulation` 权威状态与 Fixed Tick 调度；Client 仅发送命令（Command Ingress）并接收权威快照/增量进行本地投影（Projection）。
4. **统一网络传输抽象**：
   - 进程内抽象：基于 `InMemoryTransport`（已在测试 Harness 中就绪）；
   - 真实网络传输：基于通用 `AbilityKit.Network.Transport.LiteNet`（LiteNetLib reliable-ordered UDP 回环）。
5. **测试驱动验收**：通过可复用的 `TomatoEggSoupScenario`，在真实网络/管道拓扑下端到端执行番茄蛋花汤做菜全生命周期，并在各个关键阶段断言双端状态 SHA-256 哈希共识。

## 2. 核心需求与范围

- **双端网络协议与数据帧 (Packet/Wire Contracts)**：
  - 客户端握手与玩家绑定（Client Handshake & Player Assignment）；
  - 客户端做菜命令封包（Recipe Command Envelope: CommandId, Sequence, PlayerId, Operation, Item, Station, etc.）；
  - 权威命令执行结果通知（Recipe Command Result: CorrelationId, Outcome, Reason, SnapshotVersion）；
  - 权威全量快照与增量同步（Snapshot Baseline & Delta Broadcast）；
  - 通用 JSON 序列化与二进制帧编码（契约与 `CookingRecipeSnapshot` 兼容）。
- **客户端网络端点与状态投影 (Client Network Controller & Projection)**：
  - Client 端持有本地只读投影（`CookingRecipeSnapshot`）；
  - 暴露 `ICookingActor` 操作门面，将高层交互转化为网络命令异步发送；
  - 接收并应用 Host 广播的快照状态更新，保持与 Host 逻辑一致。
- **Host 端网络接入与权威分发 (Host Network Dispatcher)**：
  - 管理客户端连接建立、能力协商与 PlayerId 映射；
  - 将接收到的客户端网络命令在固定的单读时序队列（Single-Reader Ingress）中安全提交给 `CookingRecipeSimulation`；
  - 逻辑 Tick 推进后向所有已连接客户端下发最新的快照帧。
- **回环真实 UDP 拓扑适配器 (`LoopbackUdpTopology`)**：
  - 继承 `ICookingTestTopology`；
  - 内部启动真实 UDP 回环端口服务（Host 监听，Client 通过 `LiteNetTransport` 连接）；
  - 实现握手、命令排空与 `AssertStateHashConsensus` 共识断言。
- **业务场景验收**：
  - 在 `CookingReusableHarnessAcceptanceTests` 中新增真实网络拓扑回归测试：
    - 在 `LoopbackUdpTopology` 下完整运行 `TomatoEggSoupScenario`，断言 100% 达成状态哈希共识。

## 3. 非目标与约束

- **不引入 Unity**：本任务为纯 C#/.NET 运行时与网络切片，不依赖任何 Unity 引擎 API。
- **不新增业务规则**：沿用已收敛的番茄蛋花汤做菜配方与工位状态，不引入小关评分、星级或复杂失败规则。
- **不保留旧 Cooking UDP**：遵循 09-19 决策，旧的 `AbilityKit.Game.Cooking.Udp` 特化实现直接退役或旁路，统一基于 `AbilityKit.Network.Transport.LiteNet` 与通用 Transport 契约。
- **单 Level 内存生命周期**：首个网络纵切只针对单个 Level，不涉及关卡切换和持久化 SaveSlot。

## 4. 验收标准 (Acceptance Criteria)

- [ ] 基于通用 Transport / LiteNet 构建好客户端与服务端的通信管道与命令/快照封包协议。
- [ ] 实现 `LoopbackUdpTopology`，完整接入 `ICookingTestTopology` 规范。
- [ ] 在 `LoopbackUdpTopology` 下运行 `TomatoEggSoupScenario`，完成取碗、切番茄、打蛋、煮汤、倒汤装盘与订单交付。
- [ ] 双端在每个关键阶段（加工完成、倒汤、提交）均能排空同步并达成字节级 SHA-256 哈希共识（`AssertStateHashConsensus` 通过）。
- [ ] 所有单元测试与回归测试通过（`dotnet test src/AbilityKit.Game.Cooking.Tests/` 无任何失败）。
