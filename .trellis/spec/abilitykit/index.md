# AbilityKit 工程规范

## 适用范围

- 这是 Unity UPM + 纯 C#/.NET 工具库；做菜经营游戏是应用层方向，不是框架既有能力。
- `Unity/Packages/` 是共享源码主入口；相关 `src/` 工程通过 `Compile Include` 复用，Cooking 应用在 `src/AbilityKit.Game.Cooking*` 另有自有源码。修改时按真实依赖检查相关 `.csproj` 与 `.asmdef`。
- Unity 仅负责场景、资源、表现和编辑器；核心规则保持纯 C#。游戏规则、房间流程和网络权威策略属于应用层，不能塞入通用 AbilityKit。

## 按需入口

- [任务与模块路由](module-routing.md)：先按 Cooking 行为或共享包路径选择规范、设计和源码，不全量加载所有模块。
- [Cooking 本机联网工具](cooking-network-tooling.md)：防火墙、共享端口配置、GetPort 与 READY 的操作说明。
- [协调审计与执行者 SOP](supervised-issue-delivery.md)：只在涉及派发、审阅、交付和工作树管理时读取。
- [历史验证命令](validation.md)：只供定位已有工具；执行前应用根 AGENTS 的 Owner 范围与本轮授权，不能直接当默认门禁。

## Pre-Development Checklist

1. 读取相关 `.trellis/spec/`、活动 task 的 `prd.md`、`design.md`、`implement.md` 及其 research。
2. 涉及架构时读取 `ADR/decisions/`、`Docs/design/`；产品方向和范围空白读取 `ADR/long-term-goals.md`。
3. 先判断 Owner 测试范围与本轮授权；禁止测试时不执行门禁。Cooking 验收仅按已指定的 FlowAcceptance 真实场景读取入口，历史验证命令不增加授权。
4. 不把路线图、任务计划或未执行测试写成已实现或已通过。
5. 多代理交付读取 [协调审计与执行者 SOP](supervised-issue-delivery.md)：职责、Issue/Trellis/PR 分工、依赖调度、审计与工作树收尾。

## Quality Check

- 不编辑 Unity 自动生成 `.csproj`、`Library/` 或 `Temp/`。
- 不因另一 Unity Editor 占用项目而删除锁文件或强制批处理。
- 报告实际执行的命令和 pass/fail/blocked/skip；缺少 Unity managed DLL 或环境不算通过。
- 任何稳定工程结论更新到相应 `.trellis/spec/`、ADR、设计文档或测试，而不复制为平行事实。
