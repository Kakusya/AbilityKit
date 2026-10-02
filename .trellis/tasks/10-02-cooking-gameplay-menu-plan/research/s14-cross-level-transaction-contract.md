# Research: S14 跨关事务契约

- Query: 成功接续、失败重开、trusted next layout/seed、在途供应与allocator的原子安装顺序。
- Scope: internal；当前master源码只读，不跑.NET、不改production。
- Date: 2026-10-02

## Findings

### 当前事实与缺口

`CookingLevelEtHost.cs:987-1028` CreateSuccessor先InstallGeneration，再完成前厅进行中任务、导出handoff、Adopt、AcceptSuccessHandoff。`InstallGeneration:1524-1578`已移除旧ET level、提交源lifecycle、替换Binding并清pending/terminal。其后的handoff拒绝虽尝试恢复厨房checkpoint，却不能恢复已提交的lifecycle/ET树、前厅完成任务与host队列。因此不是跨关完整原子事务。

`CookingRecipeCheckpoint.cs:385`已经改为EffectiveSpatial.InitialPoses，修正了fixture读取，但如果下一关几何还未安装，读取的仍是上一关有效布局seed。`BeginPreparationCore`到新layout安装发生在后续准备(:578)，晚于handoff；安装Project若以旧livepose为输入且位置合法会保留它，不能证明下一关从trusted出生位开始。

`CookingLevelLifecycle.cs:800-814`候选复用同configuration和factory；next policy应由factory按nextScope生成，而不能从上一关InstalledLayout或payload派生允许设备。`AdoptSuccessorKitchen:830-839`只接受Created且没有gameplay的candidate，现有扩展可以在Created预构建候选厨房，不需要第二authority或改变生命周期。

### 最小可执行设计

增加host内部 `BuildSuccessorPlan(candidate, nextPreparation)`（纯准备、不发布），与独立 `CommitSuccessorPlan(plan)`；公开CreateSuccessor可增加带trusted next preparation的重载，旧签名只能用于旧无空间兼容，空间产品路径必须有明确next layout/policy。

plan包含冻结的source recipe/front快照、next scope/config/factory policy、next几何及normalized seed、已验证的候选厨房/front、handoff统计。尽量复用现有factory.Create、Project、CanInstallPreparedGeometry(restoreReferences)、InstallPreparedGeometry、AcceptSuccessHandoff、CreateInitialFront、AdoptSuccessorKitchen；不得在正式发布后再次执行可失败的factory/投影/恢复。

1. 验证源Ended+Success、next epoch/level、choices已确认、可信next policy及布局权限。使用下一关trusted policy的InitialLayout和InitialPoses，不用source livepose作为next seed。
2. 将FinishInProgress施于源snapshot的隔离厨房/front副本，复用原规则完成当前询问/洗碗；不要提前改live源。导出SuccessHandoff清订单/结算/dedup/事件、worker，保留half-finished、portion、tombstone、allocator与供应ledger。
3. 创建同match的新候选厨房（相同游戏authority类型），以handoff.Recipe作为引用集验证下一关投影，不受候选初始stock旧位置干扰。安装next有效几何与normalized InitialPoses，再AcceptSuccessHandoff；它读取的EffectiveSpatial现在确属next关。初始标准供应不能叠加成功carry库存。
4. 候选front由trusted next配置新建，派生路径/端点，固定伙伴计数与业务clock按既有跨关规则清零；menu/order允许范围来源next配置。候选actor worker=null，逻辑tick归零，出生位MovementTick=-1。
5. 所有内容/工位/供应配置不兼容提前拒绝。尤其成功保留的物件/在途供应必须在next关allowed范围和anchor中合法；不要丢掉不兼容物件、迁移订单到新菜或自动赋权限来掩盖冲突。通关路线需设计兼容carry桥接或明确的准备阶段处理，不能默认“解锁过就允许”。
6. 在候选厨房/front全部通过后Adopt到candidate，再进入唯一commit：安装ET candidate、提交源lifecycle、替换owned kitchen/binding/front/preparation/effective layout/队列。旧authority释放/关闭只在提交成功后。所有可抛异常初始化提前完成。

构建独立候选只是事务暂存，并非并行运行的第二authority；其不注册ET driver、不推进Tick、不向外发布。若必须保持同一个Simulation对象identity，需扩展现有domain为内部validated-state swap，先在私有候选验证再原子安装；不能用提前修改live几何再恢复作为替代。

### ET发布失败原子性

当前InstallGeneration先删旧Level，失败后仅还原lifecycle和Binding，不重建旧driver。因此还需改为ET临时child/component准备后再发布，或在catch中用原lifecycle重新InstallLevel(injectFailures:false)、恢复旧Binding/driver及host所有字段；最终commit前不得清队列。fault injector导致的任一点失败应返回结构化拒绝并保持完整源snapshot/host ingress，不能只声明厨房canonical相同。

候选发布不是存档提交成功：major checkpoint只按既有成功收口和Locked choices写；如果该转换要求write先于next发布，应复用现有store“先写成功才发布”规则。write失败必须保持源generation与choices、不能以CreateSuccessor已成功代替存档成功。

### 失败重开、major choices与供应

CreateRetry(:933-984)现factory.Create+ApplyStandardInitialSupply+ApplyRetryChoices在InstallGeneration前，方向正确，但adopt/front清理仍在commit后，须放入同一候选预验证与ET发布事务。失败不携带现场items/stock、delivery pending、origins、portion、allocator水位或customer/companion成长；标准库存与supplier初始数量重新由trusted当关配置构建。保留当前进程已确认unlock/decoration/buff，但**应用需满足该关allowed范围**，不因progress有定义就越权摆放。失败不写major checkpoint。

成功供应复用既有checkpoint字段：CookingSupply.cs:23采用RemainingTicks而非绝对tick，因此pending可跨逻辑clock重置原样保留；Arrived保留可领取，Received保留origin与tombstone链，有限库存保留实际数量。AcceptSuccessHandoff已重开Closing=false，下一关固定Tick继续剩余等待；不得重复物化货物或重发相同request。成功保留NextProductId及所有AllocationSequence，下一次分配严格高于水位；不要对allocator执行恢复重算。配置变更若supplier identity或unit/package定义不兼容，构建阶段拒绝。

### 验收要求

不同next出生位/layout先安装再handoff；源位姿即使在next地图合法也不被carry；过程/份数/stock/pending remaining保存；成功新scope无旧订单/dedup但allocator不归零；失败恢复标准库存与供应、保留已确认且当关允许的choices；未知next许可/缺anchor拒绝源完整零变更；factory/allocator/geometry/front/ET安装/write故障逐点注入；取消pending只发生成功commit；下一代真实ET入口准备→营业→自然结束与重放恢复一致。

## Caveats / Not Found

本报告是当前缺口与最小事务设计，不证明S14实现或通过。候选工厂原有固定front配置接口不按scope变化，若next菜单/前厅需要不同值，应在既有factory内部增加scope-aware方法或policy返回，不创建新配置authority。网络与Unity不在本次实施范围。
