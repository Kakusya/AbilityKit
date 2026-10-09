# AbilityKit 工作区指引

## 核心规则
- 永远要以推进业务为核心。
- 使用的语言要非常浅显易懂,使用的语种为简体中文.

## Owner SHA 校验禁令（2026-10-09）

- 禁止模型执行、要求或通过脚本、工具、执行者间接进行任何形式的 SHA 校验，包括 SHA-1、SHA-256、文件／产物／源码 hash 比对、批量 hash 清单及精确 SHA 审阅。
- 不得将 SHA 校验作为研究、审计、测试、提交、推送、交付或恢复工作的前置条件；不得用历史门禁、dot 流程、技能或证据规范绕过本条。
- 本条先于仓库中既有 SHA 校验和精确 SHA 审阅要求；只有 Owner 后续明确改变此规则，才允许例外。

## Owner JSON 证据产物禁令（2026-10-09）

- 禁止模型新增展开、美化的 JSON 证据、全量状态快照及其副本。
- 测试原始输出只留在原输出目录；禁止复制到 Trellis、research、evidence 或其他归档目录，禁止提交 Git 或推送。
- 排查只读取必要字段，禁止在终端或报告中输出完整 JSON 状态；报告只记录简短结果、失败原因、原日志路径和退出码。
- 禁止新增证据包、快照归档和批量 hash 清单；不得以研究、审计、可追溯、补齐验证或技能要求绕过。单行化、压缩或改名也不授权复制全量状态。
- 本条先于历史证据、审计与技能产物要求。只有 Owner 后续明确指定具体产物，才允许例外。

## Owner 测试范围约束（2026-10-09）

- Owner 否定本轮 88 项框架控制作为工作目标，要求禁止模型继续自主编写类似测试。本条先于历史测试计划、门禁要求、技能的“补齐测试”建议及 dot 方案适用；它们不能增加测试授权。
- 模型不得自行新增或扩写“测试测试框架”的控制套件、断言清单、合成状态／证据、mock/fake、通过反射访问私有实现的测试或故障注入套件；不得以覆盖率、控制数量、审计完整性或“修复前置”为由扩大工作。
- 测试只使用 Owner 指定的新 Cooking FlowAcceptance 框架及真实 Flow 场景；禁止使用旧 rich／NetworkRichRecoveryAcceptance 测试入口。新增场景必须来自 Owner 明确指定的可观察行为、输入和预期结果；模型不能自行把框架内部校验变成新的测试目标。框架专用测试需要 Owner 后续明确指定该具体场景。
- 协调者派发时列明允许修改的文件及已指定的 Flow 场景；禁止执行者追加测试文件、控制 profile／request／rule 或测试专用入口。审查实际差异；超出指定范围的测试改动不接受、不派生补齐任务。
- 禁止以这类控制的 Passed 数量代替产品验收或完成进度。代码与记录的舍弃或清理由 Owner 决定。当前工程保持暂停，不能沿用已删除 Issue 的旧派发或规划恢复执行。
- 本条是模型与审阅约束，不是已经安装的文件权限、CI 或运行时封锁；不得宣称拥有尚未配置的机器强制保障。

## 最高优先级产品范围（Owner 重申，2026-10-05）

- 只推进 Cooking（Cook／做菜项目）。Shooter、MOBA、Orleans 等其他项目是示例，不得修改其代码、配置、协议、脚本、测试或专用门禁，不为其补齐功能、修复失败或维护示例完整性。
- 本范围约束先于 Issue 优先级、历史计划、标签、SOP 和执行者派发适用；它是授权边界，不能用 P0、统一基础设施、全仓回归或“顺手修复”绕过。只有 Owner 后续明确改变范围才可解除。
- 共享框架／工具改动必须服务于已批准的 Cooking 具体需求，计划与审计逐文件说明 Cooking 消费者、必要性和验收。不得把无关示例的适配、迁移或修复列为 Cooking 的隐藏前置；不清楚关联时停止该部分实施并重审范围。
- 已产生的越界示例改动保留原提交和证据，隔离且不得合入 master；撤销相应后续派发。通过测试不代表范围被授权，不能沿用此前整项 Issue 的合并授权接纳越界改动。
- 恢复、范围审议和来源搜索必须枚举本仓库 Git／Orca worktree，检查相关其他工作树的 AGENTS、task、research 和实际改动，不能只搜索当前工作树；尤其检查 `issue6-test-gate-results`。跨树搜索不授权修改或清理用户已有工作。

当前阶段、验证指针与完整剩余出口的唯一入口是 [Cooking progress](Docs/design/CookingGame/progress.md)。执行前再读对应 Issue 的最新正文、标签、依赖与批准范围；状态摘要和 `orca-ready` 不增加授权。历史通知与损坏原文见 [历史索引](Docs/design/CookingGame/history/agents-notices-2026-10-04.md)。

## 任务与模块路由

先判断请求涉及什么，再按需读对应入口。文档链接说明“去哪里找”，不增加修改、测试、恢复、合并或发布授权。

| 请求涉及 | 先读 | 再读 |
|---|---|---|
| Cooking 当前工作、恢复或剩余目标 | [progress](Docs/design/CookingGame/progress.md) | [.trellis/spec/cooking/index.md](.trellis/spec/cooking/index.md)、对应 task／最新 Issue；先应用上方暂停与 Owner 约束 |
| Cooking 玩法、菜单、配置、网络或存档 | [模块路由：Cooking](.trellis/spec/abilitykit/module-routing.md#cooking-应用) | 表中对应稳定契约；规划再读 [gameplay-menu-plan](.trellis/spec/cooking/gameplay-menu-plan.md) |
| 共享框架、协议或跨模块问题 | [工程规范](.trellis/spec/abilitykit/index.md)、[模块路由：共享框架](.trellis/spec/abilitykit/module-routing.md#共享框架) | 对应设计／包文档与实际源码；修改须先证明 Cooking 消费者和必要性 |
| Cooking 防火墙、端口、房主 READY | [本机联网工具规范](.trellis/spec/abilitykit/cooking-network-tooling.md) | `tools/cooking-firewall.ps1` 及对应 Cooking 调用方；不据工具说明自行运行验收 |
| AGENTS、Trellis、任务、协作或工作树 | [.trellis/workflow.md](.trellis/workflow.md)、[协作 SOP](.trellis/spec/abilitykit/supervised-issue-delivery.md) | 相关配置、task 与已批准的工作树；模块路由维护规则见 [路由规范](.trellis/spec/abilitykit/module-routing.md#路由维护) |
| 产品方向、架构取舍或来源冲突 | [长期目标](ADR/long-term-goals.md)、[ADR 索引](ADR/README.md) | 对应 Accepted ADR 与 [ET 合同](Docs/design/CookingGame/current-et-foundation-contract.md)；明确列出冲突 |

- 只读当前问题相关的模块行及其文档，不把全部设计、历史 task 和所有技能加载成必读上下文。
- 跨模块问题按 Cooking 消费链读取，分别说明各层职责；不能由名字相似推导复用或实施许可。
- 共享源码主要在 `Unity/Packages/com.abilitykit.*`，Cooking 应用源码在 `src/AbilityKit.Game.Cooking*`。`src/` 既有 Compile Include 工程，也有自有源码，以实际 `.csproj` 为准。
- 未命中路由时，从 [设计总索引](Docs/design/00-index.md) 和包文档定位；入口缺失、历史文档与源码冲突时如实记录，不恢复已删除技能、不猜测 API、不扩大范围。
- 本文件的 Owner 约束先于链接中的历史通知、命令和技能流程。用户要求不测试时，停止测试路由，仅审阅文档与必要源码。

## 架构与权威

- 当前 ET 固定仓库副本 `core@3.0.3`、`sourcegenerator@3.0.1` 和提炼 runtime；包版本不等于统一 ET 大版本。禁止隐式升级或把完整 ET 网络/调度栈作为隐藏前置。来源、宿主闭包、现状/目标与规则检查映射见 [当前 ET 合同](Docs/design/CookingGame/current-et-foundation-contract.md)。
- Cooking 目标是由 ET Entity/Component/System 实际持有可变领域状态。每状态族指定唯一 writer、释放责任方、允许 System、驱动时钟和导出/恢复出口；迁移时同时撤销旧可写权威。适配器可委托调用或导出只读 DTO；合法查询、无状态算法和库复用不受禁止。
- ET Parent 只表达生命周期归属，业务关联使用稳定 ID；不得把 Entity/EntityRef/InstanceId、DI 容器、Unity 对象或内部可变集合当 wire/存档身份。异步继续前重验存活、代次、取消与 scope；销毁、取消、退订、关闭和归还须有重复调用控制。
- 一房间只有一个权威 Tick 入口。网络回调仅入队，Unity/诊断不能旁路提交。owner、生命周期、五类消息或事务语义变化必须有前后对照、消费者影响及正负控制。
- 保留当前分阶段提交：accepted command 的 effects/event/Executed terminal 在随后 fixed-step 失败时保留，LogicalTick/HostFrameSequence 不推进。整帧回滚是独立行为变更，不能藏在重构中。同 stable ID/同 payload 重试不得重复扣料、结算或新增事件。
- Command、准入/处理终态、已提交 Domain Event、完整 Projection/Baseline、Join/ACK/Ready/Close/Rebind 分别定义。Task 完成、传输 ACK、业务提交、客户端完整投影和精确 ACK/Ready 是不同事实。通知监听异常不等于业务回滚；事件须注明线程、发布时点、顺序、重入、异常与订阅释放。

## 复用、依赖与证据

- 新基础设施先评估锁定版本的当前框架能力、平台/BCL、已有适配和成熟依赖。新增依赖、复制上游、长期 fork 或第二套同类机制须 ADR 记录差距、拒绝理由、宿主、维护者、来源/许可、测试、回退和退出条件；薄适配与业务规则无需强套通用框架。
- DI 装配与 ET 释放责任不得重叠。日志与观测从提交结果旁路采集，不驱动成功，不默认记录凭证或无界 payload。持久化须声明正常重启/进程崩溃/OS/掉电故障模型；文件存在、hash 或序列化成功不能证明掉电事务。
- 网络、存档、配置 schema 各有唯一权威源；生成文件不得手改，Check 不得改输入/产物。变更覆盖旧新互读或明确拒绝、未知/缺失字段、编号不重用、损坏与超限；同一 serializer 不意味着 schema 兼容。
- 工具链、生成器、UPM/NuGet/npm 与 vendored 来源记录不可变版本、patch、license/notice、宿主、消费者和退役条件；遵守上方 SHA 校验禁令。新依赖不使用浮动 latest/main；允许的版本差异逐宿主审阅。SDK/最终应用恢复闭包待补齐时如实标待建，不虚称已锁定。
- 当前 ET 保持 internal-only；发布必须检查受限代码的传递依赖闭包，不能仅靠名字或 IsPackable=false。未知许可/再分发范围独立审阅，不据其他版本推断当前授权。
- 架构规则映射到合同中的现有 gate、待建 gate 或明确人工 check；新机器检查必须有正确正例与故意违规负例。AKET001/002 不代表完整所有权防线。
- 结果使用 Passed / Failed / Blocked / Skipped / NotRun；声明覆盖所需环境缺失、声明应执行测试或必需测试覆盖却实际零测试、旧产物均不得记 Passed。纯构建或文档检查可按自身声明覆盖记 Passed；不适用的测试明确记 N/A，未运行的测试记 NotRun，不得冒称测试通过。记录工作分支／dirty、工具版本、实际命令／退出码、覆盖数量、原日志路径和已知运行程序身份；不做 SHA 校验、不复制原始输出、不新增 JSON 证据。保留原失败，性能延期不等于通过。CI/required checks 需实际核实，不能由配置引用推断已存在。
- 默认由协调审计者接替 dot，负责方案、审阅与 Issue 组织，Orca 执行者在已批准边界内实施；历史 dot 审阅保留来源，默认不再等待该角色。Owner 允许时可就具体疑问咨询 dot；咨询不等于启动完整工作流或恢复旧工程。仅显式调用 [$cooking-dot-workflow](.agents/skills/cooking-dot-workflow/SKILL.md) 时，由调用主会话唯一调度、dot 做最终技术规划与裁决，按 [协作 SOP 的显式流程约定](.trellis/spec/abilitykit/supervised-issue-delivery.md#显式-cooking-dot-工作流2026-10-05-owner-批准) 执行该需求授权与恢复；不得执行其中已被上方 Owner 禁令覆盖的 SHA 校验、JSON 证据归档或框架控制扩写。旧 orca-ready/#6 不解锁。交付、依赖调度与收尾遵循 [协作 SOP](.trellis/spec/abilitykit/supervised-issue-delivery.md)。经批准且依赖已审阅的有界任务可用 orca-ready 交接；标签、报告、草案和 Proposed ADR 不授权升级、扩大范围、合并或发布。删除旧框架先查反向消费者及替代验收。

## 项目与来源边界

- 项目是 Unity UPM + 纯 C#/.NET 工具库；Cooking 是应用产品方向。先读 [长期目标](ADR/long-term-goals.md)、[ADR 索引](ADR/README.md)；游戏规则、房间流程和权威策略由应用层拥有，不塞入通用框架。
- `Unity/Packages/` 是共享源码主入口；相关 `src/` 工程以 Compile Include 复用，Cooking 应用另有自有源码。修改前同时核对 csproj、asmdef、包依赖与生成器闭包；纯 .NET 编译不能证明 Unity Mono/IL2CPP/AOT 或跨宿主兼容。Server/Orleans 是示例，不是游戏必需服务；Coordinator 为精简契约，不假设旧 SessionCoordinator 或 Local/Remote/Hybrid 实现。
- 应用路线唯一正文为 [technical-roadmap](Docs/design/CookingGame/technical-roadmap.md)，规范入口为 [cooking index](.trellis/spec/cooking/index.md)。相关规划先读 [gameplay-menu-plan](.trellis/spec/cooking/gameplay-menu-plan.md) 及其 Task 注册/菜单整合/架构记录路由。参考资料先读 [reference README](Docs/design/CookingGame/reference/README.md)，再读主题；参考、路线、计划不代表实现或验证。
- 项目待办统一在 [Docs/Todo](Docs/Todo.md)；菜单原始资料见 [menu-v0.1](Docs/design/CookingGame/reference/menu-v0.1/README.md)，候选目录不等于 runtime 配置。保持已确认固定伙伴、自然完成和成功检查点语义。
- 来源归属：长期方向/空白 → ADR/long-term-goals；架构取舍 → ADR/decisions；框架设计 → Docs/design；工程/稳定契约 → .trellis/spec；当前目标/研究/check → .trellis/tasks；.trellis/migration 只读。冲突显式列出，不自行合并成产品新语义；Proposed 不能因写完提案改 Accepted。

## Cooking 本机防火墙与共享端口工具

按需读取 [本机联网工具规范](.trellis/spec/abilitykit/cooking-network-tooling.md)，包含默认 UDP 范围、安装、共享配置、GetPort、READY 核对和阻止规则修复。操作说明不提供测试或环境修改授权；端口查询、防火墙规则、应用监听和真实远端连通分别判断。

## 构建与验证路由

- 先应用 Owner 测试范围与本轮授权，再读 [Cooking FlowAcceptance](Docs/design/CookingGame/testing/fixed-flow.md)。不将全仓 gate、示例 build、旧 rich、框架控制或 Unity 检查当作默认下一步；用户要求不测试时，本路由停止执行。
- [测试规范](Docs/AbilityKit测试门禁与批量回归规范.md)、[历史命令](.trellis/spec/abilitykit/validation.md) 与 `tools/test-gates.json` 只用于定位既有配置；配置存在不证明已授权、已运行或已通过。协议生成先读 [Protocols README](Protocols/README.md)，不默认导出 shooter／moba。
- 实际构建目标与 SDK 以相关 `.csproj` 和已存在的配置为准；主要 .NET 工程为 net10.0，README 中的 SDK 版本不证明已有固定配置。Unity 工程版本为 2022.3.62f1；Cooking Unity 仍后置且未授权实施。不得编辑 Unity 自动生成 csproj、Library/、Temp/，不得删 Editor 锁文件。
- 报告只记录实际执行的结果、退出码、未执行原因和原日志路径。未运行的测试记 NotRun；物理双机不可用时保持 NOT_VERIFIED，不用同机替代、不重复询问硬件，见 [physical runbook](.trellis/tasks/10-02-cooking-network-gameplay-loop/research/physical-lan-runbook.md)。

## Orca、工作树与 Trellis

- 修改前看 Git 状态，保留用户解决方案及 `Unity/Assets/Practice/`、`src/AbilityKit.Demo.MyPractice/`；不用清空/reset/全局 kill 处理任务。
- 获准并行工程使用 Orca 受管 worktree 与 supervised orchestration；恢复核对实际 worker/dispatch/进程，不凭状态文件重启。本轮文档任务不得清理用户工作树、备份或进程，不沿用旧合并授权。
- 全项目长期约束：worktree 只能由根协调者直接创建。任何处于非主工作树中的 worker、agent 或终端都不得调用 `orca worktree create`、`orca orchestration worker-start --worktree new-child|new-top-level`、`git worktree add` 或等价入口来创建后代工作树，也不得委托其他会话代建；需要新的源码隔离时必须停止当前扩展并上报根协调者，由根协调者复用现有工作树或记录必要性后创建。该约束不禁止根协调者在已经批准的现有工作树中启动新终端。违反该约束产生的运行与验证不得作为独占执行证据，派生资源须保留到根协调者完成审计和回收。
- 持续检查 worktree 生命周期。仅在无活动任务/进程、改动已合并或妥善保存、证据已迁出后按授权及时清理；worker 停止不等于分支可删。保留来源、更新路由，见 [生命周期记录](.trellis/tasks/10-02-cooking-gameplay-menu-plan/research/worktree-lifecycle.md)。Windows 递归删除/移动前核对绝对目标位于指定 workspace，不跨 shell 拼删除命令。
- 本仓库只用 Trellis 管理工程任务与记录。复杂任务在 planning 完成并审阅 prd/design/implement/context manifests，明确批准后才 in_progress。恢复读 task 全部产物、research 与 manifest 规范；迁移 planning/blocked 不意味着已实施。验证、提交、归档独立，不提交本机 developer/runtime/cache/log。
- Trellis 安装 0.6.17，Node >=18、Python >=3.9，见 [安装参考](ADR/reference/README.md)。共享配置 .trellis/；宿主集成 .zcode/、.codex/、.agents/skills/trellis-*。磁盘 hook/技能存在不代表宿主已批准或加载；技能列表需新会话刷新，宿主禁用 hooks 时按 bridge 提示安装后新开会话。
