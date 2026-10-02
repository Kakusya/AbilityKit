# Research: Preparing 布局安装与复合恢复

- Query: integration 585e15982 后 Preparing 恢复 WIP 的最小安装/恢复设计。
- Scope: internal；只读 `C:/Users/Administrator/orca/workspaces/AbilityKit/cooking-integration-s06-s14` 当前源码，不跑.NET或改production。
- Date: 2026-10-02

## Findings

### 当前缺口与准确证据

- `CookingPreparedGeometry.cs:7-11` CanInstall 用 IsGameplayMutationOpen=true 拒绝布局；Preparing已允许备料，因此这个旧proxy把合法Preparing也挡住。:44 的 IsAuthorityMutationOpen也不能替代准备阶段专用许可。
- `CookingPreparedGeometry.cs:17-38` 对live旧位姿LastMovementTick、旧item位置、process/station引用检查，适合动态编辑但不适合restore的新Recipe内容；restore不能拿尚未安装的旧references证明新payload合法。
- `CookingLayoutGeometry.cs:102-105` 已把Geometry.InitialPoses.LastMovementTick正规化-1，同时ProjectedPoses保留当前tick。这两个数据的用途必须分开：initial seed进入持久化布局身份，live pose保持本代际移动水位。
- `CookingLevelEtHost.cs:336-346` `_frontConfiguration` 与identity readonly；:440-492 front绑定/route/endpoints由它驱动；布局安装需要更新有效flow却不能把trusted原配置换成checkpoint自给配置。
- `CookingLevelEtHost.cs:995` 目前InstalledLayout非null直接拒绝；:1100直接Recipe.Restore，因此没有先安装布局再校验新Recipe位姿与锚点。
- `CookingRecipeCheckpoint.cs:385` AcceptSuccessHandoff仍从 `_fixture.Spatial.InitialPoses`取出生位，动态有效布局与immutable fixture不一致时错误。

### 最小API与所有权（设计建议，未实现）

不新增authority；保留host生命周期、Simulation厨房和immutable fixture。domain保留 `_installedSpatial` / `EffectiveSpatial` 为有效几何，原fixture配置身份保持内容授权基线；动态布局作为host拥有的已验证附加状态进入Level checkpoint和canonical。

1. host公开 `TryInstallPreparedLayout(layout)`，只在Created/Preparing且无正在执行批次允许；供trusted factory的初始布局与owner准备态编辑复用同入口。
2. domain internal `ValidatePreparedGeometry(projection, referenceState)`：纯只读校验几何、players、anchors、appliances、supply与references，返回冻结候选；referenceState可取live导出checkpoint或restore的候选Recipe，不能混用。动态live模式保留LastMovementTick；restore模式使用payload.Poses验证而不是同旧pose对比。
3. domain internal `InstallValidatedPreparedGeometry(candidate)` 仅由host专用准备阶段授权调用，不能通过公开bool bypass lifecycle。host明示状态门替代旧GameplayOpen proxy。全批次先验证/计算好字典与checked版本，再安装；失败不变更。
4. host分离 `_trustedFrontConfiguration`（readonly授权基线）和 `_effectiveFrontConfiguration`（已验证派生flow/endpoints）；binding所有引用读有效配置，trusted identity永不由payload覆盖。只有flow/anchor几何可派生，schedule/menu/manual/delivery语义不能由布局selfgrant。

### 动态安装原子序列

先确认host Created/Preparing和命令队列安全点 → 用trusted unlocked∩levelAllowed验证layout设备及区域 → 从有效空间策略(radius/speed等)和当前pose Project → 用live kitchen references校验 → 从layout验证路径并产生新的effective front configuration → 在孤立候选front上ConfigureFlow/绑定既有trusted manual/delivery策略并验证空营业状态 → 计算所有版本/冻结candidate → 同一host同步边界swap geometry、poses、front config、front owner与binding、installed-layout记录。

Preparing必须尚未营业，不能丢客户/工作：若任何front business state非pristine则拒绝，而非靠reset掩盖。手工厨房process可延续elapsed但释放ActiveWorker；物件identity/contents/portion/allocator不变。所有可能失败的操作在swap之前，包括front路径/端点和delegate binding准备；swap阶段不再次运行任意外部factory/验证代码。使用暂存候选而非安装后回滚以避免局部恢复。

### 恢复原子序列与防selfgrant

先外层scope/config identity/格式检查 → trusted factory重新提供preparation policy、允许设备、基础front配置、空间policy → InstalledLayout按这些trusted许可重验并重算geometry及front flow → geometry.InitialPoses作为saved seed来源（全LastMovementTick=-1），payload.Poses仅作为live运行位姿，不许把其运行tick写进初始seed → 创建隔离host至Preparing/Ready初始化阶段 → **在Recipe.Restore前**安装经过验证的有效geometry/front候选 → Recipe.Restore验证payload的物件/工位/pose/worker/supply against新几何 → front恢复against派生有效front配置 →生命周期Running或Preparing、水位、dedup与service-start一致性全部通过才返回host；任一步失败Dispose隔离host。

恢复candidate references应从checkpoint.Recipe中读取；只要求它们在新几何上有合法anchor与设备许可，不能要求空初始化厨房所有旧seed位置都在新布局，因为payload最终会整体替换。反过来不能因为payload自己声称有anchor就接纳新anchor；anchor集合只能来自trusted-policy验证过的布局投影。供应Source/Receiving必须在允许的target路由中；checkpoint不可新增supplier/menu/能力/速度/radius。

InstalledLayout checkpoint应包含layout、经正规化的initial seed和trusted-policy/config identity；restore重新投影并比较派生geometry/flow canonical，不能把snapshot几何直接当可信来源。当前Recipe恢复配置身份检查要继续使用immutable授权基线；布局身份另外验证，避免动态布局被错认为修改菜单配置。

### 跨关与seed修正

AcceptSuccessHandoff出生位应读取 `EffectiveSpatial.InitialPoses`；若下关trusted新布局不同，先投影/安装下一关有效geometry，再以其InitialPoses替换handoff.Poses。不能用上一关live位置作为新出生seed，不能回退旧fixture位置。失败重开仍由当前关trusted初始布局与标准供应创建，不能继承失败现场的临时布局授权或items。

### 必须新增验收

Preparing可安装布局并继续备料；Running拒绝；geometry/front任一失败时完整checkpoint不变；恢复布局先于Recipe使新anchor物件与pose成功恢复；篡改允许设备/策略/flow/selfgrant拒绝；live LastMovementTick保留且seed=-1；恢复后的移动不被旧tick阻断；新布局恢复使用新references；成功下一关使用有效新出生位；失败重开标准seed；前厅menu/schedule/伙伴成长不因布局丢失。

## Caveats / Not Found

以上是最小设计建议与现源缺口，不表示安装或恢复已完成，不是实现代码或通过证据。没有加载角色隔离禁止的implement/check manifests，也没有运行.NET。配置readonly分离为trusted/effective是既有owner内部状态修正，不新增架构层或authority。
