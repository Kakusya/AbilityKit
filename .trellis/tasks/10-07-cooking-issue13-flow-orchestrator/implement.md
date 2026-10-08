Current API amendment: [accepted-api-amendment](research/accepted-api-amendment.md) supersedes affected draft signatures and adds only narrow CLI diagnostics/output override in the original allowed files. [Full decision](research/dot-api-amendment-decision.md). No additional gameplay/rule/dependency scope.

# Accepted 技术执行计划；开始 S1

Owner后续要求完成Issue13，已批准所呈类别与有界S1→S2→S3；[approval](research/owner-approval-20261007.md)。历史S0-only段落保留来源；当前先一个worker实施S1，独立检查/技术审阅后推进S2/S3，不重复要求Owner确认routine步骤。测试严格按必要范围，不扩全仓或累计控制数。

来源：[完整dot回复](research/dot-plan-reply-raw.txt)、[API](research/accepted-api-design.md)、[决定](research/dot-plan-decision.md)。task继续planning，Issue13继续OPEN/blocked；规则与S1具体范围待Owner。

- [x] 阅读最新Issue与项目权威、盘点worktree/运行身份、隔离旧Run。
- [x] 写S0来源/首个Flow/五类Draft规则；脱敏规划10文件发布并核对SHA。
- [x] dot当前request/完整SHA接受技术规划，保存完整回复与具体API。
- [x] 按dot修订规则、两个Flow与实施候选列表。
- [x] Issue首次回报；Owner续作目标批准类别、API/文件/预算与完成要求。
- [ ] ready/task start，一个Orca managed-worktree worker，证明accepted input+turn-start。
- [ ] S1完成即停，main独立检查/冻结candidate+交付hash，dot final-review；不自动派发S2。
- [ ] S2/S3范围另确认；未运行范围继续NotRun。

## 精确文件、消费者及验收（dot原文）

S0 当前可写：本 task 的 prd/design/implement、六份研究/规则/flow文档及必要 context/决定证据；可回报 Issue。不得据本裁决修改下列运行时代码。

Owner 批准 S1 后，允许一个 Orca worker 的候选范围：

src/AbilityKit.Game.Cooking.FlowAcceptance/

AbilityKit.Game.Cooking.FlowAcceptance.csproj：net10.0、IsPackable=false；引用已有 Cooking/EtRuntime，链接原 SingleThreadOwner.cs；不升级依赖。验收为该宿主与聚焦测试可构建。
Program.cs：run --request <file>、请求验证/退出。验收 invalid/normal/异常出口。
FlowContracts.cs：上述 DTO/API与一个 JSON 契约。验收往返、缺失/未知/超限。
FlowOrchestrator.cs：预算、步骤、判定、统一结束路径。验收超时/取消/不完整不误过。
FixedFlows.cs：两个注册 C# flow；无 DSL/LLM。验收目标与调用顺序。
FlowFixture.cs：最小合法场景装配。验收角色/位置/资格/物品版本前提。
OfflineFlowAdapter.cs：owner 入队、Tick、终态、只读探针。验收竞争、拒绝、精确重放。
FlowEventCollector.cs：唯一文件 writer及限额。验收错序、缺失、背压、写失败。
FlowRuleEvaluator.cs：批准规则目录和确定性检查。验收正确/故意违规事实。
FlowResourceScope.cs：本次资源账目、幂等有界归位。验收正常/异常连续运行。
FlowReport.cs：小摘要、失败包、本地HTML、最后发布result。验收缺终态/部分写入不能成功。

src/AbilityKit.ET.Runtime.Tests/

AbilityKit.ET.Runtime.Tests.csproj：只增加新测试宿主项目引用；不改包版本或现有测试。
CookingFixedFlowTests.cs：新增聚焦控制，标记 FlowStage=S1/S2/S3；合成规则反例标 synthetic。

Docs/design/CookingGame/testing/

fixed-flow.md：真实命令、支持范围和失败阅读方式；
flow-rules.json：Owner批准后才填批准类别；
requests/compete-offline.json、pickup-drop-offline.json：批准后的合法示例。

S2 另行确认后新增 NetworkFlowAdapter.cs、NetworkRoleHost.cs、FlowRoleProtocol.cs；csproj 加已有 LiteNet 项目引用；扩展同一聚焦测试，并新增两个 network 请求样例。验收独立server+两外部client，而非in-process伪网络。

S3 不预建 watcher 服务。先复用 Program/FlowReport 和说明验证真实完成返回；若运行器确需小 wait helper，先给出已验证能力与具体最小文件，再决定，不提前授权另一套调度器。

旧 RichRunner、SingleThreadOwner原文件、产品 Session/codec/authority、Unity源码、tools/run_test_gate.ps1、test-gates.json、其他示例均不在这批修改清单内。若实际需要改变产品接口，停止该部分，提交具体差距重新审议。

九、必要验证与停止点

S1：

两个合法竞争动作：一个领域 Accepted、一个领域 Rejected，不固定赢家；
immediate duplicate、准入拒绝无terminal、同一terminal多观察来源不重复计事件；
独立手槽/location不一致反例、拒绝误增成功事件、重复执行反例；
目标未达到、frame fault保留先前command效果、取消/超时、host启动失败；
未批准规则/错版本/旧输出、必要事件缺失/溢出、报告写失败；
归位异常独立记录，两次全新运行无残留；
两个flow各有真实正例；故意违规的 evaluator 输入是合成控制，不冒称修改了游戏再验证。

S2追加：
真实三子进程、不同frame到达、原样返回失败方业务终态、错scope/generation/旧baseline不能收敛、发出后取消不宣称未提交、child crash/EOF/复用旧run拒绝、单writer及完整收尾。

先一次构建测试项目及其新宿主引用；随后使用同一未变化源码的构建输出：
dotnet build src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj -c Debug
dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj -c Debug --no-build --no-restore --filter "FullyQualifiedName~CookingFixedFlowTests&FlowStage=S1" --logger "trx;LogFileName=flow-s1.trx" --results-directory <本次新目录>
dotnet src/AbilityKit.Game.Cooking.FlowAcceptance/bin/Debug/net10.0/AbilityKit.Game.Cooking.FlowAcceptance.dll run --request <本次已批准请求>

这些是未来实现后的命令，不是现在已存在可运行的入口。S2/S3改用相应trait，并保留必要的S1短回归。不运行默认全仓gate，不要求把旧全部rich恢复/编译证据链跑完。

S1完成即停：主控检查源码差异、短测试与实际结果，再申请候选审阅；不自动接着派发S2。S2和S3分别有明确范围确认，不能通过“后续计划已写”获得当前实施权。


## S3 completion 试点

当前 dotnet --version shell 等待只证明普通子进程完成返回，Orca advertised capabilities 不等于新玩法自动唤醒已经接通。

可实现的首路径：
调用器启动 run 命令并等待进程 → 编排者内部完成全部等待 → 完整写出 result 并退出 → 同一次工具返回小摘要 → agent读取对应result/failure包。

以后用一个短成功flow和一个故意失败flow实测：

分别只提交一次；
等待期间模型不反复发 status；
结束后获得匹配request/run的结果与真实退出；
失败只读取小失败包；
另测进程无result退出，调用器必须报告不完整。
记录实际工具是否yield、如何等待、是否真的产生完成通知。若只能一个显式wait，则如实采用，不冒称后台自动唤醒；文件本身不唤醒agent。

当前运行验证全部 NotRun；产品 API 无需修改，但新测试 API 如上，不能写 N/A。上述是具体规划，不是已实现能力。


## 当前验证状态

S0文档diff/context/符号/链接与scope检查按其声明覆盖Passed；Trellis Phase loader缺Phase Index记Blocked。产品构建/测试、offline/network运行、Orca玩法自动完成唤醒全部NotRun；二进制N/A。发布不包含个人绝对路径、原始runtime inventory或无关会话。跨树.NET验证串行，保留命令/源dirty/计数/native exit和原失败，不启动默认全仓gate作为调研。


## Current S2 continuation (2026-10-08)

S1 exact candidate `2201931e1846acb9ab8a205d501e39731b5a39bb` is accepted with complete API behavior finding. [Current decision](research/dot-s1-revision-review-decision.md) and [bounded 14-file S2 execution plan](research/s2-dispatch-plan.md) supersede historical deferred-S2/S0-only scheduling passages. One retained managed Orca worker may now implement S2 under existing Owner approval; main reviewed these artifacts and context manifests. Independent check/freeze/new dot review precedes S3. Branch-only and honest evidence remain.


## Current S2 replay clarification (2026-10-08)

[Exact accepted replay contract](research/accepted-s2-replay-contract.md) and [full dot source](research/dot-s2-replay-api-reply-raw.txt) resolve the worker semantic question without new public API or category meaning. Read actual native/domain identities, mode-specific evaluator path and supporting current binding observations; keep original14file boundary and focused checks. Main reviewed this supplement before replying to the existing worker. Dirty S2 remains unaccepted until independent check/freeze/final review.


## S2 R1/R2 revision, 2026-10-08

[Complete current dot decision](research/dot-s2-review-decision.md) requires [five-file bounded revision](research/s2-revision-plan.md) before S3. Main reviewed plan and context; valid Owner scope permits same-worker new dispatch. Earlier positive checks remain evidence, not exact candidate acceptance.
