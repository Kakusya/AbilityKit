Current API amendment: [accepted-api-amendment](research/accepted-api-amendment.md) supersedes affected draft signatures and adds only narrow CLI diagnostics/output override in the original allowed files. [Full decision](research/dot-api-amendment-decision.md). No additional gameplay/rule/dependency scope.

# Accepted 技术规划；Owner已授权完成Issue13

Owner后续目标授权见 [approval](research/owner-approval-20261007.md)：按下述设计推进全部切片，禁止过度测试，模糊点及时向dot询问。下文“S0-only/未批准”是当时的dot决定及历史事实，不再表示当前实施阻塞。不得扩大其他示例/Unity/依赖或修改规则降低标准。

dot对请求 `AK-I13-S0-PLAN-20261007-01` / 规划SHA `a8ea630ca1fd29fcf0327556f7847e13f589c218` 返回 **accept-plan，限S0**。完整原文：[dot-plan-reply](research/dot-plan-reply-raw.txt)；完整具体C#签名：[accepted-api-design](research/accepted-api-design.md)；身份/时间/范围：[decision](research/dot-plan-decision.md)。所有新API为拟新增测试层合同，不是已编译事实。Owner规则和S1 API/文件范围未批准。

## 组件与消费者

采用新的Cooking FlowAcceptance薄测试CLI，S1引用已有Cooking/EtRuntime，链接原SingleThreadOwner.cs并保留namespace，不复制/修改该类。S2再引用已有LiteNet项目与三子进程。旧RichRunner/成功专用planner保持原用途；最小fixture不导入全菜单。product API/wire v3/游戏语义不变更。

CLI验证FlowRequest和受审规则目录后创建全新RunIdentity/fixture；IFlowOrchestrator驱动注册C#Flow，经IFlowSession正式提交/只读capture，IFlowRule确定性判断，单writer IFlowEventSink收集，独立cleanup token归位，IFlowReportWriter最后发布结果。全部具体API/DTO与调用序列按accepted-api-design；RoleControl含dot明确补充的ReplayOfCallId，Action与重放二选一。

## 正式路径与证据

offline同构造owner线程同步尝试所有TryEnqueue，组内不Tick/await；同组合法正SimulationBatch，新组高于LastCommittedSimulationBatch。唯一driver Tick。汇合准入即时terminal、Tick.Dispositions、DispositionHistory和可用FinalDispositionHistory；网络通知队列不能当offline完整来源。

network父进程管理独立server/client-a/client-b；有界NDJSON仅控制装配/Arm/Release/Observe/Stop。游戏动作仍经真实client SendCommandAsync→v3/LiteNet→Session ProcessOwnerFrame→同一个ET host，不额外Tick或用local transport冒充。network batch必须0由server映射；barrier不保证同帧到达。

CallId为一次调用，BusinessId为幂等业务身份。Replay保留原完整冻结payload（item version、offline原batch/network0），只换CallId；新动作用新ID/版本。client内部correlation不可观察时null。准入、Executed、Outcome、ACK、已安装投影分别记录；协议拒绝不冒充所需领域拒绝，timeout/断线不伪造domain Reject。

authority同次idle-owner capture读取独立ItemInHand及item location，随即丢弃TryPeekBoundKitchen借用引用。不能用同源Observe.HeldItem作独立手索引证明。只输出复制DTO，不暴露Entity/simulation/可变集合。client手标ClientProjection；Pickup后、下次改变item前检查实际安装当前scope/session/generation baseline及version≥fence/必要字段。允许更高version，不要求全世界相等，不用Drop补证明Pickup，不声称目标baseline精确ACK。

## 生命周期、失败与归位

现有ET owner仍是游戏writer/唯一Tick。触host continuation不Task.Run/ConfigureAwait(false)，每await后重验取消/run generation/role/scope与network instance/generation；意外换scope/重连停止首版flow。取消等待不撤销已发command。fixed-step失败保留先前effects/event/terminal；完整capture不可得标Available=false，保存最后有效cut/可用终态。

collector不让owner等待磁盘；有界TryPublish失败在带外锁存evidenceIncomplete并停止新动作。CollectorSequence仅收集顺序；按hostSequence/command关联事实。先固定首次失败cut，再停动作/扰动、独立token有界收集调用、关闭client、server处理断开清理、session/idle host释放、确认本次child/readers退出、collector flush、发布failure/report/result。CloseAsync幂等；未知writer/退出阻塞复用。仅管理确认身份的本次child；同进程卡死由调用器监督，无result不伪造正常终态。

测试schemaVersion1独立于wire v3，BCL JSON/文本枚举/必需字段与上限、未知版本动作拒绝。发布只声明正常进程文件交付，不证明掉电事务。ProductFailure与后续环境/归位错误并存。Passed须目标、批准规则、证据、归位全部完整；启动前Blocked，启动后超时/故障Failed且执行不完整，未知Verdict=Undetermined。整体退出0/1/2/3/4分别Passed/Failed/Blocked/Skipped/NotRun；native child exit另记。

## 预算与停点（提案，不是Owner批准或性能实测）

startup/step/convergence各10s；overall120s含startup/execute/reset/publish，预留reset10s/publish2s，活动工作最迟overall−12s停止。等待取局部与剩余较小预算。collector queue256、单event64KiB、4096events/8MiB、每角色stderr256KiB；不能默默放宽以抹除失败。Owner审批卡可约定允许参数范围。

S1只offline两个注册Flow/真实正例/明确synthetic反例与归位控制，完成即停，经main独立检查/冻结候选/dot review。S2/S3另确认范围。当前runtime/Orca玩法完成回执全部NotRun；S0接受不授权实现、后续派发或产品验收。


## Current S2 continuation (2026-10-08)

S1 exact candidate `2201931e1846acb9ab8a205d501e39731b5a39bb` is accepted with complete API behavior finding. [Current decision](research/dot-s1-revision-review-decision.md) and [bounded 14-file S2 execution plan](research/s2-dispatch-plan.md) supersede historical deferred-S2/S0-only scheduling passages. One retained managed Orca worker may now implement S2 under existing Owner approval; main reviewed these artifacts and context manifests. Independent check/freeze/new dot review precedes S3. Branch-only and honest evidence remain.


## Current S2 replay clarification (2026-10-08)

[Exact accepted replay contract](research/accepted-s2-replay-contract.md) and [full dot source](research/dot-s2-replay-api-reply-raw.txt) resolve the worker semantic question without new public API or category meaning. Read actual native/domain identities, mode-specific evaluator path and supporting current binding observations; keep original14file boundary and focused checks. Main reviewed this supplement before replying to the existing worker. Dirty S2 remains unaccepted until independent check/freeze/final review.
