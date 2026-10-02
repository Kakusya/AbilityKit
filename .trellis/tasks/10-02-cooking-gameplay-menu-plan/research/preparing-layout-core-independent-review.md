# Preparing 布局 core 增量独立审查

2026-10-02；只读审查 integration core 当前增量。Host 正由单 owner 实装，本次没有审阅其 WIP，没有修改生产代码或运行 .NET。

## 结论

在内部 trusted projection 和后续完整 Recipe restore 的既定调用边界内，core 增量限定通过，未发现新增阻断。不能据此宣布布局 ET 接线或 S06/S07/S08/S14 完整出口。

## 授权与原子性

- `ICookingGameplayLifecycleGate.IsLayoutInstallationOpen` 默认 false；旧 gate 不会因新增接口获得安装权限。Lifecycle 只在 Created/Preparing 且未 gameplay closed 时打开布局 gate。
- `CanInstallPreparedGeometry` 拒绝 lifecycle closed、mutation in progress 和关闭的 layout gate；真正安装仍另行要求 `IsAuthorityMutationOpen`。因此布局 gate 不替代宿主 mutation ownership。无 lifecycle gate 的独立 simulation 仍属于内部调用边界，并非新的公开 gameplay 命令入口。
- geometry Freeze、pose/process 新字典构造及 checked state version 递增均在赋值前完成；拒绝路径没有状态写入。成功安装清除各 process 的 ActiveWorker，避免旧位置下手工作业继续由原玩家占用。

## 引用闭合与恢复边界

- 新增检查覆盖所有 configured appliances，包括空闲且没有 item/process 的工位；每个 configured station 必须存在，投影的 station anchor 也必须属于配置。这闭合了此前“空工位丢失仍可通过”的缺口。
- 常规安装使用真实 live item/process 引用，并保留每个玩家 LastMovementTick；同时验证完整 player 集合、几何合法性及玩家互不重叠。供应 SourceAnchor/ReceivingAnchor 也必须存在。
- `restoreReferences` 分支改用保存 checkpoint 的 scope、player 集合/合法 pose/互不重叠、非 removed item 及 process station 引用，避免拿将被恢复覆盖的 factory 临时物件阻挡合法恢复。此分支仍执行 configured appliances 和 supply 检查，并不豁免 layout/authority gate。
- 这是几何引用 preflight，不是完整 checkpoint 校验：ID、计数水位、订单、process 外键及 movement tick 与 logical tick 的关系仍由随后 `RestoreCheckpoint` 验证。调用方必须在 trusted restore 流程中完成完整恢复；不得把安装成功视为 checkpoint 已合法或已接受。Host 的失败清理/发布顺序留给其单独审查。

## seed 与水位

- `CookingLayoutGeometry.Project` 的 ProjectedPoses 保留真实移动水位；Geometry.InitialPoses 是独立 seed，明确把 LastMovementTick 置为 -1。
- `AcceptSuccessHandoff` 改用 `EffectiveSpatial.InitialPoses`，因此已安装布局的新几何与 seed 会参与下一关交接；不会退回 fixture 的旧几何，也不会把当前 live pose 的非零 movement 水位带入 LogicalTick=0 的交接。原有 handoff shape、计数器检查及完整 RestoreCheckpoint 校验仍在。

## 证据与限制

静态核对了 CookingPreparedGeometry、CookingRecipeLoop lifecycle gate、CookingLevelLifecycle、CookingLayoutGeometry 和 CookingRecipeCheckpoint。五项 PreparedGeometry 测试包含真实拾取距离改变、live anchor 丢失零变更、空 configured appliance anchor 丢失零变更、saved references 与 factory 引用分离且随后完整 restore canonical 相等、明确布局 gate 开闭。新增两项不是仅断言 getter 的镜像测试。

Root 报告该聚焦组实际 5/5 通过；本审查没有自行重跑或把其数量视为完整语义覆盖。Lint/TypeCheck/Tests 未由本 reviewer 执行，以避免与 root/host 当前共享输出竞争。尚不证明 host 对布局 checkpoint 的持久身份、恢复失败清理和发布原子性。
