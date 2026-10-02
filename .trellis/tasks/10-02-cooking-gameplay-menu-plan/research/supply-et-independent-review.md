# S07 ET 接线与准备选择只读复审

2026-10-02。范围 integration HEAD `c61cce1f5` 加当前 WIP。未写生产、未运行 .NET；独立审阅仅本文件落盘。

## 结论

供应ET入口与Created选择增量限定静态通过，未见新增阻断。已知Supply sequence0恢复问题由producer另行修复，仍未解除；本报告不标全部S07/S06/S08/S14完成。有效geometry安装仍未接host，不作为已实现可变布局证据。

## 接线核对

- fingerprint旧字段写完后，仅SupplierId/DeliveryId/SupplyRequestId任一非null才加SUPP magic、version1与三nullable长度编码；全null不附加任何byte，旧golden保持。新增golden与delimiter/空串/交叉字段冲突反例覆盖实际bytes，不按普通字符串拼接。
- Request/Receive/Take相关payload由同ET ingress处理；供货clock由固定Tick推进，Pause不调用推进；前厅closing后StopNewSupplyRequests但继续pending到货/接收。恢复明确拒绝front closing=true而supply closing=false的矛盾状态。
- Level格式7、Recipe schema5严格检查；测试覆盖拒绝旧版本、必要字段缺失、pending codec/dispose/freshfactory恢复及received再次恢复。物理供货测试从请求、暂停、到货、接收、搬包、拆份到实际加工交付，初始只存在空plate；唯一厨房物件与Supply ledger复用。重复请求/接收检查同一Delivery/package，不复制。
- Created准备 mutation只有host私有窗口可开，finally关闭，外部Check和窗口重复进入均拒绝；AuthorityGate只在同owner thread、执行窗口、正确Created state允许操作。不是把Created永久开放为任意用户直接改world。
- ChooseDecoration先验证progress锁定/shape，再迁移；迁移拒绝时不改progress。Unlock先验证locked/blank/duplicate，Duplicate提前返回不PlaceUnlock；成功放置后再登记progress。后一个动作在同线程、不可重入窗口外提交，无中间callback可切换progress锁定。测试含未知station/锁定progress失败零变更及duplicate不复制。

## PreparedGeometry 的有限检查与尚缺门

EffectiveSpatial getter优先安装值，移动、interaction/preview和扩展checkpoint校验已改为effective读取。内部预检校验全部player数量/唯一性、ValidPose、相互碰撞、LastMovementTick保留；护住live world/station物件、加工station、supplier source/receiving anchor引用。安装预先freeze/建字典/checked版本后再一次更新，手工ActiveWorker释放。

**不能单独作为完整安装合同：** 当前preflight不要求每个既有空闲Appliance都有station anchor，只检查live物件/process涉及的station；若调用者传入有空设备但删其anchor的投影，可能通过这一内部检查。计划host适配须验证全部有效设备/前厅anchor和通路（或内部补全此守卫）；不要把“live引用有效”等同“全工位可用”。新geometry还未完整进入host安装、可信layout identity与恢复/跨关流程，安装字段本身不是checkpoint持久化证明。source/receiving当前按唯一ID存在检查，真实供应规则应沿同一个effective geometry进行reach解析。

## F08 恢复证据纠正

现有F08恢复Theory新加提交后再export→dispose→restore→final CanonicalText一致，证明终态可恢复。没有不中断control臂的同命令序列/最终fullcheckpoint对比；文件唯一终态canonical断言是final与再次恢复相等。不能记录为“恢复与不中断control最终等价”。如要求该判据，补真实control臂；该差异已告知root。

Tests/TypeCheck/Lint本审阅未运行，等待root实际聚焦和组合门禁。Supplyseq0已知blocker未重复接手，不把受限静态通过扩为全部供应出口。
