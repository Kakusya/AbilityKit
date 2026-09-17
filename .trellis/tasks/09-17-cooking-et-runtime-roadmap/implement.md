# Implementation checklist

## 批准与当前状态

用户已批准四阶段计划并要求继续施工；task 为 in_progress。原先“只规划、不 start”的说明已过期。八项业务决定与未决语义见 prd.md；下列计划不代表已完成能力。

## 已完成

- [x] 保存业务地图、八项决定、未决问题、设计、依赖研究、Proposed ADR 和 context manifests。
- [x] 既有 Cooking 基线 57/57。
- [x] Demo 宿主 ET 生命周期探针 1/1（不是独立 ET 内核验证）。
- [x] CookingSnapshot.Items → Scene/Registry/Item 投影探针 4/4：Pickup 位置与版本、移除/EntityRef 失效、重放稳定性、Scene 递归销毁。
- [x] 修正新探针缺少项目引用、虚构 fixture/命令签名、AddChild 与 EntityRef API 不匹配。

## 短期施工顺序与出口

1. 独立内核提炼：完整核对源码、生成器、许可证与编译闭包；建立不依赖 DemoEntry/反射 Fiber 的可测试启动与释放入口。验证真正的 System 注册/执行、单 Tick、清理、实例代际、跨阶段存活与多实例隔离。当前 Share/App 引用不是最终包边界，不能直接宣布完成阶段二。
2. Cooking 纵切：保留唯一权威状态与既有主机/远端统一输入合同。实现前细化成功延续、失败供应、扩建冲突和存档 checkpoint；尚未确认的业务不自行选默认值。完整快照需覆盖处理进度、订单、阶段、引用与 epoch/命令水位；当前物品投影不具备恢复能力。
3. 回归：修改 UDP 时运行 cooking-udp；核心/同步改动运行 core-stability；宿主装配改动运行 runtime-contracts；大范围迁移运行 regression。两物理 PC LAN 单独人工验收。
4. 清退：验证完成、依赖清零、测试迁移后分批提交删除范围。Moba/Shooter/Samples 后续单独处置，不作为当前探针 blocker；不删测试换通过，不在本轮删除 ECS。

## 当前探针边界

- 仅接收同一 scope、上游已验证、按应用顺序到达的可信完整物品快照；不是直接消费网络包的客户端投影。
- ET Id 自动生成，领域 ItemId 单独保存；Location 是关系数据，不通过树移动物品来模拟持有。
- CookingSimulation 仍为权威 owner，没有新增 Tick、协议、加工、存档或 Unity 实现。
- 依赖完整 Demo 初始化，尚未完成独立提炼；已知旧快照、跨 scope、非法输入、对象池复用、重建继续运行等未验证，不用于生产装配。

## 实际验证

命令、TRX 指针、通过/失败/跳过以及依赖告警统一见 research/validation.md。之前 regression 在 HFSM DefinitionJsonTests 的 2/47 失败保持失败，不以本轮聚焦测试替代。

## Rollback and scope

不自动提交、清理用户工作区、改防火墙或解除 Cooking Unity 禁止。构建触碰的既有 DLL 单独记录，避免与业务源码改动混淆。本轮收口为 Demo 依赖下的集成探针；完整四阶段路线保留为后续未完成工作。已调用 finish-work，但任务源码尚未提交，按该流程不能归档或运行自动提交。未获得 Git 提交授权，归档受阻，不伪造 completed/archived 状态。
