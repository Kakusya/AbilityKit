## Context

本 change 的动机与范围见 [proposal.md](proposal.md)，行为契约见 [spec](specs/cooking-interaction-foundation/spec.md)。现有应用路线要求纯 C# 权威模拟、稳定命令顺序、host 与远端共用验证路径，以及 Unity 只做 authoring/view；现有 `src/AbilityKit.Ability/AbilityKit.Ability.csproj` 通过 `Compile Include` 复用 `Unity/Packages/com.abilitykit.ability/Runtime/**/*.cs`，Unity 运行时程序集为 `Unity/Packages/com.abilitykit.ability/Runtime/com.abilitykit.ability.asmdef`。因此实现必须同时检查 .NET 与 Unity 宿主，不能把尚未存在的游戏项目、网络传输或场景能力当成现状。

## Goals / Non-Goals

**Goals:**

- 建立可序列化、可比较的 Session/Player/World/Match/Item/Command 身份和生命周期边界。
- 以纯 C# 状态机/聚合完成位置唯一性、配置资格/范围/容量、稳定排序、原子提交和幂等。
- 让本地 host 与 remote 仅通过不同 in-process adapter 进入同一 command handler；adapter 不拥有规则。
- 提供只读权威快照到 Unity projection 的 seam，并准备最小 scene/config fixture。
- 为每个契约场景建立带输入、动作、断言、runner 和 pass criterion 的测试矩阵，实施时可加入适配门禁。

**Non-Goals:**

- 不选择网络库、wire codec、监听端口或真实 LAN/WAN；P1 才做 transport spike 与决策。
- 不实现完整房间/Match 生命周期、配方、订单、移动、烹饪计时、存档、重连或主机退出语义。
- 不固定 30Hz、渲染帧率或严格 lockstep；不把 Unity Rigidbody/Update 当作权威模拟依据。
- 不把 fixture 的一只物品、一个手槽或站点容量推广为全局玩法规则。

## Decisions

### 1. 领域核心放在共享纯 C# 源码

新增独立游戏应用包（建议 `Unity/Packages/com.abilitykit.game.cooking/`）及对应 SDK 项目（建议 `src/AbilityKit.Game.Cooking/`），由该游戏项目的 Compile Include 与自己的 Unity asmdef 消费共享 Runtime 源码。命名为拟议路径，实施前检查是否冲突。现有 `AbilityKit.Ability.csproj` 仅作为共享源码组织的参考，不向该通用能力项目加入烹饪规则。未来新增应用测试项目显式引用游戏 SDK 项目。不在 Server/Orleans 中放入规则。备选是在 Unity MonoBehaviour 中直接裁决，但会失去无 Unity 单元测试和 host/remote 同路径证据。

### 2. 命令管道是唯一写入口

定义不可变 command envelope（Session/World/Match/Player/Command identity、逻辑序号、操作、目标和配置上下文），由 adapter 入队；单线程/显式模拟步按 `(simulation order, player identity, command identity)` 等已声明的稳定键排序，再执行完整验证和一次提交。不得以接收时间、线程调度或字典顺序排序。备选是 adapter 直接调用 Item/Slot setter，但无法保证原子拒绝和一致路径。

### 3. 位置采用显式互斥状态而非表现层父子关系

Item 的当前位置和 owner 只存在于纯 C# 权威状态；PlayerHand/StationSlot 以配置容量和占用索引表达。pickup/drop 在临时验证结果中完成所有检查后一次性更新 item 与目标/源槽位；失败返回结构化拒绝原因且不发布部分事件。备选是依赖 Unity Transform parent，但无法表达跨实例身份、快照纠正或无 Unity 集成测试。

### 4. 幂等键限定在 session/player/command 作用域

处理器保存已处理命令的最小结果/指纹，重复提交返回同一结果而不再变更；不同 Session 或 Player 的同值 command identity 不互相去重。生命周期版本/存在性先于业务验证，旧实例命令必拒绝。存储形式可由实现选择，契约不暴露集合结构。

### 5. Adapter 与 projection 都是边界组件

HostLocalAdapter 和 RemoteInProcessAdapter 只负责把同一 command envelope 投递到 handler；它们不是两套规则。Unity projection 只消费带版本的只读 snapshot/event，维护表现版本水位并忽略旧/未知输入，绝不调用权威 setter。真实网络 adapter 留给后续 LAN change。

### 6. Fixture 是验收夹具，不是产品玩法决定

使用一个可移动 item、一个手槽、一个 station slot、两名逻辑玩家和显式容量/资格/范围配置覆盖争抢；fixture 的容量可在测试中变化以证明规则来自配置。它不声明正式关卡布局、人数上限或一人一物规则。

## Test Matrix

以下 ID 必须由实施任务逐一链接；本 change 创建时只写计划，所有 runner/path 均为未来项目或实施时确认，故不宣称已运行。

| ID | 输入/动作 | 断言与可测量通过标准 | Runner（未来路径） |
|---|---|---|---|
| T01 | 有效 session/player/item 与空手槽 pickup，再 drop 到空站点并重新 pickup | 每次转移后 item 唯一位置正确，源空、目标占用；每个唯一命令提交计数=1，状态差异仅为预期字段 | .NET xUnit 纯 C# 单测（未来 `src/...CookingGame...Tests`） |
| T02 | 已移除/跨 session/stale item ID | 结果为生命周期拒绝；完整权威状态字节/结构相等，无事件 | 同上 |
| T03 | 不具资格、超范围、当前位置不符、目标不可用 | 每种拒绝有稳定原因；每次前后状态相等 | 同上 |
| T04 | 已满 station slot drop | 容量拒绝；item 仍在原手，槽内容/计数不变 | 同上 |
| T05 | host-local 与 remote-in-process 同一 envelope | 两 adapter 经过同一 handler seam，结果/状态/事件等价 | .NET adapter integration（未来 `src/...IntegrationTests`） |
| T06 | 两玩家同批次争抢同一 item | 仅稳定排序胜者成功；重复运行赢家和事件序列一致 | 同上 |
| T07 | 重复提交同一 command identity | 第二次返回幂等结果；变更和事件最多一次 | 同上 |
| T08 | 同一显式模拟批次、相同命令集合以不同入队顺序提交，在批次封闭后统一执行 | 稳定排序后最终快照和事件序列相同；不要求跨已提交批次回溯排序或网络回滚 | 同上 |
| T09 | 新快照、旧快照、未知 item 投影 | 仅最新有效版本更新视图；旧/未知不回写 authority、不覆盖新视图 | Unity EditMode projection（未来 Unity 测试程序集，路径待确认） |
| T10 | 最小 scene/config fixture 加载 | 1 item、hand、station、2 logical players 可解析；无公共网络/存档依赖；容量来自 fixture | Unity EditMode scene smoke（未来场景/测试路径） |

所有测试以“实现后运行并记录产物”为退出证据；本规划阶段不运行构建或测试。

## Risks / Trade-offs

- [Risk] 应用领域代码放入通用 AbilityKit 包会越过框架边界 → [Mitigation] 实现前确认存放位置；只复用共享源码，不把烹饪规则扩散到通用框架，Unity 与 SDK 编译双检。
- [Risk] 未来 wire/transport 需要不同 envelope → [Mitigation] 本阶段只固定可序列化语义和 adapter seam，不固定编码或传输。
- [Risk] 版本、命令排序与生命周期遗漏导致幽灵所有权 → [Mitigation] T02/T06/T08 作为阻断测试，拒绝路径统一比较前后状态。
- [Risk] projection 被误用为权威 → [Mitigation] T09 明确只读输入与 authority 不变断言。
- [Risk] fixture 被误读为产品规则 → [Mitigation] proposal/spec/design/tasks 均标明容量、人数和一物品仅为测试配置。

## Migration Plan

这是新能力，无既有运行时数据迁移。实施时先添加纯 C# 契约与单元测试，再接入两种 in-process adapter，最后添加 Unity projection/fixture；任一阶段失败则不接入后续宿主，移除新增 fixture/适配器即可回滚，不修改现有框架契约。

## Open Questions

以下不影响本 change 的契约或实现顺序，留给后续 change：P1 的 LAN transport 选择与 benchmark；正式房间/Match 生命周期；配置 hash 与旧快照兼容策略；重连、房主退出/迁移、存档归属；是否启用预测/插值以及测得的 tick/响应性目标。若这些问题改变本 change 的范围，应新建或更新 OpenSpec change，而非在此默认决定。
