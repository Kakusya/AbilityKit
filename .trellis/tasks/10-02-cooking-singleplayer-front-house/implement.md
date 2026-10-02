> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S06 待执行清单

- [ ] 核对依赖、历史 check 与实际代码；解决本 Task 的契约差异。
- [ ] 补齐具体设计、验收场景与影响的 snapshot/checkpoint/schema。
- [ ] 最终规划审阅及 owner 批准；当前不可 start。
- [ ] 批准后按独立纵切实现：F01–F09/G01/G11–G12/H01–H02。
- [ ] 实际运行聚焦 .NET 测试与适用 gate；记录 pass/fail/blocked/skip。
- [ ] 核对 复用询问/队列/用餐/离席与洗碗，补人工接手及通路；停止接单与结束分离；未满足不业务失败。

当前所有实现与测试步骤均未执行。命令选择见总任务 implement.md；Unity 必须解除禁令并确认环境后补齐场景命令。

## Current execution sequence after review

Owner authorizes audit then implementation. Follow the reviewed design and parent research/final-review.md; historical planning-only text is superseded. Check actual dependency evidence before starting, preserve stopped worktree edits, fix review findings before accepting prior implementation, update payload/fingerprints/config identity/canonical/checkpoint together, run focused behavioral and applicable integration gates, record actual pass/fail/blocked/skip and commit evidence. Never declare this Task complete from metadata or directory counts.

## 受限域增量实际进度（不代替完整出口）

- [x] 现有前厅 owner 中补有界 FIFO、路径/桌位与真实清桌状态。
- [x] 补人工认领/持续/停手与伙伴互斥、保留进度、人工不增长；脏轮 WorkId 独立。
- [x] snapshot/canonical/前厅 checkpoint 含新状态，malformed 候选拒绝零替换。
- [x] 聚焦前厅域实际 36/36 pass，0 skipped；日志和命令见 design 最后域层证据。
- [ ] 主 owner 接入统一 ET ingress、核心 geometry/布局适配与跨厨房 worker 互斥。
- [ ] 主 owner 接入统一 Level 配置身份/checkpoint/wire 版本；真实 ET 完整营业与成功跨关门禁。
- [ ] 独立审阅受限增量与组合门禁后方可判断 S06 完整出口；本记录不改 task 状态。

## ET/Level integration increment

Canonical ET ingress, initial trusted geometry, front/kitchen work exclusion and Level codec6 restoration are implemented. Actual pass/fail evidence and the remaining independent review/full gate boundary are recorded in research/et-integration-verification.md. No Task status change is made from these focused counts.
