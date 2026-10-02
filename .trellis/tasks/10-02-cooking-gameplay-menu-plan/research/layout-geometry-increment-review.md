# S08 最小几何投影增量

2026-10-02，integration HEAD 901f465b5，保留 S06 并行改动。根据主任务 S08 Reviewed installation implementation contract，仅新增 CookingLayoutGeometry.cs 与 CookingLayoutGeometryTests.cs；没有编辑已有 RecipeLoop/SpatialInteraction/lifecycle/host/checkpoint，未提交。

## 已实现的候选接口

`CookingLayoutGeometry.Project(candidate, unlocked, levelAllowed, trustedFootprints, trustedPolicy, currentPoses)` 返回 `CookingLayoutGeometryResult`：Validation、FrozenLayout、Geometry、ProjectedPoses。它只计算候选，不安装、不动 allocator。必须 trusted footprint 及许可交集；CellSize 固定1000，候选 ActorRadius 必须等于 trustedPolicy；InteractionRadius/MovementSpeed 同样来自 trustedPolicy。

启用 floor union 仅枚举合法 floor cell（总量沿 validator 上限65536）；bounding box 内 floor 补集按启用行排序、每行缺口interval与整段缺失行 slab生成，绝不遍历远端 bounding box。墙为cell closed rectangle，设备为实际旋转后的 rectangular footprint。StationSlot anchors 为经validator验证的设备外部 interaction cell 中心；所有 world target anchors 为目标 cell 中心。补集真正阻止穿洞与远端未启用区域，不只是寻路提示。

保留几何合法且不重叠的 current poses；所有需要移动者按稳定 PlayerId 排序，仅在入口连通可行 cell 中心找位置，检查 S01实际 closed obstacle/radius 与 mutual collision。先保留所有合法 pose，再放置非法者，不让顺序靠前的非法者抢占合法位置。无可用位置整体返回失败，geometry/layout均为空。

输出 Geometry.InitialPoses.LastMovementTick=-1 满足现有 S01 factory Validate；ProjectedPoses 为冻结的实际 runtime位置与原移动水位，installer应安装 runtime字典而不能用InitialPoses替代它重置同tick限速。此区别已由实际fixtureValidate和restore→Move拒绝控制验证。

## 实际验证

经root与supply_component协调，独占integration .NET窗口，仅执行：

`dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj --filter FullyQualifiedName~CookingLayoutGeometryTests --nologo --verbosity quiet --logger trx`

首次：11通过、1失败、0skip；两处测试nullable warning。失败为测试比较完整checkpoint忽略了合法拒绝回执去重，业务状态未变；改为Snapshot业务状态断言并修nullable用法，没有弱化生产验证。保留 `local/Logs/cooking-execution/layout-geometry-first-failure.log`、同名trx。

最终：12/12通过、0skip；增量编译输出无warning/error。证据：`local/Logs/cooking-execution/layout-geometry-final.log` 与 `.trx`。覆盖实际S01 Move穿floor hole拒绝、非方设备四旋转的实际anchor/obstacle、negative+remote sparse bounds及有界障碍数、trusted半径/尺度/footprint/definition/permission拒绝、保留合法pose/稳定重定位/无空间全拒绝、来源集合冻结、外边界接触与闭障碍相切、一套可factoryValidate配置+runtime水位控制。窗口已释放给S06 fullgate；该完整门禁结果由其owner另报，本文不预称通过。

## 仍待真实S08安装

主owner需在S06稳定后接 preparing唯一候选厨房、全部live anchor/slot/item/process/front-house引用预检、station/index迁移、manual worker清除、阶段权限及原子厨房/前厅/preparation commit、几何hash与required full layout checkpoint/schema、同Level恢复及success/retry策略。ET真实布局操作改变后续Move/interaction尚未在本增量实现；helper通过不等于完整S08，也不等于Unity。

## Root复审后生产纠正（最新源码证据）

root发现新增未挂target的孤岛floor可让原先几何合法pose仍被保留，无法返回入口/工位。已修：入口连通cell集合一次计算复用；保留条件增加旧pose按floor-div（含负坐标）所在cell必须连通。S01真实geometry.ValidPose仍检查半径、closed obstacle和外边界，合法但孤立者走同一稳定重定位。

新增正/负坐标孤岛各一控制，明确断言旧pose单看S01 ValidPose=true，但投影必须迁回主区并保留runtime watermark。修后focused实际14/14、0skip，编译输出无warning/error，证据 `local/Logs/cooking-execution/layout-geometry-connected.log` 与 `.trx`。当前原有12/12、S06 owner组合477/175均为该纠正之前的证据；修后全组合门禁由root另行运行。本次已释放.NET、两新源文件稳定，仍未实现完整layout安装。
