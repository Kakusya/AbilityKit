# 前厅域增量只读独立审阅

日期：2026-10-02。范围：integration 的 `CookingFrontOfHouse.cs`、`CookingFrontOfHouseFlowTests.cs`、S06 design/implement；依据总任务 `operation-review.md`、`final-review.md`。未修改 integration 代码，未运行 .NET，避免与主会话门禁争用输出。

## 结论与阻断发现

当前增量仍有一个恢复状态验证缺口，修复并复跑后再接收受限域增量。即使修复，也不能据此前厅域组件判断完整 S06 或 ET 出口。

**阻断：同一个真实脏碗可恢复出多个非终态洗碗工作。** `ValidateExtendedState` 最后对按 Bowl 分组的工作仅检查 Cycle 编号连续、唯一，未限制非 Completed/Cancelled 的数量，也未要求非终态为最新轮。可从第1轮 Completed、第2轮 Working 的合法状态，把第1轮改成 Paused、ElapsedTicks=0，保持第二轮 Working 与玩家不变。两轮都引用同一真实 dirty bowl，状态/编号/时长/执行者各自校验均可通过；恢复接受本来不可能由 EnsureJobs 正常产生的状态。之后旧 `wash:<bowl>` 可被另一个玩家认领，违反“一份工作成果一个 owner”、陈旧 workID 不能参与新脏轮的合同。

建议局部修复：每 Bowl 的非终态工作最多一项；如存在，Cycle 必须为该 Bowl 的最大 Cycle。新增上述 poisoned checkpoint 的 static Restore 与现有 RestoreCheckpoint 拒绝测试，断言旧前厅和厨房 canonical 都不变。本审阅遵照只读范围，已先向主 owner 报告，未直接修复。

## 已确认行为

- Flow 配置仅营业前，冻结路径与空间；每格中心/相邻边按核心 PlayerRadius 和障碍检验，入口、排队、桌位、出口端点一致。队列容量包含 Arriving/Queued；FIFO 按 ArrivalOrder；桌在行走期 Reserved、离席后 Dirty，完成 Clear 才再次供客人使用。
- 营业时钟到截止后不再 TrySeatGuest；既有客人继续排队、入座、等待或离开。CanSucceed 包含 transit、桌位 Dirty、伙伴、人工认领检查，未满足订单没有新增正常失败。新 Flow/manual 路径禁用 FinishInProgress 的历史强制完成行为，成功 Reset 必须自然收口。
- Claim/Continue/Stop 不推进时间；Step 推进人工和伙伴进度。被占工作不能再次 Claim，同一玩家不能在该前厅占两项；人工离开/停手保留 elapsed 并释放，伙伴接续。已推进洗碗保持 RequiredTicks，途中解锁速度不缩短旧作业。新工作保留人工认领窗口。
- CompleteInquiry/CompleteWash 识别实际完工者，人工不计伙伴成长，伙伴接手后实际完成只计一次；Clear 不计旧询问/洗碗成长。客人离开取消 Inquiry，不产生幽灵订单。
- 洗碗使用厨房的真实脏容器集合；人工完成删除真实 queue 项，cycle编号产生不同 workID。正常运行 EnsureJobs 只对没有非终态的碗生成下一轮；上述缺口位于恢复验证，正常生成路径本身没有同时生成两轮。
- snapshot/canonical 包含 Flow、manual policy identity、work、tables 和 route progress；冻结 Flow 不暴露原列表。恢复先验证再 Apply/CopyFrom，同配置的实例恢复保留已绑定只读谓词；静态 Restore 后必须重新绑定同 identity 才能 Step。客户订单/模板、桌号、路径索引、工作执行者与伙伴进度有结构预检。

## 证据与缺口

`local/Logs/cooking-execution/front-house-component.log` 实际存在，报告 **36 passed / 0 failed / 0 skipped**，182ms。源文件包含旧21项与新15项；新例覆盖 FIFO与清桌、人工互斥/接续/增长、取消、Flow钩子、路径拒绝、正常恢复继续等价、洗碗脏轮、malformed字段、速度解锁和自然Reset。这是无 TRX 的聚焦原日志，审阅未自行复跑，也没有依据测试总数推导全部语义覆盖。

**尚未证明/接入：** ET scoped ingress、幂等/指纹和同tick command排序；跨厨房/前厅唯一玩家工作；真实距离朝向谓词的安装；固定Tick与整局 Pause 门；有效初始/可变布局到 Flow 的适配；统一 Level checkpoint 格式和配置 identity、网络codec；完整营业和成功跨关恢复。当前测试的 canWork 常为 true，并不能证明上述授权和空间规则。ClaimFrontWork 的 kitchen 参数没有做玩家/工作预检，恢复人工 PlayerId 也仅检查非空/唯一，需由主 owner 的真实 kitchen/ET 适配验证，不能把委托占位视为已有产品能力。

S06 implement 已记录这些待接入项，保持未完成状态正确。旧 checklist 历史文案不应覆盖最新授权，但不影响本次受限代码结论。

Lint 未执行；TypeCheck 未复跑（本次仅原日志/静态代码审阅）；Tests 为上述实际 worker 既有36例结果，恢复双非终态反例目前缺失。

## 修复复审：洗碗轮次恢复阻断已解除

只读核对后续修复：`ValidateExtendedState` 每 Bowl 分组现在同时要求编号连续唯一、非终态最多一项、非终态 Cycle 等于该组最新 Cycle。它保留所有历史已完成/取消轮，不依赖枚举顺序，直接排除原报告的两类非法恢复状态。

新增 `Restore_rejects_two_unfinished_wash_cycles_or_an_unfinished_old_cycle_without_mutation` 从真实完成第一轮/认领第二轮生成合法 checkpoint，先验证合法恢复，再分别毒化为“双非终态”和“最新轮已取消但旧轮仍 Paused”。static Restore 与实例 RestoreCheckpoint 均断言拒绝，实例 canonical 保持不变，最后再次验证原 checkpoint 可恢复。测试未直接断言厨房 canonical，但恢复实现验证阶段只读厨房，不写厨房，原阻断路径已封闭。

实际 `local/Logs/cooking-execution/front-house-wash-cycle-restore.log` 报告 37 passed / 0 failed / 0 skipped，182ms。本审阅未运行 .NET，主 owner 的独立聚焦复跑与组合门禁仍应保留独立证据。

**更新结论：原阻断已修复，可以接收受限前厅域增量。** 统一 ET ingress、Pause、真实厨房互斥/几何策略、Level checkpoint/配置/wire 及完整营业跨关出口仍未由该域组件证明，保持前述未完成边界。Lint/TypeCheck 本次未复跑。
