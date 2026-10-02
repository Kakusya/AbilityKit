# Observation host 接线独立审查

2026-10-02，integration 当前未提交五文件增量只读复核：CookingLevelObservation、其 domain Tests、CookingPreparedGeometry.ConfiguredPlayers、CookingLevelEtHost.Observe、CookingObservationEtTests。保留他人更改；没有修改生产或运行 .NET。

## 限定结论

未发现 blocker，可接受本次只读观察接线。不能据此标记 S14 全部出口，尤其未覆盖成功跨关事务与 durable-save failure 的观察场景。

- Observe 先执行只读 Check：disposed/faulted/非 owner thread/正在 Tick 或 preparation mutation/initialization 均拒绝。随后直接读取当前 _ownedSimulation、lifecycle、front、installed layout/policy 和 HostFrameSequence；不调用 Create、Start、Tick、Restore、ExportSuccessHandoff，不创建厨房、不移动命令队列或 ledger、不重新判断交付合法性。
- 同步 idle owner thread 内没有可重入用户 callback，投影/identity 计算不推进权威状态。读取使用当前恢复后 owner，而非缓存旧 Recipe/Front。ConfiguredPlayers 仅复制 fixture IDs 为排序只读数组。
- Created 尚无厨房时 recipe/items/layout 可为空，factory CreateCount 保持零。正常 host scope/frame 来自同一 owner，解决纯 helper 无法独立证明 epoch归属的集成边界；它仍不是网络远端输入校验或并发快照协议。
- 全嵌套冻结保持前一份 observation-independent-review 结论：Containers、Supply Ledger/Origins、Front Flow/table paths/Spatial、installed layout 集合均复制。旧观察在真实 owner 后续 mutation 后 canonical 不变；投影不保留 simulation/callback引用。
- 字段已改为 `SupplyUnitsInTrackedPackages`，准确描述“原供货单份当前位于任意已追溯 package”。不再暗示留在其自身 original package。LiveObjects/remaining portions/external余额仍分开，没有第二 ledger。

## 实际测试与边界

ET 新测试通过真实 supply request enqueue（尚未 commit时余额/物件不提前出现）、Tick、receive、手持箱内容、取单份、加工progress、产物装杯、真实订单绑定、Ready/Running/Paused与resume，以及 dispose→Restore。StableRead 每次比较完整 Recipe checkpoint、lifecycle、frame、pending/disposition计数和实际 Recipe/Front snapshot；恢复后 observation canonical 与保存前 observation 相等。第二例只证明新 epoch Created 与旧冻结 observation 区分且读不 Create，不是成功跨关 handoff proof。测试没有真实拒绝 gameplay command 或 durable-save failure 场景，不能从暂停 Tick 拒绝推导这些覆盖。

独立只读核对日志：local/Logs/cooking-observation-root-kitchen-gate.log 为 473/601/238，均 0 failed/skip，gate passed；local/Logs/cooking-observation-root-et-gate.log 为 601/238，均 0 failed/skip，gate passed。该证据是 root 实际执行，不是本 reviewer 重跑。没有单独执行 lint/typecheck；门禁通过不等于全产品语义完成。若随后源码继续改变，需由 root 判断是否重跑相应范围。
