## Draft / NOT ready to apply

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
- 依赖前置 recipe/config/match changes 的已实现契约和测试证据：预计链接 `openspec/changes/add-cooking-recipe-loop/`、`add-cooking-config-validation/`、`add-cooking-match-lifecycle/`，但这些 change 尚待实现，不能作为当前完成证据。
- 预计测试覆盖 .NET 纯 C#、应用层持久化故障夹具、Unity 重启/读回 smoke；本规划阶段不运行 tests、build 或门禁。
