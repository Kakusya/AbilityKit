# Research: S08 准备态实际安装最小合同

- Query: 沿当前factory/lifecycle/host/厨房所有权接入布局安装及首次准备，不破坏S06服务、S07供应与恢复。
- Scope: internal；integration当前源码只读，未跑.NET。
- Date: 2026-10-02

## Findings

### 精确当前来源与限制

- `src/AbilityKit.Game.Cooking/CookingLevelLifecycle.cs:158` factory仅Create(scope,configuration)；`:164` 可选front factory可信配置。`:450` BeginPreparation只复制preparation；`:462` CompletePreparation校验后Ready；`:480` Start首次才Create，successor已有厨房则复用。首次Preparing没有厨房，故现有准备安装只能用于successor，不能声称首关也可编辑。
- `CookingLevelEtHost.cs` Prepare一次调用BeginPreparation/CompletePreparation；Start再获取模拟、ownership、Driver绑定。准备过程没有独立创建owner；后续须拆显式BeginPrepare/CompletePrepare，同时保留旧Prepare convenience。
- `CookingRecipeLoop.cs` readonly `_fixture`；`CookingSpatialInteraction.cs` 所有空间读取 `_fixture.Spatial`，`_poses`另存运行态。布局仅更新外部grid不会改变实际命令；须增加当前有效空间字段/只读属性，所有距离/碰撞/可达/供应anchor查询一致改用它。不要修改factory原始fixture实例共享配置。
- `CookingSpatialConfiguration`字段 Min/MaxXY、PlayerRadius、InteractionRadius、InitialPoses、Anchors、Obstacles、MovementSpeed；布局坐标换算使用显式CellSize，中心=cell*CellSize+CellSize/2，long预检后转int。所有floor并集空洞都要变不可通行区域，不能只扩大外包矩形漏掉未启用地板。
- `CookingLevelEtHost.cs:729` ChooseDecoration先修改progress，失败只在previous非空时回滚；`:747` Unlock先PlaceUnlock再progress.Unlock，Locked拒绝会泄漏实际厨房。这些是本次S08需要实际修复的已确认原子性缺口。
- 当前S07候选已扩operations20–22和fixture Supply，未来Recipe5/Level7由协调者归并；S06/F08当前host绑定manual/交付predicate，必须从同一新的有效布局读取位置与trusted配置，不可自建前厅geometry副本。

### 最小factory与lifecycle合同

1. 在现有factory增加可选 `ICookingPreparationGameplayFactory`（或内部初始化适配器）提供可信 `CookingPreparationConfiguration`：初始布局、设备Definition→尺寸/交互面/capabilities元数据、默认授权定义、本关允许定义、CellSize、必要目标列表。不能将候选equipment自报Width/Height/capability当权威；factory签名沿既有Create，不新建simulation owner。
2. lifecycle新增准备厨房初始化/绑定内部方法，允许Created/Preparing、工厂只调用一次，沿原publicationguard、ownership与lifecyclegate；失败释放候选，不留下Driver/新tree。首次Start必须复用该同一引用，不能因不是 `_receivesSuccessorKitchen` 再Create。不要复用successor标志冒充首次准备来源，单独明确“本代际已初始化厨房”状态。
3. host BeginPrepare调用初始化并发布同一Kitchen/Driver，CompletePrepare验证有效布局及S14许可后Ready；旧Prepare按Begin+Complete适配。TryPeek只读观察Created/Preparing厨房；原gameplay admission仍Running才开放。
4. 准备编辑使用host管理operation门，不开放原running gameplay gate。若需求包含准备走位/备料/供货时钟，新增同一authority下显式PreparationTick/允许操作白名单，禁止在Preparing误开SubmitOrder/OpenOrder/前厅work。不另起timer，不靠直接simulation.Submit绕门。

### 准备时钟与前厅开始offset

准备是否允许持续物件制作尚须协调者明确；静态布局编辑本身无需推进clock。为不扩大产品范围，最小实现可以准备操作不推进fixedTick，offset=当前Kitchen.LogicalTick（通常0），Start时记录一次。若准备确有fixedTick备料，HostFrameSequence和Recipe.LogicalTick仍保持同一clock，供应按同一步推进而前厅不Step。

新增可信运行字段 `FrontServiceStartedAtTick`：首次Running开始赋值，same-Level恢复保留，不以恢复Start自动重写；successor重置新关值。恢复关系从旧 `min(Recipe.LogicalTick,ServiceTicks)` 改为 `min(Recipe.LogicalTick-FrontServiceStartedAtTick,ServiceTicks)`；要求offset>=0且<=logicaltick，与lifecycle/前厅配置一致。DTO/canonical/JsonRequired/信封版本/host恢复指纹一起归并；不能仅放checkpoint而frontconfiguration identity仍验旧初始geometry。

### 布局prepare/commit合同与checkpoint

`TryApplyLayout(candidate)`仅Created/Preparing，内部先BuildLayoutPlan：

- 从可信definitions取占地/交互面/能力，校验候选只改变实例位置/朝向；定义许可=本关允许∩(默认基础∪解锁)。重复stationID、目标ID及跨Kind同名歧义应拒绝，F08使用稳定唯一目标。
- 复用现有RestaurantLayoutValidator/GeometryBuilder做半径净空、所有玩家入口→必需工位、顾客入口→queue→table→exit。生成完整effective Spatial、工作/顾客路径、接收/仓储/wash/serving锚点。
- 验证所有现存WorldPosition/StationSlot物件仍有合法目标且单槽；容器内部引用无需搬出。StationID不变的位置移动优先保持引用，ID替换复用既有MigrateStations原子计划。进行中的自动工序保留，手工认领释放并保留进度，不能销毁batch/剩余份数。
- 玩家当前位置合法则保留，否则按玩家ID稳定顺序选择最近合法入口/位置，检查角色互撞、手持物随玩家；选择过程不会推进move水位/allocator。没有可容纳位置拒绝整体候选。
- S07 supplier配置的SourceAnchor/ReceivingAnchor都映射当前geometry且receive目标仍可放物，pending/arrived不能因移动工位丢失。S06路径和F08 ServingAnchor/CustomerTable保持同一geometry；需根据可信布局重新派生effective front config，保留初始factory可信policy与layout生成规则，不能信payload自报Flow。

所有校验通过后一次安装space/poses/station映射/effectivefront及layout identity，并重新绑定manual/delivery predicates到同一kitchen。异常的commit前无变化，commit后不再调用可失败的配置/geometry validator。不要先写progress再尝试厨房。

checkpoint至少保存有效布局、effectivegeometry identity、许可identity、frontserviceoffset；恢复先由外部可信definition/policy验证布局并派生空间/front，再恢复recipe和引用，原子安装host。信封中的geometryhash不是可信来源，不能据此允许缩小设备。Recipe5候选需区分原fixture身份与有效布局字段，不应把有效空间回写原fixture导致configuration identity随玩家摆放漂移。Level7由root统一记录新增完整字段，禁止独立抢版本。

成功下一关带有效厨房和布局；下一关允许集变窄时先阻断Ready要求合法新布局，不删除内容。失败重开沿既有成功基线/标准供应，再应用已确认major选择，不能保留失败临时布局/供应/认领。若装修已定义为major选择，应明确失败保留的是已确认选择而不是失败场景任意candidate。

### 已有两个原子性问题的最小修正

- ChooseDecoration：预检progress.Locked、替换合法性和厨房迁移计划；厨房可行后一次commit选择+迁移。若仍保留旧顺序必须对空数组也rollback，但后续不可异常的commit更可靠。测试previous空、冲突拒绝后progress和kitchen hash均不变。
- Unlock：先progress允许/definition已存在/本关许可和厨房placement预检，再一起commit；Locked提前拒绝。duplicate解锁不能重复放标准供应。测试locked+尚未放置definition、重复unlock、供给位置冲突与未知definition均零新增物件/allocator。

### 可独立分配的源码ownership

- 空间owner：仅新增LayoutPlan/可信footprint描述/GeometryBuilder及对应测试，给root返回不可变plan，不编辑正在S07/S06修改的RecipeLoop。
- lifecycle/host owner（root或一位串行implement）：CookingLevelLifecycle、CookingLevelEtHost、LevelCheckpoint、majorprogress原子修复、frontoffset和F08重绑定；不能两位worker同时各改host。
- 厨房owner：新partial空间安装文件、readonly有效空间接入；RecipeLoop/RecipeCheckpoint仅协调窗口内修改，与S07 Recipe5合并字段，避免替换供应状态。
- 集成验收owner：新专属ET测试文件可独立编写，等上述真实接口稳定后跑.NET，由root给窗口。

### 真实验收（计划）

首次BeginPrepare后厨房存在；编辑/生成供应成果，然后Complete/Start引用同一个simulation且factory Create计数1。合法move设备后原位置交互失败、新位置成功；四向非方footprint、地板空洞/窄道半径、目标歧义、恶意缩尺寸/伪capability/未授权设备全部拒绝且全态hash不变。带内容锅和自动进度搬移不丢份，手工释放可续。

准备若推进clock：推进若干fixedTick供应到货但frontServiceTicks=0，Start后1tick才前厅ServiceTicks=1；销毁恢复offset及所有clock等价。F08原出餐位置无效、新位置成功；前厅路径与同geometry障碍一致。S07 pending/arrived移动收货点保留、重复接收仍幂等。

same-Level保存动态布局后Dispose/Restore验证可信footprint、造假geometry/offset/禁止definition结构化拒绝；继续动作与不中断基线等价。Ready/Running/Paused/Ending/Ended不能安装布局。成功跨关保留成果、限制变化不静默删锅；失败恢复已确认major选择且不泄漏临时layout/采购。

## Caveats / Not Found

- 当前尚未找到首次Preparing创建并绑定厨房或effective空间安装API。本报告是具体建议，不是实现/验证结果；未跑.NET，未写生产。
- 协调者需裁决准备是否允许实际tick备料、失败临时布局与major确认选择的界线、下一关geometry允许范围、准备态自动process是否推进。这些影响offset/权限，不可通过代码顺手默认营业在Preparing开始。
- 首次准备行为修改影响现有fixture的factory调用时机、start重入/ownership失败注入测试；必须保留相同安全不变量，不能只更新期望计数放过双owner。
