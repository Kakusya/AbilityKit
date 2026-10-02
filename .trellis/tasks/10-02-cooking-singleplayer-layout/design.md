# S08 初始设计

状态：执行设计收敛，等待 S01/S06/S07 接口；不是已交付。

准备态移动/旋转/区域扩建；占位/交互面/顾客与玩家通路验证后提交；营业中拒绝修改。

沿用 [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md) 的 owner 与数据边界，不创建平行模拟，不修改通用框架的产品职责。具体 API/数据形状、错误矩阵、版本迁移、恢复/跨关状态与测试断言在执行前补齐；本初稿不声称已达到实施就绪。

## Scope / Trigger

在既有准备态与逻辑layout内表达房屋可用区域、设备footprint/旋转/交互面、入口/排队/桌位/出餐/供应/回收锚点。连续玩家移动复用S01，路径检查的离散格子只用于布局验证，不强制玩家棋盘步进。只在现有 Created/Preparing 阶段允许，不能新造独立关间准备产品对象。

## Signatures / Contracts

`CookingRestaurantLayout`：稳定layout identity、已启用floor区域、设备instance/definition、基准格与0/90/180/270朝向、墙障碍、顾客/玩家入口与必须可达交互锚点。`TryApplyLayout(candidate)`先全量验证占位、引用、权限和路径，再原子安装同一模拟空间/工位映射，不先搬真实物件后再发现非法布局。扩建只增加声明区域，不绑定未经批准价格和关号。

工作/顾客路由是同一有效地形的各自目标集合；BFS邻接序明确，角色实际pose/半径通路约束与S01一致。设备移动保留其物件和合法加工状态；manual worker清，station/id迁移保持snapshot/checkpoint索引一致。解锁可用内容与本关允许列表取交集，解锁不保证每关都可摆。

## Validation & Error Matrix

非准备态→InvalidState；非正面积/非法rotation/越界→InvalidLayout；definition未知/未解锁/本关不允许→DefinitionUnavailable；footprint重叠→OccupancyConflict；玩家不可达工位或顾客不可达桌/出口→UnreachableTarget；已有物件无可映射目标→拒绝，不删除或落到任意位置。所有失败保留旧layout/pose/物件/进度/hash。

## Good / Base / Bad

Good：准备态旋转设备、扩一块合法区域，保留备料且改善通路，之后原厨房继续营业。Base：同layout重复应用无重复搬物。Bad：设备堵住唯一出口/桌边，必须拒绝整次修改，不能只给提示但保留无路布局。

## Tests Required

非方设备四方向footprint与交互面、狭道/玩家半径、厨房分隔交接、入口队列桌到出口、进货到回收动线。多设备批量调换位置、带容器/剩余份的工位迁移、权限交集、营业拒绝、非法布局零变更、同Level恢复及成功承接。ET host准备操作改变真实后续Move/交互结果，不能仅验证独立编辑器数据。

## Wrong vs Correct

Wrong：layout编辑只更改一张grid，实际模拟继续用旧ReachableStations；顾客路径另用忽略设备的地图。Correct：验证后安装一份权威逻辑空间，各用途查询同一障碍与锚点，后续命令重新校验。
