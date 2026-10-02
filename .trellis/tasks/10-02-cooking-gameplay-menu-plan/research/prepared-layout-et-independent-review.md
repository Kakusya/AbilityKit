# Prepared layout ET 增量独立审查

2026-10-02，integration d9b0e5603 后当前稳定 source increment，只读；不修改源码、不运行 .NET。结论：本次受限 Preparing 布局安装/同代恢复接线静态通过，未发现新增 blocker。跨关 seed 事务仍待后续，不构成 S08 或其它 Task 完整出口。

## 实际调用路径

- BeginPreparation 先验证 preparation，再从 trusted factory Freeze policy、InitializePreparationKitchen、绑定 authority，安装实际 geometry/derived front，随后 PublishKitchen 和开放 preparation admission。初始布局不是只写 checkpoint 元数据。
- Restore 在创建 gameplay 前核对 trusted preparation identity、installed layout 必须与有无 preparation factory 一致、policy 投影合法、seed 全部 LastMovementTick=-1 且与投影 InitialPoses 排序相等。投影使用 configuration 的 configured appliances。ActorRadius、footprints、station binding、BaseAuthorized/LevelAllowed 来自 factory policy，checkpoint 不可自行授权。Host 的安装调用没有传入 caller unlocked set。
- 创建恢复厨房后先安装保存 geometry，并按保存 Recipe references 做几何 preflight，再完整 RestoreCheckpoint。不会要求将被覆盖的 factory 旧物件继续具有旧 anchor。完整 Recipe restore 失败会 Dispose host；不能把 preflight 当完整 restore。
- Preparing fresh-front canonical 检查使用保存布局推导的有效 front policy；Running 恢复随后重建 saved front 并重新绑定 callbacks。原始 trusted front identity 仍参与配置核验，不能用 payload flow identity 自授予新路线。

## 安装原子性与保留状态

- 动态入口只允许已初始化的 Created/Preparing、无 pending/in-flight。core 保留 authority/layout gate、live item/process/supply 引用、完整 player 集合、移动水位及 overlap 检查。
- 新 front/flow/menu、manual callback 配置、candidate geometry 下的配置验证以及 installed checkpoint 均在 core swap 前完成；拒绝不修改已安装 geometry/front/checkpoint。旧 front 非初始业务 state 时拒绝重建，防止布局调整清空客户/进度。
- swap 后 `BindFrontOfHouse(prepared:true)` 不再执行 validator 或 ConfigureManualWork。后续仅绑定 delivery/work delegate 和 authority gate；这些是赋值操作，没有业务验证失败分支。manual predicate 使用更新的有效配置和实际 kitchen geometry；delivery delegate 捕获新的 house，避免指向旧对象。
- 相同 layout canonical 在合法 preflight 后直接成功，不清 worker、推进版本或重建 front。不同布局保留 contents、locked inputs、process elapsed、item version 与产品计数；清除 ActiveWorker，后续需重新 Continue。ProjectedPoses 保留 movement watermark；checkpoint GeometrySeedPoses 是归 -1 的独立 seed。
- 初始 trusted layout 可投影但缺厨房 live/supply/front 引用时，安装拒绝后释放 simulation ownership、abort/remove lifecycle 并 Fault，driver 未发布。测试另创建 host 成功，证明没有残留进程 owner。Freeze 本身的配置错误发生于厨房创建前，不应把这种早期抛错和已创建厨房安装失败混写。

## 测试与证据边界

审阅 CookingPreparedLayoutEtTests：初始真实 bounds/obstacles/derived flow；同 layout canonical 幂等；Running 调整拒绝零变更；live anchor/front route 缺失零变更；Preparing/Running 保存新 anchor 后恢复 exact full checkpoint；actor radius/unauthorized equipment/非归一 seed/伪造 flow 创建前拒绝；partial manual work 保留 elapsed/locked inputs/watermark 并释放 worker、重新继续实际生成 product；初始缺必需引用 Fault 后可创建新 host。

该恢复测试确实比较完整保存/恢复 canonical，并继续实际 Move；它不是两臂推进至终态的 uninterrupted control proof。manual preservation 测试有真实 ET command/Tick，非纯目录或 getter 断言。

Producer 的 17 focused 通过由 parent 提供，本 reviewer 未重跑。只读看到 local/Logs/cooking-prepared-layout-root-gate.log 已有 464/592/235，均 0 failed/skip 和 gate passed；这不是对随后所有 WIP 的冻结证据。最终 full gate 仍需 source freeze 后由 root 确认，不能将日志数字当全功能或跨关证明。Lint/TypeCheck 未自行运行，未争用共享输出。

## 后续限定 resolution：实际工位迁移、双臂恢复与 codec 防空

再次只读审阅更新测试及 CookingLevelCheckpoint DTO。原文“不是两臂推进至终态 control proof”的限制已由新增控制臂部分解除：Preparing/Running 两例在保存后，原 owner 实际执行同一 Move+Tick，恢复 owner 从保存点执行相同 Move+Tick，最终完整 Level checkpoint canonical 与 uninterrupted 相等。这证明该后续动作终点等价，不能扩大为跨关或完整营业终态证明。

新增 occupied station 测试实际将工位从 (4,2) 移到 (6,2)，保留同一 process/item/locked inputs/elapsed/item version，并清 worker。旧 pose 下真实 ET Continue 和 Pickup 都拒绝 TargetOutOfRange，进度不变；两次 Move 到新位置后 Continue 推进、Tick 完成，并在原 station ID 生成 product。补足实际交互 anchor 移动证据，而非仅新布局 ID 或 bounds 变化。Parent 报告 layout focused 9/9，本 reviewer 未重跑。

Root 另发现并以红测试验证 InstalledLayout canonical 对 null seed/floor/equipment/target entry 的 NullReferenceException。最新 `CookingInstalledLayoutCheckpoint.CanonicalText` 在任何 layout canonical 或 seed 排序之前检查 Layout、四类引用列表与其 entry，以及 Walls 列表，统一抛 ArgumentException；Walls entry 是值类型，不需要 null entry guard。既有 codec 的 ArgumentException catch 返回 RecordTruncated，所以毒化 payload 在 integrity hash 计算阶段结构化拒绝，不再向调用方泄露 NullReferenceException。

新 Recovery Theory 四例先生成合法 envelope，再将 JSON 对应数组改为 [null]，实际通过 Deserialize 断言 RecordTruncated；覆盖的是反序列化 integrity 路径，不是直接调用 guard 的镜像断言。Parent 记录 red 1，随后 entries 1 pass/3 fail，修复后 green 4/4；本 reviewer 静态确认修复闭合但没有重新执行这些测试。该 guard 不替代 layout policy 或完整 Recipe/Level restore 校验。最终 full gates 因新 source 正重新运行，先前日志不作为本新增修复的最终门禁证据；跨关 seed 事务仍未完成。
