# S07 ET/codec 与 Created 选择原子性增量

2026-10-02。integration c61cce1f5及主owner同期EffectiveSpatial WIP，未提交。只修改CookingLevelEtHost、CookingLevelCheckpoint codec、new CookingSupplyEtTests及ET既有版本控制/F08恢复控制；不修改RecipeLoop/checkpoint/menu，也不修改root有效空间源码。

## 改动与实证

- Command fingerprint 全部SupplierId/DeliveryId/SupplyRequestId为null时完全保留legacy bytes；任一非null追加明确SUPP marker (`0x53555050`)、extension version1、三个nullable UTF8长度编码字段。包括空字符串、字段错位、分隔符内容、illegal extra fields的不同payload均不alias。既有真实golden向量回归通过，新suffix golden是`535550500000000101000000017300010000000172`。
- ET shape使用已有RecipeCommandValidation，没有新解析旁路。不同SupplyRequestId同command key立即CommandIdentityConflict，所有已admit成员terminalize且零ledger reservation。malformed字段拒绝，不把供应商不存在混作shape invalid。
- 真实ET Request→fixedtick，暂停不推进，到货→Receive单package/Nunits→重复回执不复制→Pickup/Drop箱→TakeOut现有raw→Drop工位→Start/Tick→Pickup/PutIn盘→Submit真实settlement。有限外部余额只预留一次，剩下2个raw留箱。没有inject成品或中间态。
- 前厅进入Closing时host调用StopNewSupplyRequests；后续新采购拒绝、原pending继续arrive/Receive、无限Take仍允许完成既有业务。restore拒绝Front Closing却Supply Closing=false的篡改，不信现场重新开放采购。
- Level codec升候选7并严格要求内层Recipe5；拒6/Recipe4及缺required SupplyOrigins。成功pending/received状态恢复绑定freshowner，当前最终checkpoint canonical完全等价。F08 Serving/Table两mode成功后的完整Level checkpoint再次dispose/restore等价控制亦通过。
- Created successor选择：progress.ValidateDecoration先纯预检，再MigrateStations，成功才记录新choice，失败时原空choice也保留。Unlock先Validate（locked/empty/duplicate），再PlaceUnlock，成功才记录；duplicate不再放物。
- 新真实测试揭示既有成功Unlock自身仍抛异常：PlaceUnlock只临时detached lifecycle，HostAuthorityGate仍仅fixedtick。root确认后新增仅内部预检Created操作的RunPreparationMutation，try/finally撤销flag，Check/Dispose拒重入，HostAuthorityGate仅该Created窗口允许。success后外部AddItem仍拒绝，无authority泄漏；IsGameplayMutationOpen没有对外开放。这不是Preparing时钟/采购完整支持或S08布局安装。

## 实际运行与失败保留

首次专属5cases：2pass/3fail（真实成功Unlock authority问题、fixture第二live ET host争占恢复、把legalshape unknownsupplier当Malformed）。修生产guard及fixture控制。第二组合43cases：42pass/1fail，测试误认为同ID payload conflict admission会Accepted，现正确断言立即CommandIdentityConflict及2个terminal Conflicted；不弱化生产行为。

最终组合focused **43/43 pass，0skip**：供应5 + existing Fingerprint/golden + LevelCheckpoint + CoreEtExpansion + OrderBinding + FrontDelivery + FrontOfHouse。随后只新增Closing篡改/F08最终checkpoint等价断言，再实际 **12/12 pass，0skip**（供应5+F08 7）。incremental最终输出无本增量warning/error；首次/域编译含既有shared Network/LiteNet/InMemory warnings，未抑制。

按root额外要求只读跑CookingPreparedGeometryTests，实际 **2/2 pass，0skip**，编译成功；两例为root受限域安装源的reach变化与缺liveanchor拒绝，仅域helper不声明ET layout安装。

全部日志/TRX已复制main `local/Logs/cooking-execution/cooking-supply-et-{first-failure,second-failure,final,controls}.{log,trx}` 及 `cooking-prepared-geometry.{log,trx}`。首次和第二次失败原始证据保留；新测试改动之后未重跑完整gate，独立review/组合gate交root。窗口已明确释放，source稳定，未commit。

## 出口边界

仍未实现Preparing唯一实例采购/固定Tick入口、真实布局host安装与动态几何checkpoint、trusted bind工位约束或全部S14。S07 component/ET增量不替代整个单机出口；root另收敛成功/失败baseline与allocator降零修复、整体测试与精确checkpoint identity。Unity和联网产品仍按独立阶段门。

Independent review clarification: the F08 added final comparison is export-after-submission -> second Dispose/Restore -> canonical equality. It proves final checkpoint round-trip, not final equivalence to an uninterrupted control branch. Keep the original 7 destination controls and do not relabel this additional assertion as uninterrupted execution equivalence.
