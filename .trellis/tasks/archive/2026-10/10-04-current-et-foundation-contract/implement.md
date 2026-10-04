# 实施与检查

- [x] 重读完整 Issue，保存快照；核对 SHA/dirty/remote/Orca 链接与 ADR 编号。
- [x] 落盘最小计划和冲突清单。用户要求完整执行已批准 Issue，无新增产品决策。
- [x] 按真实源码闭包完成 A–D 合同与 E 检查映射。
- [x] 原通知无损归档，精简 AGENTS，更新 progress。
- [x] 独立 Trellis check 审阅全部条款，修正遗漏。
- [x] 实际运行 UTF-8/链接/Markdown/范围检查与 git diff --check，保存结果。
- [x] 提交前重读 issue 标签/依赖，按权限提交并回报 commit/PR，不自行合并。

.NET/Unity/游戏进程：NotRun，文档范围不新增运行验收。

## dot 首轮复审修订（2026-10-04）

授权来源：PR review 5404506672 与 Issue comment 5977054362；用户明确要求按已批准文档范围修订同一 PR，后续任务不放行。

- [x] R1：Closing 仅拒绝新身份的有限 RequestSupply；补既有批准配送关闭后仍推进/可领取正例，保留重复规则，单列无限供应路径和其他前提。
- [x] R2：零测试限制仅适用于声明应执行测试的覆盖；同步 AGENTS 与合同 E，构建/文档自身覆盖可以通过，不冒认测试通过。
- [x] R3：复现方法分别核验 BASE→reviewed HEAD 的已提交差异、本地未暂存差异、暂存差异；补跑并向 Issue 回报最终 SHA，不循环提交自身 SHA。
- [x] 独立复查、文档检查与 final committed diff 检查后提交/推送，#5 开放、PR draft、不合并、不解锁其他 Issue。
