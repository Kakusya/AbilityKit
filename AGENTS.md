# AbilityKit 工作区指引

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
- 工具链、生成器、UPM/NuGet/npm 与 vendored 来源记录不可变版本、hash、patch、license/notice、宿主、消费者和退役条件。新依赖不使用浮动 latest/main；允许的版本差异逐宿主审阅。SDK/最终应用恢复闭包待补齐时如实标待建，不虚称已锁定。
- 当前 ET 保持 internal-only；发布必须检查受限代码的传递依赖闭包，不能仅靠名字或 IsPackable=false。未知许可/再分发范围独立审阅，不据其他版本推断当前授权。
- 架构规则映射到合同中的现有 gate、待建 gate 或明确人工 check；新机器检查必须有正确正例与故意违规负例。AKET001/002 不代表完整所有权防线。
- 结果使用 Passed / Failed / Blocked / Skipped / NotRun；声明覆盖所需环境缺失、声明应执行测试或必需测试覆盖却实际零测试、旧产物、SHA 不匹配均不得记 Passed。纯构建或文档检查可按自身声明覆盖记 Passed；不适用的测试明确记 N/A，未运行的测试记 NotRun，不得冒称测试通过。记录源 SHA/dirty、工具版本、实际命令/退出码、覆盖数量、原始结果和二进制身份；保留原失败，性能延期不等于通过。CI/required checks 需实际核实，不能由配置引用推断已存在。
- 默认由协调审计者接替 dot，负责方案、审阅与 Issue 组织，Orca 执行者在已批准边界内实施；历史 dot 审阅保留来源，默认不再等待该角色。仅显式调用 [$cooking-dot-workflow](.agents/skills/cooking-dot-workflow/SKILL.md) 时，由调用主会话唯一调度、dot 做最终技术规划与裁决，按 [协作 SOP 的显式流程约定](.trellis/spec/abilitykit/supervised-issue-delivery.md#显式-cooking-dot-工作流2026-10-05-owner-批准) 执行该需求授权、精确 SHA 审阅及恢复；Owner 范围与真实证据仍优先，旧 orca-ready/#6 不解锁。交付、依赖调度与收尾遵循 [协作 SOP](.trellis/spec/abilitykit/supervised-issue-delivery.md)。经批准且依赖已审阅的有界任务可用 orca-ready 交接；标签、报告、草案和 Proposed ADR 不授权升级、扩大范围、合并或发布。删除旧框架先查反向消费者及替代验收。

## 项目与来源边界

- 项目是 Unity UPM + 纯 C#/.NET 工具库；Cooking 是应用产品方向。先读 [长期目标](ADR/long-term-goals.md)、[ADR 索引](ADR/README.md)；游戏规则、房间流程和权威策略由应用层拥有，不塞入通用框架。
- `Unity/Packages/` 是共享源码主入口；`src/` 以 Compile Include 复用。修改前同时核对 csproj、asmdef、包依赖与生成器闭包；纯 .NET 编译不能证明 Unity Mono/IL2CPP/AOT 或跨宿主兼容。Server/Orleans 是示例，不是游戏必需服务；Coordinator 为精简契约，不假设旧 SessionCoordinator 或 Local/Remote/Hybrid 实现。
- 应用路线唯一正文为 [technical-roadmap](Docs/design/CookingGame/technical-roadmap.md)，规范入口为 [cooking index](.trellis/spec/cooking/index.md)。相关规划先读 [gameplay-menu-plan](.trellis/spec/cooking/gameplay-menu-plan.md) 及其 Task 注册/菜单整合/架构记录路由。参考资料先读 [reference README](Docs/design/CookingGame/reference/README.md)，再读主题；参考、路线、计划不代表实现或验证。
- 项目待办统一在 [Docs/Todo](Docs/Todo.md)；菜单原始资料见 [menu-v0.1](Docs/design/CookingGame/reference/menu-v0.1/README.md)，候选目录不等于 runtime 配置。保持已确认固定伙伴、自然完成和成功检查点语义。
- 来源归属：长期方向/空白 → ADR/long-term-goals；架构取舍 → ADR/decisions；框架设计 → Docs/design；工程/稳定契约 → .trellis/spec；当前目标/研究/check → .trellis/tasks；.trellis/migration 只读。冲突显式列出，不自行合并成产品新语义；Proposed 不能因写完提案改 Accepted。

## Cooking 本机防火墙与共享端口工具

- 源码入口：[cooking-firewall.ps1](tools/cooking-firewall.ps1)，默认配置：[cooking-network.defaults.json](tools/cooking-network.defaults.json)。只服务 Cooking Windows 联网测试；默认开放 **UDP 18090–18099**，Private/Public 网络类别，来源限制为 LocalSubnet。不切换网卡类别、不关闭防火墙，不自动开放 TCP。
- 管理员终端配置：`powershell -ExecutionPolicy Bypass -File tools/cooking-firewall.ps1 -Action Open`。自定义范围同时指定 `-StartPort` 与 `-EndPort`。只有规则实际写入并核对后，才保存到 `%LOCALAPPDATA%\AbilityKit\CookingNetwork\ports.json`；各 worktree 共享本机这份配置，后续工具读配置，不把端口常量复制到各工作树。参数覆盖已保存配置，已保存配置覆盖随工具分发的默认值；损坏或不支持的配置报错，不静默回退。
- 固定位置安装：`powershell -ExecutionPolicy Bypass -File tools/cooking-firewall.ps1 -Action Install`，将工具及默认配置复制到 `%LOCALAPPDATA%\AbilityKit\CookingNetwork\bin`，向当前用户 PATH 去重追加目录；新开终端后运行 `cook-firewall -Action Show` 或 `cook-firewall -Action Check`。固定安装无常驻服务；PATH 只负责找到命令，不授予 worktree 子进程防火墙权限。
- Windows Cooking 联网测试现在自动调用 **`GetPort`**：`run-cooking-network-process-acceptance.ps1`、`run-cooking-network-concurrency-acceptance.ps1`、`run-cooking-network-rich-recovery-acceptance.ps1`、`run-cooking-network-process-measurement.ps1` 与固定 Flow 的 Network 模式均在启动房主前取端口。优先调用 `%LOCALAPPDATA%\AbilityKit\CookingNetwork\bin\cooking-firewall.ps1`，未安装时调用当前仓库工具；两者读取同一份本机配置。包装脚本默认 `-Port 0` 表示自动取端口，显式非零 `-Port` 保留调用方选择；Client 只使用房主端口，BuildOnly 不取端口。
- 自动路径将 JSON `port` 直接交给房主，核对实际 `READY` 的端口与进程身份后再启动客户端。端口选择仍只由工具查询共享配置和 UDP 占用；`portReserved=false` 表示未保留候选。范围耗尽、工具失败、绑定冲突或 READY 不一致均明确失败；重新运行测试会重新取端口，不静默退回随机端口或范围外端口。选择与实际端点证据保存在运行目录。手动启动仍先执行 `cook-firewall -Action GetPort`。
- `Show` 读取配置；`Check` 核对本机规则。两者只读，可通过 `-ProgramPath` 检查确切 Cooking EXE 的阻止规则。显式 Block 优先于 Allow；若需修复该程序冲突，管理员使用 `Open -ProgramPath '<实际 Cooking EXE 完整路径>' -RepairProgramBlock`，先备份，再仅从该程序的本地 UDP 阻止规则扣除配置端口范围，保留范围外端口和原 TCP 规则；不删除其他应用或策略规则。输出提供备份路径，回退使用 `-Action RestoreBlock -BackupPath '<备份路径>'`。
- 默认 Check，操作输出 JSON，失败返回非零退出码；`-ConfigPath` 用于显式隔离配置或测试。`Show` 与 `GetPort` 只读配置／端口占用，不证明防火墙或远端连通。规则核对、端口占用、应用监听、远端实际连通是不同事实；不能用 `Test-NetConnection -Port` 的 TCP 测试证明 UDP。缺少实际物理双机证据时连通性保持 NotRun/NOT_VERIFIED。现有房主输出 `READY <实际端口> <PID>`；本工具获取候选端口并管理防火墙及范围配置，尚不负责房主启动／关闭／查询或端口保留。
- 聚焦控制：`powershell -ExecutionPolicy Bypass -File tools/cooking-firewall.tests.ps1`。测试使用隔离模拟，不修改真实防火墙或用户 PATH；本机应用另保留规则、端口过滤器、配置与原始退出码证据，不把它当产品 LAN 验收。
- 集成控制：`powershell -ExecutionPolicy Bypass -File tools/cooking-test-ports.tests.ps1`；固定流程回归：`dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj -m:1 --filter "FullyQualifiedName~CookingFixedFlowTests|FullyQualifiedName~CookingNetworkTestPortTests"`。这些测试启动时只读取配置与端口占用，不自动调用 Open/Install。离线模式、非 Windows Flow、专用 impairment relay 与进程内传输 fixture 保留各自运行语义；同机通过仍不证明物理双机 LAN。

## 构建与验证（仓库根目录）

- 默认：`powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1`，`-List` 查看、按范围选择 `-Gate core-stability` / `-Gate runtime-contracts`；权威为 [test-gates](tools/test-gates.json)。先读 [测试规范](Docs/AbilityKit测试门禁与批量回归规范.md)，不能用 MOBA precheck 代替全部范围。
- 聚焦构建：`dotnet build src/AbilityKit.Demo.Moba.Console/AbilityKit.Demo.Moba.Console.csproj`。主要 .NET 项目 net10.0，其他宿主按实际 csproj；README SDK 10.0.300 不表示已有 global.json 固定。
- Unity 2022.3.62f1，打开 Unity/；不得编辑自动生成 csproj、Library/、Temp/。编译辅助：`powershell -ExecutionPolicy Bypass -File tools/run-unity-compile-check.ps1`；需要本机 managed DLL，缺失或脚本 exit0 跳过都不算通过。
- EditMode：`powershell -ExecutionPolicy Bypass -File tools/run-unity-editmode-tests.ps1 -TestAssembly AbilityKit.Ability.Editor.Tests`；另一 Editor 占用时不运行批处理、不删锁文件。
- 协议修改先读 [Protocols README](Protocols/README.md)。Catalog：`powershell -ExecutionPolicy Bypass -File tools/compile-protocol-catalogs.ps1 -Check`；wire：`tools/export-protocol-wire.ps1 -Projects shooter,moba -Check -Strict`。Catalogs/WireSchemas 修改后经生成器更新，勿手改派生文件。
- 以上是已发现命令，交付报告实际执行和未执行原因；Cooking Unity 仍后置且未授权实施。物理两 PC 不可用时保留 NOT_VERIFIED，不重复询问硬件、不用同机替代出口；见 [physical runbook](.trellis/tasks/10-02-cooking-network-gameplay-loop/research/physical-lan-runbook.md)。

## Orca、工作树与 Trellis

- 修改前看 Git 状态，保留用户解决方案及 `Unity/Assets/Practice/`、`src/AbilityKit.Demo.MyPractice/`；不用清空/reset/全局 kill 处理任务。
- 获准并行工程使用 Orca 受管 worktree 与 supervised orchestration；恢复核对实际 worker/dispatch/进程，不凭状态文件重启。本轮文档任务不得清理用户工作树、备份或进程，不沿用旧合并授权。
- 全项目长期约束：worktree 只能由根协调者直接创建。任何处于非主工作树中的 worker、agent 或终端都不得调用 `orca worktree create`、`orca orchestration worker-start --worktree new-child|new-top-level`、`git worktree add` 或等价入口来创建后代工作树，也不得委托其他会话代建；需要新的源码隔离时必须停止当前扩展并上报根协调者，由根协调者复用现有工作树或记录必要性后创建。该约束不禁止根协调者在已经批准的现有工作树中启动新终端。违反该约束产生的运行与验证不得作为独占执行证据，派生资源须保留到根协调者完成审计和回收。
- 持续检查 worktree 生命周期。仅在无活动任务/进程、改动已合并或妥善保存、证据已迁出后按授权及时清理；worker 停止不等于分支可删。保留来源、更新路由，见 [生命周期记录](.trellis/tasks/10-02-cooking-gameplay-menu-plan/research/worktree-lifecycle.md)。Windows 递归删除/移动前核对绝对目标位于指定 workspace，不跨 shell 拼删除命令。
- 本仓库只用 Trellis 管理工程任务与记录。复杂任务在 planning 完成并审阅 prd/design/implement/context manifests，明确批准后才 in_progress。恢复读 task 全部产物、research 与 manifest 规范；迁移 planning/blocked 不意味着已实施。验证、提交、归档独立，不提交本机 developer/runtime/cache/log。
- Trellis 安装 0.6.17，Node >=18、Python >=3.9，见 [安装参考](ADR/reference/README.md)。共享配置 .trellis/；宿主集成 .zcode/、.codex/、.agents/skills/trellis-*。磁盘 hook/技能存在不代表宿主已批准或加载；技能列表需新会话刷新，宿主禁用 hooks 时按 bridge 提示安装后新开会话。
