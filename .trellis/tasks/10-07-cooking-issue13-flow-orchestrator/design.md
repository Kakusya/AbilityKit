# 技术规划输入（S0；待 dot 返回 API 设计）

这份文件是规划输入，不是 Accepted 实施设计。唯一技术规划者为 dot；主会话盘点真实来源并检查范围，不代补缺失 API。见 [来源盘点](research/source-inventory.md)、[规则卡](research/rule-categories.md)、[首个 Flow](research/first-flow.md)。

## 边界和候选复用

普通 C# 固定函数/构建器表达顺序、并发、barrier、有界条件等待、固定循环与分支；JSON 只选择受支持 Flow 和参数。不加载任意脚本。ET host 是领域状态的现有权威；offline 仅 TryEnqueue→owner Tick→disposition，network 仅 client SendCommandAsync→v3 transport/session→ProcessOwnerFrame→同一 ET host。

建议 Cooking 专用小 CLI 项目，不修改现有 RichRunner 的长恢复用例/默认命令。原因：现有 runner 紧耦合恢复切点、10分钟场景、成对证据和诊断，不能简单改一个 case 就承担通用拒绝/竞争/规则审批/归位报告。复用它已有 session/owner 生命周期模式，优先 Compile Include 现有 SingleThreadOwner（不复制机制）；dot 须判断是否适合链接或是否需要更小组合。现有 Rich planner 公有 Go/WaitUntil 可借鉴/复用，但 Dispatch 强制 Accepted、Pick 私有，不能用它断言竞争失败方。

拟新增文件类别（准确 API/文件拆分待 dot）：

| 候选路径 | Cooking 消费者 / 必要性 / 验收 |
|---|---|
| `src/AbilityKit.Game.Cooking.FlowAcceptance/AbilityKit.Game.Cooking.FlowAcceptance.csproj` | CLI；只引用现有 Cooking/EtRuntime/LiteNet 项目、BCL，不变 TFM/版本；构建闭包检查 |
| 同目录 `Program.cs`, `FlowContracts.cs`, `FixedFlows.cs` | 请求验证、固定流程、运行终态；invalid/normal/timeout 入口 |
| 同目录 `OfflineFlowAdapter.cs`, `NetworkFlowAdapter.cs` | 正式操作与只读观察；竞争真实业务终态、真实 transport、owner/thread/generation 控制 |
| 同目录 `FlowEventCollector.cs`, `FlowRuleEvaluator.cs`, `FlowReport.cs` | 单 writer、有界证据、批准规则、报告/失败包；故意违规/丢事件/写失败控制 |
| 同目录 `FlowResourceScope.cs` | 只释放本次资源；失败先快照、取消/退出有界、连续两 run 不残留 |
| `src/AbilityKit.ET.Runtime.Tests/CookingFixedFlowTests.cs` | 聚焦 orchestration 正负控制；不扩其他示例测试 |
| `Docs/design/CookingGame/testing/fixed-flow.md` 与规则/请求样例 | Cooking 操作者；可执行命令、审批与支持范围 |

S0 当前只能修改本 task 内规划/研究/context/evidence。以上是未来候选，任何 shared/API 变化另列必要性，不能由读源码推导授权。

## 请求/结果与失败语义输入

Request 最小项：requestId、flowId/version、offline/network、fixture/角色/初始目标、approved ruleSetId/version、seed（适用时）、步骤/整体/归位预算、输出目录与日志上限。编排者分配独立 runId/attemptId；不覆盖已有目录。执行、规则判定、证据完整性、归位与整体状态分开；ProductFailure/HarnessOrEnvironmentFailure/Finding 区分。已确认业务失败优先保留；没有 approved 规则不整体 Passed。

结果采用一个版本化契约，临时文件完整关闭后原子发布 result.json，只声明正常进程交付，不证明断电事务。故障窗口、退出码、缺失/未知字段处理、必要事件背压、跨 host 因果关联与预算范围由 dot 给具体 API/DTO 与行为。按两 PC NOT_VERIFIED 边界，network 首版为同机实际网络。

## 必须由 dot 决定

1. 具体 C# 接口/类型/签名、输入输出、existing/new/changed 标记、错误/取消与调用示例。
2. offline/network 的统一操作提交与分离观察契约；拒绝/Execute/ACK/投影不可合并。
3. 初始化最小竞争 fixture，合法距离/能力、同一 item version；不引入菜单生产/新增玩法前置。
4. 子进程控制通道、唯一 Tick/thread、continuation/代次/scope、single-writer 背压/丢事件失败与归位。
5. 具体文件边界、优先 S1、S2/S3 停点、必要正负控制与目标预算。短 Flow 编译后几十秒至两分钟是设计目标，S0 尚无实测承诺。

只有完整且当前 SHA 的 dot 设计，才能填入 Accepted 技术部分；只有 Owner 类别与实施计划批准，才能进入实现。
