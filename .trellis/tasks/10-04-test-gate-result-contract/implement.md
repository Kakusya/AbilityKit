# 实施顺序与验收矩阵（未执行）

## 设计门

- [x] Owner 授权开始 #6；干净最新 master 的独立 Orca worktree；读取最新 Issue/#5 接受合同。
- [x] 保存 plan、JSON schema/sample、源码数量/hash、全部脚本兼容表。
- [ ] Dot 明确接受 design 第 6 节及计划；此前不启动运行器实现。

## 批准后

1. 隔离 fixture 在修复前复现 skip→Passed，保存真实 red；目录在 temp 或指定 ignored run 下，不污染真实 local/Logs，不伪造历史。
2. 在现有入口引入 result context/schema validator、规范化覆盖和身份；先迁移 dotnet/脚本/Unity/嵌套 gate，再统一 summary/console/exit。
3. 18 脚本逐个兼容，实际参数传镜像工程，保留已有 TRX/XML 断言；更新唯一配置和正式测试规范。Dot 拒绝的扩大路径先停止审议。
4. 使用可注入 tool probe/process executor 执行完整 fixture；stub 仅用于隔离 runner 控制，不证明真实编译/测试/Unity 通过。不增加依赖或默认安装 Pester；复用现有 PowerShell 自测风格，失败 throw/nonzero，保存计数和结果。
5. 根串行验证窗口内执行适用真实 .NET gate（候选 runtime-contracts，按实际工具/范围核对）；Unity mirror 仅在实际前提可验证且批准时运行，否则真实 Blocked/Skipped。此处是计划命令，不是已运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate runtime-contracts
```

6. 对照所有 kind/脚本完成记录与正式文档/schema，提交 final SHA；Dot 独立复核后才完成/归档。不自动合并、不启动 #1–4/7–9。

## 必需自动控制

| 场景 | 应有结果 |
| --- | --- |
| 本次有效结果+真实成功 fixture+完整 required coverage | Passed/0 |
| 缺 Unity required / optional | step 非 Passed；父 Blocked/2 / .NET 范围 Passed 且保留 Skipped |
| 工具或进程不可启动 | Blocked/2，原因明确，后续 NotRun |
| 编译失败、超时、已启动后取消 | Failed/1，原始退出码保留，后续 NotRun |
| exit0 缺 result / 无效 JSON / 缺字段或错误覆盖 | Failed/1 |
| 空产物 / 无效 XML/TRX / 声明测试却零 / TRX失败而 exit0 | Failed/1；纯 build/文档 N/A 不被零测试误伤 |
| 旧目录有效报告 / 不同 SHA或runId / 覆写DLL / 来源中途变化 | Failed/1；缺 NoBuild manifest 为 Blocked/2 |
| child Blocked/Failed/required coverage缺失 | 父不能 Passed |
| StepName 聚焦 | 未选 NotRun，完整 gate 未接受；选中步骤单独成功 |
| NoBuild/NoRestore 缺/正确/不匹配 provenance | 缺前提 Blocked，匹配方可继续，不匹配不能接受 |
| 日志恢复XML指向旧目录、同名并发run、重复嵌套gate | 不能串用身份/产物 |
| optional 实际失败、optional 全未跑且无完整覆盖 | 不吞失败；全未跑 Skipped |
| 所有生产 kind 与18脚本 | 无 exit0-only 成功旁路；旧独立CLI兼容 |

每项写实际命令、工具版本、测试/断言数、runId、source SHA/dirty、退出码、原日志、产物manifest/hash。保持原失败及缺环境，不把新 clone 或合格 fixture 回写为历史通过。
