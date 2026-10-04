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
