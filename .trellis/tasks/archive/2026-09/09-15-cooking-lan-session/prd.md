# P1 LAN listen host/client

> 归档状态：本 task 已于 2026-09-16 按 `completed-limited-scope` 归档，Trellis status 为 `completed`；完成只覆盖现有 `check.jsonl` 支持的纯 .NET 受限交付。

## 当前状态与授权边界

- 本 task 已于 2026-09-16 归档，Trellis status 为 `completed`；最终语义为 `completed-limited-scope`，只覆盖 L01-L09 纯 .NET contract。
- 已实现且已验证的受限范围：**transport-neutral 纯 .NET session contract**。D1 仅完成 non-decisional transport spike report model；候选比较尚未运行。
- 此结果不表示完整 P1 LAN 已完成或可推进完整产品出口；但它足以按重划后的 pure .NET limited scope 归档。
- 当前可用前置：P0 的 T01-T08 纯 .NET authority、command、snapshot 与实际证据。P0 Unity T09-T10 未运行并已移入长期禁止的 future scope；它们不再是本 task、successor 或 LAN 后续的 blocker。
- 未完成的 non-Unity successor：D1 production transport choice、D2 地址格式/发现协议与连接策略、D3 host/disconnect/exit/save/leave/结算产品语义、D4 benchmark workload/thresholds、production adapter、同机集成、两台物理 PC LAN 验收与 benchmark gate。Unity 只见 prohibited future scope。
- 已实现且实际通过的合同验证：L01-L09 纯 .NET tests，覆盖 shared authority ingress、connection binding、handshake、baseline/epoch/sequence、dedup/queue/cancel/close/dispose、diagnostics 与 application-level transport loss。
- D1 candidate spike、L10-L12、production adapter、two-PC LAN 与 benchmark gate 均为未启动的 non-Unity successor。Unity 未运行且只见 prohibited future scope。

## 来源与可追溯性

来源快照：`.trellis/migration/legacy-cooking-changes/add-cooking-lan-session/`。完整文件哈希与目标映射见 `.trellis/migration/legacy-cooking-changes/manifest.json`。

## Non-Unity successor（未启动、未批准）

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
- D2 的地址格式、发现协议、连接策略与错误语义属于 non-Unity successor；仅 Unity UI/控件/可视化发现属于 prohibited future scope。
- D3 的 host exit、remote disconnect、save、leave、结算、reconnect 与 migration 产品语义。
- D4 的 workload、采样、性能阈值和 benchmark gate。
- P0/P1 Unity projection、asmdef、scene、EditMode、UI/控件与可视化发现只记录于 prohibited future scope，不是 successor 前置。
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

P1 non-Unity successor 若获新授权，仍需：host 同时承担服务端和本地玩家、真实远端客户端、共享权威 command queue、session-scoped identity binding、兼容 handshake、baseline-before-delta、epoch/sequence rejection、bounded ingress 与分层 LAN 验收。两 PC LAN 仍是完整 P1 必需出口；同机多实例不能替代它。

## Impact

- 当前准备范围只影响未来纯 C# 应用层 session/identity/queue/diagnostic contract 和 D1 spike 设计，不修改现有 P0 业务逻辑。
- 后续正式 protocol 源必须遵循 `Protocols/README.md` 的 Catalog/Wire 分离与生成检查；在 D1/D2 前不得创建 cooking wire schema。
- 依赖 ADR-0001 的 listen host 角色和 ADR-0002 的权威固定 Tick/状态同步基线；二者都不替本应用选择 transport 或 host exit 产品流程。
## 2026-09-16 受限范围收口

- Owner 已批准将本 task 的最终完成边界重划为：**L01-L09 transport-neutral 纯 .NET session contract（权威证据为 20/20；6 个 JSONL、50 条记录）**。该范围由现有 `check.jsonl` 支持，已于 2026-09-16 按 `completed-limited-scope` 归档，status 为 `completed`。
- D1-D4、production transport/wire、同机 socket、真实两 PC LAN 与正式测量移至 `Docs/design/CookingGame/successor-backlog.md`；Unity 移至 future scope。
- 旧 checklist 中未运行的 Unity、真实 LAN、production transport、正式内容、durable storage 或完整产品出口不再是本旧 task 的归档 blocker；它们也没有因此变成已完成。
- 本 task 的 `completed`/归档语义只修饰上述纯 .NET 受限交付，不表示完整 P1、Unity 可玩版本、真实 LAN 或完整 P0-P6 产品出口完成。
- 原始检查事件保持不变；closure 只记录 2026-09-16 的 owner scope 决定。
