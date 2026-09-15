## P1 施工范围

将 P1 `cooking-lan-session` 启动为受限的 `in_progress` 实施：只实现已获授权的 transport-neutral 纯 .NET session contract 与 D1 spike 的报告/准备模型；完整 LAN 交付继续保持 blocked。

## 实施内容

1. **任务状态与证据边界**
   - 使用 Trellis 任务入口将 P1 从 `planning` 启动到 `in_progress`。
   - 保留 `migration_status: blocked` 和完整 P1 的阻塞项：D1 production choice、D2/D3/D4、production adapter、Unity、L10/L11/L12、formal Cooking protocol schema 和 P2 解锁。
   - 更新 P1 checklist 与 check evidence，将本轮实际代码/测试与仍未执行的 LAN/Unity/benchmark 工作分开记录。

2. **纯 .NET session authority**
   - 在 `src/AbilityKit.Game.Cooking/` 新增 Cooking session contract/implementation，保持为无 Unity、无 transport library、无 Orleans、无 protocol schema 依赖的应用层代码。
   - 定义 `ConnectionId`、session descriptor、protocol/config identity、epoch、capabilities/policy、connection origin、lifecycle 和稳定 rejection taxonomy。
   - 以 host-owned `connection → session/world/match/player` binding 为唯一授权来源。客户端声称的 player/scope 只能与 binding 比对；有效 `CookingCommand` 的 scope/player 由 binding 生成后才进入现有 `CookingSimulation`。

3. **Handshake 与统一受控 ingress**
   - 兼容 handshake 验证协议 identity/version、config identity、capabilities 和 scope metadata；失败不创建 gameplay binding。
   - host-local 与 remote-in-process 都通过同一 session ingress 和同一 authority batch queue，最终调用既有 `CookingSimulation.ExecuteBatch`。
   - 添加有界 queue、queue-full、dedup/conflict、cancellation、connection close、dispose、resource-unbind 的结构化结果；网络/receive 线程概念不直接改模拟状态。
   - 不实现 reconnect、migration、host exit、save、leave、settlement 或实际网络 I/O。

4. **Baseline/delta contract**
   - 定义携带 scope、epoch、snapshot sequence、baseline reference、config identity 与 snapshot hash 的 baseline/delta records。
   - 实现 baseline-first admission 及 mutation-safe stale epoch、wrong scope/config、duplicate/stale sequence、gap、missing/mismatched baseline 的拒绝或 wait/unsynchronized 状态。
   - 仅实现合同状态机，不创建 wire packet、Catalog/Wire Schema 或 production transport adapter。

5. **结构化运行诊断与 D1 准备模型**
   - 新增可 JSON 序列化的 session diagnostics record/writer，覆盖 event、UTC timestamp、correlation、session/connection/player/command、epoch/sequence/baseline、state/reason/queue depth/direction。
   - 新增 D1 spike comparison report model/template，固定候选版本、许可、平台、配置、workload、framing、listen/connect、lifecycle/cancel、backpressure、diagnostics、host cost、结果与限制字段。
   - 不实际执行候选 transport spike，不选择 production transport，也不把模板/模型写成 spike evidence。

6. **纯 .NET 合同测试**
   - 在现有 Cooking xUnit 测试项目新增 P1 test class，并以 `CookingLanSession` Gate trait 标识。
   - 覆盖当前可验证的 L01–L09 合同：统一 ingress、绑定身份/foreign scope 拒绝、handshake、baseline-first、epoch/sequence、dedup/conflict、bounded queue/cancel/close/dispose/unbind、structured diagnostics JSONL round-trip。
   - 断言 before/after snapshot hashes、mutation/event count、queue/dedup 状态；不声称 same-machine 或 two-PC LAN。

7. **实际验证**
   - 运行 Cooking 项目 `dotnet build` 和 `dotnet test`，通过环境变量保留 session diagnostics JSONL 工件。
   - 运行 `git diff --check`。
   - 将实际命令、通过/失败结果和产物路径写入 P1 `check.jsonl`；明确 Unity、真实 transport、same-machine socket integration、two-PC LAN、benchmark、协议生成与现有 global gates 未运行及原因。

## 不做的工作

不编写 Unity、连接 UI、production transport adapter、正式 Cooking Catalog/Wire schema；不选 D1 production transport；不运行真实 LAN、两机验收或 benchmark；不解锁 P2；不归档 P1。