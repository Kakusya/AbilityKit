# P1 LAN listen host/client：准备范围设计

> 完整 P1 LAN integration 仍为 `blocked` / **Draft / NOT ready to apply**。本设计的 transport-neutral 纯 .NET session contract 已实施并由 L01-L09 focused tests 验证；D1 仅完成 non-decisional spike report model，候选 transport 比较仍为 not-run。代码、测试、模型均不构成真实网络、Unity、协议或两 PC LAN 证据。

## Context

当前可复用前置是 P0 的纯 .NET T01-T08：权威 command path、稳定排序、幂等、canonical snapshot 与业务验收日志已实际验证。P0 Unity T09-T10 是 deferred，不能被写成 P0 未实现，也不能被误写为已完成。

本准备范围不依赖 Unity T09-T10，但未来 Unity/LAN integration exit 仍依赖它们。当前框架没有现成 cooking session/transport 实现；不得将历史 `SessionCoordinator`、`Local/Remote/Hybrid` 或通用 transport 模块当作应用层已选方案。

## 已实施的纯 .NET contract

实现位于 `src/AbilityKit.Game.Cooking/CookingSessionAuthority.cs`，通过 `CookingSessionAuthority` 围绕既有 `CookingSimulation` 建立 host-owned binding 和 queue seam；不向 P0 领域模型加入 transport 或 Unity 依赖。`src/AbilityKit.Game.Cooking.Tests/CookingLanSessionContractTests.cs` 对 L01-L09 使用 host-local / remote-in-process fixture、无真实 socket 的 application fault 和 JSONL diagnostic evidence 验证合同。

## 授权范围与完整出口

### 已授权、仍未实施

1. 定义 transport-neutral 的纯 .NET session descriptor、epoch、兼容性和 lifecycle contract。
2. 定义 host-owned connection binding、统一 ingress、baseline/delta、dedup 和 diagnostics contract。
3. 准备 D1 的可重复候选 transport spike 与比较产物格式。

### 仍 blocked/deferred

- D1 production choice 与 production adapter；D2 connection UX；D3 host exit/disconnect 产品语义；D4 benchmark thresholds。
- Unity、连接 UI、正式 Wire/Catalog schema、同机 integration、两 PC LAN、benchmark gate、P2 解锁。
- 本范围不改变完整 P1 的 `planning` status、`blocked` migration status 或下游依赖。

## 设计契约（future / not run）

### 1. Host authority owns connection binding

Session facade 必须拥有 `connection -> session/world/match/player` binding。客户端 payload 的 claimed PlayerId、session 或 command scope 仅用于验证其与 binding 一致，不能成为授权依据。未绑定、foreign identity、重复 binding 或 scope mismatch 必须形成稳定 reason code，不泄露不必要的其他 session/player 信息。

### 2. Session descriptor and lifecycle

descriptor 至少包含 session/world/match identity、epoch、protocol identity/version range、config identity/hash 与 capabilities/policy。最小状态为：

```text
Connecting -> Handshaking -> Bound -> BaselinePending -> Synchronized
                                             -> Rejected / Disconnected / Disposed
```

只有 `Bound` 后 command 才可进入 host authority ingress；baseline 完整安装成功前不得应用 delta。session close、cancel 与 dispose 必须停止新 ingress，并在后续实现中明确已入队 command 的结果；它们不定义 host exit UI/save/leave/结算/迁移产品流程。

### 3. Synchronization and command invariants

baseline/delta 都必须关联 session identity、epoch、snapshot sequence、baseline reference 与 config identity。old epoch、old/duplicate sequence、gap、missing baseline、不可应用 delta 必须拒绝或进入明确的 wait-for-baseline/unsynchronized 状态，不能静默覆盖较新状态或继续宣称 synchronized。

host-local 与 remote-in-process 未来均接入同一 ingress facade 和 authority queue。queue 依赖显式模拟批次与稳定排序，不依赖接收时间、线程、字典或 transport 到达顺序。dedup 的作用域是 session/player/command，记录结果摘要与 mutation/event 计数。

### 4. Structured diagnostics

runtime diagnostic event 的最小字段：

```text
eventType, timestampUtc/simulationTick, correlationId,
sessionId, connectionId, playerId, commandId,
epoch, snapshotSequence, baselineReference,
ingressOrSessionState, reasonCode, queueDepth, direction
```

Future contract tests 需能证明并输出：unbound/foreign identity、incompatible handshake、malformed/oversize、queue full、cancelled、closed/disposed、stale epoch/sequence、missing baseline、duplicate command、transport loss -> disconnected/unsynchronized 的结构化结果。P0 的 test evidence JSONL 不能代替这些 runtime logs。

### 5. D1 transport spike boundary

D1 spike 比较候选 TCP/UDP/library 的 framing、listen/connect、关闭/cancellation、backpressure、diagnostics、configuration、platform/license 与 host cost。它必须用固定 workload 和可机器读取比较报告保存版本、条件、结果、失败原因和限制。

spike 不是 production adapter，不自动选择 transport，不是 two-PC LAN acceptance。TCP 只以 stream segmentation/coalescing/order 测试 framing；delay/loss/duplicate/reorder 由应用层 fake message channel 注入，不能宣称 raw TCP packet reorder 对应用可见。

## Future validation matrix

| 分组 | 未来场景 | 必需产物 | 当前状态 |
|---|---|---|---|
| Contract | connection binding、compatibility handshake、baseline/epoch/sequence、queue/dedup/cancel/dispose | rejection taxonomy、compatibility report、state/event/hash/dedup/queue trace | authorized future / not run |
| D1 spike | 候选 transport framing/lifecycle/cancel/backpressure/diagnostic/host-cost 对比 | workload 定义、版本/license 矩阵、JSON/JSONL 比较报告、限制记录 | authorized future / not run |
| L10 | 同机 host/client integration | endpoint config、双方 structured logs、snapshot/message trace | deferred / blocked |
| L11 | 两台物理 PC LAN | 环境表、网卡/地址/防火墙、双方 logs、message trace | blocked; 不得由 L10 替代 |
| L12 | benchmark 与 D4 gate | workload、采样、p50/p95/p99、队列/错误率/内存 JSON/CSV、owner threshold decision | blocked |

## Risks

- 将 client-provided PlayerId 直接映射到 P0 `CookingCommand.Player` 会越权 → binding 必须在 host authority 层生成/验证 envelope。
- 将 P0 in-process adapter 等价性误读为网络身份安全 → P1 contract 必须独立验证 connection binding。
- 将 D1 spike 当作 transport 决策或 LAN success → owner 明确选择和 L11 仍是独立 blockers。
- 将 dispose/connection loss 当作安全退出/重连 → 仅记录 disconnected/unsynchronized，D3 前不扩展产品行为。
- 将 P0 业务证据 JSONL 当 runtime network log → 分离 evidence 与 session diagnostics schema。
