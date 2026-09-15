## Why

阶段 2 只建立会话与同步边界，阶段 3 需要在其上形成可验证的一条取料、加工、装盘、交单领域闭环。当前没有已接受的正式配方、订单 owner 或结算语义，因此本 change 先建立可审查的契约草案与决策门，而不把路线图示例当作正式内容，也不把规划文件当作实现证据。

## What Changes

- 新增一个数据驱动的最小 recipe/process loop：从合法食材实例开始，经已配置工序与厨具处理，产出可放置的菜品/容器状态，并支持交单边界。
- 以原子命令复用阶段 1 的位置、所有权、容量和生命周期验证；取料、加工、装盘、交单是彼此独立的权威步骤，各自失败都不得部分提交，订单提交失败不得回滚已成功装盘的菜品。
- 定义处理进度、完成条件、输入消耗、产物身份和重复命令的可观察契约；计时使用模拟逻辑时钟，不依赖 Unity 动画或墙钟。
- 将订单/结算 owner、正式配方内容、评分与失败处理保留为 Draft / Blocked 决策门；测试 fixture 只能作为验收夹具。
- 保留阶段出口区别：纯 C# 闭环可在阶段 1 证据后推进；若宣称联机验收，必须依赖阶段 2 已稳定的 session/command/snapshot 路径及 LAN 集成证据，不删除阶段 2 的 transport/D4 决策门。
- 不创建通用配方编辑器，不实现长期经营、持久化、地图生命周期或网络测量；后续阶段 6/P5 由 [add-cooking-persistence-management](../add-cooking-persistence-management/proposal.md) 规划，阶段 7/P6 由 [add-cooking-network-measurement](../add-cooking-network-measurement/proposal.md) 规划，本 change 只建立依赖指针。

## Capabilities

### New Capabilities
- `cooking-recipe-loop`: 定义数据驱动的取料、加工、装盘与交单闭环，以及失败原子性、进度和重复提交边界。

### Modified Capabilities
- 无；现有阶段 1/2 仍为规划，未毕业为主规格，不在此复制或修改。

## Impact

- 未来影响应用层纯 C# cooking domain、command handler、process/recipe fixture、订单接口与状态快照；不得把规则加入通用 AbilityKit 或 Orleans 宿主。
- 预计复用阶段 1 的共享源码边界，并在实施时同时检查对应 SDK `.csproj` 与 Unity `.asmdef`；测试项目和程序集路径均标为 future/待实施确认。
- 依赖阶段 1 的可运行 identity/location/lifecycle seam。纯 C# 领域工作不以阶段 2 transport 选型为代码前置，但联机验收依赖阶段 2 稳定 session；阶段顺序出口仍受阶段 2 D1-D4 与 LAN 集成门约束。
- 后续 `add-cooking-config-validation`（阶段 4）依赖本 change 的实际数据类型；`add-cooking-match-lifecycle`（阶段 5）依赖阶段 1/2/4。本 change 不假定阶段 6 `add-cooking-persistence-management` 或阶段 7 `add-cooking-network-measurement` 的文件已存在。
