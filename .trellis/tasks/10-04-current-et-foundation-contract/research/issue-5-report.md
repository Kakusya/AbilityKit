## Architecture report

AbilityKit 架构体检与当前 ET 试用方案

供 Kakusya 审阅  2026年10月4日  提案未应用

完整文字版，包含全部实质判断、表格、技术附录和证据索引，可直接在 Issue 正文阅读，不依赖聊天附件。技术内容沿用已交付评估；仅补齐下列交接状态并把附件引用改为本页对应部分。

### Handoff status

本次交接的当前状态（2026年10月4日）：#5 是唯一 orca-ready，旧 #1–#4 与新 #6–#9 均为 blocked。#5 仅授权本 Issue 已列明的架构合同、文档和 AGENTS 规则工作，不启动运行时迁移、协议替换、升级、合并或发布。以下保留 04:02、04:10、04:13 UTC 的历史记录以明确来源；它们不覆盖本段调度说明。执行前重新读取相应 Issue 的最新批准范围与标签；标签不证明本地 Orca 已认领、已开工或已停止。

源码审计基线仍为 a2cd7284e12d50a10bbb7abee6fc265577b9aa7c；本次交接已核验的后续 master 为 5312c6e4bf2b612260297e2d8623aa362a9051e6，仅新增 48 行 reclone handoff 文档，源码与配置未变。后者不是重新执行审计、构建或运行验收的证据。[G6](#g6)

[阅读 AGENTS 完整草案](#agents-proposal) · [返回阅读顺序](#orca-reading-guide)

### Report scope

**结论与审阅范围**

**建议把 Cooking 的领域状态和生命周期全面收进当前 ET，并按完整业务路径分批迁移。** 最终应由 ET 真正拥有可变状态，旧 Simulation 撤掉重复的可写存储；纯 C# 规则、DTO、网络和存档边界继续保持清楚。不能把“加了 ET 外壳”当成全面完成。

**已确定的约束是先试用当前 ET，不升级最新版。** 本次固定 master a2cd7284，实际 vendored 包为 core@3.0.3 与 sourcegenerator@3.0.1；这些是包版本。Cooking 使用的是裁剪后的 0.1.0 runtime，不等于已经接入完整 ET 网络和调度栈。

#### 最重要的五个判断

- 你对 protobuf 的印象基本正确。Cooking 当前网络是 JSON v3；已提交的四份协议目录共 88 条消息，75 条 MemoryPack、13 条 custom-binary、0 条 protobuf。仓库存在 protobuf 导出器和工具资产，但未接通当前业务 protobuf 编解码链。

- ET 领域迁移可以先做。网络传输、编码格式、存档格式和所有示例框架不应在同一批里替换。第一轮保留 LiteNet 与 Wire3，减少同时变化的原因。

- 成熟框架的目标是减少需要自己维护的基础设施。当前已有 ET、LiteNetLib、MemoryPack、Luban、xUnit、Roslyn、Microsoft.Extensions 等能力，先用好已有边界，再决定是否新增依赖。

- 先修验证依据。Unity 缺环境的跳过可能被父门禁记为 Passed；配置引用的根 CI workflow 未随该提交提供；SDK 与最终依赖闭包还没有完整固定。

- 迁移必须保留原失败与验收资产。command366 无终态和 P6 投影超时的原因没有由本次体检证明，不能归因于丢包，也不能宣称 ET 化或 protobuf 会自动修复。

#### 本报告的范围与当前调度

本报告基于固定源码和调用链审查，实际运行了版本审计与发布 dry-run 两个只读 Node 检查。未运行 .NET 构建、游戏/网络进程验收、Unity 或 IL2CPP。代码存在、历史通过、本次通过分别表述。

按 2026年10月4日 04:02 UTC 的最新调度，架构体检、当前 ET 试用方案与 AGENTS 提案优先级最高，Issues #1–#4 均已置为 blocked。原计划保留，等待重新审议；本地 Orca 是否已认领或停止未在本报告中核验。AGENTS 提案尚未应用到仓库。

补充核对：04:10 UTC 的 master 已到 5312c6e4，仅新增 48 行交接文档，源码与配置未变。下文结论仍以固定 a2cd7284 为审计基线；这次文档差异检查不构成新的运行验证。[G6](#g6)

调度补充截至 04:13 UTC：已发布 #5 架构合同与 AGENTS 任务并标记 orca-ready，只允许约定的文档与规则工作，不启动运行时迁移；#1–#4 继续 blocked。后续任务状态以对应 Issue 的最新审议为准。[I2](#i2)

### Current architecture

**现有底座与真正的权威**

“权威”指一份业务数据最终由谁负责写入；“生命周期”指谁创建、持有和释放对象。当前架构已有清楚的入口，但领域数据还没有全部迁入 ET。以下是源码现状，目标架构另见后文。[E1](#e1)、[E2](#e2)、[E3](#e3)、[E4](#e4)

| 层次 | 当前事实 | 改造判断 |
| --- | --- | --- |
| 领域规则 | CookingRecipeSimulation 持有物品、手持、Process 索引、容器、订单、去重、版本、分配器和事件水位。 | 迁移真正的状态所有权；规则计算可保持纯 C#。 |
| ET 组织 | 已有 Application → MatchRegistry → Match → RestaurantRuntime → Kitchen / Level → Driver 树。Level/Driver 仍引用普通 lifecycle 与 Simulation。 | 已有真实 ET 基础，当前仍属 Phase A。 |
| 权威宿主 | CookingLevelEtHost 与 Adapter 提供唯一 owner 入口。Host 仍持有 pending、terminal、history 和通知等状态。 | 每个状态族明确 owner；不能另建模拟或第二个时钟。 |
| 网络会话 | Session 负责 join/rebind、generation、稳定命令、baseline、ACK/Ready 和 close。 | 业务身份与恢复语义保留；通用 plumbing 可审议复用。 |
| 传输和分帧 | NetworkHost / ConnectionManager / 长度前缀 framing 连接 LiteNetLib ReliableOrdered。 | 已复用成熟传输，并非全自研 UDP。 |
| 编解码与数据 | Cooking JSON v3；其他消息多用 MemoryPack，另有 custom-binary。checkpoint、报告、record 各有独立用途。 | 按数据边界选格式，不把所有数据一次性统一。 |
| 宿主和共享源 | UPM 是共享源码主干，src 通过 Compile Include 复用；Orleans、ET Demo 有独立闭包。 | 检查 csproj、asmdef 与实际引用，不能只看目录名。 |
| 配置与工程工具 | 已有 Luban、YamlDotNet、xUnit、Roslyn、32 个配置门禁及发布脚本。 | 优先补可复现和负例，避免另起一套工具。 |

仓库盘点为 104 个 com.abilitykit.* UPM 包和 255 个 src/Server 项目（含 vendored 工具）。这是组织规模，不是一个产品同时运行 255 个组件，更不能据此认定所有模块都冗余。[G1](#g1)

Cooking.EtBridge 仍是旧 Demo runtime 的 snapshot 投影探针；World.ECS、Entitas 与 Orleans 还有其他消费者。Cooking 全面 ET 化不自动授权删除这些库或结束其他宿主支持。[E4](#e4)

### ET target architecture

**固定当前版本的 ET 目标架构**

建议先把“全面”限定为 Cooking 应用和必要运行时：Match、Participant、Level、物品、容器、工位、加工、订单、供应与业务账本逐步有明确 ET owner。网络全栈接管与全仓库清退属于后续独立决定。[E1](#e1)、[E2](#e2)、[E3](#e3)、[E4](#e4)

#### 当前版本已经有和还没有接上的能力

| 能力 | Cooking 当前实际状态 | 采用边界 |
| --- | --- | --- |
| Entity 与 System | 82 份 C# 提炼内核，含 Entity 生命周期、EntitySystem、ETTask 与显式 Tick。 | 用于领域树；保留少量 aggregate driver，不给每个物品另开业务时钟。 |
| Fiber 与异步 | EtRuntimeHost 单 owner、单进程一个 active host，可按 Scene 显式 Tick。 | 多 Scene 不等于多线程；Task/ValueTask 可作为外层适配。 |
| 事件 | EventSystem / AEvent 源码在包内，但宿主未安装 EventSystem。 | 先定义消息职责；广播只适合提交后的通知，不替代可失败的业务调用。 |
| 网络与 Timer | 提炼内核没有 KService/TService、Session、mailbox、完整 scheduler 或 TimerComponent。 | 同仓原版本另有这些源；按具体缺口另做最小试验，不自动拷入。 |
| 序列化与约束 | 提炼移除了原序列化接线；AKET001/002 只覆盖特定关系调用。 | DTO 继续独立；不能声称已有完整风格、异步和所有权静态检查。 |

#### 必须写清的四条语义

- 一份可变状态只有一个写入 owner。迁移期旧 API 可以转交调用，不能维持两套可独立修改的字典、缓存或 Simulation。只读对比不等于双写。

- ET Parent 表达释放责任，不等于物品放置关系。容器与物品、Participant 与 Connection、Level 与跨关 Runtime 的业务关系要用稳定 ID 表达。

- 实体 InstanceId、Parent、EntityRef 不能直接变成 wire 或存档身份。await、重建和池复用后重新核对存活、scope/epoch 与 incarnation。Dispose 也不等于业务结算与正常 close。

- 保持现有分阶段提交：命令先 Submit/terminalize，再执行 fixed-step。当前测试明确 fixed-step 失败时已接受命令的效果和事件仍保留。要改整帧回滚必须另立行为变更，不能藏在重构中。[E5](#e5)

同仓完整 FiberManager.Create 的初始化异常分支存在只记日志、未结束等待 Task 的源码路径；Cooking 当前提炼宿主不走它。若选用该模块，先做失败注入与有界取消控制，不把风险当作已复现的 Cooking 故障。[E5](#e5)

### ET migration plan

**把迁移拆成可验证的纵切**

整体目标可以全面，施工单元应是一次能完成输入、写入、输出、保存、恢复和继续执行的闭环。以下阶段是待批准方案，仍由本地 Orca 实施，不代表已经启动。

| 阶段 | 具体交付 | 可以继续的条件 |
| --- | --- | --- |
| M0 固定基线 | 当前版本与来源清单；真实状态 owner 图；消息职责；事务边界；已接受与未完成出口。 | 每条命令能追到唯一 writer、实际 terminal、投影及同步许可；原失败和冻结源保存。 |
| M1 整理权威入口 | 列齐 Simulation 写路径与 exporter；建立窄的 typed authority/transaction seam。旧 API 单向委托。 | 没有第二份 mutable cache；规则无 ET/Unity 依赖；能阻止旁路写入。 |
| M2 首个真实纵切 | 物品归属、容器、手持、版本及所有相关索引；必要的 Process lock、allocator 同步纳入。 | 真实 pickup → put/container → save/restore → 继续执行；所有读写迁走，旧字典撤除。 |
| M3 扩展领域 | 按事务依赖迁移 Process/Station、订单/结算、供应/前厅、身份与生命周期。 | 每切片完整状态、事件水位、去重、索引和失败语义相等；唯一 Tick 保留。 |
| M4 网络与恢复出口 | 完整菜单、自然结束、跨关、独立进程、四断点、cached retry、P6、ACK/Ready/close。 | 同一最终源与真实二进制验收；原 schema 默认不变，变更则有互读/拒绝矩阵。 |
| M5 退役重复层 | 依据反向引用清单删除已无状态的 facade、重复索引和无用适配。 | 旧消费者与测试已替代；未迁的非 ET 边界有理由；无未经审议的发布/许可扩张。 |

#### 首切片不能只迁一个类

物品迁到 ET 后，如果放置、自动产物、订单绑定仍会写旧 _items，权威仍然分裂。因此首切片的大小应由事务闭包决定，必要关联必须一起迁；这比规定“一次只能搬一个 Entity”更可靠。[E2](#e2)、[E3](#e3)、[E4](#e4)

#### 回退与对比

生产路径始终只有一个 writer。回退靠已验收提交和兼容 checkpoint，不靠影子双写。候选树先构造并完整验证，再发布；失败只销毁候选。旧新对比采用顺序或独立进程，避免单宿主限制；随机 instance/token 可显式映射，业务图、版本和 receipt 不能忽略。

完成标准是所有计划内状态归属清楚、写入与终态可证明、恢复可继续、旧可写状态退役。Entity 数量、目录重排和“编译通过”都不足以代表完成。

### Protocol findings

**protobuf 实际使用与采用条件**

当前业务主链没有采用 protobuf wire。仓库中有 .proto、Google.Protobuf DLL 或导出器，分别只能证明某个文件、工具或生成能力存在，不能证明 Cooking 已经使用 protobuf。[N1](#n1)、[N2](#n2)、[N3](#n3)

| 路径 | 源码实际使用 | 判定 |
| --- | --- | --- |
| Cooking Session v3 | CookingNetworkWireCodec 使用 System.Text.Json；Host/client 都经该 codec。 | 当前网络为 JSON。 |
| 协议 catalog | 4 catalogs / 88 条：MemoryPack 75，custom-binary 13，protobuf 0。 | MOBA 17/9；Room 51/0；Shooter 7/2；System 0/2。 |
| ET demo .proto | Proto2CS 输出 MemoryPackable / MemoryPackOrder；MessageSerializeHelper 调 MemoryPackHelper。 | .proto 在该链是生成输入，不代表 protobuf 编码。 |
| CatalogCompiler | 存在 --export-protobuf、ProtobufProtocolBackend 与文本导出测试。 | 可选导出能力，未接业务 runtime。 |
| Orleans | 外部 wire 多用 MemoryPack/专用二进制；grain DTO 使用 GenerateSerializer / Id。 | Orleans 内部 serializer 是另一边界。 |
| Luban 与记录 | Luban 附带 protobuf 工具资产，当前 MOBA 导出 json/bin；存档、报告、record 另有格式。 | 工具依赖与业务线上格式分开判断。 |

#### 不要先把 protobuf 当成必选项

第一轮保留 Cooking JSON v3，使 ET 状态迁移有稳定比较基线。同为 C#/.NET/Unity 且没有跨语言需求时，已有 typed MemoryPack 是增量成本较小的候选；需要标准跨语言 schema 和独立协议演进时，再评估 protobuf。选择仍需具体需求和兼容证据。

现有 protobuf backend 的静态缺口包括 package 小写而自定义类型沿用 C# 全名、逐文件输出缺 import、uint8 映射不完整；当前测试检查文本和文件，没有运行 protoc。它需要补齐并验证，不能把导出成功当作后端成熟。[N3](#n3)

#### 采用任何新编码的准入条件

- schema 和生成器有唯一来源；protoc 或对应生成器确实成功，生成 C# 被编译，并跑实际 runtime roundtrip。

- 旧新版本互读或明确拒绝、字段缺失/未知值、损坏与超限、.NET/Unity 宿主矩阵都通过；业务身份、幂等、顺序与容量规则保留。

- protobuf 不提供消息流分帧；deterministic bytes 也不等于跨版本 canonical。协议编码、帧边界、业务 hash、存档版本必须分别设计。[O1](#o1)

### Framework consolidation

**成熟框架的保留与收敛**

建议采用“先用当前框架和平台能力，再验证真实缺口”的顺序。保留一种技术不等于冻结所有代码；新增库也不等于减少维护。决定依据应是它替代了什么职责、留下什么适配以及如何回退。[G3](#g3)、[G4](#g4)、[G5](#g5)

| 能力 | 建议 | 原因与主要验收 |
| --- | --- | --- |
| ET 与 LiteNet | 保留当前版本。ET 接管领域状态，当前 LiteNet/网络 SDK 先保留。 | 成熟基础可复用；只在明确缺口下试同版本 ET 网络，避免永久两套 Session owner。 |
| Luban 配置 | 适用的新表优先复用；先补 generator/runtime 来源与可重现。 | 已有正式 MOBA Excel 管线。验证同输入重建、跨表引用和宿主一致；不强迫 Cooking 所有 JSON 改表。 |
| DI 与宿主 | World.DI 兼容维护并冻结扩张；.NET 普通宿主优先 Microsoft.Extensions。 | 旧 DI 含 borrowed/owned、seed、init/dispose 顺序等语义，不能换名就算等价；ET 与 DI 不得共同释放同一对象。 |
| 日志与观测 | 统一宿主适配和关联信息；ActivitySource 可按缺口引入，遥测后端延期。 | 保留业务因果 trace；观察只旁路采集，不驱动成功。验证多 Host 隔离、异常 sink、flush 和脱敏。 |
| 本地持久化 | 保留业务契约；先定义故障模型。确需多记录事务时，再评估 SQLite。 | 当前多处文件写入/rename 存在重复 I/O 面，但未证明已丢数据。数据库不能替代结算幂等、恢复规则。 |
| 测试与 analyzer | 继续使用 xUnit、Test SDK、Roslyn 和现有门禁。 | 补选择规则、负例、真实覆盖和产物身份；不另造测试框架。 |
| Flow HFSM Timer | 先按实际语义盘点，不按名字批量删除或统一。 | 状态机、业务流程、领域触发、逻辑时间不是同一职责。不得引入重叠 owner 或时钟。 |
| 其他宿主与 ECS | Orleans、Entitas/Svelto、GameFramework 等保留现有消费者边界。 | Cooking 不新增不必要依赖；退役前完成反向引用及验收替代，不能用全面 ET 暗含全仓清退。 |

通用服务适合做无状态能力或可重建索引。新 facade 只有在减少真实维护面时才值得存在；用新库包住全部旧实现却保留两套责任，通常不会减少复杂度。

版本一律以当前已验证范围为起点。MemoryPack、Entitas、Roslyn 在不同宿主的版本差异先核对最终闭包和必要原因，不机械拉齐到最大数字，也不因此自动升级 ET。

### Verification and governance

**先修可以验证的治理断口**

以下是优先级建议，不是已经实施的修复。前四项直接影响后续迁移结论是否可信，宜在大规模搬迁前处理。[G1](#g1)、[G2](#g2)、[G3](#g3)、[G4](#g4)

| 发现 | 证据与限定 | 建议出口 |
| --- | --- | --- |
| Unity 跳过被记绿 | 缺固定 Unity DLL 目录时子脚本 exit0；父 gate 把 powershell-script 的 exit0 写为 Passed。未证明某次交付已被误收。 | 区分 Passed/Failed/Blocked/Skipped/NotRun；requiredCoverage 明确；缺环境、零/旧产物、真实失败/成功都做控制。 |
| 根 CI 路由未落地 | test-gates.json 指向 .github/workflows/abilitykit-test-gates.yml，但该提交无根 .github。不能推断所有外部 CI 不存在。 | 核实实际执行器与 required checks；恢复 workflow 或更正文档；按改动路径与依赖图选择门禁，绑定 exact SHA。 |
| 版本闭包未固定 | 无 global.json、NuGet packages.lock.json、Directory.Packages.props；有 UPM lock。声明版本不等于 restore 结果。 | 固定已验证 SDK 与生成器；最终可执行/test 根做 locked restore；对 UPM Git 引用使用不可变提交。 |
| internal-only 发布防线不足 | ET 有内部使用 notice 与 IsPackable=false；当前发布 allow-list 无 ET。前缀 deny 主要排 thirdparty/demo。 | 显式标记 distribution 与禁止发布闭包；把受限依赖加进测试 fixture 时必须拒绝。不能仅依赖包名。 |
| 来源与文档存在欠账 | 部分工具/DLL 包装版本不足以确认上游版本；AGENTS 顶部多份历史“当前”状态，首行有字面问号损坏。 | 记录来源、hash、补丁、license、使用范围；只保留一个当前状态入口，历史证据归档，乱码从原件恢复。 |

#### 具体版本差异需要查解析结果

MemoryPack：Unity manifest 1.10.0，src 多处 1.21.0，Orleans 1.21.4；Entitas：World.Entitas 声明 1.5.0，MOBA 声明 1.14.2。它们构成闭包与跨宿主兼容待核项，尚不是已证实冲突。unity-mcp manifest 指向 main，但 lock 已有具体 hash，不能写成“完全没锁”。

#### 许可只核对当前副本

现有 ET 副本带 LICENSE.ET-Core 与 internal-use notice。应核实计划分享/发布方式及已有授权，不能把当前上游另一版本的条款套到旧副本，也不能据本报告认定已有违法。Luban 工具来源、内嵌 DLL、HFSM notice 等须补清单；未来若启用带 EPPlus 的 exporter，再按实际用途复核，不能说当前 Cooking 已在用它。[G4](#g4)

### Network issues and acceptance

**网络正确性与现有 Issues 接续**

截至 2026年10月4日 04:02 UTC，#1–#4 均为 blocked，先完成本轮当前 ET 评估和任务重排。以下保留原任务价值，不代表重新解锁；本地已认领任务的真实运行状态仍需单独核实。[I1](#i1)

| Issue | 保留的关键事实 | 在迁移中的位置 |
| --- | --- | --- |
| #1 command366 | 未观察到 command366 terminal，尚未进入该命令的 committed projection wait；600s 是整体预算。没有证据证明丢包或某个隐藏状态。 | 先保存/补齐从 send、ingress 到 terminal 的因果链。明确缺陷先做最小修复，反例成为后续迁移回归。 |
| #2 客户端复用 | 共享 facade 有 Connect/Reconnect/Send 等，但没有公开完整 Ready identity/ACK ledger。Task 完成不等于目标投影完成。 | 条件推进，不是 #3 的强制前置。先证明普通流程与证据等价；raw-wire 负例继续保留。 |
| #3 四断点恢复 | manual-paused、automatic-active、unbound-cup、submitted-reply-lost 都要真实独立进程与完整图。 | 触及生命周期的切片跑适用子集；最终源跑完整四案、自然收尾、durable successor 和专用反例。 |
| #4 P6 投影超时 | 原 repeat2 healthySample=false、recoveryPassed=true，不能算 pair 通过。generation2 后来恢复不能证明 generation1 flight 及时完成。 | 与 #1 独立定位，不假定同因。保留 accepted、target version、full projection、ACK/Ready 与原 30s 判定边界。 |

#### 迁移期间不变的基线

- 不提高 deadline、不减菜单/负载、不裁剪业务历史、不用诊断补造 terminal 或 ACK/Ready，也不把单机/同进程结果冒认独立进程或物理 LAN。

- 一次变更明确 source-impact，重新冻结必要编译源；保持源、二进制、脚本、MVID/runtimeconfig 和原始结果可核对。

- O03/O04 吞吐、延迟、timer/native scheduling 仍延期；物理两 PC LAN 仍 NOT_VERIFIED；Unity/S15 后置。功能迁移通过不替代这些出口。

#### 新的网络风险只作为待验证设计项

Host 当前在方向/阶段判断前进入通用解码，错误方向 Baseline 可触发较大的解析预算；已有容量边界，未做攻击或耗时复现。建议先收紧 c2s kind、连接预算和阶段准入。现有 LiteNet 路径未配置加密层，可靠有序与 SHA-256 摘要不能当作安全信道；若扩展不可信 LAN/WAN，需要独立威胁模型。[N4](#n4)

### AGENTS adoption

**把 AGENTS 原则变成执行约束**

本 Issue 下方的 AGENTS proposal 完整收录 AGENTS.proposed.md 补充草案，尚未应用。应保留有效的工作区安全、Orca 与 Trellis 规则，将长期政策、当前事实与计划分开；当前进度指向已有 progress.md，再沿引用核对原始证据，实施授权还需核对对应 Issue 的最新批准范围与标签。[G1](#g1)

| 原则 | 落地检查 | 实施者要交的证据 |
| --- | --- | --- |
| 当前 ET 版本固定 | 版本/来源 manifest 与允许差异表；不允许未审议升级。 | actual SHA、源 hash、生成器和宿主闭包。 |
| 状态和生命周期唯一 | 每状态族列 writer/owner；依赖方向和允许 System 检查。 | 旧 mutable store 退役；双写、旁路写入、重复 dispose 负例。 |
| 一个业务时钟 | 网络、Unity、诊断不能旁路提交或驱动模拟。 | owner-thread、重入、pause/resume、旧 callback 拒绝。 |
| 消息职责分离 | Command、terminal、domain event、projection、Session control 分别定义。 | 失败有结构化终态；广播监听异常不能冒充提交成功。 |
| DTO 与稳定身份边界 | 跨层只传 DTO、typed ID 和版本；禁止 ET/Unity/内部可变对象泄漏。 | 恢复后引用重建；InstanceId/EntityRef 不进入持久协议。 |
| schema 与生成唯一 | 生成器锁版本；Check 无 diff；兼容或拒绝策略明确。 | 旧新读取、未知/缺失/损坏/超限；实际宿主矩阵。 |
| 真实覆盖与发布治理 | 结果状态、requiredCoverage、internal-only 传递闭包有专门 gate。 | 零测试/缺工具/旧产物必须失败或明确阻塞；发布禁止负例。 |
| 复用与退役有理由 | 新依赖/第二套机制/长期 fork 需 ADR；删除前查消费者。 | 替代了哪些职责、剩余适配、负责人、回退与退役条件。 |

现有 AKET001/002 不是完整架构防线。新增规则要配真实正例与故意违规的负例；grep 或类名统计不能证明运行期没有双写。对所有权、取消竞争、重入和恢复仍需真实宿主测试。[E5](#e5)

AGENTS 不能继续充当不断追加的运行日志。历史通知保留到历史索引并标注适用源与 superseded，不能让旧授权重新生效，也不能猜测损坏文本原意。

当前源码中的“不要改变架构”等旧阶段限制应与新方向显式协调到 ADR/任务；本报告不把 Proposed 改成 Accepted。规则落地后，还需要门禁、执行者和审阅者共同证明它被执行。

### Technical appendix

**技术附录与验收边界**

#### 当前 Cooking wire 与独立版本

帧结构为 4-byte little-endian length prefix + 16-byte NetworkPacketHeader + payload。prefix 的长度包含 header、不含 prefix 本身；header 依次为 Flags:uint16、HeaderSize:uint16、OpCode:uint32、Seq:uint32、PayloadLength:uint32，均为 little-endian，HeaderSize 必须为 16。[N5](#n5)

Cooking opCode 为 0x434F4F33，JSON envelope 包含 ProtocolVersion、Kind、CorrelationId、Payload，wire version=3；字段 camelCase、枚举字符串。baseline 另有 LevelFormatVersion=8、RecipeSchemaVersion=5。这些版本彼此独立，Cooking 消息不属于前述 catalog 的 88 条。

baseline identity 包含 ServerSessionInstance、Participant、ConnectionGeneration、Scope、Epoch、SnapshotSequence、StateHash、IssueId。客户端核对版本、scope、hash 和 sequence 后 ACK，再等待 Ready；Accepted、完整目标投影与 exact ACK/Ready 必须分别留证。

#### 编码迁移必须保持的验证语义

JSON v3 拒绝未知/重复属性，要求可写属性齐全，拒绝枚举整数，depth 上限 32；baseline payload 8 MiB、command 16 KiB、control 4 KiB，baseline token 1,048,576、collection 16,384。解码后还核验 nullability，Freeze 通过 JSON 往返隔离对象。[N1](#n1)

8 MiB 是 payload 上限，不是全部 transport buffer。FrameReader 使用 FrameBytes+64；process acceptance 的 LiteNet buffer 使用 FrameBytes+4+NetworkPacketHeader.Size。换编码不能把这三种配额合为一个，也不能删掉 framing。

#### 最少的迁移测试矩阵

- 生命周期：Awake/Update/Destroy、跨 Scene 隔离、host restart、EntityRef 池代次、重复 close、构造失败与旧回调；引入 FiberManager 时另测 init failure/cancel/shutdown 有界终结。

- 事务与恢复：重复/conflict/stale 命令、已提交后 fixed-step 失败、allocator/导出异常、候选树发布失败、冷启动新 instance、跨关 carry 与去重/receipt conservation。

- 网络与 schema：分片/合并、队列上限、lost-response retry、terminal/projection 分离、close/drain、双端真实退出、旧新 reader/golden 与适用宿主。

#### 本次实际执行和未执行

实际：node tools/publish/audit-versions.js exit 0，版本 mismatch/BOM/off-cohort 均为 0；node tools/publish/release.js 默认 dry-run exit 0，只列 7 个 candidate，无 ready、无 tag。审计副本检查前后干净。

未执行：dotnet build/test、游戏/真实网络进程、攻击/性能测试、Unity/EditMode/IL2CPP、用户本机任务状态确认。历史 S01–S14/N01 接受仍按原范围保留；本次没有把 N02/N03 或其他未完成出口重新认领为通过。
