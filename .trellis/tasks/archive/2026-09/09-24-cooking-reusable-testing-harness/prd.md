# Cooking 局内与网络纵切可复用测试框架 PRD

> 状态：`planning`。本任务用于落地统一、解耦、多拓扑支持的 Cooking 测试体系 Harness，使得相同业务场景（如番茄蛋花汤、工位争抢、断线重连）能够在单机内存、Host/Client 虚拟网络对、真实 LiteNetLib 传输拓扑下复用运行与一致性校验。

## 1. 目标与背景

根据 `09-19-cooking-productization-network-slice-planning` 确认的架构决策：
1. Cooking 纯 C# 领域规则与模拟在单机与局域网网络运行线中**同构且共享**；
2. 通用网络传输栈（`AbilityKit.Network.Transport.*`）为基础设施，真实传输唯一采用 `LiteNetLib` reliable-UDP，本地测试采用 `InMemoryTransport`；
3. Host 拥有唯一玩法权威，Client 仅通过命令 Ingress 派发与状态投影（Projection）；
4. 需要一套标准、可复用的测试 Harness，替代此前特化孤立的测试结构，支撑多拓扑、多场景的端到端自动化验证。

## 2. 核心范围

- **抽象拓扑接口 (`ICookingTestTopology`)**：
  - `DirectMemoryTopology`：纯本地 Simulation，直接派发并同步执行，零传输开销；
  - `InProcessPairTopology`：Host 与 Client 分立，通过 `InMemoryTransport` 消息管道进行命令与快照同步；
  - `LoopbackUdpTopology`：Host 与 Client 通过真实回环端口（127.0.0.1）与 `LiteNetTransport` 进行传输。
- **角色门面与统一操作 DSL (`ICookingActor`, `CookingTestDriver`)**：
  - 封装统一的拾取、放置、入容器、启动加工、装盘倒出、提交订单等标准权威动作；
  - 对测试代码屏蔽底层是本地同步执行还是网络命令封包。
- **确定性与一致性断言扩展 (`CookingAssertionExtensions`)**：
  - 验证双端快照 SHA-256 哈希共识；
  - 验证快照序列号连续性、命令相关性与幂等去重。
- **代表性可复用场景用例落地**：
  - 提取番茄蛋花汤端到端闭环场景为参数化可复用 Scenario，支持挂载到不同拓扑执行。

## 3. 非目标与约束

- 不修改 Cooking 既有的核心业务规则逻辑（保持纯 C# 领域稳定）；
- 不引入任何 Unity Engine 运行时或编辑器依赖；
- 不扩充非 Cooking 示例功能（Shooter、MOBA 等维持现状）；
- 测试框架作为纯测试支持工具库/模块，纳入日常回归门禁。
