# N03 performance follow-up source review

2026-10-03；仅本报告可写，生产/阈值未改，无.NET。读取recovery受管树冻结测量17218c56e、Session publication/codec/client、ET full capture/adapter及domain fixed-tick audit实现。以下为后续审议建议，不是批准的优化或性能PASS。

## 实际事实与测量边界

最终带退出Ready检查的InProcess三个60秒样本：600offered各580/578/571admitted/accepted/completed，20/22/29背压跳过；owner同步调用managed累计47.886/49.207/49.119GB，RTTp95约157.9/159.1/158.6ms。SameMachineUdp同样600offered各73accepted/completed，527跳过，RTTp95约1463.5/1404.7/1488.8ms。每轮独立authority，两个空工具合法Pickup/Drop，无假port；真实16/17receipt控制、退出绑定/Ready/sameframecapture、完整baseline哈希均通过。数据入口measurement-increment.md及最终原始JSON，不以旧缺退出gate样本代替。

这是closed-loop一participant最多一inflight，必须等terminal AND覆盖commit版本的完整投影才能发下一动作。600是定时offered机会而非600真正入队；527明确未发出，不是生产拒绝/丢包。RTT计send→terminal，下一次准入还等待projection，故背压不能只由RTT解释。UDP是同一进程真实loopback sockets，不是双进程/两PC；CPU/working-set包括Host+两client。InProcess callback可同步发生在owner调用内，因而ownerGC包含其反序列化/哈希等；UDP其他线程的client分配不在该GC计数内，不能把48GB vs6GB直接解释为UDP少分配8倍。GC为累计allocation，非常驻内存；InProcess采样working-set约322–336MB，UDP约389–464MB。无正式吞吐/延迟目标，不能判达标。

Preparing空工具fixture避免长动线/菜单工序，却每10ms调用真实ProcessOwnerFrame；空帧仍推进合法fixed tick并保留tick审计。60秒里大量idleaudit而非仅几百receipt，这会放大历史成本。真实营业也有时钟/历史，因此不是纯fixture假成本；但其100Hz调度、无关卡结束、单inflight投影等待、同进程拓扑限制与真实目标调度/网络/服务长度须分开。不能为了漂亮结果删除audit或跳过自动/顾客/供应时间推进。

## 可定位的生产成本（源码证据，不冒称profiler占比）

1. SessionHost.ProcessOwnerFrame先CaptureFullState，再Adapter.ConsumeFrame开始capture，Tick后再次capture；Host.CaptureReadOnlyFullState导出Observe、FullRecipe、FullFront和ExportCheckpoint。checkpoint又携recipe/front快照，故单frame重复重建大图。Prepare中可恢复镜像、display.fullrecipe和观察摘要都在协议里，不可随意删字段。
2. 固定tick BuildFixedTickPlan在CookingRecipeLoop.cs:1216创建长度N+1新List、AddRange全部既有TickEvents。累积审计每tick复制；Capture/export/canonical排序/数组又读取全历史。该路径存在随关长增长的O(N)成本，具体占比仍待profile。
3. SessionHost.Publish（约141）每可发布peer刷新view、BaselineHash序列化完整authority+Session、构造新的issuedidentity，再Encode全baseline。没有业务变更检测或发布rate上限；每真实tick都改变clock/audit，不能仅用“工具未移动”当无变更。SingleAwaitingAck防重复覆盖issued identity，但ack后仍立即发布最新全态。
4. WireCodec.Encode（约73）先SerializeToElement(payload)建立JSON DOM，再SerializeToUtf8Bytes(envelope)，随后TryDecode进行kind扫描、全token/duplicate-field扫描及envelope/DOM解析。Hash（约156）另生成完整UTF8数组并SHA256。只读集合converter.Write调用ToArray也额外复制集合。
5. 客户端TryDecode解析envelope/DOM，Read<Baseline>再typed deserialize并全图ValidNullability遍历（metadata缓存，实例GetValue/集合访问仍发生）；TryInstallBaseline又全图SerializeToUtf8Bytes做BaselineHash。这里不能声称“typed反序列化两次”：源码明确是DOM+typed+validation+hash多遍，不是每一步都是typed graph。
6. 全量增长真实可见：最终InProcessbaseline约2.49–2.51MB/161–166k tokens，UDP约3.25–3.31MB/427–436k tokens；默认预算8MiB/1048576tokens未扩大。UDP样本传输包含许多完整快照；原样本约233–237MBSessionpayload/60秒，不等于UDP/IP开销。可靠有序大消息分片与单issuedack可产生projection等待，但没有packet级profile不能断言是哪种重传/锁占了多少。

## 推荐顺序（保持现有ET owner，先审议再实施）

1. 先定位成本：同冻结fixture/算法增加独立计时allocation阶段capture、tick审计copy、hash、encode/scan、clientdecode/hash、transportenqueue、projection等待/ACK周转；记录发布帧数与bytes/baseline、所有线程分配及GC pause/collection，避免再次误比owner-onlyGC。仅测量scope和输出额外统计，不绕过完整校验。
2. 无wire变化的重复工作优化：同一owner frame复用严格冻结的capture/immutable Session view，复用同view的内容hash；减少Observe/export重复和集合ToArray；使用writer/streaminghash减少中间数组/DOM。缓存必须包括scope/lifecycle/pause、HostFrameSequence、domain全状态、preparation/layout、globalMajordisplay、Session binding/watermarks/cleanup等，不能只看RecipeVersion（trustedMajor变化可不改它）。优先单frame局部复用，跨frame缓存另审议。Pool buffer必须保持既有owned-copy及所有异步send完成前寿命，不能回收仍在UDP队列使用的bytes。
3. 审议ready-client发布节奏/coalescing：推荐先实验20Hz最大常态全量发布（5个10ms ownercall一次），initialjoin/rebind/scopechange/unavailable即刻；游戏fixedTick仍按既有规则，CommandResult立即返回。仍一个AwaitingAck issuedidentity，ack后取最新完整态，不覆盖未ack identity。该20Hz是候选实验值，不是批准阈值或已实现能力；测量closed-loop投影等待和ready/baseline时限，不能延后ack/篡改Ready造成假吞吐。
4. 如果全量发布本身仍主导，再单独审议可保持完整恢复的delta/增量展示协议。N01 wirev3要求完整baseline，不能直接把该变更混入“编码优化”；须版本/required-fields/hash/receipt/audit恢复与fullcheckpoint重新review。当前不建议先做delta或删历史。
5. Domain audit存储成本可研究分段/持久immutable结构的内部表示，仍导出同样完整schema5/canonical、allocator/receipt/tick记录；不是删除/截断历史，也不是新持久化职责。批准前先profile确认收益，保留断线/冷恢复/重放门禁。

## 验收口径与防止测量假改善

同source/provenance、同业务tick/菜单geometry、同两工具/两人、10s预热+60s*3fresh；分别InProcess、同进程UDP，物理独立另跑。保留offered/issued/admitted/accepted/completed/rejected/cancelled/pending、scheduler/backpressure；新增terminalRTT vsprojection可继续等待的独立分布。全部最终Ready/currentbinding/same-frameauthority、容量17拒绝/旧duplicate、fullhash、sourcecancel/pausedcleanup/coldinstance/boundmissing/tampering等correctness控制不得弱化。

分别比较perownerframe/peracceptedallocation、所有线程allocation、totalwindowCPU/GCpause/working-set、publishedframes及payloadbytes、decode/encode成本和p50/p95/p99；吞吐分母固定60秒，不缩短窗口/把未发出当成功。结构/字节预算不增、receipt不删除、业务时钟不减速来制造收益。正式目标仍UNSET；root应先确认期望5offered/s是否需持续可接受/投影延迟预算和目标拓扑，再决定正式threshold。

结论：成本与低UDPaccepted数据真实，需要后续性能审议；当前证据不足以给组件占比或判产品性能达标。以上顺序不改变ET架构，但发布节奏/协议增量是独立明确批准项。无源码/阈值/测试运行变更。
