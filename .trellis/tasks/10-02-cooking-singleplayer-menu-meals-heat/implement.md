> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S10 待执行清单

- [ ] 核对依赖、历史 check 与实际代码；解决本 Task 的契约差异。
- [ ] 补齐具体设计、验收场景与影响的 snapshot/checkpoint/schema。
- [ ] 最终规划审阅及 owner 批准；当前不可 start。
- [ ] 批准后按独立纵切实现：M2：F17–F20/F28–F42。
- [ ] 实际运行聚焦 .NET 测试与适用 gate；记录 pass/fail/blocked/skip。
- [ ] 核对 未熟披萨/蒸饺可交接，烤/蒸/炸/烤架能力独立，出餐小锅不混同后厨锅。

当前所有实现与测试步骤均未执行。命令选择见总任务 implement.md；Unity 必须解除禁令并确认环境后补齐场景命令。

## Current execution sequence after review

Owner authorizes audit then implementation. Follow the reviewed design and parent research/final-review.md; historical planning-only text is superseded. Check actual dependency evidence before starting, preserve stopped worktree edits, fix review findings before accepting prior implementation, update payload/fingerprints/config identity/canonical/checkpoint together, run focused behavioral and applicable integration gates, record actual pass/fail/blocked/skip and commit evidence. Never declare this Task complete from metadata or directory counts.
