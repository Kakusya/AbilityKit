> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S01 待执行清单

本次仅完成主分支证据核对和空间/目标规则细化；以下实施项仍待批准。

- [ ] 位姿与空间配置纳入既有 owner，提供确定性的移动及碰撞规则。
- [ ] 统一预览/执行的朝向、距离、遮挡和上下文动作判定。
- [ ] 全部已有交互复用同一可达性，保留 scope/version/幂等/占位检查。
- [ ] 同步 DTO、领域与 ET 指纹、configuration identity、snapshot/canonical/hash/checkpoint。
- [ ] 验证 design.md 的正反例及同 Level 恢复/成功交接；先聚焦 .NET，再跑适用门禁。
- [ ] 报告逻辑验证与 Unity 手感验证的边界；后者由 U01 负责。

- [ ] 核对依赖、历史 check 与实际代码；解决本 Task 的契约差异。
- [ ] 补齐具体设计、验收场景与影响的 snapshot/checkpoint/schema。
- [ ] 最终规划审阅及 owner 批准；当前不可 start。
- [ ] 批准后按独立纵切实现：A02–A07/A10，网格放置与连续移动分离。
- [ ] 实际运行聚焦 .NET 测试与适用 gate；记录 pass/fail/blocked/skip。
- [ ] 核对 朝向/距离/障碍改变候选，空手/持物解析稳定；拒绝零变更；普通手槽与台面单物件。

当前所有实现与测试步骤均未执行。命令选择见总任务 implement.md；Unity 必须解除禁令并确认环境后补齐场景命令。

## Current execution sequence after review

Owner authorizes audit then implementation. Follow the reviewed design and parent research/final-review.md; historical planning-only text is superseded. Check actual dependency evidence before starting, preserve stopped worktree edits, fix review findings before accepting prior implementation, update payload/fingerprints/config identity/canonical/checkpoint together, run focused behavioral and applicable integration gates, record actual pass/fail/blocked/skip and commit evidence. Never declare this Task complete from metadata or directory counts.

## Worker implementation record (pending coordinator review)

> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S01 待执行清单

本次仅完成主分支证据核对和空间/目标规则细化；以下实施项仍待批准。

- [ ] 位姿与空间配置纳入既有 owner，提供确定性的移动及碰撞规则。
- [ ] 统一预览/执行的朝向、距离、遮挡和上下文动作判定。
- [ ] 全部已有交互复用同一可达性，保留 scope/version/幂等/占位检查。
- [ ] 同步 DTO、领域与 ET 指纹、configuration identity、snapshot/canonical/hash/checkpoint。
- [ ] 验证 design.md 的正反例及同 Level 恢复/成功交接；先聚焦 .NET，再跑适用门禁。
- [ ] 报告逻辑验证与 Unity 手感验证的边界；后者由 U01 负责。

- [ ] 核对依赖、历史 check 与实际代码；解决本 Task 的契约差异。
- [ ] 补齐具体设计、验收场景与影响的 snapshot/checkpoint/schema。
- [ ] 最终规划审阅及 owner 批准；当前不可 start。
- [ ] 批准后按独立纵切实现：A02–A07/A10，网格放置与连续移动分离。
- [ ] 实际运行聚焦 .NET 测试与适用 gate；记录 pass/fail/blocked/skip。
- [ ] 核对 朝向/距离/障碍改变候选，空手/持物解析稳定；拒绝零变更；普通手槽与台面单物件。

当前所有实现与测试步骤均未执行。命令选择见总任务 implement.md；Unity 必须解除禁令并确认环境后补齐场景命令。

## Current execution sequence after review

Owner authorizes audit then implementation. Follow the reviewed design and parent research/final-review.md; historical planning-only text is superseded. Check actual dependency evidence before starting, preserve stopped worktree edits, fix review findings before accepting prior implementation, update payload/fingerprints/config identity/canonical/checkpoint together, run focused behavioral and applicable integration gates, record actual pass/fail/blocked/skip and commit evidence. Never declare this Task complete from metadata or directory counts.

Reviewed planning is authorized by current dispatch; actual evidence will be appended after fresh gates, prior stopped-run results are not acceptance.

## Worker actual implementation / evidence

- [x] Read reviewed design and reconciled retained candidate against current authorization.
- [x] Implemented corresponding authority, command, configuration, canonical and coordinated checkpoint changes.
- [x] Added domain and real ET ingress/recovery behavioral tests; ran actual cooking-kitchen-loop and cooking-et-level-runtime gates successfully.
- [x] Coordinator reviewed repairs, merged locally to master and reran both applicable gates.

Detailed commands, counts, failures and logs: research/verification.md. This record supersedes the historical unexecuted checklist above only for the worker implementation scope.
