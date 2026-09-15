# AbilityKit 工程规范

## 适用范围

- 这是 Unity UPM + 纯 C#/.NET 工具库；做菜经营游戏是应用层方向，不是框架既有能力。
- `Unity/Packages/` 是共享源码主入口；`src/` 通过 `Compile Include` 复用源码。修改时同时检查相关 `.csproj` 与 `.asmdef`。
- Unity 仅负责场景、资源、表现和编辑器；核心规则保持纯 C#。游戏规则、房间流程和网络权威策略属于应用层，不能塞入通用 AbilityKit。

## Pre-Development Checklist

1. 读取相关 `.trellis/spec/`、活动 task 的 `prd.md`、`design.md`、`implement.md` 及其 research。
2. 涉及架构时读取 `ADR/decisions/`、`Docs/design/`；产品方向和范围空白读取 `ADR/long-term-goals.md`。
3. 读取 `Docs/AbilityKit测试门禁与批量回归规范.md`，按受影响范围选择实际门禁。
4. 不把路线图、任务计划或未执行测试写成已实现或已通过。

## Quality Check

- 不编辑 Unity 自动生成 `.csproj`、`Library/` 或 `Temp/`。
- 不因另一 Unity Editor 占用项目而删除锁文件或强制批处理。
- 报告实际执行的命令和 pass/fail/blocked/skip；缺少 Unity managed DLL 或环境不算通过。
- 任何稳定工程结论更新到相应 `.trellis/spec/`、ADR、设计文档或测试，而不复制为平行事实。
