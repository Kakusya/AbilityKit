# P4 关卡/地图/Match 生命周期：cooking-match-lifecycle

> 迁移状态：**blocked**。本规范由只读来源快照 `.trellis/migration/legacy-cooking-changes/add-cooking-match-lifecycle/specs/cooking-match-lifecycle/spec.md` 转换；原始 SHA-256 见 [迁移清单](../../migration/legacy-cooking-changes/manifest.json)。
>
> 本文件保留待审阅的规则和验收场景，但不使它们自动成为实施批准、测试通过或已归档的事实。开始实现前必须审阅对应 Trellis task 的 PRD、design 和 implement checklist。

## 迁移边界

- 依赖：P0 identity/location/lifecycle、P1 session、P3 config/layout 校验。
- 阻塞：房间人数、房主退出、断线恢复和主机迁移仍待产品决策。
- 规划验证：M01-M08（均为 future 规划，尚未执行）

## 迁移的行为草案

## Purpose

为做菜经营游戏定义可重入的地图、关卡、房间和对局生命周期，确保每局在准备、开始、结束与重新开局之间具有明确状态边界，并能与配置、会话和权威快照正确隔离。

## ADDED Requirements

### Requirement: 地图与关卡必须在 Match 准备前完成解析
系统 SHALL 从已验证的 Level/Map 配置加载逻辑布局、可用内容和必要初始化参数；解析失败、引用缺失或重复实例 MUST 阻断进入准备完成状态。

#### Scenario: 合法地图加载并准备
- **WHEN** 有效 session 选择有效 Level，且其 Map、Recipe、Appliance 与布局引用均通过阶段 4/P3 校验
- **THEN** 系统 SHALL 创建隔离的 Match 准备上下文，绑定 session/config identity，并提供可验证的逻辑布局；Unity GameObject identity 不得成为权威身份

#### Scenario: 地图或配置引用失败
- **WHEN** Level 缺少 Map/内容引用、逻辑布局非法或 config identity 不兼容
- **THEN** 系统 MUST 返回结构化诊断，Match 不得进入 Ready/Started，且不得创建可继续接收 gameplay command 的实例

### Requirement: Match 必须支持准备、开始、结束和重新开局
系统 SHALL 按稳定生命周期顺序支持 `Preparing -> Ready -> Started -> Ended`，并从 Ended 创建新的隔离 Match 实例或明确的新局代际；非法跳转 MUST 被拒绝且状态不变。

#### Scenario: 完整最小生命周期
- **WHEN** 客户端加载地图、完成准备、发起开始、完成一局并请求重新开局
- **THEN** 系统 SHALL 依次产生准备、开始、结束和新局准备结果；新局拥有新的 Match instance/epoch，旧局不得继续接受命令

#### Scenario: 非法状态迁移
- **WHEN** 在 Preparing 重复开始、在 Ended 提交 gameplay command 或在 Started 再次准备
- **THEN** 系统 MUST 拒绝操作并保持当前生命周期状态、事件和局内实例不变

### Requirement: Match 实例与会话和配置必须隔离
系统 SHALL 将 Match identity、session identity、config identity 和生命周期代际绑定到权威状态；不同 Match 的实体、订单/recipe 进度、计时器与快照不得互相污染。

#### Scenario: 两个 Match 并行运行
- **WHEN** 同一地图/Level 创建两个独立 Match 并分别提交局内命令和推进 Tick
- **THEN** 系统 SHALL 分别维护状态、事件和快照版本；任一 Match 的命令不得改变另一 Match

#### Scenario: 旧局命令或快照进入新局
- **WHEN** 新局已准备或开始后收到旧 Match identity、旧 epoch 或旧 snapshot sequence 的输入
- **THEN** 系统 MUST 拒绝或标记过期，不得改变新局状态，也不得宣称旧局仍 synchronized

### Requirement: 生命周期状态必须接入权威快照
系统 SHALL 为准备、开始、结束和重新开局输出包含 Match identity、生命周期状态、代际和逻辑版本的可序列化权威状态；客户端/表现层不得通过场景对象自行推进生命周期。

#### Scenario: 生命周期快照按序应用
- **WHEN** 客户端按顺序收到准备、开始和结束状态快照
- **THEN** 系统 SHALL 更新对应视图与状态水位，并保留可恢复到批准边界所需的版本信息

#### Scenario: 旧或乱序生命周期快照
- **WHEN** 客户端收到旧版本、乱序版本或未知 Match 的生命周期快照
- **THEN** 系统 MUST 忽略或拒绝该输入，不覆盖较新状态，不修改权威 Match

### Requirement: 未决退出和长期状态不得被生命周期默认决定
系统 SHALL 将 host exit、断线恢复、主机迁移、人数上限、存档归属及结束后的长期经营结算保持为 Draft / Blocked owner 决策；本 change MUST 只定义局内生命周期。

#### Scenario: Owner 决策未确认
- **WHEN** 发生 host close、remote loss 或结束后需要保存/迁移的情形而相应 owner 语义未确认
- **THEN** 系统 SHALL 输出 blocked/未决状态，不得自行推导安全退出、自动重连、迁移或长期奖励
