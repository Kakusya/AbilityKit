# Issue #13 — Cooking 固定 Flow 测试编排者

状态：planning；当前批准范围仅 S0 阅读、盘点、提交计划。Owner 于 2026-10-07 显式调用 `$cooking-dot-workflow 请你开始对Issue #13工作.`，按 Issue 最新正文保留规则类别与实施计划审批门。交付模式为 branch-only 规划提交；不授权实现、合并、关单、发布或清理。

## 目标

让 agent 一次提交固定玩法测试，程序负责执行、等待、自动判断和归位，返回小摘要及失败包。首个目标为两个角色竞争同一可拾取物品，offline 与同机真实联网分别验证，另一个小 Flow 证明组合复用。运行过程中不调用模型，不建设 MCP 或常驻服务。

## 已确认事实

- 当前 source 基线为 master `271f76c8e4ab88ae93b28fb49e46f4700a399b66`。GitHub PR #12 实际 MERGED，mergeCommit `3c178851819abac5755c5bc53fa03c8b94257e2a`，2026-10-07T09:27:21Z。Issue 发布时的 OPEN/baseline 表述已过时；不自行重写旧任务、验收或关闭 #6。
- 正式 `CookingRecipeOperation.Pickup`、ET host 准入/Tick/处理终态和 v3 network session 已存在。接口、限制与引用见 [盘点](research/source-inventory.md)。测试层不得直接写领域状态。
- 规则类别均为 Draft；Owner 审核后才可作为通过依据。同类别合法数据变体无需重新审批；语义、范围、阈值放宽或例外变化须重审。

## 要求及验收

| ID | 要求 | 可观察验收 |
|---|---|---|
| R01 | 启动前验证 request、固定 Flow、规则版本、资源/时间/日志上限 | 不支持动作、组合、旧输出拒绝，创建 host 前返回非成功 |
| R02 | 两角色同一目标竞争；允许任一合法胜者 | 两个真实业务终态、恰一持有者、失败者手为空、无直接状态改写 |
| R03 | 复用同一 ET 权威与正式客户端/codec/transport | offline owner 路径与独立 server/client 同机真实联网分别报告；无伪 network |
| R04 | 确定性规则判定与单 writer 事件 | 正确场景通过，故意违规检出到 step/action/host；必要事件缺失/断档/溢出不能 Passed |
| R05 | 失败先留现场，再有界归位 | 成功、断言失败、超时、取消、host 退出、写入失败均明确执行/判定/证据/归位状态；原始失败不被抹去 |
| R06 | 一次 CLI 提交/程序等待/终态返回 | exit0 仅整体 Passed；缺失/不匹配/未发布完结果不能通过；连续两次独立运行无残留 |
| R07 | 小摘要、HTML 报告、失败包与回归选择 | 展示成功/失败报告；目的、覆盖、首次差异、NotRun、构建/执行/归位耗时清楚；模型不高频轮询 |

S0资料已形成：真实源码接口表、复用取舍、具体候选文件范围、dot API设计、规则类别卡与两个固定Flow、CLI completion支持/限制。[完整dot回复](research/dot-plan-reply-raw.txt) 对 `a8ea630ca1fd29fcf0327556f7847e13f589c218` 返回accept-plan，仅技术规划；[API](research/accepted-api-design.md)、design/implement已按其定稿同步，仍须Owner类别及S1具体范围批准。两Flow分开竞争/原样重试与Pickup/Drop，不用后者收敛掩盖前者。未跑长期gate或产品测试。

## 范围外

Unity/UI/键盘、物理双机 LAN、其他示例、ET/SDK/依赖升级、第二套领域运行时/协议/事件总线/DSL、全仓门禁/编译输入归档、自发修复循环、旧失败重标、旧 Run 接管、用户资源清理。读取共享源码不授权修改。

## Owner 待决定

审批 [五类规则卡](research/rule-categories.md) 及 dot 审阅后的 S1 文件/API/验收边界。此前不添加 `orca-ready`，不运行 task start，不派发实现。
