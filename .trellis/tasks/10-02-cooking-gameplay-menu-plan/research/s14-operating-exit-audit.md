# Research: S14 当前完整经营出口差额审计

- Query: 当前master及Preparing/供应/前厅/恢复集成后，尚未闭合哪些单机出口？
- Scope: internal；主任务资料及integration源码只读，不.NET、不改production。
- Date: 2026-10-02

## Findings

读取 S14 PRD/design/implement、completion-contract、preparing-composite-verification，沿先前 s14-runtime-exit-research 接续。87菜真实制作/盛装/提交及恢复已由原内容owner覆盖，本轮不重新建菜单生产图。当前报告区别master7a043b6cb、已核对integration d9b0e5603边界和正在修改的动态layout源，WIP不是已验证交付。

### 当前差额与精确接入位置

| 出口 | 现有可复用 | 真正剩余 |
|---|---|---|
| scoped观察 | RecipeSnapshot与FrontOfHouseSnapshot；host Binding/Lifecycle/HostFrameSequence | 单一完整帧捕获的聚合只读观察，携带LevelScope/epoch、当前phase、geometry/policy identity，不能返回可写simulation给外部 |
| 物件/手持/加工 | Recipe Items.Location/Definition/Recipe/BoundOrder/RemainingPortions、Containers.ItemIds、Poses、Processes.ActiveWorker及elapsed/required | stable definition/menu提示键，手持按Hand位置派生，半成品阶段按definition；关联无悬空ID、库存真实实例汇总，不维护第二状态账 |
| 订单/前厅 | Orders.RequiredRecipe/container/status、物件绑定、Front work/customer/table/queue/service clock | aggregate正确区分未绑定/已绑定/待交付/已完成与伙伴/manual负责人；F08服务目标配置来自可信owner，不用提示侧重新判断配送成功 |
| 供应 | Recipe5供应载荷及实际物理包装，有限余额/在途/到货/接收 | readonly产品摘要区分外部余额、包装内部单位、手持单位、已消耗/丢弃，不能把有限餐具池或外部余额当厨房原料库存 |
| 许可 | CookingMenuCatalog.Requirements/ValidateLevel；CookingPreparationConfiguration.Project 已具 BaseAuthorized∪unlocked 再LevelAllowed | 当前Project主要检验设备布局，不是整条菜品供给/容器/工序/交付闭包；真实Ready门需按menu逐条检查 |
| 自然营业 | S06可信Flow、固定伙伴/manual排他、stop来客、未满足记录、自然成功 | 使用实际菜单/采购/布局整合fixture证明取料→制作→核单交付→来客用餐/离开→清桌；不能手动OpenOrder/BeginEnd假装自然链 |
| 恢复重放 | Preparing/Running composite信封恢复、service offset及新owner callbacks | 综合帧轨迹连续基线、无恢复重放、销毁恢复三者逐帧对比（各owner canonical/命令结果/水位），含供应和动态layout |
| 跨关 | host CreateSuccessor/CreateRetry、原成功handoff/majorchoices | 有真实剩料/在途/认领/布局场景的成功与失败控制，权限变化不静默删除物件；新关重置必要短态且旧scope拒绝 |
| durable基线 | CookingMajorCheckpointStore Write/Read，host WriteMajorCheckpoint | 一局成功基线包含新增库存/供应/有效layout/majorchoices并能销毁后可信重建及失败恢复；字符串读回完整性不是产品加载流程证明 |

### 最小existing-owner API

1. 在host加 `Observe()` 冻结记录，直接组合已拥有RecipeSnapshot、FrontSnapshot、供应摘要、installedlayout/preparation policy、scope/lifecycle/frame；只允许完整提交边界读取，不添业务owner。只读hint可独立文件/纯函数，不新增Unity资源依赖或UI。
2. 在catalog附近加纯 `ValidateSelectedMenuAvailability`，从现有Requirements逐菜取闭包，结构化UnknownMenu和缺项记录menuID/definition/capability/container。host CompletePreparation调用，失败不Ready。实际availability来自已安装厨房、supplier配置、允许定义及可达设备；不接受外部caller自报capability。
3. `CookingPreparationConfiguration.cs:72` Project已有可信StationBindings与Appliances集合检查，`:84` BaseAuthorized+unlocked交集逻辑可复用；必须让菜单许可复用同一有效授权，不能另用所有已加载Definitions。supplier.UnitDefinition、全部加工inputs/outputs/carriers和出餐容器都属于许可闭包。有限当前库存不足不能自动拒绝Ready或造成正常失败，允许补货；但供应类型缺失或结构不可制作明确拒绝。
4. `CookingLevelEtHost.cs:887` WriteMajorCheckpoint当前沿SuccessHandoff；`CookingMajorProgress.cs:136/168` store已有Write/Read，read返回KitchenCanonical字符串。优先扩原成功基线封装/加载而不发明随时保存系统。动态布局与trustedfactory配置验证需同一个原子恢复入口；不信文件自报Footprint/permission。确认持久范围后将真实store版本与Level/Recipe格式区别记录。

### 必需实际fixture，不重复菜单研发

- F01+D31实际经营：准备合法layout/许可并有限采购、包装拆份、切配换人、自动设备、奶盖最后收尾、餐食核单与饮品绑定、正确服务点提交。至少另一客人未满足自然离开、清桌、0星亦能成功。伙伴固定询问/洗碗保留原level-local成长；人工完成不增长伙伴。
- 每关键帧Observe检查scope/携带物/容器成分及剩份/当前worker/订单绑定/库存与在途/顾客与工作状态；持物移动后的提示一致，失败动作只产生拒绝不虚报拿放成功。pause整态时钟固定，Preparing供应/工序推进但front时钟不动，Start offset明确。
- 权限：default允许且未unlock可用；仅unlock但不在allowed不能供应/加工/摆放；在allowed却未授权拒绝；配方存在但缺supplier/carrier/可达工位/饮品贴单能力不能Ready。保留已带来的禁止物件只允许保留/清理规则，不能静默删除。
- 控制轨迹同时含pending/arrived供应、未绑定已制杯、手工半途、顾客排队及有效动态布局；先跑完整不中断baseline，再samecommands重放，再中途Dispose/restore续跑，对比每帧全部canonical和结果，不仅 immediate export或单tick hash。
- 成功下一关：保留厨房剩料/批次/容器、有限外部余额及在途的已定规则；清订单/绑定/认领/伙伴level成长；Created/Preparing可观察、旧scope命令拒绝。新allowed变窄先要求合法准备，不消灭带内容锅。
- 技术Failed分支：先有真实成功基线，再追加失败场景采购、物件/认领/临时布局；CreateRetry恢复基线供应和已确认majorchoices，失败现场不泄漏。未满足订单不进入Failed。
- durable控制：临时目录写成功基线→销毁host/store→重新Read并可信创建→相同继续轨迹与内存基线等价；截断、改hash、旧format、错scope/config/设备尺寸均拒绝且不替换当前authority。清理仅本fixture临时目录，不触碰用户存档。

### 源码分工建议

Readonly observation与菜单许可纯validator+新ET验收文件可独立owner；host Ready接线/dynamiclayout/majorbaseline/恢复格式仍由单一hostowner串行，Recipe库存只读取S07实际公开接口。不要让观察worker修改S07供应commit或S06交付predicate，不将已完成87内容再次生成。

## Caveats / Not Found

- 当前动态layout源码正在变更，本次只定位接口和缺额，尚未独立证明实际安装、跨关恢复或aggregate observation。
- durablebaseline是否要求文件恢复完整新厨房+供应+布局，必须与既有成功检查点产品约定核对：当前有文件store，不能说完全没有durable实现；同时“KitchenCanonical读回”不能代证完整重启加载。
- Preparing composite报告最初Running-only限制已有后续d9b0e5603升级，旧报告是历史边界，不可重新当当前阻断；同理S14旧planning-only段落已被最新授权取代。
- 未运行任何.NET、未改生产/其他task、没有新增UI/投掷/评分失败规则。
