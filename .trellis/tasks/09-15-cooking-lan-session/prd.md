# P1 LAN listen host/client

## 当前状态与授权边界

- Trellis task status：`in_progress`；完整 P1 LAN exit 的迁移元数据：`blocked`。
- 已实现且已验证的受限范围：**transport-neutral 纯 .NET session contract**。D1 仅完成 non-decisional transport spike report model；候选比较尚未运行。
- 此结果不表示完整 P1 LAN 已完成、已验证、可归档或可推进 P2；完整 P1 仍为 **Draft / NOT ready to apply**。
- 当前可用前置：P0 的 T01-T08 纯 .NET authority、command、snapshot 与实际证据。P0 Unity T09-T10 明确 deferred，不阻塞本准备范围，但仍阻塞未来 Unity/LAN integration exit。
- 仍阻塞完整 P1：D1 production transport choice、D2 连接入口/地址发现 UX、D3 host/disconnect/exit/save/leave/结算产品语义、D4 benchmark workload/thresholds、production adapter、Unity 接入、同机集成、两台物理 PC LAN 验收与 benchmark gate。
- 已实现且实际通过的合同验证：L01-L09 纯 .NET tests，覆盖 shared authority ingress、connection binding、handshake、baseline/epoch/sequence、dedup/queue/cancel/close/dispose、diagnostics 与 application-level transport loss。
- D1 candidate spike、L10-L12、Unity、production adapter、two-PC LAN 与 benchmark gate 均为 deferred 或 blocked，尚未执行。

## 来源与可追溯性

来源快照：`.trellis/migration/legacy-cooking-changes/add-cooking-lan-session/`。完整文件哈希与目标映射见 `.trellis/migration/legacy-cooking-changes/manifest.json`。

## Draft / NOT ready to apply

> 完整 LAN 集成仍不可实施。当前已完成的范围仅限纯 .NET transport-neutral contract；后续不得创建 production transport adapter、Unity/连接 UI、正式 cooking wire schema，或把本轮 contract test、D1 model 或同机结果当成两 PC LAN 证据。

## Why

P0 已提供不依赖真实网络的纯 .NET 交互 authority seam：同一命令路径、稳定排序、幂等、canonical snapshot 和结构化业务验收证据均已存在。P1 需要在不预选 transport、不中断产品决策边界的前提下，先准备连接身份、兼容握手、baseline/delta、受控 ingress 与诊断契约。这样可以避免未来将客户端自报 PlayerId、默认 host exit 语义或某个未验证 transport API 偷偷固化到实现中。

## 获授权的纯 .NET 准备范围（future / not run）

- transport-neutral session descriptor：session/world/match identity、epoch、protocol/config identity 与 capabilities。
- host authority 持有的 `connection -> session/world/match/player` binding；wire payload 的 claimed PlayerId 只能做一致性检查，绝不能作为授权来源。
- compatibility handshake 与稳定 rejection taxonomy。
- baseline-before-delta、epoch/snapshot sequence/baseline reference 的可观察契约。
- bounded ingress、queue full、cancel、close、dispose、session/player/command dedup 与 resource-unbind 语义；host-local 与 remote-in-process 只接入同一个 queue seam。
- runtime structured diagnostic event schema 与 contract/spike 的可审阅产物格式。
- D1 transport spike：比较候选 TCP/UDP/library 的 framing、listen/connect、lifecycle、cancellation、backpressure、diagnostics、configuration、platform/license 和 host cost；输出比较矩阵，但不选择 transport，也不实现 production adapter。

## 已实现的纯 .NET contract 范围

`src/AbilityKit.Game.Cooking/CookingSessionAuthority.cs` 现在提供 transport-neutral 的 session descriptor、host-owned connection binding、compatibility handshake、统一 authority ingress、bounded queue、dedup、close/dispose、baseline/delta sequence state 和 runtime diagnostics。`CookingLanSessionContractTests` 已实际运行 L01-L09；产物与命令见 `check.jsonl`。

## D1 transport spike 准备（未运行）

`CookingTransportSpikeCandidate`、`CookingTransportSpikeObservation` 与 `CookingTransportSpikeReport` 固化 future spike 的机器读取报告结构。它拒绝 production-decision 标记；没有固定候选、没有执行比较、没有选择 production transport，也没有创建 transport adapter。

## 不在本准备范围、继续 blocked/deferred 的工作

- D1 的 production transport owner choice 与 production cooking transport adapter。
- D2 的连接入口、手工地址/发现、错误展示与 Unity/product UI。
- D3 的 host exit、remote disconnect、save、leave、结算、reconnect 与 migration 产品语义。
- D4 的 workload、采样、性能阈值和 benchmark gate。
- P0 Unity T09-T10 以及 P1 Unity projection、asmdef、scene、EditMode、cross-host validation。
- L10 同机多实例 integration、L11 两台物理 PC LAN、L12 benchmark、正式 Catalog/Wire Schema、协议生成与下游 P2/P4/P6 解锁。
- WAN/NAT/relay、严格 lockstep、账号平台和产品人数上限。

## 结构化诊断与未来证据契约

后续纯 .NET contract/spike 的 runtime diagnostic event 至少需要可机器读取地关联：

```text
eventType, timestampUtc/simulationTick, correlationId,
sessionId, connectionId, playerId, commandId,
epoch, snapshotSequence, baselineReference,
ingressOrSessionState, reasonCode, queueDepth, direction
```

每个 future 验收场景还应保存：compatibility report、before/after snapshot hash、mutation/event count、dedup counter、queue/cancel/dispose/resource-unbind trace、baseline/delta trace、sequence rejection/wait-for-baseline log、framing byte vectors/decoded artifact 与 application fault matrix。

P0 的 JSONL 是已执行的**业务验收**证据；它不能替代未来 host/client runtime diagnostic log、`.NET test` 退出码、两 PC 环境证据、D1 owner 决策或 D2-D4 产品决策。

## 完整 P1 的后续能力

完整 P1 未来仍需：host 同时承担服务端和本地玩家、真实远端客户端、共享权威 command queue、session-scoped identity binding、兼容 handshake、baseline-before-delta、epoch/sequence rejection、bounded ingress 与分层 LAN 验收。两 PC LAN 仍是完整 P1 必需出口；同机多实例不能替代它。

## Impact

- 当前准备范围只影响未来纯 C# 应用层 session/identity/queue/diagnostic contract 和 D1 spike 设计，不修改现有 P0 业务逻辑。
- 后续正式 protocol 源必须遵循 `Protocols/README.md` 的 Catalog/Wire 分离与生成检查；在 D1/D2 前不得创建 cooking wire schema。
- 依赖 ADR-0001 的 listen host 角色和 ADR-0002 的权威固定 Tick/状态同步基线；二者都不替本应用选择 transport 或 host exit 产品流程。
