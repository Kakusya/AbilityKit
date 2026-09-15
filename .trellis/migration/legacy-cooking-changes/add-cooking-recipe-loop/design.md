## Context

见 [proposal.md](proposal.md) 与 [spec](specs/cooking-recipe-loop/spec.md)。阶段 1 规划确立了显式位置、唯一所有权、稳定排序、幂等和原子命令边界；路线图确认配方应由数据驱动处理规则表达，状态同步由业务层导出。现有配置系统提供通用加载/构表/提交能力，但不拥有 cooking 表目录或跨表业务规则；当前没有 cooking 实现、durable specs 或可直接复用的游戏测试宿主。

## Goals / Non-Goals

**Goals:**

- 定义可由最小 fixture 驱动的食材→工序→产物→装盘→订单提交数据流。
- 复用阶段 1 命令入口和身份/位置/容量不变量，令每个跨实体步骤具有全量校验、单次提交和可审计事件。
- 将逻辑时钟、输入消耗、产物生成、装盘及提交结果纳入可序列化业务快照 seam。
- 建立纯 C#、同机双消费者和（仅在阶段 2 证据满足后）LAN 联机验收矩阵；所有测试宿主路径标为 future。

**Non-Goals:**

- 不选定正式菜谱、订单 owner、评分、结算或失败产品 UX；这些保留为 Draft / Blocked owner decision。
- 不开发通用 recipe editor，不大量扩展内容，不实现地图/Match 生命周期、持久化经营、重连、主机迁移或网络测量。
- 不把阶段 2 transport/D4 决策隐式改为领域代码依赖，也不以同机测试替代真实 LAN 出口。

## Decisions

### 1. 用数据驱动的最小链路而非菜品专用状态机

Recipe 定义输入、Process 拓扑、Appliance 能力、容器/产物约束；运行时只保存引用 DefinitionId 与 Item/Container InstanceId、阶段索引、逻辑时间和提交状态。这样增加第二个 fixture 菜谱主要改变数据，不复制一套规则。替代方案是每道菜一个状态机，难以扩展且会把未决内容固化。

### 2. 每个领域动作采用 prepare/commit 事务边界

取料、开始工序、推进完成、装盘、提交订单分别先形成验证结果，再由模拟线程各自一次性提交相关状态和事件。装盘命令成功后，其菜品状态独立保留并可被拿取或交单；提交命令只消费已装盘提交物并更新订单。订单提交失败仅拒绝本次提交，不回滚先前装盘。替代方案是先移动物品再补记订单，会制造半提交。

### 3. 订单接口只固定可观察边界，不决定 owner

定义可注入的订单接受端口和提交结果摘要；订单规则、评分、计时与结算的 owner 在实施前必须确认。fixture 可用测试 double 验证提交原子性，但不得把 double 当作正式订单系统。替代方案是直接在 recipe handler 内计算得分，越过产品 owner。

### 4. 领域与阶段出口分层

纯 C# 阶段 3/P2 验收依赖阶段 1 的实际证据；联机一致性验收额外依赖阶段 2/P1 的稳定 session、共享 queue、snapshot seam 以及 LAN 集成出口。阶段顺序上，P1 的 D1 transport、D2 入口、D3 disconnect/exit、D4 benchmark 决策仍是进入后续阶段出口的门，不被“领域可先做”删除。矩阵如下：

| 工作项 | 纯 C# 前置 | 联机前置 | 阶段出口 |
|---|---|---|---|
| Recipe/process/装盘/交单规则 | 阶段 1 证据 | 阶段 2 稳定 session | 阶段 3/P2 契约测试与批准内容边界 |
| 阶段 3 LAN 一致性 | 同上 | 阶段 2 LAN 集成证据 | P1 D1-D4 与 LAN 门仍满足 |
| 阶段 4 配置校验 | 阶段 3 实际数据类型 | 若校验 host/client hash 则阶段 2 handshake seam | P3 启动阻断/hash 证据 |
| 阶段 5 Match 生命周期 | 阶段 1 identity/lifecycle | 阶段 2 session；阶段 4 config/layout 校验 | P4 准备/开始/结束/重开与 snapshot 证据 |
| 后续阶段 6/P5、7/P6 | 分别依赖 P4、P1-P5 可运行链路 | 见各自 change（尚不存在） | 不在本 change 假定文件存在 |

### 5. 测试矩阵与证据计划

| ID | 场景 | Runner/证据（future） | 通过标准 |
|---|---|---|---|
| R01 | 最小 fixture 取料、加工、独立装盘、提交订单 | 未来 .NET cooking contract tests；命令日志、各步骤快照 | 取料/加工/装盘/提交均成功；装盘后独立状态可断言，提交一次 |
| R02 | 工序未完成、厨具不符、输入缺失 | 同上；拒绝原因与 before/after 快照 | 无部分 mutation，无错误事件 |
| R03 | 装盘成功后订单不可接受 | 同上；装盘快照、提交结果与事件 trace | 装盘成功状态保留；仅本次提交无变更，不回滚容器/菜品 |
| R04 | 同 identity 重复装盘/交单，不同 identity 重交已消费菜品 | 同上；dedup counter、event trace | 同 identity 返回已处理结果无二次变更；不同 identity 拒绝已消费物 |
| R05 | 增加第二 recipe/appliance 仅修改 fixture 数据 | 未来 .NET fixture parameterized test；两组数据与同一 runner 输出 | 不改规则代码即可完成第二条闭环，能力校验来自数据 |
| R06 | host-local 与 remote in-process 相同闭环 | 未来 integration runner；handler path evidence | 两来源状态/事件等价 |
| R07 | 两实例 LAN 闭环 | 未来 two-PC LAN runner；双方日志、snapshot/message trace | 仅在阶段 2 LAN 门满足后通过；同机不替代 |

## Risks / Trade-offs

- [Risk] 未决正式配方/订单语义被 fixture 偷渡 → [Mitigation] R01-R05 只验结构；proposal/spec 保留 Draft / Blocked 和 owner gate。
- [Risk] 装盘成功后订单提交失败导致已装盘状态被错误回滚 → [Mitigation] R03 强制独立命令、装盘后快照保留和提交失败前后状态比较；成功提交才消费提交物并更新订单。
- [Risk] 为联机方便而把 transport 固化进领域 → [Mitigation] 阶段 2 adapter/session 作为可选集成边界，P1 D1-D4 仍由其 owner 文档负责。
- [Risk] 第二菜谱实际需要新规则 → [Mitigation] R05 是阶段核心验收；若不能仅改数据，阻止 P2 收口并记录差异。

## Migration Plan

无既有 cooking 运行时数据迁移。实施顺序为：阶段 1 证据复核→纯 C# 数据/规则与 R01-R05→阶段 1 adapters 与 R06→阶段 2 seam 满足后 R07。失败时删除新增应用层代码/fixture，不修改通用 AbilityKit、旧 changes、路线或主 specs；不在本规划阶段执行测试。

## Open Questions

跨阶段 owner 决策统一链接 [ADR/long-term-goals.md](../../../ADR/long-term-goals.md)、[delivery-plan.md](../../../Docs/design/CookingGame/delivery-plan.md) 及阶段 2 change 的 D1-D4，不在此重复定义。正式菜谱、订单 owner、评分/结算和失败产品语义未确认前，本 change 保持 Draft / Blocked。
