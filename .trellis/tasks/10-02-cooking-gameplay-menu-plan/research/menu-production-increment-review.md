# 菜单真实加工与 ET 恢复增量审阅

2026-10-02。固定审阅提交 `380f903b6dc1a91ea078b1cfee3ee0b646949cd6`，比较基线 `973cb3319`。保留菜单 worker 仍 live；使用 git show/git diff 固定提交读取，不改源、不运行 .NET。

## 结论

可接收此受限增量，未发现阻止所述范围接入的问题。现在有范围相称的 **87 菜品真实空间加工、装盘及 ET checkpoint 恢复继续**证据；其中56餐食/甜品实际提交，31饮品仅加工装杯，仍等待 S05 核票绑定和提交。手工中断/换玩家、供应采购和完整营业生命周期不在这次证据内，不能标整个 S04/S09–S14 出口完成。

## 固定源码核对

- mapper 给每个实际 CookingContentRecipe 设置 RequiredProcessingContainerDefinition，并在自定义 recipeFactory 投影后逐项检查不允许删掉或改成另一载体；保留 Execution、YieldPortions、多重集校验。配置输出带 ContentProvenance：schema、catalog SHA、来源摘要和去重排序所选菜单。Sources SHA大小写归一，不以文字大小写制造另一个身份。
- 真实 formal loader 身份测试比较所选顺序无关、来源hash改变实际 content.Identity 改变、大小写同义hash不改变 identity。三个蛋糕中间物在相同容量/成分兼容的伪造模具中，Preview不建议加工，Start拒绝且 canonical不变，真实转回模具后继续完成。不是仅靠容器容量负例冒充 RequiredCarrier规则。
- 生产fixture只初始化原料和空容器，原料/容器使用独立空间anchor；运行经公开 Move/Pickup/Drop/PutIn/StartProcess/TakeOut/ServePortion。单产出准备件实际 Transfer到声明OutputStorageContainer，检查工作容器为空和产物OwnerId；多份通过ServePortion逐份扣余额，之后检查工作容器清空/解除completed。未注入准备件/阶段/成品，未另建配方计算引擎。
- ET测试项目只链接同一测试fixture源文件，避免复制driver实现；不增加生产层依赖。ET driver的CommandDispatcher确实走 Host.TryEnqueue→Host.Tick→单个 disposition；FrameAdvance确实走 Host.Tick；测试没有落入默认direct Submit支路。Order开单使用Simulation.OpenOrder作为测试前置，非完整前厅点单/营业证明；wrong-carrier反例为域测试，也没有声称通过ET ingress。
- 全部87条Theory每条运行两臂：不中断与恢复。恢复在首次产物/完成批次出现后导出实际host checkpoint，经envelope serialize/deserialize，dispose旧host，由空模拟factory创建并Restore新host，再替换driver读写的模拟引用。立刻比较checkpoint CanonicalText，继续加工后比较两臂最终完整checkpoint CanonicalText。fixture私有调度意图/列表继续存在，但真实物件、份数、进度及ET入口均从恢复host继续，不是把旧simulation复用冒充restore。

## 独立证据核对

- 固定提交 `hooks-catalog-tests.log`报告128/128，`et-menu-tests.log`报告87/87。
- 实际 `local/Logs/test-gates/20261002-192620-cooking-kitchen-loop/cooking-kitchen-loop/gate-summary.json`及TRX核对：281聚焦、402Cooking、160ET全部Passed，失败/跳过均零。ET TRX内确有87条 `Every_candidate_uses_ET_ingress_and_continues_after_real_checkpoint_restore` 且全部Passed，不是由总数倒推覆盖。
- 固定日志记录 cooking-et-level-runtime pass 19.2s、runtime-contracts pass48.0s；runtime日志保留既有transitive/Moba nullability警告，不能据此声称整个仓库零warning。未独立运行门禁或改worker构建输出。

## 未完成边界

31饮品RequireBinding/Disposable提交尚未由S05 hook证明，当前ET测试特意跳过这些提交。手工仍由同一逻辑玩家连续加工，不证明中断、他人接力或跨前厅工作互斥。恢复切点在首次完成中间物/批次，不是每个加工中间tick、所有订单/营业状态的穷举恢复。初始stock是显式fixture，未证明S07进货/补杯，也未证明S14完整准备→营业→自然收尾→跨关。旧spatial-production逐菜记录为pre-hook历史记录，不可当本提交的最终逐菜证据。

Lint未执行；TypeCheck为已有实际门禁证据，本审阅未复跑；Tests为核对固定提交日志和实际TRX，不冒充主会话独立组合回归。

## 追加：真实手工接力与部分批次恢复

固定审阅 `a8976c6a22468a7490ce24e39cd9cea96182cf00`。结论：可接收此测试增量，无新增阻断。原87路径默认 fixture 条件不变；manualHandoff 为默认 false 的可选测试模式，才把 Manual耗时改为4、添加真实第二玩家、停靠位及1200交互距离。这些是明确进度测试数值，不是改正式平衡或降低原87验收。

- F01/D31 手工测试均通过 ET dispatcher真实 StopProcess；记录非零加工进度，推进暂停帧和第二玩家移动，断言 elapsed不变；换到配置中的另一 PlayerId，真实 ContinueProcess 并完成。加工容器先 Drop至合法station，第二玩家在真实距离内接续，完工后离开停靠位，原玩家真实取回容器。不是修改 snapshot执行者或直接推进伪造进度。
- 恢复臂的 AfterFrame 在 ActiveWorker=null 的暂停状态触发，发生于 StopProcess后的帧、第二玩家Continue之前；复用原codec/dispose/fresh factory/restore路径，立即canonical一致。对照臂也执行相同Stop/移动/Continue但不重建；两臂终态full checkpoint相等。仅代表这两条菜单案例，并非所有菜手工接续、前厅互斥或所有暂停切点。
- D31部分批次测试在 ContainerCompleted且RemainingPortions=1触发重建；并未把YieldPortions配置改成1。真正初始批次仍2份，第一份由ServePortion取走，之后恢复剩余1份再继续ServePortion、按原RequiredCarrier生产后续菜品。沿用原真实storage与份数扣减断言，无载体/盛装旁路。
- 固定 `et-progress-tests.log` 报告新增聚焦3/3；`et-progress-gate.log`报告完整ET163通过（新增3包含于其中，不将两项重复相加）。root报告对应integration `03b138cd2`独立组合340/462/166通过，本追加未自行复跑该组合，仍以root原证据为准。

31饮品绑定交付仍未包含；新增手工及部分批次恢复是切点扩展，不能把S05或整个S14出口自动标已完成。本次未.NET、未生产修改。

## 最终菜单提交与五批证据复审

固定对象：生产/测试增量 `868622cfc228bcc47d2d4f273bac5558940652f3`，五批证据整理 `6e2ecdafec1529bb15bed78bd5d2b145492a91cf`。结论：可接收菜单生产与交付增量，未发现新阻断；本轮不主张 S06/S07/S08/S14 完成。

- 唯一新增 formal adapter 行为是把 catalog Disposable 映射到 CookingContentContainer.DisposableOnSubmission、RequiresBinding 映射到 OrderTemplate：31饮品必须贴票/消费杯，56餐食甜品无需贴票/仍可洗。未替换authority、改工序或改变旧汤/吐司规则。原附件实测hash仍为先前两值，未改原文。
- 真实driver用公开BindOrder/RebindOrder/UnbindOrder与SubmitOrder；31条域反例分别拒绝未贴、错误订单、改绑后旧票、解绑后裸提交，并比较拒绝前后snapshot canonical和空settlement。成功后断言唯一settlement/精确模板配方容器/score100、产品与容器离开active snapshot；从真实ExportCheckpoint读Removed tombstone，餐食Dirty=true，饮品Dirty=false。这是在核对核心已有提交退役状态，不是手写假状态或绕过洗池。它尚不证明后续实际清洗复用闭环。
- ET87条现在全部实际SubmitDelivery；额外31条在真正BoundOrder出现后导出codec/dispose/empty factory恢复，继续匹配提交并消费杯。连同两手工与一部分批次，共121条恢复对照。写 evidence 前先Assert最终完整checkpoint等于不中断控制臂，`finalCanonicalEqualsUninterrupted=true`不是未经比较即打印。
- 独立读87份domain artifact，均status submitted且有唯一settlement；读121份ET artifact，均有实际命令数、identity/provenance、intermediate/final SHA、compare=true和唯一settlement。逐份检查exported servingVessel Removed=true、Dirty按饮品/餐食区分，SHA为64位lowerhex。记录保存的是摘要而非完整canonical，无法仅用artifact离线重新计算最终SHA；可信关联来自受审源码在同次运行比较后对canonical直接Hash并写记录和实际测试结果，不声称文件自身包含完整可重算状态。
- 原始worker日志160 catalog/121 ET子集通过。独立核对最后gate实际summary/TRX：330 focused、452Cooking、197ET Passed，failed/skipped均零，ET TRX实际31条bound-cup测试全部Passed；并非通过数量猜测覆盖。
- 五batch索引覆盖各候选并路由新domain/ET证据；S04及五内容Task仍in_progress、completedAt=null，pending为协调复核/合并和其他单机出口，而不是提前completed。整理提交未将采购、完整前厅布局、正式平衡或Unity纳入其完成声明。

建议主owner接收后在组合版本保存独立回归证据，并继续其它经营出口。Lint未新跑；TypeCheck/Tests只核对worker已有真实证据；本复审不修改生产代码或执行.NET。
