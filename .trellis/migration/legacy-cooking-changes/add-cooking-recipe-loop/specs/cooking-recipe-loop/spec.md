## Purpose

为做菜经营游戏提供一条可由纯 C# 权威模拟验证的最小数据驱动闭环，覆盖取料、加工、装盘与提交订单，同时把未决的正式内容和 owner 决策明确隔离。

## ADDED Requirements

### Requirement: 取料与加工必须复用权威物品交互边界
系统 SHALL 允许玩家从合法来源取得配方所需食材，并仅在身份、当前位置、资格、范围、容量和生命周期检查全部通过时创建加工动作；失败 MUST 保持权威状态不变。

#### Scenario: 合法取料并开始加工
- **WHEN** 玩家在有效 Match 中取得满足配方输入的食材，并向具备所需能力的厨具提交加工命令
- **THEN** 系统 SHALL 原子地锁定输入、创建加工进度，并记录可观察的 recipe/process identity；不得产生重复位置或所有权

#### Scenario: 取料或厨具资格失败
- **WHEN** 食材缺失、玩家无资格、距离不可达、厨具能力不匹配或输入已被占用
- **THEN** 系统 SHALL 返回稳定失败原因，且加工进度、输入位置、厨具占用和事件序列均保持不变

### Requirement: 加工进度与产物必须由模拟逻辑驱动
系统 SHALL 使用明确的模拟 Tick 或逻辑时钟推进已接受的工序，并在完成条件满足时一次性消耗声明的输入、创建声明的产物；不得依赖 Unity 动画回调或墙钟作为完成依据。

#### Scenario: 工序按逻辑时间完成
- **WHEN** 已开始工序经过不足完成阈值的 Tick，再经过达到阈值的 Tick
- **THEN** 系统 SHALL 在前者保持进行中，在后者只提交一次完成结果，并使产物身份、数量与配方定义一致

#### Scenario: 重复完成命令
- **WHEN** 相同 command identity 或同一工序完成请求被重复提交
- **THEN** 系统 SHALL 返回幂等结果，输入只消耗一次、产物只创建一次、事件只发布一次

### Requirement: 装盘与提交订单是独立的权威原子步骤
系统 SHALL 将完成产物装入合法容器/槽位作为独立的权威原子命令，并将已装盘且仍可交单的菜品提交给订单 owner 作为另一独立的权威原子命令。装盘成功后状态 MUST 保留并可被拿取或后续提交；订单校验失败只拒绝本次提交，不得回滚已装盘菜品或产生其他部分变更。

#### Scenario: 合法装盘并保留
- **WHEN** 完成产物、容器容量、装盘资格和当前位置均有效
- **THEN** 系统 SHALL 原子地完成装盘并发布一次装盘结果；菜品保留在容器/槽位中，可被拿取或作为后续订单提交物，订单状态不因装盘命令改变

#### Scenario: 装盘失败
- **WHEN** 容器已满、产物不适配、当前位置不符或装盘范围无效
- **THEN** 系统 SHALL 拒绝本次装盘，产物、容器、订单进度和事件保持不变

#### Scenario: 订单校验失败但装盘已成功
- **WHEN** 已装盘菜品提交时订单不可接受、菜品不匹配或提交范围无效
- **THEN** 系统 SHALL 只拒绝本次订单提交，已装盘菜品和容器状态保持不变，不得回滚装盘

#### Scenario: 合法订单提交
- **WHEN** 已装盘菜品、订单所需 recipe identity、提交资格和订单状态均有效
- **THEN** 系统 SHALL 原子地消费该提交物并更新订单，产生一次可关联的提交结果

#### Scenario: 提交命令重复与跨身份重放
- **WHEN** 相同 command identity 重复提交，或不同 command identity 再次提交已消费的菜品
- **THEN** 系统 SHALL 对相同 command identity 返回已处理结果且不产生二次变更；对不同 command identity 拒绝已消费菜品，且不得重复更新订单

### Requirement: 正式配方与订单语义必须经过决策门
系统 SHALL 将正式配方内容、订单 owner、评分/结算、计时边界和失败处理标记为 Draft / Blocked，未获 owner 确认前不得把路线图示例升级为产品承诺；测试 fixture MUST 明确仅用于验收。

#### Scenario: 决策未确认时创建闭环 fixture
- **WHEN** 实施者使用最小测试配方验证领域流程，但正式配方或订单 owner 尚未确认
- **THEN** 系统 SHALL 允许以 fixture 验证结构和原子性，但交付状态保持 Draft / Blocked，不得宣称正式内容或结算已接受

### Requirement: 联机验收必须依赖稳定阶段 2 会话
阶段 3 的纯 C# 领域闭环 SHALL 可在阶段 1 identity/location/lifecycle 证据后推进；若验收声称 host/client 联机一致性，则 MUST 依赖阶段 2 稳定 session、共享 command path、snapshot seam 及 LAN 集成证据。阶段出口仍 MUST 遵守阶段 2 D1-D4 决策门。

#### Scenario: 纯 C# 闭环验收
- **WHEN** 阶段 1 证据齐全且执行最小 fixture 的取料、加工、装盘流程
- **THEN** 系统 SHALL 能验证最终状态、产物与失败原子性，而不要求真实 transport

#### Scenario: 联机闭环验收前置缺失
- **WHEN** 阶段 2 session 或 LAN 集成证据、transport/D4 决策门尚未满足
- **THEN** 系统 MUST 将联机验收标记为 blocked，不得以同机或 in-process 结果替代真实 LAN 出口
