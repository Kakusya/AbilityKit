# N03 可审议设计

状态 planning；以下为拟实施契约，不是新 API 或已通过证据。

## 既有接口与 owner

使用 CookingNetworkSessionClient.ConnectAsync/ReconnectAsync/Disconnect/SendCommandAsync 与 LatestBaseline/IsSynchronized/ServerSessionInstance/RebindToken；服务端由 CookingNetworkSessionHost.ProcessOwnerFrame 在唯一 ET owner 上推进，网络 callback 只冻结入队。Authority CaptureFullState/CancelSources/ConsumeFrame/ApplyControl 沿用 N02。错误时捕获完整 domain canonical、full capture、Session view 和 terminal 输出，拒绝可以改变审计/连接 bookkeeping，但不改变业务状态。

Raw 协议控制使用现有 ITransport/FramePacket + CookingNetworkWireCodec 的 Join/BaselineAck/Command，不新增绕过 Host 的测试入口。Wire v3，Recipe5/Level8 保持不变。Join/Joined、BaselineIdentity 的 instance/participant/generation/scope/epoch/snapshotsequence/statehash/issueid 必须全部验证；StateHash=BaselineHash(authority capture, Session projection)。单独 gameplay 共识用 Hash(full capture)，不要求两端退出后的连接 view 相等。

## 恢复和失败矩阵

| 场景 | 所需结果/断言 |
|---|---|
| live 同 instance 有效 rebind | 原 participant 重绑定、generation 增加、token 轮换；旧连接不能继续；精确 issued baseline ack 后 Ready |
| 首次 join 或 rebind 未 ack | 不接收业务；错误/过期/篡改 hash/issue/scope ack 不释放 Ready |
| 每连接等待 baseline ack | 采用已审议 SingleAwaitingAck：未确认时不反复替换 issued identity；Ready 客户端后续最新全态按已冻结 source 规则发布；ack 后补最新态，不丢 committed projection |
| live stable ID 同 payload 重试 | 沿首次 domain ID/batch/ordinal、cached terminal；已 committed 不重复扣库存/出餐 |
| stable ID 不同 payload | conflict；原 receipt 保留，不执行第二种动作 |
| disconnect pending caller | 只取消来源 caller；partial duplicate survivor 保留；all cancelled 不伪造 domain execution，按 Session CancelledNoExecution 规则处理 |
| paused disconnect | clock/进度/顾客/供应不推进；恢复后的 cleanup ordinal0 先于 user1；原 participant 暂不可操作至实际释放 |
| old scope/epoch/generation/sequence | 明确拒绝、不跨关业务 mutation、不覆盖当前 receipt/watermark；各具体 Reason 对照冻结源码而非新增字符串 |
| cold new instance | 旧 token/instance 拒绝；以当前新 join 凭证建立新绑定和完整基线；新 domain ID 包含新 instance，不命中旧网络 mapping |
| authority Busy/Faulted/Disposed | typed false/null capture或明确不可用；无 damaged export，不强行 Ready |
| budget/exhaustion | 明确拒绝或 AuthorityUnavailable，保存最后合法投影；不继续消费不可发布态、不截断 receipt |

Cold 恢复仅使用既有 CookingMajorCheckpointStore / Host.LoadMajorBaseline + trusted factory 公共路径；成功基线并不包含旧 Session 映射。分别保留 old/new instance artifact，cold控制结束后重新执行一次真实新命令证明非永久拒绝。推荐延迟报文在交关、pause/resume、rebind 前后各固定 owner frame 边界注入；不依赖随机 sleep 争抢。

## 有界负载和测量定义

既有默认：business256/control32/connections8/perconnection64、receipt16384/waiters8/prefix256；配置 participant 首轮最多4。baseline8MiB、1048576 tokens、单 collection16384；command16KiB/control4KiB，其他报文65536 tokens/4096 collection；depth32和字符串限制沿 N01。Host terminal outbox+accepted callers2048。边界测试注入较小合法预算，在 limit-1/limit/limit+1 各验证；结构超限不因字节仍合法而放行。

推荐本地控制2、4 participant各运行10秒预热+60秒采样，输入速率每 participant 5/20/50 command/s，另提供单次 capacity+1 burst；每配置重复3次，固定脚本/种子。必须使用实际合法 Pickup/Drop 等可重复动作与有界 receipt，避免纯拒绝吞吐冒称经营吞吐。建议延迟档0/50/150ms单向、抖动0/20ms、丢包0/1/5%；故障注入层是transport交付队列，不直接改domain。正式选择仍待root批准。

receive→consume→commit 用同一个 Host Stopwatch 时间域；端到端 RTT 用发起 client本地 Stopwatch。禁止跨机器时钟相减。逐档报告p50/p95/p99、采样数量、accepted/rejected/cancelled、吞吐分母、queue high-water、payload bytes、baseline bytes/tokens、owner线程GC分配（排除callback/native/client）、CPU/内存峰值采样方法及采样窗口。没有性能目标时报告数值，不打性能 PASS。

每次报告SDK/runtime、commit+dirty状态、Host/Session MVID、menu/config身份、wire/schema、tick调度、客户端数、拓扑/IP/PID/机器身份、时长、故障参数和哈希。物理LAN需两机器配对日志与基线/继续业务证据；同机、InProcess分别标识，缺环境记NOT_VERIFIED。

## 待 root 决定（推荐）

1. N02物理gate未满时是否批准先做本地N03工程控制：建议仅上述恢复矩阵与本地bounded测量，整体完成仍保留物理blocker。
2. 采样/负载档：建议采用上文2/4人、3重复、60秒和固定故障档；正式性能阈值先UNSET，收集基线后决定。
3. 权威调度：建议沿N02单线程owner每10ms调度，区分业务frame推进与纯网络等待；不为性能报告改变生产tick规则。
4. 多进程恢复超时：建议baseline/connect15秒、单操作30秒、准备120秒、整个故障场景240秒；超时保存artifact并只终止自己启动的Process对象。

## Root planning review decisions (2026-10-03)

The existing owner authorization covers N03. Once the assembled N02 source and rich process corrections are frozen and reviewed, local deterministic recovery/boundary engineering may proceed before physical LAN evidence is available. N02 remains in_progress and neither task's complete product exit is inferred. Task execution metadata/context must be updated when this bounded increment actually starts; this paragraph alone does not start it.

Retain the existing single owner scheduling and Client's actual configured timeouts. Runner30-second operation guards do not override the production Client15-second timeout. The existing rich N02 preparation120/Host540/whole-process600-second diagnostic budget remains separate from short N03 fault-scene budgets; record the effective values in each artifact.

First measurement increment uses2participants,10-second warmup,60-second sampling,5 offered commands/s/participant,3 repetitions, then a separately labelled capacity burst. Higher2/4participant20/50offered rates and fault combinations remain registered expansions after the first baseline. Record offered, admitted, accepted, rejected and completed separately: receipt16384 is a count bound intersecting the8MiB/token/collection bounds; no promise that a60-second high-rate run fits. Structured budget exhaustion is a boundedness result, not successful full-duration gameplay throughput. No history deletion, receipt reuse or larger budget to force a pass. Performance thresholds stay UNSET.

## 2026-10-03 baseline-driven diagnostic follow-up

The final three SameMachineUdp baseline repeats each accepted73/600 offered opportunities with527 explicit closed-loop backpressure skips and RTT p95 approximately1.4–1.5seconds. This is not loss or a performance PASS. Root authorizes a runner-only diagnostic follow-up in the existing NetworkMeasurement project: all-thread managed allocation/GC collection counters, independently measured terminal versus committed-projection waiting, owner-frame timing, and observable baseline publication counts/bytes. Keep the original fixture, tick scheduling, correctness checks, bounds and sampling/repeat settings. Internal capture/codec stage costs that cannot be externally isolated remain source hypotheses, not measured component percentages.

See research/performance-followup-review.md. Production profiling hooks,20Hz publication,delta wire changes,audit deletion and budget increases are not authorized by this diagnostic increment. Subsequent production changes require a concrete reviewed contract and actual validation; Unity remains deferred.

## 2026-10-03 independent-process local fault/load increment

Root approves the minimal implementation proposed in research/process-fault-load-plan.md: a separate application runner project and owned wrapper, one real ET authority with local framed client and remote independent-process LiteNet client, genuine lost Submit response followed by live rebind/cached retry, and three fresh pairs with10-second warmup/60-second sample at5offered opportunities per second per participant. Initial, terminal and full-projection gates remain intact; a trusted final Pause may establish stable complete gameplay consensus after all issued work drains. Source/SDK/MVID/hash/PID/exit evidence and actual offered/admitted/completed accounting are mandatory.

No existing production files, transport callback authority, wire contract or fixed-tick semantics change. This increment is local separate-process evidence; random packet delay/loss, physical two-PC and formal performance acceptance are still distinct unverified exits. All .NET execution windows remain root-serialized.

Use deterministic scripted boundary injection before transport timing experiments. Distinguish failure of reliable delivery from intentional bounded rejection and from delayed client projection. Physical two-PC execution remains NOT_VERIFIED until paired artifacts exist. Unity remains deferred.


## Root disposition of registered load/fault topics ? 2026-10-03

Root reviewed research/load-and-packet-fault-followup-plan.md (df6fafbac). Adopt its configurable workload matrix as concrete pending execution: preserve historical default2participants/5offered; add4participants/5offered as ordinary scalability control and2/4participants at20/50offered as bounded closed-loop stress. Three fresh repeats,10s warmup/60s sample and explicit per-participant offer/issue/ET-admission/terminal/projection accounting remain. Rates are input opportunities, not simulation/publication frequencies or sustained accepted-throughput promises. Finite exhaustion must be preserved and classified, without pruning/widening bounds. First source implementation ownership is the existing measurement Program/Fixture/Profile, wrapper and new load increment report; no production changes. Both existing topologies remain separate actual evidence requirements.

The proposed P0-P6 raw-datagram delay/jitter/loss profiles are retained as adopted follow-up design scope, with P0 transparent-relay control required before impaired measurements. Relay source implementation is a later separately owned increment after independent-process runner acceptance. Application response loss cannot satisfy packet loss. No kernel/global-network setting change or external proxy is authorized. Exact relay bounds/seeds/queue saturation/recovery contract must be reviewed before implementation; this decision does not start an unreviewed proxy or close any execution row.

Forward ordinary-load engineering goals adopt >=285/300 admitted/accepted/terminal/projected sample offers per participant, zero correctness/timeouts/unresolved work; terminal p95<=100ms/p99<=250ms and validated full projection p95<=200ms/p99<=400ms. These goals are frozen before optimization rather than fitted to73accepted. Reference applicability still requires recorded machine/hardware/contention plus an explicitly chosen Release deployment baseline for2/4participants at5offered with3fresh repeats and separate process/physical topology coverage. Existing Debug reports keep performanceTarget=UNSET and are not retroactively converted to performance passes. Current approximately3second UDP projection cost does not establish these goals. Stress/impaired profiles initially judge bounded safety/recovery/accounting and report costs, not ordinary smoothness. Formal resource/QoS thresholds and physical applicability remain unresolved decisions.

.NET execution remains root-serialized. Current process runner has the window; new load/boundary test source work cannot build/test until a specific later grant. Unity/S15 and arbitrary nested Session reentry remain outside this authorized increment.
