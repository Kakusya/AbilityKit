## Orca reading guide

本 Issue 正文现已完整内嵌架构评估与 AGENTS 草案。本地 Orca 可直接从 GitHub 读取，不需要访问聊天附件。

- #5 是当前唯一 orca-ready，仅按本 Issue 的已批准范围做架构合同、文档和 AGENTS 规则工作。
- 旧 #1–#4 与新 #6–#9 均为 blocked。可预读资料；只有后续明确批准、依赖满足并重新标记 ready 后，才可在各自边界内实施。
- 草案仍是 proposal，报告中的建议、Proposed ADR 和阅读动作都不构成额外授权。执行前重新读取对应 Issue 的最新正文、标签及依赖状态。
- 源码审计固定在 a2cd7284e12d50a10bbb7abee6fc265577b9aa7c；已核验的后续 master 5312c6e4bf2b612260297e2d8623aa362a9051e6 只新增 48 行交接文档，源码与配置未变，不构成新的运行验收。

| Issue | 何时读 | 必读内容 |
| --- | --- | --- |
| #5 架构合同与 AGENTS | 认领和任何文档/规则修改前；提交审阅前再对照 | [完整报告](#architecture-report)（含全部附录与来源）及 [AGENTS 完整草案](#agents-proposal)；先明确事实、目标、事务与授权边界。 |
| #6 门禁可信度 | 当前可预读；获准实施前及验收设计时 | [治理断口](#verification-and-governance)、[规则验收](#agents-adoption)、[实际执行与未执行](#technical-appendix)、[治理来源 G1](#g1) 及 [AGENTS 草案](#agents-proposal) 的工程门禁和证据部分。 |
| #7 依赖、CI 与许可 | 当前可预读；选择版本、改 CI 或发布配置前 | [成熟框架取舍](#framework-consolidation)、[治理断口](#verification-and-governance)、[治理证据 G1–G6](#governance-sources) 及 [AGENTS 草案](#agents-proposal) 的复用、版本、来源、兼容和发布限制。 |
| #8 ET 纵切迁移 | 当前可预读；获准设计与实施前、每个切片验收前 | [当前权威](#current-architecture)、[ET 目标](#et-target-architecture)、[迁移阶段](#et-migration-plan)、[网络与旧任务](#network-issues-and-acceptance)、[技术附录](#technical-appendix)、[领域来源](#et-sources) 和 [AGENTS 草案](#agents-proposal)。 |
| #9 协议与 protobuf | 当前可预读；获准比较或试验编码前 | [协议现状与采用条件](#protocol-findings)、[ET 边界](#et-target-architecture)、[技术附录](#technical-appendix)、[网络来源](#network-sources) 及 [AGENTS 草案](#agents-proposal) 的消息、schema 与版本规则。 |

下方保留本 Issue 原有任务范围、交付物与验收要求；完整参考资料位于其后。若范围或状态发生变化，以最新明确批准的 Issue 内容为准，不能把阅读清单当成解锁条件。


# 固定当前 ET 的架构契约并落实 AGENTS.md
## 状态与交付目标
本任务是底座重构的第一项，可以由本地 Orca 领取；范围是源码核验、架构文档、AGENTS.md 和任务依赖整理，不实施运行时迁移。
用户已将当前 ET 版本的底座体检与改造列为最高优先级，明确不升级 ET，Unity 继续后置。dot 负责方案与审阅，本地 Orca 实施。
完成后应让后续执行者明确知道：现在哪里存状态、目标由谁持有、消息如何流动、哪些规则不可改变、怎样证明迁移完成。不能只添加“优先使用 ET”等口号。

## 基线与必读证据
静态审计基线 a2cd7284e12d50a10bbb7abee6fc265577b9aa7c；发布前 master 为 5312c6e4bf2b612260297e2d8623aa362a9051e6，两者差异仅新增仓库重建交接文档，无运行时代码变动。本 Issue 未运行游戏/.NET/Unity 验收，不代表新 HEAD 全量通过。
- [当前重建交接](https://github.com/Kakusya/AbilityKit/blob/5312c6e4bf2b612260297e2d8623aa362a9051e6/Docs/design/CookingGame/reclone-handoff-2026-10-04.md)：历史产物位置发生变化，部分 DLL 不可用；不可凭摘要重建历史“已通过”证据。
- [AGENTS.md](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/AGENTS.md)
- [当前进度入口](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Docs/design/CookingGame/progress.md)
- [ET 目标树及迁移边界](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Docs/design/CookingGame/reference/et-entity-tree.md#L463-L615)
- [实际树/写权限测试](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.ET.Runtime.Tests/CookingLevelEtHostTests.cs#L19-L98)
- [现有事务语义测试](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.ET.Runtime.Tests/CookingLevelEtHostTests.cs#L577-L595)
- [实际 wire 类型](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking/Session/CookingNetworkWireCodec.cs#L12-L33)
- [当前提炼包来源](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/com.abilitykit.et.runtime/THIRD-PARTY-NOTICES.md)

## 已确定方向与未批准范围
1. 固定仓库现有 core@3.0.3、sourcegenerator@3.0.1 及本地提炼 runtime 的实际源身份；这些是包版本，不是一个统一 ET 大版本号。不下载/升级新版，不导入原版本整套网络/Actor/FiberManager 作为隐藏前置。
2. Cooking 最终的可变领域状态由 ET Entity/Component/System 明确持有和修改，不永久停在 Simulation 外套生命周期壳。纯算法、规则函数、配置、DTO 与 I/O 接口可保持普通 C#。
3. 状态族迁入 ET 时同步撤掉旧位置对应可写权威；适配器可委托/导出只读状态，不允许两条业务提交路径。不要为迁移先复制所有状态再以后处理一致性。
4. 此任务只定义、记录与拆分上述目标。具体状态族纵切、生产 API/协议/存档改变需后续独立有界任务；不清退尚有消费者的 ECS/Orleans，也不重新宣布 S01–S14/N01 未完成。
5. #1–#4 暂停领取，保留故障原件和验收资产；架构方案审阅后决定何时恢复。原问题并未因为重构计划而被修复。

## 修改范围
允许：根 AGENTS.md；Docs/design/CookingGame/progress.md；现有架构决策/参考文档中确需校正的段落；依仓库已有 Trellis 规范建立本项计划、check 与证据索引。
执行前先发现当前 ADR 目录与编号，不猜路径、不重编号旧 ADR。不将旧 Proposed 决策仅因提案写完改成 Accepted。
新建详细设计文档可使用 Docs/design/CookingGame/current-et-foundation-contract.md（执行前查重，若已有等价入口则复用）。
不允许：修改 src/、Unity/Packages/ 运行时、包版本、生成器、网络参数、存档/报告、CI 安全设置；不运行 Unity 实施或启动性能优化；不得清理用户工作树/备份/进程。若发现文档任务必须改实现才能继续，回报阻塞，另案处理。

## 必须交付的五份可审阅内容
### A 当前树和目标树
分别画实际树与目标树，清楚标注已实现/仅提案。实际基础为 Scene → Application → MatchRegistry → Match → RestaurantRuntime；Kitchen 与 Level 同属 Runtime，Driver 在 Level 下。
对 Item、手持/容器、Station、Process、Order、库存/供应、Session、去重账本、事件水位、版本/分配器分别列：
- 当前存放类型/文件/符号；目标 owner 与允许写入的 System；
- 创建、切关保留、销毁、引用失效、恢复重建与查询索引；
- 迁移一致性组及必须一起更新的引用；
- 旧写入口如何撤销、哪些消费者需适配；
- 对应正例、故意越权/旧引用/重复释放负例。
Parent 表达生命周期归属，不等于网络身份、存档 schema 或全部业务关联。不能把每条命令/事件变成 Entity。

### B 消息契约清单
至少分开 Command、准入/处理终态、Domain Event、完整状态 Projection/Baseline、Join/ACK/Ready/Close/Rebind。
每类写明 producer/consumer、owner线程、payload及不可变性、stable/domain/correlation身份、instance/generation/scope、顺序、幂等、重复/迟到/取消/异常处理、容量与可观察字段。
字段可直接携带或通过明确上下文关联；不能一律给所有DTO追加字段。说明业务事件已提交事实、通知订阅者失败与业务事务失败的区别。
提供一条普通操作和一条断线重试的时序：输入 → 准入 → 命令提交/终态 → 帧推进 → 完整投影 → 精确ACK/Ready；列每一步实际代码、何时完成和何种证据不足。

### C 事务与时钟边界
必须明确当前行为：accepted command 后 fixed-step 失败时，命令 effects/event/Executed terminal 保留，LogicalTick/HostFrameSequence 不推进。不能把目标文档里“完整帧一次原子提交”直接当已实现规则，更不能在迁移中悄悄改成整帧回滚。
领域逻辑Tick、网络deadline和真实耗时分别定义；保持一个权威驱动。Task完成、ACK、业务提交、客户端投影完成是不同事实。
重试同stableID与同payload不得重复扣料、重复结算或新增业务事件；旧generation/scope的回调不得写新对象。

### D 框架与协议能力台账
每项标实际“编译并调用/编入未接线/仅源代码或工具/提案”，附入口及依赖闭包。
当前Cooking Wire3用System.Text.Json；已提交catalog默认MemoryPack，部分custom-binary；protobuf仅可选导出能力，不能称业务已采用。ET .proto生成MemoryPack，与Google.Protobuf wire分开说明。
不把protobuf设为迁移硬条件，不同时替换存档/日志/报告格式。优先检查当前框架已有能力，其次平台能力和已有适配，再决定是否新增依赖。
记录源tag/commit/patch/许可/适用宿主/消费者/退役条件；保持当前ET internal-only边界，不因本次文档任务批准发布或判断许可证违规。

### E AGENTS.md 落地
根文件保留精炼、可执行的长期原则与唯一当前进度入口，详细清单放链接文档。整理旧Current/LIVE状态段落并保留历史索引和来源；不猜修复乱码，不删除仍有效的安全与Orca/Trellis约束。
至少落实：
- 当前ET版本固定，禁止隐式升级与双权威；
- owner/生命周期/消息五类/事务语义的变更必须有前后对照；
- 新基础设施先评估当前成熟能力；引入第二套同类机制必须记录差距与退出条件；
- schema权威源唯一、生成物不可手改，跨宿主兼容不能靠共享源码推断；
- Passed/Failed/Blocked/Skipped/NotRun分开，缺环境不等于通过；
- internal-only与依赖来源边界要有检查项，机器规则需正负控制；
- dot审阅与组织任务，本地Orca实现。经批准、依赖已审阅的有界任务可用orca-ready交接；标签不授权扩大范围、升级、合并或发布。
规则必须映射到现有/待建gate或明确人工check；本项记录待建检查，不假称已实现。禁止让新规则意外禁止合法只读查询、DTO导出或无状态库复用。

## 实施步骤
1. 核对当前HEAD、dirty、remote、工作树和最新重建交接，保存基线。读取现有AGENTS/Trellis/ADR与相关源码；不执行清空/reset/全局kill。
2. 形成当前事实/目标/未决三栏和五份内容；对每一项回到实际调用路径验证，不照抄旧Todo。
3. 先提交最小文档变更计划及冲突清单；若存在改变产品边界/事务语义的选择，提出具体选项等审阅，不自行选择。
4. 修改AGENTS及唯一进度入口；将旧通知迁入有来源的历史索引。现有测试记录按原SHA/范围保留。
5. 做链接、UTF-8、Markdown结构和差异检查；说明哪些长期规则已落文档、哪些门禁尚未实现。
6. 在本Issue回报commit或PR、详细diff、逐项验收及下一项建议。遵循本地已配置的分支/提交发布权限，不自行合并或部署。

## 验收清单
- [ ] 五份内容完整且都有具体类型/文件/符号，现状和目标不混写。
- [ ] 当前ET版本/剪裁范围明确，无升级、运行时代码、包或协议变化。
- [ ] Item/Process等跨对象一致性组有说明，不是机械逐类搬迁。
- [ ] 单一可写归属、跨关生命周期、幂等/旧代次/事务边界可检查。
- [ ] 明確 .proto、protobuf exporter、实际wire格式的区别，无虚构“已采用”。
- [ ] AGENTS根内容精炼，历史证据保留，坏编码不得猜造；规则附检查映射。
- [ ] 没有把新增规划当运行验证，没有重新标记大阶段完成。
- [ ] 当前#1–#4的暂停与后续解锁条件一致，无自发恢复orca-ready。
- [ ] 改动范围确为文档；git diff --check通过，链接检查有真实结果。未运行的.NET/Unity测试标NotRun，不因文档only就宣称运行通过。

## 回报模板
基线SHA/实际HEAD与漂移；改动文件；五份交付链接；现有语义冲突及处理；规则→gate/check对应表；真实执行命令/退出码；未决选择与需批准项；下一条任务是否具备ready条件。公开仅放脱敏摘要和仓库链接，不复制本机绝对路径、凭据或私有聊天。




---

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

### ET sources

**证据索引 领域与当前 ET**

除 G6 补充差异外，仓库源码链接固定到 a2cd7284e12d50a10bbb7abee6fc265577b9aa7c。行号与文件名用于定位本次判断，不保证未来 master 保持同样内容。

#### E1

**提炼来源与当前编译闭包**

[runtime csproj](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.ET.Runtime/AbilityKit.ET.Runtime.csproj) · [来源 notice](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/com.abilitykit.et.runtime/THIRD-PARTY-NOTICES.md#L32-L40) · [历史提炼记录](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/.trellis/tasks/archive/2026-09/09-17-cooking-et-runtime-roadmap/research/validation.md#L89-L120) · [core 包版本](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Demo.ET.Share/src/cn.etetet.core%403.0.3/package.json)

提炼内核与完整 vendor 必须分开。当前文件计数 82；core@3.0.3/sourcegenerator@3.0.1 是包版本，Cooking 提炼包为 0.1.0。

#### E2

**真实状态 owner 与 ET 权威入口**

[Host 树与引用](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking.EtRuntime/CookingLevelEtHost.cs#L254-L524) · [Simulation 状态](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking/CookingRecipeLoop.cs#L450-L490) · [AuthorityAdapter](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking.EtRuntime/CookingNetworkAuthorityAdapter.cs#L1-L95)

支撑 Phase A 判断与首切片范围。Adapter 复用现有 host，不另建模拟/时钟；Simulation 仍有主要领域 mutable stores。

#### E3

**目标领域树与运行机制**

[ET 参考树](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Docs/design/CookingGame/reference/et-entity-tree.md#L463-L615) · [EtRuntimeHost](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/com.abilitykit.et.runtime/Runtime/EtRuntimeHost.cs#L8-L142) · [EventSystem](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/com.abilitykit.et.runtime/Runtime/Core/World/EventSystem/EventSystem.cs#L26-L160) · [事件异常语义](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/com.abilitykit.et.runtime/Runtime/Core/World/EventSystem/IEvent.cs#L20-L32)

参考树是目标；不能当已实现。当前宿主单 active/owner；EventSystem 按类型/SceneType 分派，不自动提供事务或房间实例隔离。

#### E4

**保留边界与其他消费者**

[Demo 完整依赖](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Demo.ET.Share/AbilityKit.Demo.ET.Share.csproj#L29-L72) · [旧 EtBridge](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking.EtBridge/CookingEtBridge.cs#L1-L71) · [Projectile 消费者](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Combat.Projectile/AbilityKit.Combat.Projectile.csproj#L20-L28) · [Orleans Host](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Server/Orleans/src/AbilityKit.Orleans.Host/AbilityKit.Orleans.Host.csproj#L10-L20)

不同 runtime provider 与示例链并存不等于同一产品同时加载；删除前需要反向依赖清单。

#### E5

**容易被重构改变的语义**

[命令后 fixed-step 失败测试](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.ET.Runtime.Tests/CookingLevelEtHostTests.cs#L575-L595) · [Host 提交顺序](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking.EtRuntime/CookingLevelEtHost.cs#L1720-L1803) · [FiberManager.Create](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Demo.ET.Share/src/cn.etetet.core%403.0.3/Scripts/Core/Share/World/Fiber/FiberManager.cs#L64-L108) · [现有关系 analyzer](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.ET.RelationAnalyzer/EtRelationAnalyzer.cs#L32-L94)

测试源码不是本轮运行结果。Fiber 初始化未终结 Task 是静态路径风险，当前 Cooking 不使用该路径。

#### E6

**架构决定与稳定工程入口**

[ADR 0002](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/ADR/decisions/0002-authoritative-fixed-tick-state-sync.md) · [ADR 0003](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/ADR/decisions/0003-cooking-et-runtime-direction.md) · [AGENTS](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/AGENTS.md) · [当前状态入口](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Docs/design/CookingGame/progress.md)

ADR 0003 尚为 Proposed；新方向需显式审议。AGENTS 的历史执行通知不能代替当前状态和最新授权。

### Network sources

**证据索引 协议与网络**

#### N1

**当前 Cooking 编码与调用链**

[WireCodec](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking/Session/CookingNetworkWireCodec.cs#L28-L158) · [SessionClient](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking/Session/CookingNetworkSessionClient.cs#L27-L114) · [实际装配入口](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking.NetworkAcceptance/Program.cs#L54-L64) · [Authority capture](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking/CookingNetworkAuthority.cs#L38-L74)

JSON v3、严格输入验证、Freeze、完整 State+Session capture；源码可达不等于正式产品部署已验收。

#### N2

**88 条 catalog 与 ET proto 的真实用途**

[已提交 manifest](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Protocols/Generated/protocol-manifest.json) · [ET Proto2CS](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Demo.ET.Share/src/cn.etetet.proto@3.0.2/DotNet~/Proto2CS.cs#L77-L145) · [ET MessageSerializeHelper](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Demo.ET.Share/src/cn.etetet.core@3.0.3/Scripts/Core/Share/Network/MessageSerializeHelper.cs#L8-L41) · [Orleans grain DTO](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Server/Orleans/src/AbilityKit.Orleans.Contracts/FrameSync/FrameSyncModels.cs#L1-L22)

75 MemoryPack / 13 custom-binary / 0 protobuf；ET .proto 输出 MemoryPack；Orleans grain serializer 与外部 wire 分开。

#### N3

**protobuf 可选 backend 与测试边界**

[CLI 导出入口](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/tools/AbilityKit.Protocol.CatalogCompiler/EditorWorkflowCommands.cs#L329-L379) · [backend 类型映射](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/tools/AbilityKit.Protocol.CatalogCompiler/Emit/ProtobufProtocolBackend.cs#L28-L126) · [导出测试](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Protocol.CatalogCompiler.Tests/ProtocolBackendTests.cs#L60-L168) · [schema scalar](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Protocols/wire-schema.schema.json)

package/type/import/uint8 是源码可见缺口；没有运行 protoc，不报告本轮编译失败。

#### N4

**入站预算与安全信道边界**

[Host ingress](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking/Session/CookingNetworkSessionHost.cs#L98-L122) · [Owner kind 拒绝](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking/Session/CookingNetworkSessionHost.cs#L213-L237) · [LiteNet transport](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/com.abilitykit.network.transport.litenet/Runtime/Transport/LiteNetTransport.cs#L22-L86) · [连接 listener](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/com.abilitykit.network.transport.litenet/Runtime/Transport/LiteNetChannelListener.cs#L35-L48)

先解析后判方向是待收紧路径；已有边界，未做攻击复现。ReliableOrdered 与无密钥摘要不构成加密身份信道。

#### N5

**帧结构和旧路径**

[NetworkPacketHeader](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/com.abilitykit.network.runtime/Runtime/Network/Protocol/NetworkPacketHeader.cs#L6-L49) · [NetworkFrameCodec](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/com.abilitykit.network.runtime/Runtime/Network/Protocol/NetworkFrameCodec.cs#L8-L57) · [旧 UDP codec](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking.Udp/CookingUdpProtocol.cs#L54-L95) · [旧 harness](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking.UdpHarness/Program.cs#L25-L35)

旧 UDP v1/1200-byte fixture 仍有实际引用。先迁覆盖再退役，不能直接标为死代码。

#### O1

**官方序列化约束**

[Protobuf proto3](https://protobuf.dev/programming-guides/proto3/) · [分帧说明](https://protobuf.dev/programming-guides/techniques/) · [非 canonical 说明](https://protobuf.dev/programming-guides/serialization-not-canonical/) · [MemoryPack 版本演进](https://github.com/Cysharp/MemoryPack#version-tolerant)

官方规则用于评估候选，不证明旧 vendor 已具备所有现行功能。字段号、presence、schema 演进与 canonical hash 均要有明确合同。

### Governance sources

**证据索引 治理与任务**

#### G1

**门禁与 AGENTS 的事实缺口**

[test-gates 配置](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/tools/test-gates.json) · [Unity skip 分支](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/tools/run-unity-compile-check.ps1#L13-L17) · [父 gate 状态](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/tools/run_test_gate.ps1#L339-L345) · [AGENTS](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/AGENTS.md)

根 workflow 缺失依据该提交的 git ls-tree/ls-files；未取得外部 CI 或远端 required checks 证据。项目/包计数是静态盘点。

#### G2

**声明版本与最终闭包**

[Unity manifest](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/manifest.json) · [UPM lock](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/packages-lock.json) · [Shooter csproj](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Protocol.Shooter/AbilityKit.Protocol.Shooter.csproj) · [Orleans Contracts](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Server/Orleans/src/AbilityKit.Orleans.Contracts/AbilityKit.Orleans.Contracts.csproj)

MemoryPack 1.10.0/1.21.0/1.21.4 是不同图的声明；未 restore。无 global.json/NuGet lock/中央版本文件由完整树扫描确认。

#### G3

**现有可复用能力**

[Luban 配置权威](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/LubanConfig/Moba/README.md) · [WorldContainer](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/com.abilitykit.world.di/Runtime/World/DI/WorldContainer.cs) · [Baseline 文件写入](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking/CookingMajorBaseline.cs#L89-L126) · [结算文件写入](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking/CookingLevelSettlementStore.cs#L68-L118)

配置生成、生命周期 DI、文件存储分别有真实语义；建议按缺口收敛，未断言已有掉电丢失或 DI 错误。

#### G4

**来源 许可与发布**

[当前 ET notice](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/com.abilitykit.et.runtime/THIRD-PARTY-NOTICES.md#L32-L40) · [当前 ET license](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/com.abilitykit.et.runtime/LICENSE.ET-Core#L1-L6) · [发布清单](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/tools/publish/release-manifest.json) · [发布脚本](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/tools/publish/release.js)

当前 allow-list 无 ET；内部使用限制须显式覆盖传递发布闭包。本报告不判断既有授权或法律违规。Node 两项只读检查通过不代表游戏验证通过。

#### G5

**平台一手说明**

[SDK global.json](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json) · [NuGet lock](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files) · [DI 指南](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines) · [SQLite 原子提交](https://www.sqlite.org/atomiccommit.html)

支持固定 SDK、最终应用恢复闭包、DI scope 语义和故障模型判断；SQLite 仍是需求驱动候选，不是本轮实施决定。

#### I1

**现有四项功能任务**

[#1 command366](https://github.com/Kakusya/AbilityKit/issues/1) · [#2 共享客户端](https://github.com/Kakusya/AbilityKit/issues/2) · [#3 rich 四断点](https://github.com/Kakusya/AbilityKit/issues/3) · [#4 P6 投影](https://github.com/Kakusya/AbilityKit/issues/4)

Issue 是动态链接。状态依据 2026-10-04 04:02 UTC 已确认操作：四项全部 blocked，保留原计划等待架构优先评估。源码与历史证据仍按各自固定 SHA 解释。

#### G6

**审计后仅文档变化**

[reclone handoff](https://github.com/Kakusya/AbilityKit/blob/5312c6e4bf2b612260297e2d8623aa362a9051e6/Docs/design/CookingGame/reclone-handoff-2026-10-04.md)

04:10 UTC 对比核实 master 5312c6e4 仅增加该文档 48 行，无代码或配置变化；旧源码结论适用范围不变，未新增构建或运行证据。

#### I2

**新架构合同任务**

[#5 当前 ET 架构合同与 AGENTS](https://github.com/Kakusya/AbilityKit/issues/5)

04:13 UTC 已发布并标记 orca-ready。范围是实际源码、目标树、消息、事务与 AGENTS 的文档和规则工作，不含运行时迁移；#1–#4 继续 blocked。

后续实施以 #5 的审阅结果组织：先确认 Cooking 领域权威、当前 LiteNet/Wire3、分阶段提交与门禁合同，再审批首个物品归属纵切。其他宿主清退、ET 全网络接管、格式替换、发布与升级均不由本报告自动启动。


---

## AGENTS proposal

完整补充草案。状态仍为提案，尚未写入仓库 AGENTS.md、尚未生效；#5 可在批准范围内审阅和整合，不能用此草案扩大实施范围。当前事实区已补充审计与交接提交的区别、最新 Issue 调度和执行前复核要求。

### AbilityKit 架构与依赖治理补充草案

状态：提案，尚未写入仓库 AGENTS.md，尚未生效。下文用于审阅合并，不构成实施、发布或升级授权。合并时保留仍有效的工作区安全与 Orca/Trellis 规则；历史执行通知移到历史证据索引，不重复堆在本文件顶部。

#### 稳定原则

##### ET 应用主干与权威状态

- 本轮先试用仓库内现有 ET 版本，不升级到最新版，不把升级 ET 当作重构前置条件。现有来源标签是 core@3.0.3 与 sourcegenerator@3.0.1；以锁定的仓库提交、文件哈希和许可清单识别实际副本，不能仅凭包名认定与上游完全一致。
- Cooking 以 ET Entity、Component 与 EntitySystem 作为应用组织主干的目标，应通过纵切迁移证明。每个可变业务状态只能有一个权威所有者，每种资源只能有一个释放责任方；一律在设计中列出所有者、允许写入的 System、驱动时钟、生命周期和持久化/投影出口。
- ET Component、旧 Runtime、World/ECS、Session、客户端和 Unity View 不得各自保留同一份可独立修改的权威业务状态。迁移期适配器只转交调用或生成只读投影；双路比对只能有一条写入/提交路径，并有移除条件。
- AbilityKit 能力作为 ET 所有者调用的无状态服务或显式受托模块使用；服务私有缓存、索引和计算工作集必须可从权威状态重建，不能成为隐蔽的第二份业务真相。保留独立 ECS 子域必须通过 ADR 写明不同的数据所有权和无法替代的消费者。
- ET 的父子树表达生命周期所有权，不直接充当业务关系、跨进程身份或存档格式。引用需声明强/弱关系、失效和重建规则。实体销毁、异步取消、事件退订、计时器取消、网络关闭与池化归还均必须可重复调用且不产生重复效果。
- 一个权威房间只有一个已指定的逻辑 Tick/调度入口。网络线程、表现 Update、诊断工具和回调不能旁路驱动模拟或提交业务。异步继续执行前应重验 owner 存活、实例代次、取消状态及会话/关卡范围；不得让旧 incarnation 的回调写入新对象。

##### 消息与事件契约

- 命令表达意图，领域事件表达已经提交的事实，查询/投影只读。命令必须经权威入口验证身份、范围、版本、顺序和幂等键；传输 ACK、业务接受、业务提交和客户端投影完成是不同状态，不得混为成功。
- 事件契约必须写明 owner、payload、执行线程/时钟、发布时点、顺序、异常传播、重入、订阅释放与参数所有权。局部事件总线不能直接充当网络协议或持久化日志；隔离监听异常的通知通道不能承载必须失败回滚的业务提交。
- 跨层只传显式 DTO、稳定业务 ID 与版本信息，不暴露 ET Entity 对象、DI 容器、Unity 对象或内部可变集合。允许的 transport/serializer 由边界清单指定；不得为同一用途再新增未经审议的自定义编码器、消息总线或调度器。
- 网络协议、存档与配置 schema 的权威源各自唯一。生成文件不得手改；生成器及插件锁定版本后可从干净输入重建，Check 模式不得改文件。删除字段/编号不得重用；新增字段、未知值、旧版本升级或明确拒绝策略、大小上限与恶意/损坏输入均需测试。不同 schema 不因使用同一序列化库就自动兼容。

##### 先用已有成熟能力

- 增加基础设施前依次检查：当前已采用框架在锁定版本中的官方能力、平台/BCL 官方能力、仓库已有且边界适合的实现、仍维护且适配目标宿主的成熟依赖。只有这些途径不满足明确需求时，才自研基础设施。此顺序不要求引入不需要的整套框架，也不要求统一所有不同语义的技术。
- 每个新增依赖、上游源码复制、长期 fork、第二套同类机制或显著自研基础设施必须有 ADR。ADR 写明需求、现有能力缺口、候选与拒绝理由、适用宿主、许可证/再分发、维护人、版本/来源、迁移测试、回退和退役条件。一般业务规则、很薄的适配器和一次性工具不因这条规则被迫套通用框架。
- 依赖选型不得只用流行度或库数量论证。以减少实际维护面、兼容已使用 API/数据、故障行为、可测试性与部署成本为依据。不得把引入库本身当作完成功能、正确性或性能验收。
- DI 负责服务装配，ET 负责已指定业务对象生命周期；两者不得同时释放同一对象或各自拥有相同业务状态。现有 World.DI 的替换须先证明所有权、初始化、播种实例、销毁顺序与消费者兼容，不能按名称相同批量替换。
- 日志/观测必须从提交结果旁路采集，不能驱动业务。宿主通过适配器选择日志 sink；核心不绑定日志后端或遥测厂商。诊断相关 ID 与游戏业务 trace 的含义保持区分；默认不记录凭证、完整个人信息或无界 payload。
- 存储引擎只解决持久化机制；结算幂等、范围身份、恢复规则、业务一致性仍由应用定义。声明“持久”时明确覆盖正常重启、进程崩溃、OS/掉电中的哪些故障模型，不以文件存在、hash 校验或序列化成功推导事务/掉电保证。

##### 版本 来源和兼容

- SDK、Unity Editor、UPM/NuGet/npm 依赖、生成器及 vendored 源码记录可重现版本。允许的版本差异须按宿主列出原因和测试，不能把全部版本机械拉齐；新代码不得使用浮动 latest/main 或未经审查的自动升级。
- .NET 使用已验证的 SDK 版本及明确 rollForward 策略；可执行/test 入口提交恢复闭包并以 locked mode 验证。共享库自己的 lock 不能替代最终应用解析闭包。UPM manifest 与 lock 一起审阅，Git 依赖指向不可变提交。
- 第三方清单记录官方来源 URL、tag/commit、文件/二进制哈希、原始许可/notice、修改补丁、使用与发布范围以及负责人。包的包装版本不等于上游版本；内嵌 DLL、工具二进制也在清单内。
- internal-only 或许可未明确的代码必须被发布门禁显式拒绝，并验证传递依赖闭包；不能仅靠目录前缀、显示名称或 IsPackable=false。现有 ET 副本在许可/再分发范围单独确认前维持 internal-only，不能套用其他 ET 版本的许可结论。
- 共享源码按实际 .csproj Compile Include/Remove、asmdef、UPM 依赖及生成器闭包审查。纯 .NET 编译不证明 Unity Mono/IL2CPP/AOT、Unity 编译器版本或跨宿主二进制兼容；平台兼容测试必须逐项记录。

##### 工程门禁和证据

- 架构规则必须有对应的机器检查或明确人工审阅项。依赖方向、禁用命名空间、包/程序集直接依赖、重复权威写入口、生成产物漂移及 internal-only 发布边界均列入门禁清单。检查器本身需要一个正确样例与至少一个故意违反规则的负例。
- 延续 tools/test-gates.json 作为门禁定义入口；根据改动路径执行对应门禁，而不是仅以 MOBA precheck 代表所有子系统。CI workflow/外部执行器、触发条件、required check 名称和产物链接必须核实到实际提交。
- 结果使用 Passed、Failed、Blocked、Skipped、NotRun，并记录覆盖范围。缺 Unity/SDK、跳过、零测试、旧产物或源提交不匹配不能记 Passed；exit 0 只表示进程退出状态，不替代测试/产物语义。必需覆盖缺失则该交付不具备合并或阶段验收条件。
- 每次验收保存源 SHA 与 dirty 状态、工具/依赖版本、实际命令、退出码、测试数、原始日志/结果、构建产物身份及适用范围；生成报告不能覆盖或美化原始失败。性能延期不得写成性能通过。
- 删除旧框架/适配器前先完成反向依赖清单、替代消费者与验收资产；迁移采用可回退的纵切，禁止“外壳接好”就宣称已完全迁移。
- dot 负责审议、方案与 Issue 组织；本地 Orca 按批准范围实施。只有用户已批准范围、且 readiness、依赖和实施边界均已审阅的有界 Issue，才可通过 orca-ready 交给本地 Orca。标签不得额外授权扩大范围、升级、合并或发布；调查报告、草案与 Proposed ADR 本身不构成实施许可。dot 可在用户明确的优先级授权内调整调度；若授权或冲突不清楚，先询问，不自行启动实现。

#### 当前基线事实

以下是待合并时重新核验的事实区，不是长期政策：

- 本草案源码审计基线为 master a2cd7284e12d50a10bbb7abee6fc265577b9aa7c。补充核验的 master 5312c6e4bf2b612260297e2d8623aa362a9051e6 仅新增 reclone handoff 文档 48 行，源码与配置未变；不把交接文档提交当作新的构建或运行验收。Unity ProjectVersion 为 2022.3.62f1；主要 src 项目为 net10.0，Orleans 示例另有 net8.0，生成器有 netstandard 目标。
- 当前 ET internal runtime 来自仓库 core@3.0.3 / sourcegenerator@3.0.1 子集。其 notice 明示未包含序列化、网络、mailbox、loader 与 scheduler；不能按“已采用 ET”假定这些能力已接入。
- 当前无根 global.json 与 NuGet packages.lock.json；存在 Unity/Packages/packages-lock.json。不同宿主声明的 MemoryPack/Entitas 版本存在差异，实际恢复闭包及跨宿主兼容需验证。
- tools/test-gates.json 配置了 32 个门禁；其引用的根 .github/workflows/abilitykit-test-gates.yml 在该提交不存在。这只证明仓库未携带该 workflow，不证明所有外部 CI 不存在。
- 当前 Unity 编译辅助脚本缺安装时 exit 0，父门禁按退出码将该脚本记为 Passed。修正前必须人工将该覆盖标为 Skipped/Blocked，不可据此声称 Unity 验证通过。
- 当前阶段与剩余出口从一个最新状态入口读取：Docs/design/CookingGame/progress.md，再沿其链接核验 ADR、spec、task/check 与原始结果；实施授权还必须核对对应 Issue 的最新批准范围与标签。历史会话通知移至历史索引；不在 AGENTS 顶部叠加多份“当前”状态。
- 本次交接调度（2026年10月4日）：#5 是唯一 orca-ready，范围限于已约定的架构合同、文档与 AGENTS 规则工作；旧 #1–#4 及新 #6–#9 均为 blocked。阅读本草案、报告或 blocked Issue 不构成启动实施的授权。标签表示批准范围内的调度条件，不代表已认领、已开工、已停止或已完成；执行前重新读取对应 Issue。

#### 计划 不代表已完成

1. 复审本草案并指定每项规则对应的 gate 与 owner。
2. 修复验收状态表达，补实际 CI 路由及负例，核实 required checks。
3. 固定已验证工具链，整理依赖闭包/来源清单与 internal-only 发布拒绝测试。
4. 先以当前 ET 版本完成最小纵切和回退证明，再决定能力迁移与旧实现退役范围。
5. 日志适配、配置管线归一及持久化机制选择按具体缺口分开审议，不一次性更换所有依赖。

[返回阅读顺序](#orca-reading-guide)
