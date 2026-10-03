# N03 实施顺序与验证清单

当前in_progress，仅root2026-10-03批准的本地确定性恢复/边界工程增量。N02整体仍in_progress，物理gate未满足；这个局部依赖例外不表示N02完成。.NET由root串行授窗。最新master实测和剩余出口见research/master-local-recovery-verification.md；旧授窗记录不代表当前进程仍在运行。

1. 复读N01最终design（含baseline budget补充）、N02独立review/process实测报告与冻结publication源码（本次路由8d3261af1及后继）；核对实际commit/dirty状态、停止和活跃worker。对齐existing API/Reason，审阅N03 PRD/design，root决定本地提前范围与测量档。
2. 只在批准范围创建受管独立tree，保留其他worker编辑；先确定测试/runner所有权和.NET串行窗口。不要改metadata/manifests代表已执行，未经授权不启动Unity。
3. 先确定性InProcess矩阵：issued ack/SingleAwaitingAck publication、live generation/token轮换、旧scope/sequence/identity conflict、partial/all source取消、paused cleanup priority、Busy/Faulted/null capture。每拒绝比较前后完整业务canonical，另外检查合法Session bookkeeping终态。
4. cold矩阵：旧成功基线实际落盘→Dispose旧Host→新store+trusted factory LoadMajorBaseline→新的SessionInstance；先真实旧token/ID拒绝，再新join/完整ack/新ID合法command，验证receipt隔离。不得替换为同Host CreateSuccessor 或模拟改字符串。
5. rich live恢复：沿F01+D31 finite suppliers真实备料、manual暂停/换人、automatic离开、portion剩余、unbound成品换人bind/submit。选择至少manual paused、自动加工中、unbound杯、已提交但应答丢失四个断点；恢复后自然1unmet/2delivery/0star close，随后实际successor新scope；检查全recipe/front/supply/tombstone/allocator+Session view，不从hash单独推断完整性。
6. 边界：实际receipt增长超过历史65536 tokens（参考N02真实1600次），默认合法完整baseline roundtrip；注入小byte/token/collection预算覆盖首次/new join和原Ready连接；business/perconnection/control/outbox/receipt limit±1。保存真实红与绿，不扩大预算或删除receipt来绕过既有契约。
7. 在以上聚焦实际绿后跑适用Cooking/ET/network-sdk门禁（使用tools/test-gates.json真实配置，不发明gate名）；每次保存命令、stdout、TRX、计数、exit、commit、环境。编译通过不算玩法通过；中断/环境不足分别记aborted/blocked。
8. root审核后同机独立进程UDP有界故障和负载3重复；再物理两PC配对执行。日志按拓扑分目录，进程deadline/cleanup只处理自己启动的具体PID对象，避免全机dotnet终止。
9. 汇总测量口径/样本/峰值与失败矩阵，逐项review。root才决定集成/合并并复跑master门禁；本地绿不写整体完成，物理缺失保留NOT_VERIFIED，性能阈值UNSET保留无结论。

历史规划阶段仅进行了只读审议。随后已获准执行并合入cold4、live6与测量工具，最新master ET808/324和两拓扑容量控制实际通过；不是未执行计划。第5项丰富链四断点仍在实施，独立进程故障/负载、物理两PC和正式性能验收尚未关闭，不得由受限测试推导整体完成。


## Current verification update ? 2026-10-03

Item5 rich F01+D31 four-cutpoint recovery is integrated and verified in actual master ET328; corrected master Cooking815 and network-sdk311 also passed with zero failures/skips. See research/master-local-recovery-verification.md and rich-recovery-independent-review.md. Historical pending statements above are superseded within that local scope. Independent-process fault/load, profiling, higher-load/packet faults and physical two-PC remain open. Root granted measurement_profile_resume the sole .NET window after confirming no dotnet/testhost process; ack_fix_review continues process-runner source review only until a separate explicit grant.
