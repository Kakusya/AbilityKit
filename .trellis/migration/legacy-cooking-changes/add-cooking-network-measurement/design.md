## Context

动机与范围见 [proposal.md](proposal.md)，行为契约见 [spec](specs/cooking-network-measurement/spec.md)。阶段 7 位于 LAN session、配方、配置、Match 生命周期和持久化之后，测量不能替代这些前置能力的正确性证据。

现有 canonical 设计提供 `WorldStateSnapshot`/SnapshotBuffer/StateSync 的快照基线、按序应用和可选预测协调；回滚设计要求业务提供完整 `IRollbackStateProvider`、历史快照和可重演输入，不能把类型存在当作通用 rollback 已闭环。表现重整设计明确逻辑恢复与 View/Cue 分离；网络优化计划强调 Profile/能力与项目算法分层。LAN change 已规划 epoch/sequence/baseline、应用层 fault 与同机/两 PC 分层，但其实现证据尚待完成。

预计使用拟建的 cooking .NET benchmark/contract 测试项目及 Unity runtime/projection seam；实际路径、`.csproj` Compile Include 和 `.asmdef` 必须在实施时确认。不修改通用同步协议或把 Shooter/MOBA 专用实现当作做菜默认行为。

## Goals / Non-Goals

**Goals:**

- 用可复现 workload 生成同机和两 PC LAN 的分层指标与 fault 证据。
- 使优化策略独立可开关，记录 owner 批准范围、证据版本和回退原因。
- 以 snapshot baseline 作为无需优化的正确性出口，并验证表现层不写权威模拟、Match 结算或长期进度。
- 在具备完整业务历史证据时支持局部纠正；没有证据时安全等待/请求 baseline，而不强行 blanket rollback。

**Non-Goals:**

- 不选择或变更 LAN transport，不纳入 WAN/NAT/relay、账号平台、自动重连或主机迁移。
- 不固定 30Hz 或任何延迟、吞吐、队列、内存、分配阈值；`UNSET` 不是通过标准。
- 不默认启用插值、预测、完整 rollback 或把“报告生成”视为 owner 批准。
- 不让预测表现、插值时间轴或纠正结果写入权威命令、结算或存档。

## Decisions

### 1. Measurement contract is separated from optimization runtime

先定义统一 workload/采样 schema 和证据 artifact，再由 runtime adapter 暴露采样点。每份报告保存 workload hash、应用/配置/协议版本、拓扑、机器和 fault profile，指标定义固定但阈值由 owner 外部注入。选择此方式是为了支持前后对照而不把一次实验参数编译进产品。替代方案是在运行时写死目标，无法适应实际 LAN 条件并会把候选 30Hz 误当承诺。

### 2. Two evidence lanes and two fault layers

同机多实例验证流程/协议可复现性；两 PC LAN 验证真实网卡、地址、防火墙和连接条件，报告不可合并为单一通过。stream decoder 只接受 byte segmentation/coalescing 测试；message fake channel 单独注入 delay/jitter/loss/duplicate/reorder/disconnect。这样避免把 TCP packet boundary 或 packet reorder 错误提升为应用契约。

### 3. Baseline-first strategy registry

策略注册为 baseline、interpolation、local-prediction、correction 四类独立开关。启动默认只用 baseline。启用配置必须引用 owner decision、适用对象/拓扑、测量报告和回退规则；缺任一项保持 Blocked。插值只改表现采样；局部预测只覆盖获批输入和具备历史/恢复证据的状态；远端对象仍消费确认状态。

### 4. Correction is evidence-gated, not blanket rollback

权威快照偏差先产出 reconciliation result（偏差来源、帧、hash/状态摘要、是否可恢复）。有完整 Provider、历史和可重演输入才允许局部 restore/replay；否则冻结/回退到确认视图并请求或等待 baseline。此选择直接遵守框架回滚设计的业务 Provider 边界，避免对做菜领域未经证明的全量 rollback。

### 5. Compare authoritative invariants, then allow presentation change

优化前后同一 workload 必须比较最终权威快照、命令提交/事件、Match settlement identity、长期 progress revision，以及测量指标。只有权威结果逐项一致且批准目标满足才提交启用建议；任何污染、数据缺口或回归都关闭策略并回退 baseline。表现平滑不是逻辑正确性证据。

### 6. Test Matrix

| ID | 输入/动作 | 成功断言 | 失败/出口 | Runner/产物 |
|---|---|---|---|---|
| N01 | 固定 workload 同机优化前后 | 指标 schema、元数据和前后样本可比 | 缺字段/环境差异明确标记 | future .NET benchmark；JSON/CSV |
| N02 | 同 workload 两 PC LAN | 真实机器/网卡/地址/防火墙与消息 trace 分开记录 | 不可连接不得降级为同机通过 | future two-PC harness；双方日志 |
| N03 | stream segmentation/coalescing | decoder 消息边界/顺序一致 | 不依赖 packet boundary | future .NET codec tests；vectors |
| N04 | delay/jitter/loss/duplicate/reorder | epoch/sequence/baseline 行为可诊断，无重复 mutation | unsynchronized/recovery-required 正确标记 | future fault harness；fault matrix |
| N05 | disconnect/read/write fault | 停止 delta、不可声称同步 | 不自动宣称 reconnect | future session integration；state trace |
| N06 | owner 批准插值 | 视图变化但 authority/settlement/progress 一致 | 样本不足按回退策略 | future Unity/.NET projection tests |
| N07 | owner 批准局部预测 | 仅获批本地范围预测，权威最终收敛 | 未获批对象不预测 | future prediction contract；reconcile trace |
| N08 | 权威偏差，有/无完整 rollback evidence | 有证据才局部纠正，否则 baseline/恢复 | 不执行 blanket rollback | future reconciliation tests |
| N09 | 优化回归/数据不足 | 关闭策略、回退 baseline、记录原因 | authority 不被表现层污染 | future A/B runner；before/after diff |
| N10 | 阈值 `UNSET` 或未批准 | 可交付报告但 gate/启用仍 Blocked | 不以 looks fine 通过 | decision checklist；owner artifact |

## Risks / Trade-offs

- [Risk] benchmark 环境差异掩盖优化效果 → Mitigation：强制 workload/版本/拓扑/硬件元数据，前后对照并显式报告不可比。
- [Risk] fault harness 把应用层行为误归因于 TCP → Mitigation：N03/N04 分层，stream 只测分段合并，消息层才测 fault。
- [Risk] 局部预测污染权威结算或存档 → Mitigation：N06-N09 逐项比较 authority、settlement identity 和 progress revision，并禁止表现写入。
- [Risk] 没有完整 Provider 却强行 rollback → Mitigation：纠正前检查历史/恢复/重演证据，失败进入 baseline/recovery。
- [Risk] owner 目标未决被误写为默认性能门槛 → Mitigation：所有阈值保留 `UNSET`，批准 artifact 是启用前置条件。

## Migration Plan

这是新测量与可选优化能力。先核对阶段 1–6（P0–P5）的可运行垂直链路与实际测试证据：P0 `add-cooking-interaction-foundation` 为传递依赖，P1–P5 `add-cooking-lan-session`、recipe、config、match lifecycle、persistence 为本阶段直接依赖；再接入 transport-neutral workload 与 fault harness，随后只运行 baseline 建立基线报告，最后按 owner 批准逐项启用插值/预测/纠正并执行前后对照。若回归或证据失效，移除策略配置并回退 snapshot baseline；不修改既有权威同步、结算或持久化格式。WAN 与后续传输策略另立 change。

## Open Questions

以下必须由 owner 在对应 gate 前确认，本文不填默认值：

- 指标阈值、采样窗口、workload 规模和两类拓扑的通过标准是什么？
- 插值、局部预测、纠正各自批准的对象、拓扑、失败回退和用户可见降级是什么？
- 是否批准任何重连/恢复动作，及其与本阶段 unsynchronized 证据的关系？
