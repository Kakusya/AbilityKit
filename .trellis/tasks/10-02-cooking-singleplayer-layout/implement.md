> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S08 待执行清单

## 实际局部实施记录

独立review后进一步修正坐标尺度超出S01 int范围、外部地图边缘接触判定，并增加强制可信定义的ValidateForInstallation入口；内部墙/地板缺口保持封闭碰撞，外部包围边界允许恰好接触，与S01一致。增补后focused17/17通过、0skip、exit0，日志layout-boundary.log。真实ET安装必须使用强制定义入口；几何安装、供应/前厅整合和完整恢复仍待核心接入。

已在独立整合分支补上静态布局的角色净空和可信设备占地校验：格中心BFS可达但角色过宽时拒绝，路径与校验使用同一净空规则，几何参数进入canonical/hash；可提供定义占地表防止调用方缩小设备。此为S08独立几何部分，完整ET安装仍等待核心契约，不标Task完成。

实际 focused 命令：`dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj --filter FullyQualifiedName~CookingRestaurantLayoutTests --nologo --verbosity quiet`，exit0，13/13通过、0skip；日志 `local/Logs/cooking-execution/layout-clearance.log`。既有共享包CS1591警告已保留日志，未抑制。尚未跑完整新增布局ET出口或宣称Unity通过。

- [ ] 核对依赖、历史 check 与实际代码；解决本 Task 的契约差异。
- [ ] 补齐具体设计、验收场景与影响的 snapshot/checkpoint/schema。
- [ ] 最终规划审阅及 owner 批准；当前不可 start。
- [ ] 批准后按独立纵切实现：I07–I11/餐厅扩建、摆放、顾客工作动线。
- [ ] 实际运行聚焦 .NET 测试与适用 gate；记录 pass/fail/blocked/skip。
- [ ] 核对 准备态移动/旋转/区域扩建；占位/交互面/顾客与玩家通路验证后提交；营业中拒绝修改。

当前所有实现与测试步骤均未执行。命令选择见总任务 implement.md；Unity 必须解除禁令并确认环境后补齐场景命令。

## Current execution sequence after review

Owner authorizes audit then implementation. Follow the reviewed design and parent research/final-review.md; historical planning-only text is superseded. Check actual dependency evidence before starting, preserve stopped worktree edits, fix review findings before accepting prior implementation, update payload/fingerprints/config identity/canonical/checkpoint together, run focused behavioral and applicable integration gates, record actual pass/fail/blocked/skip and commit evidence. Never declare this Task complete from metadata or directory counts.
