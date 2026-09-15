# AbilityKit 验证规范

## 命令入口

- 默认门禁：`powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1`；用 `-List` 查看可用 gate，按范围选择 `-Gate core-stability` 或 `-Gate runtime-contracts`。配置权威是 `tools/test-gates.json`。
- 聚焦构建：`dotnet build src/AbilityKit.Demo.Moba.Console/AbilityKit.Demo.Moba.Console.csproj`。
- Unity 编译辅助：`powershell -ExecutionPolicy Bypass -File tools/run-unity-compile-check.ps1`。
- EditMode：`powershell -ExecutionPolicy Bypass -File tools/run-unity-editmode-tests.ps1 -TestAssembly AbilityKit.Ability.Editor.Tests`。
- 协议 catalog：`powershell -ExecutionPolicy Bypass -File tools/compile-protocol-catalogs.ps1 -Check`。
- wire：`powershell -ExecutionPolicy Bypass -File tools/export-protocol-wire.ps1 -Projects shooter,moba -Check -Strict`。

## 记录规则

Trellis check 仅编排与记录检查，不能替代命令的成功判定。每项任务的 `check.jsonl`、检查日志或交付说明必须分开记录：已运行并通过、已运行但失败、受阻、跳过以及原因。
