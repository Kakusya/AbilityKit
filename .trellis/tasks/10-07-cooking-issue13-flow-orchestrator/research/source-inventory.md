# S0 真实来源与复用限制

盘点基线 `271f76c8e4ab88ae93b28fb49e46f4700a399b66`，2026-10-07。本轮只读，没有产品构建/执行；以下符号是源码事实，不是已验证的新 Flow。

| 现有文件/符号 | 新测试薄层 / Cooking 消费者 / 验收 |
|---|---|
| `src/AbilityKit.Game.Cooking/CookingRecipeLoop.cs:219` `CookingRecipeCommand`；`:257` `CookingRecipeCommandResult`；`:1434` `Pickup` | 角色、scope、stable command、item/version 等类型化动作；正式校验返回 Accepted/Rejected、原因、版本、IsDuplicate、Events；竞争及重试 |
| `src/AbilityKit.Game.Cooking.EtRuntime/CookingLevelEtHost.cs:41` `CookingLevelCommandEnvelope`；`:348` `DrainNewTerminalDispositions`；`:396` `CaptureReadOnlyFullState`；`:528` `Observe`；`:669` `Prepare`；`:746` `Start`；`:905` `TryEnqueue`；`:966` `Tick`；`:2232` `Dispose` | offline driver；一个 owner线程和唯一 Tick，准入不等于业务效果，Dispose须 idle owner；不把 AdvanceTicks 当玩家命令 |
| `src/AbilityKit.Game.Cooking.EtRuntime/CookingNetworkAuthorityAdapter.cs` `ConsumeFrame` | network server 组合同一个 host，无第二 simulation/clock；保留 command提交后 fixed-step失败语义 |
| `src/AbilityKit.Game.Cooking/Session/CookingNetworkSessionHost.cs:213` `ProcessOwnerFrame` / `ApplyControl` / `Dispose` | server 只从此入口推进；callbacks入队；真实连接、scope、close/cleanup |
| `src/AbilityKit.Game.Cooking/Session/CookingNetworkSessionClient.cs:129` `SendCommandAsync(string, CookingRecipeCommand, CancellationToken)`；`ConnectAsync`、`LatestBaseline`、`IsSynchronized`、`Disconnect/Dispose` | client adapter；正式终态独立于 baseline，Ready不等于目标状态收敛 |
| `src/AbilityKit.Game.Cooking/Session/CookingNetworkWireCodec.cs:25` `CookingNetworkWireCommand/Result`；v3 `CookingNetworkWireCodec` | 复用 stable/domain/correlation、server instance、generation、scope与真实 codec；不重写协议 |
| `src/AbilityKit.ET.Runtime.Tests/CookingRichRecoveryPlanner.cs` ctor capture/send/wait/sendMoves；`:200` `Go`；`:248` `WaitUntil` | 被动 capture/有界泵送和已有动作组合。限制：private Dispatch要求全部 Accepted；Pick私有；内部ID每个planner独立计数；竞争不能直接走其成功专用路径；不将其当第二游戏运行时 |
| `src/AbilityKit.ET.Runtime.Tests/CookingRichRecoveryFixture.cs` | 当前 fixture装配，已有菜单/器皿/空间/资格/供应。但默认 F01/D31、菜单依赖/物资较大，首个 item竞争优先最小 fixture，不把所有菜单作为前置 |
| `src/AbilityKit.Game.Cooking.NetworkRichRecoveryAcceptance/SingleThreadOwner.cs` `Run(Func<Task<int>>)`；`RichRunner.cs` `Pump/Wait/Run`；项目 Compile Include fixture/planner | owner continuation回到构造线程；已有实际 network host/peer组成模式。RichRunner为恢复切点长场景（whole600s）且强耦合多证据，不改旧默认流程来承载新编排；可由dot选择链接已有owner类 |
| `src/AbilityKit.Game.Cooking.Tests/Harness/InProcessPairTopology.cs`、`LoopbackUdpTopology.cs` | 仅接口参考；前者 dummy字节/直接snapshot复制，后者旧session；不作为v3真实network验收 |
| `tools/run-cooking-network-rich-recovery-acceptance.ps1` | 有 Start-Process/有界程序循环/退出与角色隔离；其旧编译归档控制不自动继承。S0未启动该脚本 |

`CookingLevelEtHost` 当前仍是existing adapter/owner，不据Task或DTO外观宣称ET状态族迁移已全部完成；遵循当前ET合同现状/目标与分阶段提交。正式只读探针：`CookingLevelEtHost.cs:542` TryPeekBoundKitchen及`CookingRecipeLoop.cs:638` ItemInHand，在同次idle-owner观察复制独立手索引后丢弃借用引用。既有Observe.HeldItem来自item location，不作独立索引证据；测试层不取得写入权。

dot定稿补充：offline网络通知队列不完整，需汇合准入即时terminal/Tick/DispositionHistory/可用FinalDispositionHistory；同组正batch同步入队后唯一Tick。network batch0由server映射，client内部wire correlation不可见时null。retry保留原完整batch/version/payload，只换CallId；固定Tick可推进全局event水位，IDEMP只按原command业务事实归因。完整定稿见本task的dot回复与accepted-api-design。

## 宿主与依赖闭包

现有 RichAcceptance 为 net10.0，引用 Cooking、Cooking.EtRuntime、Network.Transport.LiteNet，并 Compile Include现有fixture/planner。EtRuntime 为 net10.0/internal-only，引用 ET.Runtime、Cooking与 RelationAnalyzer；ET.Runtime从 Unity/Packages复用源码。`Unity/Packages/com.abilitykit.et.runtime/Runtime/AbilityKit.ET.Runtime.asmdef` 是 noEngineReferences/autoReferenced=false。本方案只消费已存在 .NET 闭包，不改 asmdef、UPM、工具/依赖版本；纯.NET结果不代表 Unity/AOT。新项目的准确引用/linked source须dot设计及Owner审阅。

## CLI completion 与时间

实测调用器执行 `dotnet --version`，程序结束后同一个 exec_command工具返回 exit0、`10.0.300`；Python3.14.8/Node24.21.0。本轮 shell completion可用，但尚未运行新玩法 CLI，不能称S3通过。

Orca1.4.221/live runtime `31a5fbb3-7af6-4290-ad04-313c5b0330de` 广告 `terminal.prompt-delivery.v1`、`agent-session.turn-completion.v1`、orchestration contract/launch偏好等；live CLI支持 `terminal wait --for exit` 与 `orchestration check --wait`。普通CLI子进程完成→特定Orca agent唤醒的连接没有实测，NotRun；不能由capability名称推断 arbitrary callback。首版正常调用器可等子进程退出；工具等待上限时由普通程序等待/一个显式等待入口承担，不让模型几秒轮询，文件不主动唤醒模型。

减时依据仅源码：旧wrapper默认构建 runner/controls/verifier，RichRunner whole600s；新固定短Flow可只构建一次最小CLI/聚焦tests并复用同次输出，不运行全部恢复场景。实际build/start/game/reset耗时待实测，几十秒至两分钟为目标，不能为达标删必要检查。

## 工作树、历史与授权

Git worktree list与Orca全host inventory都只有当前AbilityKit checkout，无遗漏host。旧 `issue6-test-gate-results` 与 truthful-gates canonical路径当前不可见；没有重建、修复旧flow/index或claim takeover。远端旧隔离分支仍在 `d45f09ee5a42aa06bf5dcf8600e701c2daf6986b`；其AGENTS、历史task/research来源及实际diff已只读检查，含不同历史/越界示例，仅保留证据，不导入。Git/Orca未列出的实体目录及删除者/删除授权未知，不把旧笔记称当前liveness。当前master里旧Issue6 task/progress仍说in_progress，与GitHubPR12 merged并存；#13不修复它。

当前调用main session/terminal/pane与env/fleet匹配，当前checkout仅这一agent；terminal show实际显示GPT-6.1-Sol high。新Issue13未发现flow/dispatch，独立新流程由原始唯一调用main持有，run_id=null（未派发worker，无需Run）。原Run跨main接管不受支持，本轮不会接管 #6或创建其替代writer。完整运行身份与原始 inventory 保留在本机 flow evidence，不公开个人绝对路径或无关会话。

`orca` 固定exe；Orca project display仍 github:hobobo/abilitykit，实际 git remote与GitHubAPI为Kakusya/AbilityKit，只使用已核对实际remote。保留启动前两份dirty用户journal，不stage/commit。项目路径技能由Owner附件显式手动加载，宿主自动技能匹配NotRun。

已读：AGENTS、progress、Cooking index/menu plan、technical-roadmap、ADR long-term/index、当前 ET合同、SOP、测试门禁规范及invoked skill recovery/dialogue/templates/operator example；Trellis start/brainstorm与orca-cli live guide。过去决定已在Issue/当前来源中，不重复mem查找或向Owner询问仓库事实。
