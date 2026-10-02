# Research: S14 F01+D31 自然经营 fixture 复用映射

- Query: 同一厨房有限采购、双人加工、真实前厅点单/交付和连续恢复如何最小复用既有87菜测试？
- Scope: internal；只读源，不.NET、不改production。
- Date: 2026-10-02

## Findings

### 已有driver可复用与不可直接代证的边界

- `src/AbilityKit.Game.Cooking.Tests/Fixtures/CookingMenuProductionFixture.cs:32` 构造器固定单menu，初始化每raw8份独立世界槽，不能直接用于有限采购同厨房双menu。
- 同文件 `:132–135` CommandDispatcher/FrameAdvance/AfterFrame/UseRestoredSimulation是可复用ET回调模式；`:357` Produce按catalog production DAG执行，是已验证图操作算法；`:165` SubmitDelivery直接OpenOrder，不能用于S14真实front开单。
- `src/AbilityKit.ET.Runtime.Tests/CookingMenuProductionEtTests.cs:82` 实际TryEnqueue→Tick→dispositions可复用；`:90` AfterFrame检查实际状态checkpoint点、Dispose旧host、新factory恢复及driver重新指向新simulation可复用。其证据已经明确direct OpenOrder fixture边界，不修改该测试来偷换完成语义。
- `CookingFrontOfHouseEtTests.cs` Factory是真实trusted front、路径/桌/手工作业组合示例；其虚构Raw/Product制作不替代F01/D31图。`CookingPreparingEtTests.cs`、`CookingSupplyEtTests.cs`提供准备同厨房复用和供应请求/接收的真实ET envelope方式；采用模式，不复制另一套authority。

### 最小新fixture边界

新增仅测试层 `CookingNaturalOperatingFixture`，实现现有trusted factory/preparation/front/menu接口，合并catalog.Requirements([F01,D31])一次ToContentDocument并LoadContent。两menu共享同一Config/Scope/host/厨房/工位/供应账，不能分别构造两个旧fixture的simulation后拼snapshot。

设备和容器初始化可以沿catalog闭包建空器具；raw标准初始化必须移除，改为supplier unit/package定义与实际收货/仓储anchor。每种本轮所需raw有限supplier余额显式足够当前场景，数值标fixture，不模拟87菜采购总量；actual RequestSupply→等待Tick→ReceiveSupply→搬包/TakeOut提供units。容器来源可明确初始有限空器具/cleanpool；不凭生产节点需要直接AddItem原料。

生产复用两种选择：

1. 最低风险新增测试driver把既有Produce递归算法提取为测试内 `ProduceMenu(menuId,inventoryResolver,commandDispatcher)`，库存resolver取真实包装拆出的units，器具homes取真实layout anchors；算法仍catalog DAG，不手写第二菜谱。
2. 如不愿修改已完成87fixture，可新driver复用相同graph/producers方式，仅F01/D31执行动作调度；字段/节点从catalog读取，禁止硬编码重定义投入/产出。已有87逐菜测试继续原scope运行。

交付新方法 `DeliverToExistingCustomer(product,orderId)`只能查host FrontSnapshot真实开出的客户订单，BindOrder/SubmitOrder走原lane及F08正确桌/出餐anchor。禁止调用OpenOrder，不能沿旧SubmitDelivery间接开单。对象version每次从当前simulation snapshot取；恢复后driver重指向新的simulation，不保留旧ItemState引用。

### 确定性场景安排

准备阶段完成有限采购、部分raw切配/茶基底，供应/工序继续但前厅service时钟0。为双人接力，选catalog已有Manual节点并在测试可信content显式RequiredTicks>=4：A开始一tick，StopProcess释放并保持，B ContinueProcess完成；动作间通过合法Move/朝向，不靠超大interaction radius掩盖路径。

营业Start后真实伙伴或人工询问开F01和D31订单，至少第三客自然未满足。fixture明确服务窗口/到店间隔/等待足够前两单制作，第三单等待阈值耗尽自然离开。两位玩家独立推进生产节点；自动茶加工启动后离开做其他工作；D31对已有图最终奶盖必须最后节点，禁止早加可单独验证拒绝后恢复。

完成F01装盘直接核单交付，D31制成未绑定可暂存→另一人拿取/绑定真实order→按F08交付；dirtytable/洗碗可人工接手并伙伴接续，人工完成不加伙伴成长。不以全部OrderCompleted为自然成功条件：停止来客、已有客人离开/清桌、CanSucceed后TryFinishService，允许UnsatisfiedOrders非空。

注意“成功交付两单”与“0星”不是自然等价：保留现有CalculateStars，fixture可使用明确高于两单得分的已有score thresholds证明0星仍Success，不能篡改产品得分或强行断言有交付必0分。若该fixture不调整threshold，则另一个无人交付自然场景证明0星成功。

### continuous / replay / DisposeRestore trace

统一driver记录每帧前的完整envelopes或空Tick，固定commandIDs/batches/player/scoped metadata；先运行连续baseline，保存每帧 `{Frame,LevelScope,Phase,Dispositions,KitchenCanonical,FrontCanonical,SupplyCanonical,InstalledGeometryIdentity,ObservationCanonical}`。

fresh同可信factory重复播放原输入轨迹得到逐帧等价；第三次在中间强状态点（pending采购、已接收包、paused人工半成品、未绑定D31杯、排队顾客中选可同时成立组合）导出/codec、Dispose旧host、factory恢复、更新driver引用，再播放剩余完全同inputs。不是只比较最终hash或恢复后一个tick。

观察投影冻结后每帧对比scope/hand/material/container contents+portions/manual worker/自动进度/order binding/front table+work/库存及在途。客户端UI不在此验收。Restore非法payload负例由已有精准测试承载，此fixture重点证明真正营业继续等价。

成功下一关保留实际剩料/在途和有效布局，订单/绑定/伙伴关内成长及人工认领按合同清；新scope拒旧envelope。Failed控制从已成功基线开始，追加本关采购/认领/临时layout，再技术CreateRetry并检查无泄漏；未满足订单不触发Failed。这些跨关段由当前单一host producer最终稳定接口接入，不抢改host事务。

## Caveats / Not Found

当前Ready辅助仍在reviewfix、host事务与动态布局归独立producer，新fixture先定义factory/driver/trace合同，待接口stable后ET实际运行。不可把读取生产图或旧87fixture绿色直接声明自然经营完成。

本次只研究，不改源、不跑.NET；不新建订单失败、贴单距离、UI或另一套菜单图。
