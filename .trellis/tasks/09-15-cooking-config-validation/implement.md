# P3 数据配置验证：实施清单

> 以下均为迁移的**未执行**规划。任务进入 `in_progress` 前必须重新审阅依赖、阻塞决策、当前代码与验证命令；不得只因文档迁移而勾选或关闭任何项目。

## 迁移前置条件

- 依赖：P2 的实际 Recipe/Process/Appliance/Level 数据类型。
- 阻塞：旧 config/snapshot 迁移策略与相关 owner 决策尚未确认。
- 规划验证：C01-C08（均为 future 规划，尚未执行）
- 实施前将 `.trellis/spec/abilitykit/index.md`、`.trellis/spec/abilitykit/validation.md` 与本任务对应 cooking spec 加入 context manifests。

## 迁移的原实施清单

## 规划状态

以下任务均为未执行的实施规划，当前保持未勾选；checkbox 应在未来实施并完成对应验证后更新，不是本 change 的永久验收条件。

## 1. 配置注册与候选批次

- [ ] 1.1 复核阶段 3 实际 Recipe/Process/Appliance/Level 数据类型并建立应用层 registry；验证：future .NET/Unity 双宿主审查确认只复用通用 ConfigDatabase，不修改通用表目录或自动生成 `.csproj`。
- [ ] 1.2 建立候选配置批次加载、唯一 ID 与必需字段检查；验证：C01 合法批次可查询且提交一次，C02 重复 ID/缺字段输出稳定诊断并保持旧版本不变。

## 2. 跨表校验与数据扩展

- [ ] 2.1 实现外键、Appliance 能力、Container/产物和 Level 引用验证；验证：C02 对每类缺失/不匹配关系输出表、记录、字段定位并阻断启动。
- [ ] 2.2 实现 Process 图的循环、断裂和不可达终点检测；验证：C03 输出可审阅的关系路径/诊断 artifact，非法批次不提交。
- [ ] 2.3 建立“仅修改数据新增 Recipe/Appliance”的参数化 fixture；验证：C04-C05 使用同一 validator 与阶段 3 loop runner 完成闭环，不改规则代码；未知能力/Process 按 C02 失败。

## 3. 配置身份与兼容门

- [ ] 3.1 实现已验证配置的稳定规范化 identity/hash，区分 DefinitionId 与 Match InstanceId；验证：C01/C08 在不同表加载顺序下 hash 相同，单字段变更可检测。
- [ ] 3.2 在阶段 2 handshake seam 可用后接入 host/client hash compatibility gate；验证：C06 一致 hash 可继续，不一致在 gameplay binding 前拒绝并产出报告；缺少阶段 2 证据时保持 blocked。
- [ ] 3.3 定义旧 config/snapshot 的拒绝与 blocked 结果，不实现未批准迁移；验证：C07 明确输出 owner 决策引用，不能静默转换。

## 4. 宿主测试与阶段出口

- [ ] 4.1 接入 future .NET config contract tests 与必要 Unity EditMode/加载 smoke；验证：C01-C05、C08 通过并保存 manifest、诊断 JSON、before/after hash 与 runner 日志。
- [ ] 4.2 在阶段 2 LAN/决策门需要时执行分层 host/client 验收；验证：C06 的 LAN 证据不以同机/in-process 替代，P1 D1-D4 仍按 owner 门检查。
- [ ] 4.3 执行实际受影响门禁（至少评估 `runtime-contracts`/`core-stability`）及 `git diff --check`；验证：交付报告记录真实 pass 或环境导致的 skip，任务仅在实施完成对应验证后更新 checkbox。
- [ ] 4.4 将 P3 交付证据指向阶段 5/P4 的准备条件；验证：未完成 config/layout 校验时 P4 任务标为 blocked，并直接引用现有后续 change 的规划文档 `../add-cooking-match-lifecycle/proposal.md`；不把后续规划当作实现证据。
