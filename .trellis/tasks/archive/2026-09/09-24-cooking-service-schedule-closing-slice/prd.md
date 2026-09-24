# Cooking 前厅营业节奏与小关自然收尾切片 PRD

## 1. 目标与背景

根据 `09-19-cooking-productization-network-slice-planning/prd.md` 中的设计规范与讨论决定：
- **小关时间结构**：分为营业时间（Service Time）和收尾时间（Closing Time）；
- **营业时间**：顾客按配置间隔持续到店，NPC 询问点单，后厨出餐，顾客用餐；
- **收尾时间**：达到营业时长后停止接待新顾客；必须等待店内所有已在座顾客离席（用餐完成或超时离席），且 NPC 完成所有剩余收尾动作后，关卡才自然进入结束；
- **自然结束与结算**：关卡结束时无业务失败，按全部已完成订单汇总总分与星级评价；
- **局域网共识**：Host 权威负责驱动 `AdvanceFixedTick` 与前厅推进，前厅状态（营业/收尾/可结束）与小关状态随快照广播至 Client，双端 SHA-256 达成共识。

## 2. 需求范围 (Scope)

1. **Host 运行时集成前厅节奏 (`CookingSessionHost`)**：
   - 允许可选传入 `CookingFrontOfHouse` 或 `CookingFrontOfHouseSchedule`；
   - 在固定 Tick (`AdvanceFixedTick`) 中权威驱动前厅时钟推进与顾客/订单生命周期；
2. **快照扩展前厅收尾字段**：
   - `CookingRecipeSnapshot` 增加 `bool IsClosing` 与 `bool IsCompleted`；
   - 保持规范 JSON 序列化与 `Sha256()` 共识对齐；
3. **局域网全流程验证**：
   - 在 `CookingReusableHarnessAcceptanceTests.cs` 中添加前厅营业-收尾-自然完成的双端局域网集成测试用例。
