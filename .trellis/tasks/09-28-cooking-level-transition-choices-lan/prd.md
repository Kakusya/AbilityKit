# Cooking 跨关选择与状态承接 LAN 纵切

> 状态：`planning`。本文件只记录需求与验收，不授权实现。实现须等最终规划摘要被明确批准，并经 `task.py start` 进入 `in_progress`。

## Goal

把已经在纯 C# 领域层验证过的跨成功小关状态承接、装修/道具/Buff 选择和成功检查点，接入生产级 Cooking Host/Client 会话。Host 完成一次成功小关后，必须能够原子地写入大关检查点、创建下一 Level，并向 Client 下发带新 Level 身份的完整状态；Host 与 Client 对厨房、跨关进度和 Level 身份共同达成 SHA-256 共识。

用户价值：局域网闭环不再止于单 Level 做菜和结算。玩家完成一关后，下一关能看到上一关确认的厨房状态和选择，同时旧 Level 的命令、快照水位和订单上下文不会污染新 Level。

## Background and confirmed facts

- `09-19-cooking-productization-network-slice-planning` 只作为产品事实来源，继续保持 `planning_only: true`，本任务不在其目录下提交业务代码。
- 纯 C# 已有 `CookingLevelLifecycle`、`CookingRecipeSimulation.ExportSuccessHandoff`/`AcceptSuccessHandoff`、`CookingMajorProgress`、`CookingMajorCheckpointStore` 和 `CookingLevelEtHost` 的跨关能力；已有单机测试覆盖装修迁移、烤箱解锁、煮制加速、成功检查点和失败重试。
- 生产 LAN 会话 `CookingSessionHost`/`CookingSessionClient` 当前围绕单一 `CookingRecipeSnapshot` 工作，协议没有把 Level scope、跨关进度或新 Level generation 纳入快照共识，也没有跨 Level 的客户端水位切换。
- 当前长期范围是纯 C#，Unity、Profile/SaveSlot、真实双物理机 LAN 和主机迁移均不属于本任务。
- 现有产品决定要求：成功小关保留厨房现场、装修、解锁道具和 Buff；清除本关订单、结算上下文、Level 命令/快照水位；写入检查点失败时不得进入下一小关。

## Requirements

### R1 Host 权威跨关提交

- 仅 Host 可以提交已经由产品流程确定的下一关选择；本任务不定义多人投票、客户端 UI 或选择冲突解决。
- 提交前必须处于当前 Level 的成功收口/准备边界，且所有选择通过现有领域校验。
- 提交操作必须按以下顺序形成单一原子结果：确认选择 → 生成成功交接厨房 → 写入大关检查点 → 安装新 Level scope/epoch → 广播新 Level 完整状态。
- 检查点写入失败、选择非法、Level 身份不前进或交接失败时，旧 Level 和旧快照保持不变，不广播下一关。
- 同一旧 Level 只能成功提交一次；重复请求返回结构化重复/无效结果，不重复写盘或生成新 Level。

### R2 跨关状态与清除边界

下一 Level 必须保留现有领域交接已定义的：

- 食材、半成品、容器内容、脏碗/干净碗池；
- 未完成加工及其已用/所需 tick；
- 装修、已解锁 DefinitionId、煮制加速 Buff；
- 不回退的物品/工序/产物 ID 计数器。

下一 Level 必须清除：

- 本关订单与本关结算历史；
- 本关命令去重账、事件历史和逻辑 Tick；
- 旧 Level 的命令版本、快照序列和客户端投影水位。

### R3 协议和快照共识

- 网络快照必须携带至少 `Match/RestaurantRuntime/Level/LevelEpoch`、会话 generation、快照序列、跨关进度投影和厨房快照。
- Client 只接受当前 scope/generation 的新快照；旧 Level、旧 generation、乱序或重复快照不得覆盖当前投影。
- 新 Level 首个快照必须是完整基线，不依赖重放旧 Level 命令；重连在新 Level 中继续使用同一 Match 生命周期凭证并收到当前完整状态。
- Host 与 Client 的共识哈希必须覆盖 scope、generation、跨关进度和厨房 canonical，不得只比较厨房 `RecipeSnapshot`。

### R4 旧命令与断线语义

- 旧 Level 命令到达 Host 时结构化拒绝且零变更。
- Client 在收到新 Level 基线后重置本地命令关联和快照等待状态；不重用旧 Level 的 pending command。
- Client 在跨关期间断线并重连时，Host 只补发当前 Level 完整快照，不重放旧 Level 历史。
- Host/Client 断线安全释放与当前单 Level 行为保持不变。

### R5 失败阻断与可观察性

- 检查点文件写入失败时，Host 留在旧 Level 成功收口后的稳定状态，既不安装下一 Level，也不改变 Client 投影。
- 迁移成功结果必须能观察到源/目标 Level 身份、保留对象摘要、清除计数、选择摘要和新 generation。
- 所有拒绝必须有稳定 reason，不以异常或静默丢包代替业务结果。

## Acceptance Criteria

- [x] Host 在成功闭环后提交装修、解锁 DefinitionId 与煮制加速；检查点成功写入，并创建更高 epoch 的新 Level。
- [x] 新 Level 保留交接厨房、装修、解锁和 Buff；订单、结算、旧命令去重、事件和逻辑 Tick 从新起点开始。
- [x] Host 广播的新 Level 完整快照包含 scope、generation、跨关进度和厨房状态；Client 与 Host 的完整会话状态 SHA-256 相等。
- [x] 旧 Level 命令、旧 Level 快照、旧 generation 快照和乱序/重复快照均不能改变新 Level 共识。
- [x] Client 在新 Level 断线重连后收到当前完整基线，重连凭证仍有效，Host/Client 再次达成完整会话 SHA-256 共识。
- [x] 检查点写入失败时，不创建新 Level、不广播新快照、不改写旧 Level 稳定状态；失败可被结构化观察。
- [x] 重复提交同一 Level 的跨关请求不重复写盘、不重复生成 Level，且现有状态保持不变。
- [x] 既有 Cooking 单元测试、LAN 测试、`cooking-kitchen-loop` 和 `cooking-et-level-runtime` 门禁保持通过；未修改 Unity 自动生成文件。

## Out of Scope

- 规划源任务 `09-19-cooking-productization-network-slice-planning` 的直接修改或归档。
- 客户端选择 UI、多人投票、选择权限协商、主机迁移。
- 新菜谱、新装修效果、经济系统、伙伴长期成长、复杂评分平衡。
- Profile/SaveSlot 产品化、断电恢复、跨设备恢复、真实双物理机 LAN。
- Unity package、scene、authoring、projection、UI、动画和 EditMode。
- 将旧 Cooking 专用 UDP 实现迁移到其他传输库；本任务复用现有生产会话和当前已验证的回环 Transport。

## Key decisions and deferred items

- 选择提交采用 Host-authoritative（Host 权威）控制面；这是为了在不引入投票协议的情况下验证跨关状态和网络一致性。后续若需要客户端参与选择，另立任务定义 UX 和冲突规则。
- `CookingMajorCheckpointStore` 仍由调用方提供根目录；本任务只验证写入成功/失败对 Level transition 的原子边界，不把它升级为 Profile/SaveSlot。
- 运行态完整共识使用新的 session-level canonical projection；现有 `CookingRecipeSnapshot.Sha256()` 继续作为厨房局部哈希，不再单独充当跨关协议哈希。
