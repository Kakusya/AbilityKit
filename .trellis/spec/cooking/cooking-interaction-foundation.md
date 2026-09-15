# P0 交互基础：cooking-interaction-foundation

> 迁移状态：**planned**。本规范由只读来源快照 `.trellis/migration/legacy-cooking-changes/add-cooking-interaction-foundation/specs/cooking-interaction-foundation/spec.md` 转换；原始 SHA-256 见 [迁移清单](../../migration/legacy-cooking-changes/manifest.json)。
>
> 实施状态：T01-T08 的纯 .NET 权威业务闭环已在 `src/AbilityKit.Game.Cooking*` 实现并实际验证；T09-T10（Unity projection/fixture/EditMode）明确 `deferred`，未实施、未运行、未验证。完整命令与 JSONL 证据见对应 [Trellis task](../../tasks/09-15-cooking-interaction-foundation/prd.md)。
>
> 其余未实现范围不会因 T01-T08 通过而自动关闭；开始后续 Unity 实施前仍必须审阅对应 Trellis task 的 PRD、design 和 implement checklist。

## 迁移边界

- 依赖：最小配置/layout/snapshot 生命周期 seam；不依赖真实网络。
- 阻塞：未来测试项目与程序集路径须在实施时确认；不得将路线示例写成已实现能力。
- 实施验证：T01-T08 已运行并通过；T09-T10 为 deferred，尚未执行。实际命令、通过结果和 JSONL 工件路径记录在对应 Trellis task 的 `check.jsonl`，不能由本规范代替。

## 迁移的行为草案

## Purpose

本能力为做菜经营游戏提供最小、可测量且不依赖网络传输的厨房物品交互基础，使同一份纯 C# 权威规则能够服务主机本地玩家和远端逻辑消费者，并由 Unity 仅负责状态投影与场景验证。

## ADDED Requirements

### Requirement: Runtime identity and lifecycle are explicit
运行时 MUST 为 Session、Player、World/Match、Item instance 和配置 Definition 使用可区分的身份；Item instance 的位置与生命周期 MUST 能识别有效、已移除和过期 ID。Unity `GameObject` 身份 MUST NOT 作为权威模拟身份。

#### Scenario: Valid instance is resolved within its session
- **WHEN** 当前 Session/Match 中的玩家以有效 Item instance ID 提交交互命令
- **THEN** 系统按该 Session/Match 的实例状态解析物品并继续验证，而不把其他会话的同值 ID 当作同一实例

#### Scenario: Stale or removed instance is rejected
- **WHEN** 命令引用已移除、已失效或不属于当前 Session/Match 的 Item instance ID
- **THEN** 命令被拒绝且权威状态、所有权、槽位占用和事件输出均不发生变更

### Requirement: Item location has one authoritative owner
每个有效 Item instance 在任一时刻 MUST 恰有一个权威位置，位置 MUST 属于配置允许的 WorldPosition、PlayerHand 或 StationSlot；运行时 MUST 禁止重复占有、重复槽位占用和通过本能力引入的包含环。手与站点槽位容量 MUST 来自测试 fixture/运行时配置，而不是隐含的全局一人一物规则。

#### Scenario: Fixture permits one pickup into an empty hand
- **WHEN** 配置的可移动物品位于可达 WorldPosition，目标玩家的配置手槽可用且玩家有资格交互
- **THEN** 提交一次 pickup 后物品唯一位置变为该玩家的 PlayerHand，原位置不再占用，状态只提交一次

#### Scenario: Full configured destination rejects drop
- **WHEN** 玩家尝试将手持物品放入已达到配置容量的 StationSlot
- **THEN** 命令被拒绝，物品仍在原 PlayerHand，站点槽位内容和容量计数不变

### Requirement: Pickup and drop use one validated atomic command path
拾取和放下 MUST 通过同一套可注入的命令处理路径进入稳定排序、资格/生命周期/当前位置/范围/容量/可用性验证和原子提交；host 本地玩家与 remote 逻辑玩家 MUST 使用相同处理器语义。帧率、渲染 Update 和网络线程 MUST NOT 直接改变权威状态。

#### Scenario: Host and remote adapters produce equivalent accepted outcomes
- **WHEN** host adapter 与 remote adapter 分别以相同 Session/Player/Command 数据提交可接受的 pickup 或 drop
- **THEN** 两者进入同一验证路径并得到等价的命令结果、最终位置、所有权和事件语义

#### Scenario: Out-of-range or ineligible interaction is mutation-free
- **WHEN** 命令的玩家不具备配置资格、目标超出配置范围、当前位置不匹配或目标不可用
- **THEN** 命令被拒绝并且提交前后的完整权威状态相等

### Requirement: Contention order and idempotence are stable
一次性 pickup/drop 命令 MUST 具有 Session、Player、Command identity 作用域内的幂等键；同一命令重放 MUST 不产生第二次变更。同一封闭模拟批次内对同一 Item 或 StationSlot 的并发争抢 MUST 按明确、可复现的模拟顺序裁决，不得依赖线程调度、传输抵达顺序或字典枚举顺序。

#### Scenario: Two players contend for one item
- **WHEN** 两名逻辑玩家在同一批次对同一可移动物品提交有效 pickup，批次包含可复现的排序键
- **THEN** 只有排序胜出的一个命令成功，另一个被拒绝；结果在重复运行中相同，物品只有一个所有者

#### Scenario: Duplicate and reordered delivery are safe
- **WHEN** 同一 command identity 被重复提交，或同一封闭模拟批次的相同命令集合以不同适配器抵达顺序入队
- **THEN** 重复命令至多产生一次提交，稳定排序后最终状态与事件序列相同，拒绝项不产生部分变更

### Requirement: Projection is non-authoritative
Unity 投影 MUST 根据权威快照/事件将 Item instance、位置和所有权映射到表现对象；投影缺失、重复、过期或乱序输入 MUST 不回写权威模拟，且 MUST 可被后续有效快照纠正。最小 smoke fixture MUST 至少包含一个可移动物品、手槽、站点槽和两名逻辑玩家，但不定义完整玩法容量。

#### Scenario: Projection follows a newer authoritative state
- **WHEN** Unity EditMode projection 收到同一 Item instance 的较新有效状态
- **THEN** 表现对象移动到该状态指定的位置/槽位，且不会创建第二个权威物品或改变纯 C# 状态

#### Scenario: Stale projection input is ignored without authority mutation
- **WHEN** projection 收到旧版本、未知实例或不完整映射
- **THEN** 表现层不应用会覆盖较新视图的状态，权威模拟仍保持不变，并记录可诊断的拒绝/忽略结果
