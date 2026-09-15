## Purpose

为做菜经营游戏建立以真实网络数据为依据的响应性与同步优化边界，产出可复现的测量证据，并在 owner 批准后安全启用插值、局部预测和纠正，同时确保权威模拟与长期进度不被表现优化污染。

## ADDED Requirements

### Requirement: Measurements are reproducible and layer-aware
测量系统 MUST 使用带版本、配置身份、workload、采样窗口、机器/网卡/连接拓扑和 fault profile 元数据的可复现 workload；至少分别记录 command ingress-to-authoritative-commit、snapshot decode/apply、队列深度、吞吐、p50/p95/p99、丢失/重复/乱序错误率以及内存/分配。stream framing 测试与应用层 fault 测试 MUST 分开报告。

#### Scenario: Same workload produces comparable reports
- **WHEN** 在同一 workload、版本和记录环境下分别运行优化前与优化后的测量
- **THEN** 报告可按相同指标和采样定义进行前后对照，并明确环境差异或缺失数据，不以主观“流畅”替代数据

#### Scenario: In-process and two-PC LAN evidence remain separate
- **WHEN** 同机多实例和两台物理 PC LAN 分别执行相同协议流程
- **THEN** 报告分别标注拓扑、机器、网卡、地址/防火墙前提和证据来源；同机结果不能充当真实 LAN 的连接或性能证据

### Requirement: Fault behavior is observable without false synchronization
应用层测量 harness MUST 能注入可记录的延迟、抖动、丢失、重复、乱序和断线；客户端遇到无法连续应用的 epoch/sequence/baseline 或 transport fault MUST 保持明确的 unsynchronized/recovery-required 状态，不得继续宣称已同步。TCP stream 仅按字节分段/合并验证 framing，不得虚构应用可见 packet reorder。

#### Scenario: Fault injection yields a diagnosable outcome
- **WHEN** 在相同 workload 注入延迟、丢失、重复或乱序消息
- **THEN** 系统按既有 baseline/sequence 契约等待、拒绝或标记 unsynchronized，并在报告中记录 fault、状态和错误计数，不产生重复权威 mutation

#### Scenario: Disconnect stops synchronization claims
- **WHEN** 测量期间连接关闭、读写失败或断线 fault 被检测
- **THEN** 客户端停止应用后续 gameplay delta，输出 disconnected/unsynchronized 或 recovery-required 结果，不把断线自动解释为重连成功

### Requirement: Optimizations require evidence and explicit approval
客户端快照插值、局部预测和权威纠正 MUST 分别作为可开关策略；基线状态同步 MUST 在未启用优化时保持正确。每项优化只有在真实测量报告、适用 workload/范围、失败与回退条件和 owner 批准齐备后才可启用；本能力 MUST NOT 默认启用预测或完整 rollback。

#### Scenario: Approved interpolation improves presentation without changing authority
- **WHEN** owner 已批准快照插值，且测量满足批准的适用条件，客户端启用该策略并消费连续权威快照
- **THEN** 只改变表现时间取样/显示轨迹，权威命令结果、Match 结算、长期进度和快照顺序保持不变；缺少连续样本时按回退策略处理

#### Scenario: Local prediction is enabled only for an approved scope
- **WHEN** owner 已批准某类本地输入的局部预测，且该状态具备所需历史/恢复证据，客户端对该范围启用预测
- **THEN** 本地反馈可提前显示但最终状态以权威快照为准，预测输入与结果可追踪；未批准对象、远端对象和无法证明可恢复的状态不进入预测

#### Scenario: Authoritative correction does not require blanket rollback
- **WHEN** 权威快照或状态比较发现偏差，且已批准的纠正策略适用
- **THEN** 客户端按该策略导入/校正并输出 reconciliation 结果；仅在业务确有完整历史 Provider、恢复和重演证据时才执行局部 rollback，否则请求/等待 baseline 或回退确认状态

### Requirement: Before-and-after comparison has safe rollback
优化前后 MUST 在同一 workload 与可比环境下比较延迟分布、decode/apply、队列、错误、内存/分配和最终权威状态；报告 MUST 标识批准阈值为 owner 配置值或 `UNSET`。当数据不足、指标未达批准目标、fault 错误率异常或权威状态不一致时，系统 MUST 回退到确认快照 baseline 并标记优化不可用，不得保留未经证明的预测/插值状态。

#### Scenario: Regression reverts to snapshot baseline
- **WHEN** 优化后报告缺少有效样本、超过批准目标或发现权威/结算/长期进度差异
- **THEN** 客户端关闭该优化并回退确认快照 baseline，记录回退原因；权威状态和长期进度不被表现层写入或修正

#### Scenario: Successful comparison preserves authoritative outcome
- **WHEN** 同一 workload 的前后报告均完整，指标满足已批准条件，且权威状态/结算/长期进度逐项一致
- **THEN** 报告可提交 owner 审批启用优化；在审批完成前仍不得将优化视为默认生产行为

### Requirement: Performance targets and scope remain explicit gates
30Hz、延迟、吞吐、队列、内存、分配、丢包容忍度及预测/插值适用范围 MUST 不预设为本 change 的默认阈值；未取得 owner 批准或仍为 `UNSET` 时，性能验收与优化启用保持 `Draft/Blocked`，但基础测量和正确性/回退测试可独立执行。

#### Scenario: Unset target blocks optimization claim
- **WHEN** 报告只有实测数据但性能目标仍为 `UNSET`，或 owner 尚未批准策略范围
- **THEN** 系统可以交付诊断报告和对照数据，但不得宣称优化完成、启用预测或以报告替代批准决策

#### Scenario: Approved target opens a bounded gate
- **WHEN** owner 批准明确指标、workload、范围和回退规则，且报告覆盖同机与两 PC 所需证据
- **THEN** 仅批准范围内的优化可进入对应 gate；其他拓扑、对象或策略继续保持未批准状态
