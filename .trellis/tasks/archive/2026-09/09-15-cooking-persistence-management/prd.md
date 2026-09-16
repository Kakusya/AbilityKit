# P5 持久化经营管理

> 归档状态：本 task 已于 2026-09-16 按 `completed-limited-scope` 归档，Trellis status 为 `completed`；完成只覆盖现有 `check.jsonl` 支持的纯 .NET 受限交付。

## 当前实施状态与验收矩阵协调

- 本 task 已于 2026-09-16 归档，Trellis status 为 `completed`；最终语义为 `completed-limited-scope`，只覆盖已验证的 in-memory pure .NET technical contract。
- 已实施并验证受限纯 .NET settlement/progress、integrity envelope、staged in-memory fault store 与 validated restart contract；它不选择真实文件、数据库、云端介质或保存生命周期策略。
- **验收矩阵协调：**遗留 task metadata/PRD 的 P01–P07 与 implement/design 的 P01–P10 不一致。当前以 design 的 **P01–P10** 作为完整矩阵：P01–P09 是可分层验证的技术合同，P10 是 owner decision gate。此协调不代表完整 P5 exit 已满足。
- 已实际通过 P01–P09 的 in-memory technical subset 与 P10 的 explicit blocked result；不代表真实 durable write/process-crash、产品存档或 owner integration 已通过。
- 前置事实：P0–P4 均有受限 pure .NET evidence。它们的 non-Unity 产品化缺口位于 successor backlog；Unity 位于 prohibited future scope，不是 P5 前置或 blocker。
- 阻塞：存档归属、保存时机、退出/断电/取消、host exit、migration/backup/recovery、encryption/key policy 均待 owner 决策。

## 来源与可追溯性

来源快照：`.trellis/migration/legacy-cooking-changes/add-cooking-persistence-management/`。完整文件哈希与目标映射见 `.trellis/migration/legacy-cooking-changes/manifest.json`。


## 只读历史规划说明

- 下文仍保留的 `Draft`、`blocked`、future matrix 与未勾选 checklist 仅用于历史追溯，**不可执行、不是当前 task checklist，也不是本 limited delivery 的 blocker**。
- 未完成的 non-Unity 范围（durable store、process-crash 证明与存档产品策略）只以 [`successor-backlog.md`](../../../Docs/design/CookingGame/successor-backlog.md) 为入口；如获批准必须新建 task。
- Unity package、asmdef、scene、authoring、projection、UI、EditMode 与 Unity smoke 长期禁止；只见 [`future-scope.md`](../../../Docs/design/CookingGame/future-scope.md)，不得按下文历史指令实施。

## 迁移的需求说明

## 只读历史 Draft / NOT ready to apply（不可执行、不是当前 blocker）

> 本阶段只创建规划文档，所有实施任务、测试和门禁均未执行。阶段 6 的直接前置为阶段 5（P4）`add-cooking-match-lifecycle`，并依赖阶段 2–4（P1–P3）的 LAN、配方和配置契约；阶段 1（P0）通过这些阶段传递依赖。上述前置 change 尚待实现和验收，本文不假称已完成。存档 owner、保存时机、退出/中断规则、迁移与损坏恢复策略必须由 owner 决策；未决时对应验收保持 Blocked。

## Why

阶段 2-5 的对局状态与结算边界就绪后，需要把一局已确认结果可靠地转入长期经营进度，并在重启后安全读回。若没有明确的幂等结算、原子写入和版本/损坏处理契约，重试、进程中断或重复提交可能造成重复奖励或丢失进度。

## What Changes

- 新增阶段 6 的长期进度模型边界：解锁、升级、货币和经营进度与 Round/Match 临时状态分离。
- 规划经权威结算确认后写入长期进度的幂等流程；同一结算身份重复请求不得重复发奖或重复 mutation。
- 规划存档读写、完整性/版本元数据、写入中断与重启读回的可验证 seam；不硬编码保存时机、存档归属或默认 host 持有存档。
- 将旧版本、未知版本、损坏、截断和不完整写入分类为可诊断结果；迁移、备份、恢复或拒绝策略由 owner 决策门控制。
- 明确不把玩家间经济、共享账户、房主退出、主机迁移、断线恢复或跨玩家所有权纳入本 change。

## Capabilities

### New Capabilities
- `cooking-persistence-management`: 定义一局权威结算进入长期进度、幂等奖励、持久化读写、重启恢复与版本/完整性错误边界。

### Modified Capabilities
- 无。

## Impact

- 未来影响做菜应用层纯 C# 长期状态、结算适配器、存档 codec/store seam，以及 Unity/SDK 宿主入口；拟建路径须实施时确认，不向通用 AbilityKit 框架或 Orleans 宿主偷塞产品规则。
- 依赖前置 recipe/config/match changes 的已实现契约和测试证据：预计链接 `.trellis/tasks/archive/2026-09/09-15-cooking-recipe-loop/`、`add-cooking-config-validation/`、`add-cooking-match-lifecycle/`，但这些 change 尚待实现，不能作为当前完成证据。
- **只读历史、不可执行：**该 Unity package/asmdef/scene/EditMode/host 指令已由 prohibited future scope 取代；本 task 不实施，也不把它作为 blocker。
## 2026-09-16 受限范围收口

- Owner 已批准将本 task 的最终完成边界重划为：**in-memory 纯 .NET settlement/progress/staged-store 技术合同（focused 12/12；当时完整回归 52/52）**。该范围由现有 `check.jsonl` 支持，已于 2026-09-16 按 `completed-limited-scope` 归档，status 为 `completed`。
- 真实 durable store、process-crash 证明与存档产品策略移至 successor backlog；Unity 移至 future scope。
- 旧 checklist 中未运行的 Unity、真实 LAN、production transport、正式内容、durable storage 或完整产品出口不再是本旧 task 的归档 blocker；它们也没有因此变成已完成。
- 本 task 的 `completed`/归档语义只修饰上述纯 .NET 受限交付，不表示完整 P5、Unity 可玩版本、真实 LAN 或完整 P0-P6 产品出口完成。
- 原始检查事件保持不变；closure 只记录 2026-09-16 的 owner scope 决定。
