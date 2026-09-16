# Close Cooking limited-scope changes

## Goal

将七个已验证的 Cooking 纯 .NET 受限增量从长期悬空的 `in_progress` 状态中收口：按实际交付边界归档，保留全部验证证据；把长期禁止实施的 Unity 工作移入统一、非活动、不可执行的 future scope；把仍有价值的非 Unity 产品化工作记录为 successor backlog，而不宣称 P0–P6 完整产品出口已经完成。

## Owner Decision

- 自 2026-09-16 起，Cooking 方向在可预见的长期内禁止 Unity 实现。
- 禁止创建或推进 Cooking Unity package、asmdef、scene、MonoBehaviour、authoring、projection、UI、EditMode 或 scene smoke。
- 解除该限制必须由 owner 新作明确决定；不得因路线、旧 checklist 或 future-scope 文档自动推导授权。
- 若将来解除限制，必须新建 Trellis task 并重新审议当时契约，不恢复七个归档 task 直接实施。

## Requirements

1. 将七个 `09-15-cooking-*` task 的完成边界正式收窄为各自已有 `check.jsonl` 支持的纯 .NET 受限增量。
2. 原始检查事件、命令、通过数量、blocked/not-run 声明不得删除或改写；只允许追加 closure 记录。
3. Unity 执行项不得继续作为活动 task checklist、阶段 blocker 或 successor 前置；其长期有效的不变量和条件性验收边界必须保留。
4. 建立统一 Unity future-scope 文档，明确其非活动、不可领取、不构成时间表或实现承诺。
5. 建立统一 non-Unity successor backlog，收纳 production transport、正式 schema/content、durable persistence、真实 LAN 与测量阈值等未启动工作；backlog 不是 active task 或实施批准。
6. 更新 Cooking progress、technical roadmap、spec index 和阶段 spec，使“已归档受限增量”“未启动非 Unity 后续”“禁止的 Unity 范围”不再混为一谈。
7. 修复已知 metadata/manifest 漂移：P1 摘要按权威证据写 `20/20`；P0 `check.jsonl` 不再把 ignored artifact 目录当作 context 文件。
8. 使用 Trellis archive 命令移动七个旧 task；归档不得自动提交。

## Non-Goals

- 不修改任何运行时代码、测试代码或协议代码。
- 不实现 Unity、真实网络、正式内容、durable store 或性能优化。
- 不把 successor backlog 自动转换成 task，也不为其承诺日期、owner 或版本。
- 不宣称 Unity、真实 LAN、生产存档或完整 P0–P6 产品出口通过。

## Acceptance Criteria

- [x] `Docs/design/CookingGame/future-scope.md` 明确记录长期禁止的 Unity 执行范围、保留不变量及重新授权条件。
- [x] `Docs/design/CookingGame/successor-backlog.md` 明确记录 P1–P6 非 Unity 后续范围，并声明其未启动、未批准实施。
- [x] 七个旧 task 的 PRD/design/implement/task metadata 均清楚表达已于 2026-09-16 归档的纯 .NET 受限范围以及移出的 Unity/non-Unity 后续范围，status 为 `completed`。
- [x] 七个旧 `check.jsonl` 保留历史并追加 closure；P0 原始 check events 不变，仅删除无效 artifact file context，并追加 closure。
- [x] 七个旧 task 已通过 `task.py archive --no-commit --skip-branch-validation` 归档，归档后的状态为 `completed`。
- [x] Cooking progress、technical roadmap、spec index 和各阶段 spec 与新治理状态一致。
- [x] `task.py validate` 对本治理 task 与七个归档 task 均通过；六个归档 task仅警告其历史 branch 已在本地删除。
- [x] 文档搜索不再把旧七 task 描述为当前 `in_progress`，也不把 Unity 未实施写成当前阶段 blocker 或 successor 前置。
- [x] `git diff --check` 通过。

## Notes

- “归档”只表示 task 经 owner 重划后的受限交付范围完成；不表示旧 change 最初设想的完整产品出口完成。
- `.trellis/migration/legacy-cooking-changes/` 保持只读，不随本次决策改写。
