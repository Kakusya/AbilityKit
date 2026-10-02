> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: network contract review; S01-S14 accepted at 4dadd25c8 with evidence routed by master-singleplayer-exit-verification.md. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# N02 待执行清单

- [ ] 核对依赖、历史 check 与实际代码；解决本 Task 的契约差异。
- [ ] 补齐具体设计、验收场景与影响的 snapshot/checkpoint/schema。
- [ ] 最终规划审阅及 owner 批准；当前不可 start。
- [ ] 批准后按独立纵切实现：同一玩法的 listen host/remote 客户端。
- [ ] 实际运行聚焦 .NET 测试与适用 gate；记录 pass/fail/blocked/skip。
- [ ] 核对 两台物理 PC 争抢/并行/绑定/提交/跨关状态一致；host 本地与远端共享验证；拒绝不丢料。

当前所有实现与测试步骤均未执行。命令选择见总任务 implement.md；Unity 必须解除禁令并确认环境后补齐场景命令。

## Current execution sequence after review

Owner authorizes audit then implementation. Follow the reviewed design and parent research/final-review.md; historical planning-only text is superseded. Check actual dependency evidence before starting, preserve stopped worktree edits, fix review findings before accepting prior implementation, update payload/fingerprints/config identity/canonical/checkpoint together, run focused behavioral and applicable integration gates, record actual pass/fail/blocked/skip and commit evidence. Never declare this Task complete from metadata or directory counts.

## Accepted implementation contract - 2026-10-03

Use N01 design.md final source-boundary and identity sections and research/root-contract-acceptance.md as the exact interface and ownership contract. N01 independent readiness review passed; parent master-singleplayer-exit-verification.md proves dependency acceptance. Earlier draft/start prohibition below the historical header is superseded. N02 is in_progress; implementation, gates and physical LAN are not yet accepted. Three bounded increments: generic transport; authority port/ET narrow extension; framed passive Session composition. Source changes require focused controls and applicable network-sdk/Cooking/ET gates; no Unity gameplay work. Root serializes .NET windows. Preserve old dirty network worktree.
