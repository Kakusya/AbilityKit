## Purpose

为做菜经营游戏建立可验证的局域网 listen session 边界：主机同时运行服务端与本地玩家，远端客户端通过受控身份与兼容握手加入，并以权威命令和带基线的状态同步共享同一局；本能力不把传输选择或未决产品退出语义伪装成既定事实。

## ADDED Requirements

### Requirement: Listen host and remote clients share one authoritative session
系统 MUST 支持 host 同时承担服务端与本地玩家职责，远端客户端加入同一 session；本地与远端的一次性命令 MUST 进入同一个权威队列、稳定排序、验证和原子提交路径。测试夹具可使用两实例，但 MUST NOT 将夹具规模解释为产品人数上限。

#### Scenario: Host local player and remote client commit through one path
- **WHEN** listen host 的本地玩家与一个已加入的远端客户端分别提交同一局内可接受的一次性交互命令
- **THEN** 两个命令经过同一权威队列和验证入口，最终状态/事件遵守既有交互契约；不得因来源是本地连接或远端连接而采用第二套规则

#### Scenario: True two-PC LAN is a separate acceptance exit
- **WHEN** 同机多实例测试通过但尚未执行两台物理电脑的 LAN listen host/client 测试
- **THEN** P1 不得宣称真实 LAN 验收完成；必须保留网卡、地址、防火墙和跨 PC 连接证据的独立出口

### Requirement: Identity is bound by the session authority
客户端 MUST NOT 通过自报 `PlayerId` 获得或改变身份。加入成功后由 session authority 绑定连接与 session-scoped player identity；命令中的身份必须与绑定一致，未绑定、跨 session 或越权身份 MUST 被拒绝且不得改变权威状态。

#### Scenario: Client-supplied foreign player identity is rejected
- **WHEN** 已绑定玩家的客户端提交携带另一玩家或另一 session 的 `PlayerId` 的命令
- **THEN** 命令被拒绝并产生可诊断的身份错误，权威队列状态、模拟状态和事件不发生部分变更

#### Scenario: Successful join receives an authority-bound identity
- **WHEN** 客户端通过兼容握手完成加入且 host 为连接建立 session-scoped binding
- **THEN** 后续命令使用该绑定身份进行授权；客户端不能用 payload 中自行填写的身份覆盖该绑定

### Requirement: Compatibility handshake precedes session state
加入 MUST 先完成协议/配置兼容性握手。握手至少比较协议身份与版本范围、配置 hash、会话策略/能力声明和必要的 session metadata；不兼容、未知且未显式允许的版本/策略或格式错误 MUST 在进入 gameplay 前拒绝。成功加入 MUST 先收到适用于当前 session 的完整 baseline，再允许应用 delta。

#### Scenario: Configuration or protocol mismatch blocks join
- **WHEN** 客户端提交的协议版本、配置 hash 或必需能力与 host 不兼容
- **THEN** host 拒绝加入并返回稳定、可诊断的兼容性错误，且不创建可提交 gameplay 命令的绑定

#### Scenario: Baseline precedes accepted delta
- **WHEN** 新加入客户端先收到 session baseline，随后收到属于同一 session epoch 的 delta
- **THEN** 客户端先安装 baseline，再按 sequence 应用 delta；baseline 之前到达的 delta 被缓存或拒绝，不能直接重建为权威状态

### Requirement: Session epoch and snapshot sequence reject stale state
每个 session MUST 暴露不可混淆的 session identity/epoch；权威 baseline 与 delta MUST 带有该 epoch 和单调 snapshot sequence。客户端 MUST 拒绝不同 epoch、已确认旧 sequence、重复 sequence 或无法以当前 baseline 连续应用的 delta；拒绝不得覆盖较新的视图或修改 host 权威状态。

#### Scenario: Old session snapshot cannot overwrite a new session
- **WHEN** 客户端已进入新 session epoch 后收到旧 session 的 baseline 或 delta
- **THEN** 消息被拒绝并记录可诊断结果，当前 session 的状态和视图保持不变

#### Scenario: Duplicate or out-of-order delta is mutation-safe
- **WHEN** 客户端收到重复、旧序号或跳过当前 baseline/sequence 的 delta
- **THEN** 消息不重复应用、不产生重复 mutation；客户端请求/等待新的 baseline 或按协议报告缺口，不宣称已同步

### Requirement: One-shot commands are deduplicated and ingress is bounded
一次性命令 MUST 在 session/player/command identity 作用域内去重，重复投递最多产生一次权威 mutation 和一次对应事件。入口 MUST 对无效身份、格式/大小、队列容量、取消、连接关闭与 Dispose 提供结构化失败结果；网络/接收线程 MUST NOT 直接改模拟状态。

#### Scenario: Retransmitted command mutates once
- **WHEN** 同一绑定玩家的同一 command identity 因重试或传输重复到达
- **THEN** 系统返回同一幂等结果或等价重复结果，权威状态差异、提交计数和事件最多发生一次

#### Scenario: Cancellation or disposal stops ingress safely
- **WHEN** session ingress 被取消、连接关闭或 session 被 Dispose，随后仍有命令到达
- **THEN** 新命令被结构化拒绝且不进入可执行队列；已提交命令按明确生命周期完成或取消，不发生越权 mutation，资源/handler 解绑可诊断

### Requirement: Transport loss is not reported as synchronization
运行端检测到连接关闭、读写失败、超时或应用层 fault MUST 进入明确的 disconnected/unsynchronized 状态并阻止继续宣称与 host 同步；本能力 MUST NOT 自动重连、主机迁移、WAN/NAT/relay 或产品退出/存档流程。

#### Scenario: Lost transport blocks synchronization claim
- **WHEN** 客户端检测到 transport loss 或应用层注入的 receive/send fault
- **THEN** 客户端标记连接不可用/同步未知，停止应用后续 gameplay delta，并输出可诊断状态；不得声称仍与 host 同步

#### Scenario: Host exit remains a gated product decision
- **WHEN** host 退出或关闭 session 的 UI、存档和其他产品语义尚未由 owner 决定
- **THEN** 集成验收保持 blocker，不从 Dispose、socket close 或默认 UI 行为推导成功标准

### Requirement: Transport behavior is measured at the correct layer
实现前 MUST 以显式 transport spike 比较候选 TCP/UDP 方案、库、配置和证据，并由 owner 确认 D1；在决策前不得进入 production transport implementation。测试 MUST 将 TCP 字节分段/合并/有序可靠行为与应用层丢失、重复、乱序、延迟 fault injection 分开；不得把原始 TCP packet reorder 暴露给应用作为可测试承诺。

#### Scenario: TCP stream segmentation does not alter decoded messages
- **WHEN** 同一合法 TCP byte stream 以不同分段或合并方式交给 stream decoder
- **THEN** decoder 根据 framing 重建相同应用消息；测试断言消息顺序和边界，而不是假设 TCP packet 边界存在

#### Scenario: Application fault injection drives recovery behavior
- **WHEN** 在消息/应用层注入延迟、丢失、重复或乱序事件
- **THEN** session 按 baseline/sequence/ingress 契约拒绝、等待或标记 unsynchronized；TCP 本身的可靠有序语义不得被测试表述为应用可见的原始包重排
