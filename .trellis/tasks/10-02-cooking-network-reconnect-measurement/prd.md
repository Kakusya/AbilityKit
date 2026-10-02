# N03 网络恢复与测量 PRD

状态：in_progress / bounded local engineering，root2026-10-03审议批准。仅启动既有授权内的本地确定性恢复/边界增量，基线为reviewed assembled b6e102ac9。N02两个endpoint完整流程已通过但wrapper复跑和物理gate仍待完成，不将N02推导为completed。Unity不在执行范围；.NET窗口由root串行授予。

## 目标

验证 live server instance 的断线重绑定、完整基线确认后继续经营，以及 cold 新 instance 的旧 token/command 隔离；用有界故障和负载脚本记录可靠性与性能。沿用现有纯 C# Host/Session/ET owner，不增加第二个模拟、主机迁移或网络 receipt 持久化。

## 依赖与证据

契约以 ../10-02-cooking-network-contract-review/design.md 的最终身份、完整 display、2026-10-03 baseline budget 补充为准。N02 源码/独立审阅和实际结果见 ../10-02-cooking-network-gameplay-loop/research/process-integration-verification.md、session-independent-review.md、authority-independent-review.md、process-runner-independent-review.md。当前 rich 流程仍在 root 验证；同机独立进程不能替代物理两 PC。

原则上 N02 全出口满足后执行 N03。若 root 明确批准，可先在已冻结 N02 source 上执行本地恢复/codec/负载工程控制；这种局部批准不改变 N02/N03 planning 或完成状态，也不解除物理 gate。启动前核对实际 source commit、工作树和串行 .NET 窗口。

## 验收范围

- live 重连先鉴权、换 generation/token、接收完整基线、精确 ack 再 Ready；随后继续手工/自动加工、份数取用、绑定/交付和自然收尾。
- cold 新 Host instance 使用可信工厂及既有 durable 成功基线；旧 instance/token/延迟 wire ID 全拒绝，新 instance 映射不能命中旧网络 receipt。不得声称跨重启 exactly-once 重放。
- 旧 scope/epoch、旧 generation/sequence、相同 stable ID 不同 payload、伪造 ack 拒绝有明确终态与零业务变更。
- caller disconnect 取消不伪造 logical execution terminal；部分 duplicate survivor 可执行；暂停不推进业务 clock，cleanup 在恢复后先于用户操作完成。
- byte/token/collection、pending/outbox/receipt 有界；过载与 authority unavailable 明确拒绝，不裁剪真实历史来制造 pass。
- 分别记录 InProcess、同机 UDP 进程、物理 LAN；性能门槛未批准时仅报告测量，不能判性能达标。

## 不包含

Unity 场景、断网期间客户端预测权威、Host migration、旧网络映射 durable 化、跨 instance 自动重放和新增经营失败规则。
