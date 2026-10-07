# S0 与后续有界切片

- [x] 读取 Issue #13 最新正文/blocked、AGENTS、progress、Cooking index/menu plan、SOP、当前 ET 合同、ADR/路线入口。
- [x] 枚举 Git/Orca/Run/terminal，建立新任务，不接管历史 Run。
- [x] 核对正式 Pickup、ET owner、v3 client/session、Rich fixture/planner/runner 与旧 Harness 限制。
- [x] 写 Draft 类别卡、竞争 Flow 与候选文件/数据契约输入。
- [ ] 发布 S0 branch-only 规划 SHA并核实远端；以 intent/receipt 记录 dot planning 请求。
- [ ] 保存完整当前请求/SHA 的 dot 计划与 API 设计，检查内容/时间/范围，更新 design/manifests。
- [ ] Issue #13 首次回报；Owner 审核类别和 S1 文件/API/验收；保持 blocked/planning。
- [ ] 批准后才添加 ready/进入 implementation，并由一个 Orca managed-worktree worker 执行 S1；接受输入与 turn-start 必须同时证明。
- [ ] S1 离线最小闭环、正常/违规/超时/归位控制；main 独立检查后冻结 candidate/hash，dot final-review。
- [ ] 单独确认 S2 同机真实 server/client，S3 调用器回执/失败交接范围；不把后续候选视为当前派发。

S0 检查：JSON/context 引用、真实符号/链接、diff scope、flow records inspect、远端 SHA/Issue 回报 readback。产品构建/测试/二进制 N/A（文档阶段）；offline/network 执行 NotRun；Orca 玩法 completion 集成 NotRun。Trellis Phase loader 已报告 `Phase Index section not found in workflow.md`，不把它当 Passed。不安装依赖绕过缺失 validator 环境。

实施具体 dotnet 命令与过滤器待 dot 定稿；不执行默认全仓 gate。各树独占 .NET 检查串行。失败、时间、计数、命令/native exit、源 dirty 与原始结果保留；不靠退出0替代真实覆盖。
