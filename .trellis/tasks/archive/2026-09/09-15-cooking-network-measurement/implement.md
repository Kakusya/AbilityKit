# P6 响应性与网络测量：实施清单

> 归档状态：本 task 已于 2026-09-16 按 `completed-limited-scope` 归档，Trellis status 为 `completed`；完成只覆盖现有 `check.jsonl` 支持的纯 .NET 受限交付。

> 以下未勾选段落是**只读历史规划**：不可执行、不是当前 task checklist，也不是 archived limited delivery 的 blocker。non-Unity 后续只见 successor backlog；Unity 只见 prohibited future scope。

## 迁移前置条件

- 依赖：P1-P5 的可运行纵向链路与真实测量 harness。
- 阻塞：性能目标、插值/预测与重连是否启用均待批准；不得用同机证据替代真实两 PC LAN。
- 规划验证：N01-N08（均为 future 规划，尚未执行）
- 实施前将 `.trellis/spec/abilitykit/index.md`、`.trellis/spec/abilitykit/validation.md` 与本任务对应 cooking spec 加入 context manifests。

## 当前受限实施状态

- [x] 1.1 已复核 P0–P5 的实际 pure .NET evidence、P1 baseline/delta/epoch/sequence/transport-loss API 与完整 exit blockers；本轮不把前序 limited evidence 写成 LAN 或产品毕业。
- [ ] 1.2 owner artifact：**blocked**。workload scale、sampling window、topology coverage、thresholds、interpolation/prediction/correction scope、fallback 和 reconnect 语义均未批准，所有 thresholds 仍为 `UNSET`。
- [x] 1.3/2.1 已实现受限 command ingress-to-commit baseline：immutable workload/hash、version/config/protocol/sampling/topology/environment/fault metadata，及 queue peak、throughput、p50/p95/p99、current-thread allocation 和逐 invocation authority state hash。snapshot byte decode/apply、网络 error-rate 和 process/native memory 仍为 future，不由本轮报告覆盖。
- [x] 2.2 已实现显式 `InProcess` fixture runner 和 JSON/CSV/JSONL artifacts；它不声称 socket、NIC、address/firewall 或真实两 PC LAN。
- [ ] 2.3 两 PC physical LAN runner：**blocked / future**。
- [ ] 2.4 stream segmentation/coalescing decoder：**blocked / future**；本轮 application fault 不表示 TCP framing。
- [x] 2.5 已实现 deterministic logical-tick application fake channel，覆盖 delay/jitter/loss/duplicate/reorder/disconnect；沿用 P1 baseline/sequence 结果，不产生 authority command mutation。
- [x] 3.1 已实现 baseline-only diagnostic report；不含 interpolation/prediction/rollback、settlement 或 progress writes。
- [x] 3.2 已实现 baseline permitted 与 interpolation/local-prediction/correction 的显式 prerequisite gate：threshold、owner approval、真实 LAN evidence、fallback policy 和 runtime 任一缺失均保持 `Blocked`；不包含优化启用逻辑。
- [ ] 3.3-3.5 interpolation/local prediction/evidence-gated reconciliation：**blocked / future**。
- [ ] 4.1-4.3 optimization A/B、authority-pollution diff、runtime fallback：**blocked / future**。
- [x] 4.4 已验证 `UNSET` threshold、缺失 owner approval/真实 LAN evidence/fallback policy 或未实现 optimization runtime 时只可输出 blocked result，optimization gate 不可通过。
- [x] 5.1 已确认 Cooking .NET project boundary；没有 Unity/asmdef/generated csproj 修改。
- [x] 5.2 已实际生成 N01/N04/N05 JSON/CSV/JSONL evidence，N10 在 focused contract tests 中验证 blocked result。N01–N08 仍是 canonical migration matrix；N09/N10 为 design supplemental gates，完整 P6 未执行。
- [ ] 5.3 Unity、两 PC、global gates 与 protocol wire check：**not run**，本轮未更改其范围；实际 Cooking build/test/task validation/`git diff --check` 见 check evidence。
- [x] 5.4 完整 P6 产品出口未完成并移入 successor backlog；本 limited delivery 已于 2026-09-16 归档，但不得以 report、同机或 logical faults 声称 LAN 或 optimization completion。

## 1. 前置证据与测量基线

- [ ] 1.1 核对阶段 1–6（P0–P5）的可运行垂直链路与实际实现、测试和门禁证据：P0 `add-cooking-interaction-foundation` 为传递依赖，P1 `add-cooking-lan-session` 与阶段 2–5 recipe/config/match/persistence 为本阶段直接依赖；验证：记录 baseline/delta、sequence、结算 identity、长期 revision 的证据指针；仅有 change 文档时阻断后续优化任务。
- [ ] 1.2 明确并记录 owner 决策门：workload、采样窗口、拓扑覆盖、指标阈值、插值/预测/纠正范围、失败回退及重连语义；验证：批准项写入 decision artifact，未决项为 `UNSET`/`Draft/Blocked`，不得使用默认 30Hz 或毫秒目标。
- [ ] 1.3 建立不可变 workload 与报告 schema，包含版本、配置/协议身份、fault profile、机器/网卡/拓扑、采样窗口和指标定义；验证：N01 可生成带完整元数据的 JSON/CSV 报告，当前为 future/未执行。

## 2. 分层网络与故障测量

- [ ] 2.1 建立 command ingress-to-commit、snapshot decode/apply、队列深度、吞吐、p50/p95/p99、错误率、内存/分配采样点；验证：N01 同一 workload 前后报告字段和采样定义一致，缺失数据可诊断。
- [ ] 2.2 建立同机多实例 host/client measurement runner；验证：N01/N02 产出 endpoint、实例角色、session/baseline/command/delta trace，并明确不构成真实两 PC LAN 证据。
- [ ] 2.3 建立两台物理 PC LAN runner，记录机器、网卡、地址、防火墙、host/client 角色及环境差异；验证：N02 完成跨 PC 流程或明确标记连接失败，不能降级为同机通过。
- [ ] 2.4 建立 stream framing 测试，覆盖同一 byte stream 的 segmentation/coalescing；验证：N03 decoded message 边界和顺序一致，不测试或宣称应用可见 TCP packet reorder；future .NET codec tests，未执行。
- [ ] 2.5 建立应用层 fake channel fault harness，注入 delay/jitter/loss/duplicate/reorder/disconnect；验证：N04/N05 按 epoch/sequence/baseline 产出 fault matrix、unsynchronized/recovery-required 状态和错误计数，不能产生重复权威 mutation。

## 3. 基线与可选表现优化

- [ ] 3.1 建立 baseline-only 客户端报告和回放路径，确认不依赖预测/插值即可保持快照顺序与权威正确性；验证：N01/N04/N05 的基础状态、结算 identity 和长期 progress revision 一致，future tests 未执行。
- [ ] 3.2 建立独立策略开关和 owner evidence binding；验证：无批准 artifact、范围或回退条件时 interpolation/local-prediction/correction 均保持关闭并报告 Blocked，不默认启用。
- [ ] 3.3 在批准范围内接入快照插值，仅改变表现取样；验证意图仅作历史记录；非 Unity 插值/对照若获批从 successor backlog 新建 task，Unity projection 验证禁止实施。
- [ ] 3.4 在批准范围内接入局部预测，仅覆盖具备历史/恢复证据的本地输入和状态；验证：N07 预测输入可追踪，权威快照最终决定状态；未批准对象/远端对象不预测，future tests 未执行。
- [ ] 3.5 建立证据门控的 reconciliation；验证：N08 仅在完整 Provider、历史、恢复和可重演输入证据存在时执行局部 rollback，否则等待/request baseline 或回到确认状态，不实施 blanket rollback。

## 4. 前后对照与安全回退

- [ ] 4.1 以同一 workload/环境运行 baseline 与每项优化并生成可比报告；验证：N01/N09 对照延迟分布、decode/apply、队列、错误、内存/分配及权威最终结果，报告阈值为批准值或 `UNSET`。
- [ ] 4.2 增加权威污染检测，比较命令提交、事件、Match settlement identity 和长期 progress revision；验证：N06-N09 断言表现层不能写入或改变权威/结算/存档。
- [ ] 4.3 实现数据不足、回归、fault 错误异常或权威不一致时的策略关闭与 baseline 回退；验证：N09 记录原因、标记优化不可用、恢复确认快照状态，不保留未经证明的预测/插值。
- [ ] 4.4 在阈值仍 `UNSET` 或 owner 未批准时交付诊断报告但阻塞优化 gate；验证：N10 可检查 decision checklist，不能以“looks fine”或报告存在宣称完成。

## 5. 跨宿主验证与交付

- [ ] **只读历史、不可执行** 5.1 检查拟建 cooking .NET benchmark、Unity runtime/projection 与 `.csproj` Compile Include/`.asmdef` 边界；验证：未来 build/test 命令覆盖两个宿主，不修改自动生成 `.csproj`，当前未执行。
- [ ] 5.2 保存 N01-N10 的实际 JSON/CSV、日志、fault matrix、before/after diff 和 owner decision artifact；验证：每项标记 pass、fail 或 skip 原因，规划测试不得冒充运行结果。
- [ ] 5.3 按影响范围评估并运行 `runtime-contracts`、必要的 `core-stability`/`regression`、协议 check 和 Unity/两 PC 出口；验证：交付报告列出真实命令与结果/跳过原因，当前阶段不运行测试。
- [ ] 5.4 在阶段 1–6（P0–P5）前置证据、同机/两 PC 报告、批准阈值、优化前后对照和回退证据齐全前保持 `Draft/NOT ready`；验证：最终 checklist 明确所有 blocker，所有任务保持未勾选。
## 2026-09-16 归档范围

- [x] 已按现有 `check.jsonl` 将最终交付重划为：**InProcess 纯 .NET measurement/fault diagnostic baseline（focused 5/5；完整回归 57/57）**。
- [x] production transport 测量、真实两 PC LAN、批准阈值和非 Unity 优化后续移至 successor backlog；Unity 移至 future scope。
- [x] 已确认未运行项不再属于本旧 task 的完成条件，且没有被改写为 pass。
- [x] 治理 task 已于 2026-09-16 执行 archive；本 task status 为 `completed`。
