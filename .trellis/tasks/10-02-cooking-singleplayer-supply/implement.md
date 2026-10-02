> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S07 待执行清单

## 已完成的局部组件及仍待接入项

新增CookingSupplyState，由既有模拟持有和驱动；没有独立timer或可消费厨房库存。有限请求预留外部原料单位，一次只申请一包、每Delivery一真实包，其他packageCount结构化拒绝；多个不同请求可逐包申请。PreviewReceive/CommitReceive及无限单份取料计划不分配厨房物件，真实包装/单位/allocator仍由主owner原子提交。配置、余额、在途、请求和ID水位可canonical与checkpoint恢复。

组件focused8/8通过；独立review红测证实“伪造第二receipt引用同一Delivery”缺陷，补反向请求身份检查后9/9通过、0skip、exit0；独立增量build0 warning/error。日志在本worktree local/Logs/cooking-execution/supply-component.log、supply-alias-repro.log、supply-independent-review.log、supply-independent-build.log。

尚未接入Recipe/ET完整lane、真实包装与输入实例、几何、版本/全局幂等、暂停、完整checkpoint和跨关；不能标S07完成。成功下一Level须重新开放采购但保留在途/余额，失败重开重新初始化，同Level恢复保留Closing；供应配置变化的继承必须预验引用，不静默丢弃旧货。无限取料属于完成现有营业工作，可在停止新采购后继续，最终权限由Level owner判定。

- [ ] 核对依赖、历史 check 与实际代码；解决本 Task 的契约差异。
- [ ] 补齐具体设计、验收场景与影响的 snapshot/checkpoint/schema。
- [ ] 最终规划审阅及 owner 批准；当前不可 start。
- [ ] 批准后按独立纵切实现：G02–G10。
- [ ] 实际运行聚焦 .NET 测试与适用 gate；记录 pass/fail/blocked/skip。
- [ ] 核对 包装与游戏份区分、采购/到货/接收/仓储原子化；无限和有限供应不混写；库存不足可恢复。

当前所有实现与测试步骤均未执行。命令选择见总任务 implement.md；Unity 必须解除禁令并确认环境后补齐场景命令。

## Current execution sequence after review

Owner authorizes audit then implementation. Follow the reviewed design and parent research/final-review.md; historical planning-only text is superseded. Check actual dependency evidence before starting, preserve stopped worktree edits, fix review findings before accepting prior implementation, update payload/fingerprints/config identity/canonical/checkpoint together, run focused behavioral and applicable integration gates, record actual pass/fail/blocked/skip and commit evidence. Never declare this Task complete from metadata or directory counts.

## 2026-10-02 supervised domain increment

Latest owner dispatch authorizes the reviewed domain scope. Exact pre-code contract: [runtime-increment.md](runtime-increment.md). Delivered behavior, actual verification and remaining coordinator host/config hooks: [domain-increment-report.md](domain-increment-report.md). Historical planning-only/component-only paragraphs above are superseded for this increment; full S07 remains in progress, not complete.
