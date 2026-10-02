# N03 最小真实 ET 测量 fixture 源码映射

2026-10-03，natural_operating_implement；本轮只读 cooking-network-recovery-current，唯一输出本报告。无.NET、源码修改或第二个模拟。Root已批准局部本地工程，N02物理/整体出口不由此完成。

## 最小可信构建路径

参考 src/AbilityKit.ET.Runtime.Tests/CookingPreparingEtTests.cs 的 PreparingFactory/PreparingWithoutFrontFactory 与 CookingNetworkAuthorityEtTests.cs 的 registry/Host 构建，不复制 fake authority port。实现测量专用 ICookingPreparationGameplayFactory：可信 registry 提交一个合法最小配置与 spatial，Create 只创建一个 CookingRecipeSimulation 和两个空工具；该唯一simulation由 CookingLevelEtHost 持有，Session从不创建simulation。无需87目录/F01D31/供应/顾客/加工流程。

保留一个合法 recipe/appliance/plate定义可直接沿既有Preparing测试的最小 registry schema，避免臆测空recipes是否允许；测量动作不使用它们。两个item各自为容器定义（空箱/盘），以AddItem可信initial seed分别放world:tool-a/world:tool-b。两玩家cook权限、各独立合法interaction anchor，不交叉占位；推荐6×4floor、半径40、reach800、pose与toolanchor同格中心（1,1）和（4,1），面向(1,0)，station放远离两者的合法equipment格并匹配binding。InitialLayout包含对应storage/player入口及配置required targets；PreparingWithoutFront模式不装顾客/营业系统，几何仍由Host正常投影安装。

使用 registry.Current 配置身份、CookingLevelLifecycle(scope,config,factory)、CookingLevelEtHost，再 BeginPreparation(与factory layout一致的CookingLevelPreparation)。必须实际Accepted、Preparing、CaptureFullState全recipe/installedlayout有效，initial两个空工具唯一world归属。Host AllowedDuringPreparation（CookingLevelEtHost.cs:740）含Pickup/Drop；无需CompletePreparation/Start/订单timer。所有初始化在ET构造owner线程，factory只此一次；每个重复全新host/session/工具版本/instance而非清旧receipt。

## 相同 framed 路径与合法动作

构造 CookingNetworkAuthorityAdapter(realHost)→CookingNetworkSessionHost(adapter,listener,两participant凭证)，Start；InProcess使用CreateLocalClientTransport给两个真实CookingNetworkSessionClient，UDP使用LiteNetChannelListener(loopback,0,key)及正常clienttransport。ConnectAsync→完整baseline→精确ack→IsSynchronized后开始计时；callback只入队，唯一owner每10ms ProcessOwnerFrame。禁止测试直接TryEnqueue替代这段数据面，禁止直接修改工具位置/版本。

每player在其anchor循环Pickup(tool)→Drop(tool,WorldAnchor=ownanchor)，Command.Scope=当前baseline MatchScope，SimulationBatch=0（Session映射），Player=绑定者，ExpectedItemVersion来自已确认完整baseline；独立stable ID为run+participant+offered index。Await accepted terminal，再等待投影版本>=结果StateVersion并验证工具位置/新版本，才构造该player下一动作。零Move、无加工，仍产生真实domain receipt/event/allocator/checkpoint和wire全态增长。两participant可并行各一个inflight，不共享工具竞争。

## 采样与计数（不假装open-loop已实现）

每repeat 10秒warmup+60秒sample；每participant每200ms offered机会，即5offered/s，2人理想700次（warmup100+sample600）。三repeat分别新host，分开InProcess与UDP。提前协调业务不能在旧版本提交重叠同工具命令：上一terminal/projection未完成时该机会计offered并记录notIssuedBackpressure，不补发追赶burst。报告实际issued/s而非宣称持续5accepted/s；root若要求严格open-loop应另批准版本规划/多工具模型，不能偷偷提前ExpectedItemVersion。

- offered：单调时钟计划机会，包含未发出的backpressure。
- issued：真实SendCommandAsync调用；admitted：收集每ProcessOwnerFrame返回Admissions.Accepted按来源/correlation去重；mapped/pending不能替代admitted。
- accepted：非duplicate terminal的Result.Outcome=Accepted；rejected：实际Session/Host/domain拒绝分类；cancelled单独列。
- completed：issued任务在期限内到terminal，包括业务拒绝；timeouts/pending/unissued单列，核对completed=accepted+rejected+cancelled等不重叠分类。只采样区间issued的RTT，并在结束后有界drain其completion。

RTT由每client Stopwatch send→result；Host Diagnostics.Timings的Received/Consumed/Committed同Stopwatch时域，Committed标记的是结果发送路径时间，不冒称客户端收到时间；未admitted拒绝无timing样本，单列不放入相同percentile。ownerGC与CPUtiming只包同步ProcessOwnerFrame（CurrentThread GC delta），不含await/transportnative/remoteclient。Diagnostics计数/bytes从warmup结束取差值，QueueHighWater现为累计peak不能简单差分；报告全run peak或采样逐frame Pending另算sample peak。Sent/ReceivedBytes是Session payload非UDP/IP开销。记录baselineencoded bytes/tokens、authorityHash及每端own baselinehash/Sessionview、MVID/config/scope/SDK/topology/PID/机器身份；无正式performance门槛，UNSET。

## 真实容量与安全终止

默认receipt16384，700次/run远小于cap，但完整baseline结构随真实receipt增长；默认8MiB/1048576tokens/16384collection不扩大，不动态删除history/mapping。记录首次拒绝的kind/原因和前后完整canonical/lastvalidbaseline；若FullStateExceedsWireBounds/AuthorityFaulted/Disposed或同步失效，停止新offered issuance，有界drain，保存失败artifact，关闭两clients后dispose Session/Host，不将无法继续当PASS。

专门容量退出控制可用现有CookingNetworkSessionOptions(ReceiptCapacity:例如16)新run：顺序真实16次accepted，第17次ReceiptCapacityExceeded，验证工具/业务canonical零变更，已有stable ID duplicate仍可读缓存；不复用这run冒称60秒默认负载通过。Business/perconnection burst另使用已批准小capacity，保持合法每工具序列，否则ExpectedVersion拒绝会混淆容量结论。Host accepted+outbox2048通常每frame已drain，不靠单纯700总receipt声称覆盖该cap。

每operation建议30秒terminal/projectiondeadline，run总180秒含初始化/drain；只取消自己pending任务、关闭自己连接/进程，不全局kill dotnet。ET singleton限制本机并发host，因此同一owner窗口序列跑repeat/topology；正式测量期间不与其他.NET gate并跑。两PC与高档均后置，本最小fixture只证明本地真实authority测量路径。

## 待root/实施者核对

确认指标admitted来源OwnerFrameResult.Admissions去重及采样peak采集；确认接受closed-loop单inflight的5offered/s、不把backpressure隐去；正式运行前按冻结source确认publication guard和完整budget。此报告是源映射与推荐，不是已构建/已测量证据。
