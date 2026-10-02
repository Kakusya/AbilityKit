> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: network contract review; S01-S14 accepted at 4dadd25c8 with evidence routed by master-singleplayer-exit-verification.md. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# N01 初始设计

状态：draft；未实施。

## 来源冲突审议结果

已核对 `09-19-cooking-productization-network-slice-planning/research/discussion-notes.md` 第 28、29 节的 owner 明确决定：它们晚于 KCP 临时方案，且显式确认唯一真实传输为通用 LiteNet reliable-UDP；InProcess 仅用于本地与测试。TCP 不进入该路线，KCP 不是当前前置。不删除历史实现或证据。网络阶段开始时把此来源同步进 ADR 的带日期附注及生效 spec，保留旧决策追溯。

沿用既有 IChannelListener、IServerChannel、NetworkHost、ServerNetworkSession；通用 LiteNet 包补齐 Listener/ServerChannel，不新增平行传输抽象。Cooking Session 不直接拥有 LiteNet NetManager，通用传输包不拥有菜品或订单。共享源码变化必须同步检查 Unity asmdef/package 与 SDK csproj。

网络回调仅收发、解帧和有界入队，不改变模拟。Host 在固定 Tick 消费队列；网络命令使用权威接收序号先到先得，序号在入队原子分配，不能依赖线程再次竞争。单机已有稳定排序不静默变更；实现时明确两种入口的排序策略并测试 host 本地与远端使用同一网络队列。暂停停止逻辑 Tick 和命令消费；断线丢弃该 Connection 未消费输入。

首个纵切采用完整快照广播与被动 Client，不实现预测/回滚/delta 优化。重连由物理 ConnectionManager 和应用 Session 分担；Session 验证凭证、绑定原 Participant、清理临时队列、发送完整 baseline。旧 Match/Level/epoch/sequence 拒绝。新增单机位姿、加工、份数、绑定、前厅和供应状态必须真实 wire round-trip，不只携带 opaque hash。

N01 可提前研究和审议，N02/N03 生产整合等待 S14。实际双物理 PC LAN 与同机双进程 UDP 分开记证据；当前没有已配对第二主机，不能把同机通过标成两机通过。该环境缺口不阻止单机实施、通用 adapter 和可移植双端运行器准备。

更新权威决定与唯一传输方向，确定身份、命令/快照契约和真实 LAN 验收；不以旧纪要自动执行。

沿用 [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md) 的 owner 与数据边界，不创建平行模拟，不修改通用框架的产品职责。具体 API/数据形状、错误矩阵、版本迁移、恢复/跨关状态与测试断言在执行前补齐；本初稿不声称已达到实施就绪。

## N01 可审阅实施契约补充（2026-10-03，planning，基线 master 86c3eb41d）

本节补齐下一阶段设计，不启动 N02/N03。S14 最终出口由 root 收口；正式 ADR/spec 尚待 root 同步。依据 parent research/network-resume-audit.md，旧 ce3110b53 dirty network worktree 仅作为可审阅来源，不能直接合并或视为已验证。

### 唯一权威与程序集方向

唯一业务权威仍为 `CookingLevelEtHost`：TryEnqueue、Tick、Observe、生命周期与 ExportCheckpoint/LoadMajorBaseline/Restore 均沿用现有所有者。Session 不另建 CookingRecipeSimulation，不直接 Submit/AdvanceFixedTick，也不自行复制跨关保存/回滚逻辑。房主也建立 InProcess Client，通过与 remote LiteNet 相同的 FramePacket/codec/ingress 进入。

现有 Game.Cooking 被 Game.Cooking.EtRuntime 引用，不能反向 ProjectReference 形成循环。Session 中采用窄的应用 authority port，仅表达 committed observation、入队/帧结果及应用生命周期回调；实现和 ET 组合入口放在 EtRuntime（或其已有上层 composition），由应用持有一个 ET Host。此 port 不加入通用 Network，不拥有规则。逐项映射现有 API 后再定最终签名，不能用原 simulation ctor 继续产生第二份 authority。

### 有界 ingress 与固定 Tick

Session 唯一 ingress lock 管理 immutable事件队列。事件含 authority ordinal、connection generation、participant候选/已绑定身份、scope、client sequence、correlation、typed command或connection event。成功入队时原子分配递增 ordinal；拒绝不占用业务命令序号。初始容量256为测试配置，不是性能承诺。帧/消息大小、handshake/token/字符串/集合长度均设独立上限，溢出明确 QueueFull/FrameTooLarge。

PacketReceived 只验证 framing/版本/尺寸、freeze typed DTO并入队；不绑定、不读取可变世界、不发送新业务baseline。SessionClosed 只写 connection-close event。控制事件留独立保留容量；若保留区也满，记录该connection generation的待关闭tombstone，下一owner调度先清理，不能静默遗失断线或因全队列无法恢复。主机固定owner调度按ordinal消费已冻结前缀，先应用相应绑定/关闭等控制事件，再向ET提交合法命令；同一调度只推进一次ET Tick、一次业务clock。回调与owner不能并发修改ET。

**当前API实差异**：ET Host末尾 DrainPending按Player/Command排序，单纯把network FIFO批量TryEnqueue仍会重排。不能通过逐命令调用Tick、伪造Player/CommandID或simulationBatch达到先到先得。N02需审阅一个窄的Host admission ordering扩展：构造时默认existing deterministic策略；network composition显式选择 trusted-authority-ordinal策略，ordinal仅由Session赋予、重复identity使用首次ordinal。Host同一帧用该序号排序pending group并沿用既有校验/去重/atomic提交；所有原单机入口与默认测试不变。排序策略/新恢复水位若进入checkpoint，必须同步版本与canonical；如果pending不落盘，则restore后Session清空旧pending并从新generation重新分配，不能宣称checkpoint保存了旧network队列。该扩展仍属同一Host，不是平行模拟，最终须root审阅后实施。

### 暂停、断线与重绑

paused时owner仍处理有限连接控制事件、拒绝新业务命令并终结未消费业务输入；不调用会耗业务clock的Tick，HostFrameSequence、Recipe.LogicalTick、加工/供应/Front时间不推进。Resume由授权应用控制，不由普通客户端任意触发。暂停广播Observe，不强行ExportCheckpoint：当前ExportCheckpoint明确拒绝Paused，协议必须表达checkpointUnavailable=LevelPaused。

close event在owner点撤销该physical connection generation，丢弃其Session未消费输入；已经ET-admitted输入必须在同一owner调度边界保证尚未转交，或经审阅的按SourceConnectionId取消pending接口终结，不能遗留下一帧执行。已commit命令保持事实，结果缓存用于幂等。业务worker/Front claim释放由已有业务恢复规则处理，不由网络回调篡改对象。

participant属于Match，connection为可替换物理实例。初次绑定由Host预先发放的房间加入credential绑定配置participant，不能信任payload自报PlayerId。rebind token为Host生成随机凭证，绑定Match、participant、会话实例，日志不输出token；正确rebind在owner点撤销旧peer并增加connection generation。旧peer/旧token不恢复权限。新连接必须收到并确认当前完整baseline（scope/epoch/generation/sequence/hash一致）后才开放业务输入；client sequence继续当前participant已确认水位，或通过显式新connection generation序列域重置，采用后者并保留Match级业务identity缓存，不能把同一旧操作执行两次。无主机迁移或公网认证扩展。

### Typed full baseline 与 wire 版本

新wire protocol版本3与旧v1/v2拒绝互通，不做隐式fallback。保留现有CookingLanCodec/FramePacket封装，消息带required protocolVersion、schemaVersion、Match/LevelScope、authority epoch、connection generation、snapshot sequence和correlation。版本/必填字段缺失在入队前结构化拒绝；未知enum、非法数值、集合过限不能当作默认值。

full baseline DTO包含 **typed `CookingLevelObservation`**、typed `CookingLevelCheckpoint`（可用时）、checkpoint envelope format/version、配置身份、可信major progress的展示投影、网络participant/connection序列水位及对应canonical hashes。Observation用于被动显示和全场景一致性；checkpoint用于证明完整状态传输/诊断及受信任authority重建，不赋予Client修改世界、fixture/menu许可、布局policy或buff权限。首次Created/Paused等不可导出的状态仍发送完整Observation，并显式标注checkpoint unavailable原因，不能把空checkpoint伪装成完整可恢复基线。

当前checkpoint format8、Recipe schema5仅为86c3eb41d读取基线；实现时以源码常量为准，不擅自降级。保留JsonRequired nullable字段语义：present-null与missing不同。真实传输Observation/Checkpoint对象并round-trip其完整canonical文本，不只发送CanonicalText字符串或opaque SHA。客户端只安装不可变projection，不调用Host Restore；独立恢复验证在可信factory/provider内进行。trusted configuration、layout/geometry/menu权限、major progress来源保持外部校验，客户端checkpoint不得生成授权。

状态清单必须逐字段交叉检查：玩家pose/hand/加工worker，live items/tombstones与location/container contents，process模式/进度/耗时/结果，portion与allocator水位，order binding和Front询问/claims/顾客桌位动线，有限库存/购买/pending delivery/receipt origins，installed layout/geometry与preparation/menu identities，lifecycle outcome/版本/HostFrame/committed batch/营业clock和major progress。仅旧Recipe hash相同不足验收。

### 幂等与错误矩阵

业务identity采用Match+Level generation+participant+client command identity；fingerprint用现有CookingCommandFingerprint与typed payload完整字段，expected version/pose/targets/supply等不得遗漏。相同identity相同fingerprint：pending合并、已commit返回cached terminal；相同identity不同payload：CommandIdentityConflict，沿用Host已有conflict语义，不在Session另行修改业务结果。每个caller明确收到admission与最终disposition，队列满/关闭/暂停不能永久等待。

| 条件 | 反馈与状态约束 |
|---|---|
| framing/版本/required/schema/enum非法 | ProtocolMismatch或Malformed；未入队、零业务变化 |
| 未绑定/凭证错误/旧peer | Unauthorized或ConnectionSuperseded；零业务变化 |
| baseline未确认 | BaselineRequired；业务不开放 |
| 旧Match/Level/epoch/generation | ScopeMismatch；零业务变化 |
| 非正sequence/未知identity旧序号 | Malformed/SequenceStale；零业务变化 |
| 同identity不同payload | CommandIdentityConflict；沿用已有Host terminal规则 |
| 队列/帧超限 | QueueFull/FrameTooLarge；可识别恢复，不静默丢accepted input |
| Paused/非业务态/clock操作 | 映射LevelPaused/LevelNotRunning/ReservedClockOperation |
| 材料/工位/订单/版本不合法 | 保留既有domain rejection与事件；不吞料 |
| Host Faulted | authorityUnavailable，隔离；Dispose+可信durable冷恢复，不能活Host伪造retry |
| stale或hash不符baseline | Client保持先前有效projection，unsynchronized，不开放输入 |

### 将读/改文件与验证出口

读/适配 `src/AbilityKit.Game.Cooking/Session/{CookingSessionHost,CookingSessionClient,CookingLanProtocol,CookingSessionSnapshot}.cs`、CookingSessionAuthority/CookingNetworkMeasurement；读 `src/AbilityKit.Game.Cooking.EtRuntime/CookingLevelEtHost.cs`、CookingLevelObservation/CookingLevelCheckpoint及可信generation/durable实现；修改ET只允许上述审阅的ordering/connection cancellation窄触点及composition，不改普通单机规则。

通用网络读 NetworkHost/ServerNetworkSession/IChannelListener/IServerChannel/InProcess listener；计划补齐 `Unity/Packages/com.abilitykit.network.transport.litenet/Runtime/Transport/LiteNetChannelListener.cs`（新通用Listener/ServerChannel）、核对现有LiteNetTransport.cs以及package.json/asmdef和`src/AbilityKit.Network.Transport.LiteNet/*.csproj`。不得把Cooking DTO或token业务塞入LiteNet包。处理已有Game.Cooking→transport引用以及上层composition引用，不新增循环。旧worktree同名新文件先逐diff审阅。

验收：codec全字段round-trip与各missing/version/size负例；callback前后完整Observe不变；本地/远端同队列且反PlayerID顺序的冲突由首次ordinal胜出、一次Tick推进；单机默认排序回归；pause所有clock不变；close/rebind队列与终态；真实供应/portion/加工/订单/自然close/跨关baseline完整一致；非法恢复provider拒绝零变化；实际fault与可信cold load区分。N03测量接收/消费/commit、队列高水位/拒绝、bytes/allocations及p50/p95/p99并记录拓扑、SDK、commit、Tick和采样条件。

随后独立Host/Client进程使用真实LiteNet UDP，分别记录PID、完整状态hash和日志；与InProcess、同进程UDP证据分开。network-sdk与Cooking/ET适用gate需实际运行并记录。第二物理LAN主机当前不可用：two-PC出口保持BLOCKED/NOT_VERIFIED；不能从同机双进程通过推导完成N02/N03。Unity执行仍禁止。

## N01 最终接口修订（2026-10-03，取代上节未定签名部分，仍 planning）

### 服务端 batch 与重传映射

客户端typed CookingRecipeCommand中的SimulationBatch必须等于0 placeholder，否则MalformedCommand；客户端不能指定domain clock、authority ordinal或内部cleanup身份。wire业务identity为 `(ServerSessionInstance,MatchScope,LevelScope,Participant,StableCommandId)`，StableCommandId在rebind后不变，与connection generation内的client sequence不同。wire fingerprint覆盖全部semantic command字段（batch固定0），不包含传输correlation/token/sequence。首次接受时保存wireFingerprint；owner冻结bounded prefix，为该prefix所有新业务命令赋同一个 `checked(max(Host.HostFrameSequence,Host.LastCommittedSimulationBatch)+1)`。保存首次batch、映射后完整domainCommand和domainFingerprint，trusted ordinal按首次接受顺序。

同identity重传先查mapping：相同wireFingerprint复用首次domainCommand/batch/ordinal，不重新映射；pending合并等待terminal、已terminal回cached receipt。pending不同fingerprint必须将真实冲突payload按首次batch/ordinal及同一domainCommandId映射后交Host TryEnqueue，沿用整组Conflicted并通知全部旧caller；已terminal不同fingerprint交既有terminalConflict判定、拒绝且不执行。rebind只重置connection generation的传输sequence，不重置StableCommandId/domain去重。overflow时不接受新identity，缓存不得在domain receipt仍有效时LRU静默逐出。达到receipt上限返回ReceiptCapacityExceeded，并停止接新identity；历史重复仍可查询。退出该Level后关闭旧scope输入再释放其mapping；新scope不能调用旧receipt。恢复/newSession不能谎称保留了未落盘Session receipts：重新baseline明确新的network session instance，旧token失效，所有旧instance请求明确ServerInstanceMismatch，不从恢复的receipt hash重建首次command/batch。

ET现有FreezeNextBatch只挑最小SimulationBatch，因而一次prefix必须统一batch且不能直接采用wire batch。network Host排序扩展只作用同一batch的group，仍由现有Host一次Tick推进。测试覆盖反PlayerID争抢、混合恶意batch、同一prefix多命令、重传第一次映射和checked overflow；默认单机batch/排序语义不变。

### 具体 authority port / Host DTO 与签名（拟实施）

以下类型在Game.Cooking纯域包声明；port实现放Game.Cooking.EtRuntime，上层Session注入，不产生反向依赖。标注拟实施表示现有源码尚没有这些新增方法。

```csharp
record CookingNetworkMappedCommand(CookingLevelCommandEnvelopeData Envelope,
    long AuthorityOrdinal, string WireFingerprint);
// EnvelopeData为纯域镜像：LevelScope、CookingRecipeCommand、SourceConnectionId、CorrelationId；
// 不引用EtRuntime已有Envelope类。adapter仅映射，不复制校验/状态。
record CookingNetworkCancellation(string SourceConnectionId,
    long ConnectionGeneration, string Reason);
record CookingNetworkAuthorityCapture(CookingLevelObservation Observation,
    CookingRecipeCheckpoint? FullRecipe, CookingFrontOfHouseCheckpoint? FullFront,
    CookingInstalledLayoutCheckpoint? InstalledLayout,
    CookingConfigurationIdentity ConfigurationIdentity,
    CookingLevelPreparation? Preparation, long ServiceStartLogicalTick,
    long LastCommittedSimulationBatch, string? FrontConfigurationIdentity,
    string? PreparationConfigurationIdentity, string? MenuConfigurationIdentity,
    CookingLevelCheckpoint? ResumableCheckpoint, string CheckpointUnavailableReason);
interface ICookingNetworkAuthorityPort {
    CookingNetworkAuthorityCapture CaptureFullState();
    CookingNetworkCancelResult CancelSources(IReadOnlyList<CookingNetworkCancellation> sources);
    CookingNetworkOwnerFrameResult ConsumeFrame(
        IReadOnlyList<CookingNetworkMappedCommand> commands,
        IReadOnlyList<PlayerId> disconnectedParticipants);
    CookingNetworkControlResult ApplyControl(CookingNetworkOwnerControl control);
}
```

OwnerFrameResult纯DTO返回每项admission/terminal dispositions与捕获后的full capture；CancelResult返回所有被取消的pending caller terminal dispositions、source标识与结果reason。Control是封闭union Pause/Resume/TryFinishService以及应用授权transition request ID；不接受客户端构造的任意Prepare/Restore/provider对象。跨关通过现有应用Host generation/durable API与可信factory，port回新scope/capture和transition结果；Session不持有存档写实现。

Host拟新增 `CaptureReadOnlyFullState()` 返回上列capture域DTO；在owner线程、非_ticking、非inFlight publication点freeze，从同一Host取Observe、Recipe.ExportCheckpoint、RunFrontOperation(Front.ExportCheckpoint(menu))、installed layout checkpoint、全部身份/水位/preparation。initialized Created/Paused仍可以纯只读捕获Recipe/Front，**不调用Level Restore、不伪造可恢复Level image**。没有初始化kitchen时FullRecipe/FullFront为null并显式reason=NotInitialized。Faulted时capture返回structured AuthorityFaulted，不读取损坏世界。Level ExportCheckpoint保持原available/null+reason，包括Created/Paused/PendingCommands的拒绝。Observation过滤tombstones且没有NextProductId，不能单独代替full capture。

完整闭合由FullRecipe（tombstones、allocator、receipt/供应/portion/process等）、FullFront（claim/clock/客户桌位完整恢复字段）、InstalledLayout及geometry seed、preparation/lifecycle/config identities、水位共同承担；Observation只是展示投影。逐字段测试比较FullRecipe.CanonicalText、FullFront.CanonicalText、InstalledLayout.CanonicalText及其余typed字段，不把optional missing默认为null。网络projection本身没有恢复授权；恢复仍使用原Recipe5/Level8 schema及外部可信policy/provider。

Host拟新增 `CancelPendingSources(IReadOnlySet<string> sourceConnectionIds, string reason)`：owner非Tick中调用；source字符串唯一编码session instance+physicalconnection+generation。移除匹配source的pending envelopes并返回Cancelled terminal dispositions；同一business group若另有仍有效peer重复envelope，保留有效envelope并重选representative，否则terminalize全组。不会回滚已committed命令，也不改变业务世界。inFlight非空拒绝Busy，不声称可中断已经提交帧。Session控制事件与prefix drain在一个owner临界区，先CancelSources再TryEnqueue然后一次Tick，因而不存在close到下帧之间执行旧pending窗口。

### 断线岗位恢复与暂停次序

源码实际名称是 `CookingRecipeOperation.StopFrontWork` / `CookingFrontOfHouse.StopFrontWork`，不是ReleaseFrontWork；StopFrontWork调用PauseJob，保留已做进度并释放worker。manual使用已有StopProcess。owner从full capture枚举该participant ActiveWorker process以及Front owned work，生成规范StopProcess/StopFrontWork命令（配置participant、真实Process/Work ID、reserved server-cleanup StableCommandId，不接受客户端此命名空间），通过同一Host queue提交，不直接调用Front或改变域。

暂停时仅清Session/Host未消费输入并记录pendingCleanup participant集合（容量等于configured participants）；不提交会拒绝的paused命令，不推进任何业务clock。Resume先在owner点Resume，然后把待cleanup置于新prefix最前，使用同一server batch/trusted ordinal入队；一次Tick先执行cleanup后按既有推进顺序advance，离线manual/Front不能多耗一tick。重绑不能消除已排的cleanup；清理完成后再开放baseline ack/input。cleanup合法性受既有领域校验，不随意赋出reach权限；若StopProcess实际要求造成拒绝，帧结构化失败并保持participant unavailable，记录明确原因，不偷偷清state。自动设备无需停机，held item不自动删除/转移，保留已有可恢复场景状态。

### 具体可测试 bounds 与 ack

初始configuration：active configured participants<=4；每participant最多1active physical connection，连接准入槽8（含待handshake）；business pending256、control pending32、单次owner prefix256、每connection pending64；保留close tombstone槽8（与准入槽一一对应，不用unbounded dictionary）；cleanup集合<=4。连接槽在owner完成close后释放，overlimit新连接立即transport reject且不绑定；已accepted槽的close保留一个bit/generation标记，因此即使control queue满也不丢失关闭。该标记仅callback写运输生命周期，不改变业务。重复close coalesce，generation不重用。control洪泛关闭来源槽，owner按首次close ordinal处理。

frame最大8MiB（full baseline）、command frame最大16KiB、handshake/ack最大4KiB；UTF8 identifier128bytes、correlation128bytes、token256bytes、普通文本1024bytes、JSON nesting32、任一collection4096entries、总scalar/collection节点65536。codec在generic frame长度限制后进行streaming token scan计数/长度检查，合格才typed deserialize/freeze；拒绝超限baseline不截断状态。Session映射/terminal receipts每Level总16384 identities（pending包含其中）、每identity最多8duplicate waiters；超过explicit reject，不覆盖domain有效receipt。收尾旧scope最多保留一个只读receipt区，transition前拒绝新identity，释放条件明确scope退役；不宣称无限营业/无限客户端受支持。所有常量可注入更小值进行边界测试，schema/size与4participant上限是首轮配置约束，不冒充产品永久限制。

每connection generation最多一个outstanding issued baseline，identity含server session instance、participant、connection generation、完整scope/epoch、snapshotSequence、fullCapture hash与随机baselineIssueId。ack必须逐项匹配Host**实际发送并记录的该baseline**，不能猜当前worldHash开启输入，也不要求恰好等于一直推进的latest世界。替换issued baseline使旧ack失效；scope transition/rebind/close清issue。合法ack验证之后输入以当前scope/domain expectedVersion再校验，baseline比latest旧不授予额外权限。Client仅按同session/currentconnection、scope代际和snapshotSequence递增安装，hash/字段无效保持unsynchronized。ack本身是有界control事件，由owner提交开门。

### 历史入口与兼容迁移

旧SessionHost simulation ctor及direct ExecuteHostCommand不能用生产legacy simulator wrapper封装成新port继续运行。`CookingSessionAuthority`持有旧CookingSimulation并ExecuteBatch，当前composition不调用其批执行路径；只复用其纯Descriptor/ConnectionId/Reason DTO。历史P1类与受限tests保留历史范围，不擅自删除或称其当前网络实现。

迁移逐项清单：CookingLanSessionContractTests/SessionLevelTransitionTests/ReusableHarnessAcceptanceTests、Harness/LoopbackUdpTopology、NetworkMeasurementTests以及NetworkAcceptance Program的现存call sites全部rg核对；当前gameplay/lifecycle验收移到ET composition，codec/port单元可显式用只读/记录fake port；旧CookingSimulation执行测试保留为历史受限回归且不纳入N02通过计数。旧SessionSnapshot/recipe-only wire可保留历史codec测试，新生产入口只有v3/full capture+port；demo若仍旧ctor则明确历史不可作为当前房间启动入口，不能双路生产fallback。

实施前root+layout复审上述签名、Host cancellation/ordering与full capture边界；此前四项实阻塞由本节设计具体闭合，但尚未实现验证。N01仍planning，不修改恢复schema或扩大client grant。

### Port纯DTO补充定义（上述签名使用，拟实施）

```csharp
record CookingLevelCommandEnvelopeData(CookingLevelScope LevelScope,
    CookingRecipeCommand Command, string SourceConnectionId, string CorrelationId);
record CookingNetworkAdmission(string StableCommandId, string CorrelationId,
    bool Accepted, string Reason);
record CookingNetworkDisposition(string StableCommandId, string CorrelationId,
    string SourceConnectionId, string Kind, string Reason,
    CookingRecipeCommandResult? Result);
record CookingNetworkCancelResult(bool Accepted, string Reason,
    IReadOnlyList<CookingNetworkDisposition> Dispositions);
record CookingNetworkOwnerFrameResult(bool Accepted, string Reason,
    IReadOnlyList<CookingNetworkAdmission> Admissions,
    IReadOnlyList<CookingNetworkDisposition> Dispositions,
    CookingNetworkAuthorityCapture? Capture);
record CookingNetworkOwnerControl(string Kind, string RequestId);
record CookingNetworkControlResult(bool Accepted, string Reason,
    CookingLevelScope Scope, CookingNetworkAuthorityCapture? Capture);
```

上述Reason/Kind实现时定义有限enum与wire验证，不接受任意字符串驱动行为；Control.Kind仅前文closed union，生命周期transition具体可信参数保留在上层应用request registry，RequestId引用已授权request。CaptureReadOnlyFullState建议返回`CookingNetworkCaptureResult(bool Accepted, string Reason, CookingNetworkAuthorityCapture? Capture)`而非异常/损坏capture；port的CaptureFullState采用同返回类型。签名不为fault读取添加例外授权。

## 最终源代码边界收口（2026-10-03；覆盖此前签名歧义）

以下为拟实施唯一接口正文。所有方法仅在authority owner线程调用；Session回调不能调用。类型声明位于纯域包，adapter位于EtRuntime。Reason/Kind采用封闭enum，未知wire值拒绝；内部DTO不存在通过字符串执行控制的路径。

```csharp
enum CookingNetworkCaptureReason { None, AuthorityFaulted, Disposed, Busy }
record CookingNetworkCaptureResult(bool Accepted, CookingNetworkCaptureReason Reason,
    CookingNetworkAuthorityCapture? State);
enum CookingNetworkControlKind { Pause, Resume, TryFinishService, ExecuteAuthorizedTransition }
record CookingNetworkOwnerControl(CookingNetworkControlKind Kind, string RequestId);
record CookingNetworkControlResult(bool Accepted, CookingNetworkControlReason Reason,
    CookingLevelScope Scope, IReadOnlyList<CookingNetworkDisposition> Dispositions,
    CookingNetworkCaptureResult Capture);
record CookingNetworkCancelResult(bool Accepted, CookingNetworkCancelReason Reason,
    IReadOnlyList<CookingNetworkCallerCancellation> CancelledCallers);
record CookingNetworkCallerCancellation(string SourceConnectionId, string CorrelationId,
    CookingLevelScope Scope, PlayerId Participant, RecipeCommandId CommandId,
    string Reason); // Reason实现为封闭CallerCancellationReason枚举
record CookingNetworkAdmission(CookingLevelScope Scope, PlayerId Participant,
    RecipeCommandId CommandId, string SourceConnectionId, string CorrelationId,
    bool Accepted, CookingNetworkAdmissionReason Reason);
record CookingNetworkOwnerFrameResult(bool Accepted, CookingNetworkFrameReason Reason,
    IReadOnlyList<CookingNetworkAdmission> Admissions,
    IReadOnlyList<CookingNetworkDisposition> Dispositions,
    IReadOnlyList<CookingNetworkCallerCancellation> CancelledCallers,
    CookingNetworkCaptureResult Capture);
interface ICookingNetworkAuthorityPort {
    CookingNetworkCaptureResult CaptureFullState();
    CookingNetworkCancelResult CancelSources(IReadOnlyList<CookingNetworkCancellation> sources);
    CookingNetworkOwnerFrameResult ConsumeFrame(
        IReadOnlyList<CookingNetworkMappedCommand> commands,
        IReadOnlyList<PlayerId> disconnectedParticipants);
    CookingNetworkControlResult ApplyControl(CookingNetworkOwnerControl control);
}
```

上文CaptureFullState直接返回capture DTO的签名废止。Accepted=true保证State非null；失败保证State=null且Reason非None。Initialized/NotInitialized是成功State内的初始化状态，不等于fault；NotInitialized的Recipe/Front明确nullable及原因。Faulted/Disposed/Busy只返回失败，不能读取损坏世界。frame/control的Capture可独立失败，不能把已提交动作因后续capture失败回滚或重执行；动作Accepted与Capture.Accepted分开解释。cancel不暗示业务帧推进，也不附带世界写权限。

ControlReason最小集合None/InvalidRequest/Unauthorized/InvalidLifecycle/AuthorityFaulted/Disposed/Busy；CancelReason为None/AuthorityFaulted/Disposed/Busy/InvalidSource；FrameReason为None/AuthorityFaulted/Disposed/Busy/InvalidLifecycle/ArithmeticOverflow；AdmissionReason覆盖既有Host admission原因及ProtocolMismatch/Unauthorized/BaselineRequired/SequenceStale/ReceiptCapacityExceeded。Disposition.Kind有限Executed/Duplicate/Conflicted/Cancelled/Stale，必须带scope、participant、commandId、source与correlation及nullable域result，不能只用不唯一字符串commandId通知caller。域result沿用真实CookingRecipeCommandResult，不合成成功结果。

### per-caller cancellation 与逻辑 terminal 分离

`CancelPendingSources`只撤销匹配source generation的pending envelope，记录独立per-caller cancellation history供Session送达，不把局部Cancelled安装进Host logical `_terminal`。例：同business identity有A/B相同fingerprint，A断线而B仍有效，返回A Cancelled，保留B pending、首次business mapping/batch/ordinal，重选B为representative；B实际执行后logical terminal只包含真正执行/duplicate结果。后续重复只能复用真正Executed的domain result，不得复用A的取消历史。

如果全组caller都被取消，pending移除，caller均有独立Cancelled；该logical identity不安装假的execution terminal。Session live mapping保留首次command/batch与取消caller事实；同session将该identity标记CancelledNoExecution，对后来重传返回显式Cancelled，不重新赋batch/偷偷执行。另一个新StableCommandId可按当前batch正常恢复。普通lifecycle结束/Dispose原有全局logical cancellation语义不被此窄source-cancel修改；Session退役scope后拒绝旧identity。需要新增独立caller cancellation history或返回数组，不复用目前会Terminalize逻辑identity的helper。

### pending fingerprint冲突通知所有caller

Host现有TryEnqueue对pending不同fingerprint会移除全group、terminalize全部caller，但即时admission仅包含新caller disposition。network adapter必须返回整个此次terminal变化：建议新增Host owner-only `DrainNewTerminalDispositions()`（返回并清空独立notification outbox，不删除logical terminal receipt）并由ConsumeFrame在每次TryEnqueue后及Tick后调用、合并到frame.Dispositions。也可将窄network admission result携带AffectedDispositions；实施选择outbox，不能仅扫描无限history或只通知新包。

outbox容量等于已接受pending caller上限（business256 × max duplicate waiters8 =2048），在接纳caller前预留其最终通知槽；outbox只收此前已admitted caller终态。冲突新caller未admitted，其即时disposition通过Admission返回，不占outbox额外槽；adapter按source+correlation去重，不能满2048再溢出第2049项。admission拒绝不占accepted pending槽。同一调度即时冲突通知旧caller与新caller，Session按source+correlation完成所有waiting requests、清除其pending状态；已断线caller完成本地terminal记录，不等待运输送达。new terminal只送一次，可被域receipt重复查询；Session无需重复domain执行。测试必须覆盖A原包pending→B冲突包→A/B都Conflicted且零执行，及A断线/B相同payload继续成功、后续duplicate正确执行结果。

### 冷服务器 instance 与 receipt能力边界

Recipe checkpoint receipt只保存fingerprint hash，**不保存完整首次domainCommand和mapped batch**；不能从该hash重建Session mapping。每次冷启动建立新的随机serverSessionInstance；handshake/command/ack/token均带并验证instance，旧instance输入在业务mapping查询前拒绝ServerInstanceMismatch。新Session从可信baseline重新开放新instance业务identity域，不接受旧instanceStableCommandId当作重传；新instance新命令仍受恢复后的domain规则/expectedVersion约束。

只有live同serverSessionInstance的physical disconnect/rebind承诺首次映射、cached结果和幂等复用。mapping与receipt不新增持久化；不跨server重启复用旧token；无host迁移。上述此前“从domain terminal能力判定未知旧identity”的模糊表述废止：cold old-instance请求一律拒绝，客户端重新核对baseline后生成新操作，不能宣称旧请求exactly-once重放。Domain自身恢复receipt仍按原单机契约发挥作用，不被网络hash推导成完整command映射。

### 实施前剩余边界

接口正文闭合上述四项设计缺口，但需要root+layout审阅并决定正式enum字段命名及notification outbox与现有Host history的具体代码落点；这是拟接口，不是当前源码能力。N01保持planning。ADR/spec正式同步、Host窄ordering/cancel/capture/outbox实施和实际负例验证仍未完成；物理第二PC缺证据不变。本轮没有扩展恢复schema、Session持久化或迁移范围。


### 冻结用 identity 映射补充

wire businessidentity显式含ServerSessionInstance。domain RecipeCommandId确定为 `net3-` + lowercase SHA256Hex(canonical bytes)，canonical bytes使用固定tag `cooking-network-command-v3` 与顺序字段ServerSessionInstance、MatchScope的Session/World/Match、LevelScope的RestaurantRuntime/Level/LevelEpoch、Participant、wireStableCommandId；每个字符串以big-endian Int32 UTF8字节长度前缀编码，整数big-endian Int64，拒绝空白/超限/溢出。不做拼接分隔符猜测或Unicode归一化。原wire StableCommandId保留于Sessionmapping与响应，并同时返回mappedDomainCommandId以诊断；客户端不能选择domain ID。

所得domain ID固定69 ASCII bytes（net3-5字节+SHA256Hex64），满足本设计identifier128bytes上限。源码CookingRecipeLoop.cs RecipeCommandId只是string record struct；CookingRecipeCommandValidation.IsWellFormed当前仅检查Command.Value非空白，不附带128长度限制，因此长度限制由network codec/adapter落实，不修改单机校验。包含serverSessionInstance保证cold新instance即使同Participant和wireStableCommandId也不会命中旧receipt ID（标准SHA256碰撞假设）；live rebind同instance/scope仍复用第一次domainID/batch，connection generation与transport sequence不进入business hash。

pending冲突唯一规则：保留首次mapping/domainID/batch/ordinal，仅将真实冲突payload重新计算domain fingerprint后交同Host，使既有group全部Conflicted；不能在Session只reject新包。terminal冲突保留真正terminal结果并返回terminalConflict，不制造execution。outbox仅容已admitted旧caller，未admitted新冲突caller即时Admission带其disposition；结果按source+correlation去重。这些规则同时已修订前文，不能把旧相反句当另一实现路径。无持久化/恢复schema扩展。

mapping前typed DTO先freeze：服务端验证wire placeholder、完整scope/participant并覆盖domain RecipeCommandId/batch，客户端自报domainID不采信。SHA仅用于身份无歧义映射，不用于仲裁排序；排序仍首次trusted ordinal。layout独立审议已确认69ASCII映射及pending冲突/2048额外caller边界方案可接受，最终正文待复读。
