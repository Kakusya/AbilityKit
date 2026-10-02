# Research: S14 durable Host 发布与重启

- Query: typed baseline2落盘和已审阅generation transaction的安全提交次序、trusted restart与失败保证。
- Scope: internal；主工作树当前源码只读，以master125ffe906验证记录为入口；不跑.NET、不改源码。
- Date: 2026-10-02

## Findings

### 已有API与准确边界

`CookingMajorBaseline.cs:38-48`有typed Payload：Source/TargetScope、Preparation、ConfigIdentity、preparation/front/menu identities、InstalledLayout、Kitchen、Choices。:88-118 WriteBaseline执行完整序列化/重读/.next rename；:123 ReadBaseline返回typed，不负责Host restore。:155 ValidBaseline是结构校验，不能替代trusted权限。

`CookingLevelEtHost.cs:995-1059` generation staging已经创建隔离厨房，安装next几何先于AcceptSuccessHandoff，初始化front、Adopt、ownership/failure注入，然后调用InstallGeneration并发布owners。`InstallGeneration:1540-1605`有RestoreSourceTree，ET安装失败或lifecycle commit拒绝恢复旧树，但目前没有durable write参与。`WriteMajorCheckpoint`旧入口仍不能证明新baseline restart生产链。

`CookingLevelLifecycle.cs:841-861`CommitCandidate重新验证后创建下一generation；:908-935会修改_hasCreatedNextGeneration并生成event，因此在文件publish前应把所有拒绝/checked溢出条件预计算，不能假定这是纯不可失败赋值。

### 推荐实施次序（最小扩展，不新增authority）

在成功转换入口增加可选明确durable模式 `CreateSuccessor(..., preparation, progress, store)`，只允许源Ended+Success、choices Locked。原无store转换仍保留，但不宣称存档成功。不可把失败retry转成自动写基线。

1. 复用现staging完成所有候选厨房/geometry/front/menu/ownership验证，构造Payload：SourceScope为真实成功源，TargetScope为candidate Created下一关，Kitchen为候选ExportSuccessHandoff（已next seed与供应carry），Preparation为真实next preparation。choices冻结，不接受caller给自声明payload。
2. 预验证lifecycle commit与全部会溢出的version/event增量；准备front binding/delegate/adoption及最终host字段，清除所有publish后的可失败步骤。
3. ET临时安装candidate，保留旧树可恢复，在任何failure injector之前不写文件；如ET安装失败，RestoreSourceTree，旧file不动。
4. 在ET已安装、**source lifecycle commit与owner发布之前**调用WriteBaseline(payload)。返回失败即RestoreSourceTree并释放candidate；旧成功baseline必须仍可读。现WriteBaseline先校验.next后rename，可直接复用，临时文件不作为恢复current。
5. 文件rename成功是durable逻辑提交点。随后只运行预验证的无失败lifecycle/owner/binding swaps，取消旧pending，关闭释放旧simulation，返回Created next。不再调用任意factory、front验证、可抛failure injector或IO。

需要把InstallGeneration拆成“可回滚树安装”和“已预验证commit/publish”内部阶段，或接受内部persist callback置于正确位置；绝不能在现InstallGeneration返回成功后才write，write失败会留下已公布next代。也不要在ET安装之前write，注入ET失败将提前覆盖旧成功文件。

### 两资源原子性的精确声明

文件rename成功后进程终止而内存尚未swap，重启应读取**新成功baseline的Target Created**，完成commit；这属于成功收口提交点后的恢复，不是失败转换必须回旧基线。文件rename失败前任何拒绝保持旧源和旧baseline。

如果post-rename仍允许业务拒绝/注入异常，则不能同时保证“返回失败旧file不变”与“新file已经发布”；必须消除这些失败点或引入事务journal/备份协议。本轮最小方案是把所有失败提前、rename后仅commit，不新增journal架构。未知CLR OOM/硬件崩溃不应包装成可恢复拒绝。文件与内存不天然原子，没有fsync/断电耐久证明。

### Restart入口与trusted校验

建议static `CookingLevelEtHost.LoadMajorBaseline(store, expectedMatch, configuration, factory)`返回结构化read/restore reason和Host/Progress。先ReadBaseline，再使用factory按TargetScope重新生成trusted preparation policy、front配置及menu policy；比较ConfigIdentity、PreparationConfigurationIdentity、FrontConfigurationIdentity、MenuPolicyIdentity。允许scope-aware factory，但不能以payload保存的菜单/能力作为trusted来源。

构造Created lifecycle/isolatedhost，不走Running同关Restore伪装；用payload.Preparation核对targetLevel/map/logicalLayout；layout由trustedpolicy验证并重投影，seed由InstalledLayout.GeometrySeedPoses验证LastMovementTick=-1及initial语义；先安装有效geometry，再AcceptSuccessHandoff(payload.Kitchen)；先恢复choices并按target允许交集验证，不默认永久解锁可用；重建trusted initial front与menu，domain供应来源/份数/tombstones/allocator全部恢复验证。全部成功才返回Created Host，可随后BeginPreparation。菜单Ready连接后会再做真实生产可行性验证，不能跳过或自动Start。

重启不触发旧结算/奖励/供货物化，opaque allocator不调用历史sequence重算。Kitchen的logicaltick0、无订单/去重/事件是success baseline裁剪，不是运行态checkpoint；pending delivery RemainingTicks继续保存。失败restore Dispose临时host、释放ET singleton和ownership，旧文件保持不变，可以更正trusted config后重试。

### Menu身份与checkpoint8并行增量

当前验证入口说明Host Ready/menu policy及checkpoint8仍在其他分支推进。baseline2已包含JsonRequired MenuPolicyIdentity，所以新Host需接其真实factory身份（无policy明确null），并让baselineLoad与generationBuild使用同一个trusted menu解析入口。不能给payload身份“可选忽略”，不能将旧Level7默认补齐为8。store格式baseline2与Level8独立，更新其中一个不等于另一个通过。

### 生产流程测试与故障矩阵

真实source Preparing→Ready→Running自然营业→结束Success→choices Locked→durable CreateSuccessor到Target Created；销毁Host/新store实例→LoadMajorBaseline→Preparing备料→Ready→营业，与未重启对照的stock/process/portion/supply及hash收敛。

ET LevelCreated/DriverCreated/BeforeLevelPublish失败：旧baseline字节相同、旧树/源snapshot/queues相同；WriteBaseline失败：candidate rollback、旧baseline可读；rename成功后模拟进程停止：加载新Target，不重结算；wrong match/policy/menu identity、malformed typed payload/seed、levelAllowed越权：load拒绝且singleton释放；retry失败不写baseline，后续重启仍回最近成功Target；同request供应receipt不重复、allocator续水位。业务结束未满足订单仍自然成功，与原规则一致。

## Caveats / Not Found

未运行.NET或改源码。剩余不可规避边界不是产品选择缺失，而是file publish与内存commit间的崩溃窗：必须以rename定义成功commit点并提供上述重启语义，不能声称跨资源绝对原子或断电保证。后续source位置随其他owner代码修改应重新核对。
