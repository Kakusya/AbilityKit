# Research: S06 前厅 ET 接线独立审阅

- Query: integration 工作区 S06 生产/测试增量是否保持可信恢复、固定Tick和既有经营语义？
- Scope: internal；只读当前未提交源及已存在日志，未执行 .NET，未审阅并发S08新文件。
- Date: 2026-10-02

## Findings

结论：本次静态审阅没有定位到应阻止 S06 接线交付的具体代码缺陷。现有证据证明本增量范围，不证明 S07实际库存/S08动态布局/S14完整单机出口。提交前仍须由协调者核对实际最终diff与验证源一致。

### 文件与合同核对

- `C:/Users/Administrator/orca/workspaces/AbilityKit/cooking-integration-s06-s14/.trellis/tasks/10-02-cooking-singleplayer-front-house/research/et-integration-verification.md`：实现边界和真实门禁指针。
- 同工作区 `src/AbilityKit.Game.Cooking.EtRuntime/CookingLevelEtHost.cs:333` 冻结可信factory配置并计算identity；`:425` 验证house schedule/menu/Flow与可信配置一致，`:918` 恢复先比对checkpoint identity与外部factory身份，非payload自报。
- `:440` 同一实际厨房SpatialIdentity，并对入口/队列/出口/桌位路径端点检查当前anchors坐标；`CookingFrontOfHouse.cs:728` 验证路径中心、半径净空、障碍及四邻接连续性。未把静态layout测试当ET接线证据。
- `LevelEtHost.cs:464` BindFrontOfHouse给新的house重新ConfigureManualWork，其delegate明确捕获当前方法参数kitchen；恢复 `:994` 替换house后再次Bind，使用重建simulation，不继续捕获被销毁的旧厨房。
- `:997` 恢复核查所有人工worker当前可达、可用且不持厨房手工任务；`:998` manualpolicy等于trusted；`:999` ServiceTicks等于min(Recipe.LogicalTick,ServiceTicks)，阻止伪造服务时钟。旧无Flow/no-manual无trusted路径仍可恢复，新增状态不可通过旧legacy路径自报启用。
- `CookingRecipeLoop.cs:310` shape将front WorkID放WorldAnchor，拒绝厨房/移动字段；`LevelEtHost.cs:90` 原fingerprint已包含WorldAnchor。`:1008` 使用原Submit身份/scope/dedup后dispatch，先预检事件/版本overflow，成功才Commit；缺前厅owner明确拒绝。
- `RecipeLoop.cs:1011/1024/1511` 厨房持手工任务时拒绝前厅claim/continue，前厅认领时拒绝厨房continue及manual start；Stop释放不被反向互斥挡住。实际host ingress按既有稳定批次仲裁，未新增队列或排序策略。
- `CookingFrontOfHouse.cs:64` EnsureFrontMutation覆盖ConfigureFlow/ConfigureManualWork/Claim/Continue/Stop、带BindMenu的ExportCheckpoint、RestoreCheckpoint、Step、DropFailedScene、ResetForNextLevel、FinishInProgress等公开写路径；Snapshot/HasPlayerWork保持纯读。绑定gate本身internal，`:1596` host gate仅front管理操作或正在执行authoritative Tick开放；暂停没有成功Tick推进分支。
- `LevelEtHost.cs:1140` 厨房fixedTick后推进前厅，并在trusted配置路径派生厨房Closing/Completed；`:653` trusted前厅未CanSucceed不能BeginEnd Success。legacy未新增强制自然结束检查，保持旧fixtures行为。
- `CookingFrontOfHouse.cs:288` 新Flow/manual成功重置必须自然CanSucceed；`:311` 新模式不强制瞬间完成残留工作，legacy仍保留FinishInProgress语义。成功清理jobs/transit/顾客及伙伴关内成长；失败 `LevelEtHost.cs:817` 对旧house DropFailedScene清认领/短态，厨房经原CreateRetry重建基线，不重用失败模拟。
- `CookingLevelCheckpoint.cs:25` 配置身份JsonRequired，codec `:85` 格式6，旧版本拒绝；front extended字段对应JsonRequired与结构验证。恢复 `LevelEtHost.cs:1007` 派生关闭标志不增加stateversion，避免重建与不中断hash分叉。

### 测试证据读取

`src/AbilityKit.ET.Runtime.Tests/CookingFrontOfHouseEtTests.cs` 9项执行例覆盖claim仲裁/重复、两种互斥命令顺序、暂停/继续/停止、shape与scope/WorkID指纹、转身释放、销毁新factory恢复、伪造policy/geometry/双owner与clock拒绝、真实路径排队/脏桌/未满足订单0星自然成功及Created successor。

已读取 `local/Logs/test-gates/20261002-200306-cooking-et-level-runtime/cooking-et-level-runtime/gate-summary.json`：四步Passed/exit0；对应TRX Counters实际 domain477 executed477 passed477 failed0 skipped0，ET175 executed175 passed175 failed0 skipped0。domain包含并发S08既有12 helper例及新增shape3例，不归为S06新增477功能。

首轮 `20261002-195813-cooking-et-level-runtime/.../03-Cooking_domain_and_Level_lifecycle_tests.log` 保留失败：enum operation尾部缺Claim/Continue/Stop，rejection数量expected38 actual40；不是吞掉首轮记录。修正shape后最终门禁通过。研究记录另指出focused ET9、frontdomain37、shape13，不能用这些focused取代finalgate。

## Caveats / Not Found

- 没有新启动测试或执行Git操作，报告是独立静态审阅及已有日志核对。提交后源码变化须重新评估日志匹配范围。
- 新ET测试明确有成功跨关；新增人工认领后的失败重开未找到同样明确的独立新增fixture。源路径已清jobs/transit且现有失败回归在完整gate通过，但S14应补“失败前人工认领+采购+半成品”综合重试，不能把现有厨房失败测试当整套新增状态证明。
- 手动委托配置会再次冻结trustedpolicy身份；配置如果未来引入动态layout（S08），必须让trustedfront与实际新geometry一并更新/恢复，不能继续使用旧factory静态Flow。此次不越权实现该未来接口。
- 当前恢复service-clock公式依赖可信front从本关第一个fixedTick开始推进。未来S14若允许中途附加front，须禁止该能力或显式记录attach基线，不能沿用现公式而声称支持热接入。
- 不扩大S07/S08源所有权；不把保留分支未提交增量说成master已有交付。无新增外部参考。
