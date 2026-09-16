# Design: Cooking limited-scope closure

## State model

本次把过去混合在一个 task 内的三类状态拆开：

1. **Archived verified delivery**：已有代码和 `check.jsonl` 支持的纯 .NET 受限增量，作为旧 task 的最终完成边界。
2. **Non-Unity successor backlog**：仍可能有价值但尚未建立活动 task 的产品化工作，只记录入口，不获得实施授权。
3. **Prohibited Unity future scope**：长期禁止实施，只保留跨宿主不变量、历史来源和条件性验收意图。

## Truthfulness rules

- 不重写历史检查事件；closure 记录说明后续 owner 决策如何改变 task 边界。
- `completed` 只修饰重划后的 task scope，不修饰完整 P0–P6 产品能力。
- 未运行的 Unity、真实 LAN、durable store 和产品化测试继续是 `not-run`，不是 pass。
- future scope 与 successor backlog 都不是 active task、路线承诺或完成 blocker。

## Archive mechanics

1. 更新旧 task 的 task.json、PRD、design、implement 和 closure evidence。
2. 更新共享文档与阶段 spec。
3. 验证 active 目录中的 task manifests。
4. 使用 `task.py archive --no-commit --skip-branch-validation` 逐个归档。
5. 验证 archive 目录中的 task 和全局引用。

## Preserved sources

- `.trellis/migration/legacy-cooking-changes/` 保持只读。
- 旧 task 的 `implement.jsonl` 保留当时上下文，不重写为今天的决策。
- `check.jsonl` 保留原始命令与结果，只追加 closure。
- `.trellis/spec/cooking/` 继续保存稳定契约，但移除“必须实施 Unity 才能完成旧 task”的状态含义。
