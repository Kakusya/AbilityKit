# P0 交互基础：实施设计

> 归档状态：本 task 已于 2026-09-16 按 `completed-limited-scope` 归档，Trellis status 为 `completed`；完成只覆盖现有 `check.jsonl` 支持的纯 .NET 受限交付。

> 首期实现范围是纯 .NET 权威业务闭环。T01-T08 已实际执行并通过；T09-T10 未实施、未验证，现已移入长期禁止的统一 future scope；本文件不将其标记为完成，也不再将其作为旧 task blocker。

## Context

动机、范围与实际执行状态见 [PRD](prd.md)；行为草案见 [cooking-interaction-foundation spec](../../spec/cooking/cooking-interaction-foundation.md)。应用路线要求纯 C# 权威模拟、稳定命令顺序，以及 host 与远端共用验证路径。首期新建独立 `src/AbilityKit.Game.Cooking/` 与 `src/AbilityKit.Game.Cooking.Tests/`，不在通用 `AbilityKit.Ability`、Unity Runtime 或 Server/Orleans 中加入烹饪规则。

## 首期目标与非目标

**Goals:**

- 建立可比较的 Session/Player/World/Match/Item/Definition/Command identity、最小实例生命周期 seam 与 scope 验证。
- 在纯 C# 状态聚合中完成唯一位置、配置资格/范围/容量、稳定排序、原子提交与幂等。
- 让 `HostLocalAdapter` 与 `RemoteInProcessAdapter` 仅向同一 handler 投递 command envelope。
- 用 canonical snapshot、领域事件与 JSONL evidence 让接受、拒绝、争抢、幂等和重排的结果可审阅。
- 以 xUnit 实际执行 T01-T08，并将 `dotnet test` 结果和 JSONL 工件分别记录。

**Non-Goals:**

- 不选择网络库、wire codec、监听端口或真实 LAN/WAN。
- 不实现完整 Room/Match 生命周期、配方、订单、移动、烹饪计时、存档、重连或主机退出。
- 不实现 Unity projection、场景/配置 fixture、asmdef、EditMode 或 T09-T10；这些均为 `deferred` 后续范围。
- 不固定 30Hz、渲染帧率或严格 lockstep；不把 Unity Rigidbody/Update、墙钟、接收时间、线程调度或字典枚举当作权威顺序。

## 决策

### 1. 领域核心位于独立纯 .NET 应用层

`AbilityKit.Game.Cooking` 是 `net10.0` 应用层项目，`AbilityKit.Game.Cooking.Tests` 是对应 xUnit 测试宿主，二者已加入 `src/AbilityKit.sln`。它们不链接 Unity 源码，也不依赖 Unity、Host transport 或 Orleans。后续 Unity 只能消费业务层快照/事件，不反向取得权威写权限。

### 2. 最小生命周期 seam 只负责合法性判断

`CookingScope` 包含 Session、World 与 Match identity；command 还带 Player、Item、Command identity 和 item version。它用于拒绝错误 scope、已移除实例和 stale 版本。它不创建、销毁、恢复或迁移 Room/Match，也不定义房主退出或重连策略。

### 3. 命令管道是唯一写入口

`CookingCommand` 是不可变 envelope。`HostLocalAdapter` 和 `RemoteInProcessAdapter` 均调用同一 `CookingSimulation.Submit`。封闭批次由 `SubmitBatch` 要求单一 `simulation batch`，再按 `(player identity, command identity)` 的 ordinal 键排序，然后逐个执行完整 scope、生命周期、资格、范围、当前位置、可用性和容量验证。只有所有验证通过才更新 item、全位置占用索引、版本和事件；拒绝既不改变权威状态，也不产生部分事件。

### 4. 快照与幂等可判定

`CookingSnapshot.CanonicalText()` 使用排序后的结构化 JSON 输出 scope、item、location 与 station 占用，`Sha256()` 提供可审阅的前后状态摘要，避免原始分隔符字符串的编码歧义。幂等键为 `(SessionId, PlayerId, CommandId)`；相同 fingerprint 重放返回缓存结果且不新增事件，不同内容复用该 key 时得到 `CommandIdentityConflict`。

### 5. JSONL 是补充验收证据

`CookingAcceptanceEvidenceWriter` 为每个受测命令输出一条 JSONL。字段包括：`testId`、fixture、Session/World/Match、simulation batch、稳定 sort key、完整 command envelope、outcome、rejection reason、duplicate 标记、完整领域事件、前后 canonical snapshot SHA-256、断言摘要、runner 和 UTC 时间。

测试通过的权威依据仍是 xUnit 断言和 `dotnet test` 退出码；JSONL 只提供可机器读取、可审阅的运行证据。失败、blocked 或 skip 不能伪造为通过记录。

## Test Matrix

| ID | 输入/动作 | 可测量通过标准 | 实际 runner / 证据 |
|---|---|---|---|
| T01 | pickup → drop → re-pickup | item 始终唯一位置；每次成功一个事件；状态只变更预期位置/版本/占用字段 | xUnit，已通过；3 JSONL 记录 |
| T02 | cross-session、stale、removed item | 拒绝原因稳定；前后 canonical state hash 相同；无事件 | xUnit，已通过；3 JSONL 记录 |
| T03 | 无资格、超范围、当前位置不符、目标不可用、未知/不可用玩家 | 每种拒绝有明确 reason；前后 canonical state hash 相同 | xUnit，已通过；6 JSONL 记录 |
| T04 | 满 station drop | `CapacityFull`；item 保留 hand；state hash 不变 | xUnit，已通过；1 JSONL 记录 |
| T05 | host-local / remote-in-process 相同 envelope | 结果、最终 snapshot 与事件序列相同；每个 adapter 只提交一次 | xUnit，已通过；2 JSONL 记录 |
| T06 | 两玩家同批争抢同 item | 稳定唯一赢家；重复运行 snapshot/事件相同 | xUnit，已通过；2 JSONL 记录 |
| T07 | 重复 command identity | 第二次为 duplicate；无第二次事件或状态变更 | xUnit，已通过；2 JSONL 记录 |
| T08 | 同一封闭 batch 以不同入队顺序执行 | canonical snapshot 与事件序列相同 | xUnit，已通过；2 JSONL 记录 |
| T09 | 新/旧/未知 snapshot 的 Unity projection | **deferred**：尚未实现或运行 | 后续 Unity EditMode |
| T10 | 最小 Unity scene/config fixture 加载 | **deferred**：尚未实现或运行 | 后续 Unity EditMode scene smoke |

实际执行命令：

```bash
COOKING_EVIDENCE_DIRECTORY="$(pwd)/artifacts/cooking-interaction-foundation" \
  dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj
```

该次执行通过 10/10 个测试（其中 T01-T08 全部通过，另有初始化占位/canonical serialization 与 command-identity/batch 边界回归），并在 `artifacts/cooking-interaction-foundation/` 生成 8 个 JSONL 文件、共 21 条记录。`artifacts/` 由仓库忽略，不将运行态输出提交为源码。

## 风险与后续

- 领域代码误入通用框架 → 新业务项目保持独立；后续 Unity package 应只引用/消费该边界。
- transport/wire 将来需要不同编码 → 首期只固定语义 envelope，不固定 wire。
- 生命周期范围被过度扩展 → 本期仅做 scope/lifecycle 验证 seam。
- Unity projection 被误用为 authority → T09/T10 未开始，后续必须保持只读消费和版本水位规则。
- fixture 被误读为产品规则 → 一物品、两玩家和容量均仅为 T01-T08 测试数据。
## 2026-09-16 受限范围收口

- Owner 已批准将本 task 的最终完成边界重划为：**T01-T08 纯 .NET authority/interaction contract（10/10；8 个 JSONL、21 条记录）**。该范围由现有 `check.jsonl` 支持，已于 2026-09-16 按 `completed-limited-scope` 归档，status 为 `completed`。
- P0 无 non-Unity successor；T09-T10 及全部 Unity 执行移至 `Docs/design/CookingGame/future-scope.md`。
- 旧 checklist 中未运行的 Unity、真实 LAN、production transport、正式内容、durable storage 或完整产品出口不再是本旧 task 的归档 blocker；它们也没有因此变成已完成。
- 本 task 的 `completed`/归档语义只修饰上述纯 .NET 受限交付，不表示完整 P0、Unity 可玩版本、真实 LAN 或完整 P0-P6 产品出口完成。
- 原始 check events 保持不变；仅删除无效的 artifact `file` context，其余只追加 closure 记录 2026-09-16 的 owner scope 决定。
