# S14 纯观察投影独立审查

2026-10-02，只读审阅 integration 导入的 CookingLevelObservation.cs / CookingLevelObservationTests.cs，及 main S14 research/observation-increment-review.md。未审正在接线的 host WIP，未修改生产、未运行 .NET。

结论：纯投影增量限定通过，未发现阻断。下述字段命名应澄清；这不是 S14 完整出口或 host frame 一致性证明。

## 冻结与真实来源

- Recipe 所有列表均复制并包为只读；Containers.ItemIds、Supply Ledger.Balances/Deliveries/Requests、Origins.Units 递归复制。item/process/order/settlement/provenance entry 均为无可变嵌套集合的 record 值。Lifecycle 仅标量/ID；浅 `with` 不留可变容器。
- Front 的 menu/customer/wash/unsatisfied/work/table 列表复制；Flow 的 walkable/入口/出口和每桌两条路径递归复制，Spatial.Freeze 复制 poses/anchors/obstacles。InstalledLayout 的四类集合复制，其 entry 为纯数据 record/value。观察对象不持 simulation reference 或 writable callback。
- Players 来自 configured players、真实 poses、PlayerHand owner、实际 ActiveWorker 和 front Work.Player 的并集；HeldItem 按真实 hand location 提取并拒绝多物件手持。原料状态、recipe、dirty/completed、portions、绑定订单、process worker/progress 与 front job 全保留 owner snapshot 字段，不根据 ID 文本猜状态，也不重新判定可交付。
- Scope 必须等于 lifecycle 完整 scope（含 epoch）；Recipe 匹配 match scope，frame/version/tick 非负。Created 可无厨房，并保留 configured players 的空 pose/held item。Recipe snapshot 本身不含 Level epoch，因此本 helper 无法独立识别同 match 的旧 epoch Recipe；必须由 host 提供同一次已提交 frame 的当前 owner snapshots。尚未审 host，不据此宣称该集成保证已成立。

## 库存语义

LiveObjects、ContainerObjects、RemainingPreparedPortions、ProductObjects 分别按当前 live items/container index/真实份数/product flag计算。一份五份批次仍是一件物件；外部余额与 pending delivery 留在原 Supply ledger，不转成厨房物件。原 units 来源追溯只用于与 live ID 做集合关联，不建立第二 ledger。

`SupplyOriginalPackageUnits` 实际统计原 supply unit 当前处于“任意 Origins 中有 package ID 的容器”，并非该 unit 自己 origin.Package。换到另一追溯箱也计入。因此建议改名 `SupplyUnitsInTrackedPackages` 或明确 XML 语义；不应在 UI 表述为“仍在原箱”。这是命名/语义澄清建议，不是当前计算或权威恢复 blocker。UnavailableOriginalSupplyUnits 仅表示原 unit ID 不在 live itemMap，不能区分消耗/丢弃，也不代表供货余额。

## 验证与未完成项

五例测试覆盖 scope/epoch/frame与Created空厨房、手持容器和真实progress/order、external余额与physical库存分离、mutable嵌套输入清空后观察canonical不变、重复投影不改变simulation full checkpoint。冻结例确实修改 containers/origins/balances/flow/table routes/layout floors；Spatial 深冻结通过既有 Freeze 静态确认，测试未逐一毒化每个集合，不把5例当全覆盖。

Supply research 记录实际 focused 5/5、0 failed/skip，本 reviewer未重跑。Host Observe wrapper、拒绝命令/暂停/保存失败/恢复/成功跨关后的同帧观察仍由单 owner 接线并待 gate；没有审其不稳定 WIP，不把源导入或既有组件测试当作这些出口完成。Lint/TypeCheck 未单独运行，以免共享输出竞争。
