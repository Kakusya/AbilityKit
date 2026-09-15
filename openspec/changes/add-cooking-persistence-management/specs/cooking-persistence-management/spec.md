## Purpose

为做菜经营游戏建立从一局权威结算到长期经营进度的可靠边界，确保奖励幂等、写入中断可诊断、重启可读回，并把存档归属与恢复策略留在明确的产品决策门内。

## ADDED Requirements

### Requirement: Match settlement is separated from long-term progress
阶段 6 的长期进度 MUST 与 Round/Match 临时状态分离；只有前置 Match 生命周期产生的已确认、可识别结算结果才能请求应用到长期进度。长期进度至少能区分解锁、升级、货币和经营进度的业务字段；本能力 MUST NOT 默认建立跨玩家共享经济或玩家间转账。

#### Scenario: Confirmed settlement produces one progress candidate
- **WHEN** 已完成且经权威确认的 Match 结算结果携带唯一结算身份、目标进度版本和奖励明细
- **THEN** 系统生成可审计的长期进度变更候选，且不把 Round/Match 临时实体、计时器或位置直接当作长期存档字段

#### Scenario: Unconfirmed or foreign settlement is rejected
- **WHEN** 结算结果缺少确认状态、结算身份无效、属于其他 session/player scope 或引用不兼容的进度版本
- **THEN** 系统拒绝应用长期变更，返回可诊断结果，长期状态与奖励事件保持不变

### Requirement: Settlement rewards are idempotent
同一长期进度 owner scope 内，一次结算身份 MUST 至多应用一次持久奖励变更；重复请求 MUST 返回原始或等价幂等结果。不同结算身份 MUST 可独立审计，不能仅以金额相同去重。若未明确提供事务 outbox 或等价可靠投递机制，通知/事件只表示可诊断的派发尝试或状态，不得暗示跨进程 exactly-once 投递。

#### Scenario: Repeated settlement request changes progress once
- **WHEN** 客户端或宿主因重试重复提交同一已确认结算身份
- **THEN** 长期进度只发生一次预期持久奖励变更，并返回稳定的幂等结果；通知即使重复派发也不得再次改变持久奖励，且无事务 outbox 时不宣称通知 exactly-once

#### Scenario: Distinct matches remain distinct
- **WHEN** 两个不同已确认 Match 使用相同奖励数值但具有不同结算身份
- **THEN** 系统分别处理两次结算，且审计/幂等记录不会错误合并

### Requirement: Settlement ledger and progress commit atomically
已应用结算 ID 去重账本与对应的长期进度变更 MUST 位于同一持久提交边界；系统 MUST NOT 允许账本先持久化而奖励未持久化，也 MUST NOT 允许奖励先持久化而账本缺失。提交结果与响应之间发生崩溃时，重启后以持久提交状态处理同一结算重试，最终持久奖励变更 MUST 恰好一次。通知/事件只有在存在明确事务 outbox 或等价可靠机制时才可声明与该提交绑定；否则 MUST 与持久奖励结果分开表述，不能承诺跨进程 exactly-once。

#### Scenario: Crash before commit retries exactly once
- **WHEN** 进程在账本与长期进度的持久提交完成前崩溃，随后重启并重试同一结算身份
- **THEN** 重启逻辑不得把不完整账本当作已应用，也不得留下账本先落盘但奖励缺失的状态；重试完成后长期奖励与已应用结算 ID 在同一提交中出现且持久奖励恰好应用一次

#### Scenario: Crash after commit before response retries exactly once
- **WHEN** 账本与长期进度已在同一持久提交中完成，但进程在向调用方响应前崩溃，随后重启并重试同一结算身份
- **THEN** 系统识别该结算已提交并返回稳定的已应用/幂等结果，长期奖励不再次增加，持久账本与进度保持一致且持久奖励恰好一次

#### Scenario: Notification is not overstated without an outbox
- **WHEN** 持久提交成功但没有事务 outbox 或等价可靠投递机制，通知派发在提交前后任一时点失败或重试
- **THEN** 持久奖励与账本仍保持恰好一次，通知结果仅报告尝试、成功或失败状态，不宣称跨进程 exactly-once，也不通过重复通知再次修改长期进度

### Requirement: Persistence writes are integrity-protected and interruption-safe
存档载荷 MUST 带有可验证的格式版本、进度版本、owner scope 标识、单调修订信息和完整性校验元数据；写入过程 MUST 不能让读者观察到半写入载荷。写入失败、取消、崩溃模拟或重复请求 MUST 返回结构化状态，不得静默宣称已保存。具体介质和原子替换机制留给 design/owner 决策。

#### Scenario: Interrupted write preserves last committed record
- **WHEN** 在新修订写入期间注入截断、取消或 I/O 故障，并随后读取存档
- **THEN** 读取结果为上一个完整且完整性校验通过的修订，或返回明确不可恢复错误；不得返回可解析但部分应用的新奖励

#### Scenario: Duplicate write request is safe
- **WHEN** 同一结算修订或保存请求重复到达
- **THEN** 系统不会重复应用奖励或产生多份逻辑提交，并返回该修订的稳定提交/重复结果

### Requirement: Restart read-back restores only validated long-term state
系统 MUST 支持在进程重启后读取长期进度，并在安装前验证 owner scope、格式/版本、完整性、修订一致性及与当前配置兼容性。无效记录 MUST 不得覆盖当前有效内存状态；成功读回 MUST 不包含上一局临时 Match 状态的隐式恢复。

#### Scenario: Restart restores committed progress
- **WHEN** 进程以同一有效 owner scope 重启，并读取最近一次完整提交的兼容存档
- **THEN** 解锁、升级、货币和经营进度与提交时一致，且不会重复触发结算奖励

#### Scenario: Corrupt or incompatible record is quarantined or rejected
- **WHEN** 存档被篡改、损坏、截断、版本未知或配置不兼容
- **THEN** 系统拒绝安装并产出可诊断错误；是否使用备份、迁移、隔离或人工恢复 MUST 由 owner 决策，不得静默降级为新档或覆盖有效记录

### Requirement: Ownership and lifecycle policy are explicit gates
存档 owner、可保存范围、保存时机、退出/断电/取消语义、迁移/备份/损坏恢复和 host 退出行为 MUST 在进入集成验收前由 owner 明确；未决项 MUST 标记为 `Draft/Blocked`，不得由默认 host 模式、Dispose、socket close 或某个实现时机推导产品行为。

#### Scenario: Unresolved ownership blocks persistence acceptance
- **WHEN** owner 尚未确认存档归属、退出保存规则或损坏恢复策略
- **THEN** 相关实现与集成验收保持 Blocked，但幂等、完整性和无半写入等技术契约仍可单独测试

#### Scenario: Approved lifecycle policy is observable
- **WHEN** owner 已批准一套保存/退出/恢复策略，并执行对应生命周期操作
- **THEN** 系统按该策略报告保存、取消、失败、恢复和退出结果；测试证据明确引用批准决策，而不是以规划文件存在替代决策
