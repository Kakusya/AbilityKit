## P6：受限纯 .NET 基线测量合同

### 目标与状态
- 将 `.trellis/tasks/09-15-cooking-network-measurement/` 从 `planning` 启动为 `in_progress`，同时持续保留 `migration_status: blocked`。
- 仅修改 `src/AbilityKit.Game.Cooking/`、`src/AbilityKit.Game.Cooking.Tests/` 和 P6 Trellis 任务记录；不改 Unity、UPM 网络包、Orleans、通用同步协议、生成文件或生产传输。
- 本次实施仅覆盖 **N01、N04、N05 与 N10 的受限技术合同**。迁移元数据仍以 N01–N08 为 canonical planned verification；设计中新增的 N09/N10 将标为 supplemental future gates。不会以本次工作声称 N01–N10、完整 P6 或 LAN 已完成。

### 实现设计
1. **不可变 workload 与元数据**
   - 新增 Cooking 专用的版本化 workload、sampling window、fault profile、topology/endpoint metadata 与 report 模型。
   - 报告包含 workload/version/hash、configuration identity、protocol/build identity、明确 `InProcess` topology、endpoint role、环境字段、NIC 的 `not-applicable`/`unknown` 状态以及阈值 `UNSET`。
   - 采用 canonical JSON + SHA-256 创建 workload identity，避免把不同输入误判为可比较。

2. **复用 P1，而不修改 P1**
   - 添加外置 baseline runner，直接驱动 `CookingSessionAuthority` 现有的 handshake、queue、batch、baseline/delta、epoch/sequence API。
   - 测量 ingress-to-commit 延迟、队列深度、吞吐、p50/p95/p99、状态 hash、去重/错误计数和同步状态。
   - 不建立 socket/listen/connect，不选择 transport，不主张 TCP、production adapter 或两台物理 PC LAN。

3. **确定性的应用层 fake channel**
   - 围绕现有 `CookingSnapshotBaseline` / `CookingSnapshotDelta` 构造消息级测试通道，仅注入可追踪的 delay、jitter、loss、duplicate、reorder、disconnect。
   - 利用既有 epoch/sequence/baseline 验证：连续性失败时得到 unsynchronized/recovery-required；disconnect 后停止同步声明；重复消息不导致重复权威 mutation。
   - 不实现 stream framing、packet boundary 行为、重连或 rollback。

4. **诊断产物与优化门控**
   - 输出 Cooking 专用 JSON report、确定性 CSV 汇总和 JSONL event/fault trace。复用已有 Benchmarking 的计时、分位数、determinism digest 和 JSON 风格，但不修改其通用 core，也不以其缺少 workload/config/topology compatibility checks 的 comparator 作为 P6 通过依据。
   - 插值、local prediction 和 correction 都继续返回 `Blocked`：没有 owner artifact、实际范围、真实 LAN 证据和回退规则时，不实现、不启用，也不宣称优化结果。

### 测试、验证、文档与提交
- 新增 `CookingNetworkMeasurementTests`，带 `Gate=CookingNetworkMeasurement`，覆盖：
  - **N01**：固定 workload 的 schema、metadata、聚合统计与 in-process 标签可复现；不能冒充 LAN；
  - **N04**：delay/loss/duplicate/reorder 具有可诊断结果，且 authority 无重复 mutation；
  - **N05**：disconnect 和等价 read/write 模拟故障使同步停止并保持 unsynchronized；
  - **N10**：阈值为 `UNSET` 且无 owner 批准时，optimization gate 为 `Blocked`。
- 更新 P6 `prd.md`、`design.md`、`implement.md`、`task.json`、`check.jsonl`：记录 N01–N08 与 N09/N10 的归属差异、已实现的窄范围、确切执行命令、产物路径和未运行项目。
- 在 `artifacts/cooking-network-measurement/` 生成真实 JSON/CSV/JSONL evidence。
- 实际运行 Cooking build、完整 Cooking xUnit suite、P6 task validation、`git diff --check`；Unity、真实两 PC LAN、global gates 与 protocol/wire checks 如未运行，会如实记录原因。
- 验证通过后只提交 P6 代码、测试与 Trellis 任务记录；不 push，且不提交现有会话 plan 文件。

### 明确保留 blocked
- N02 两台物理 PC LAN；N03 stream decoder/framing；生产 transport、发现与连接流程。
- N06–N09 插值、局部预测、纠正/rollback、优化 A/B 与优化回退运行时。
- 任何默认 Hz、延迟、吞吐、队列、内存、分配或丢包阈值，以及任何性能达标声明。
- reconnect、host migration、Unity projection、结算/持久化语义变更与 P6 完整归档。