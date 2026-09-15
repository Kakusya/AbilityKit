## Draft / NOT ready to apply

> 所有任务均为规划，未执行。实现前置条件：P0 `add-cooking-interaction-foundation` 已实现并有测试/门禁证据；D1 transport、D2 入口、D3 host/disconnect、D4 benchmark targets 均由 owner 确认；host exit UI/save 语义在集成验收前解除 blocker。任一前置条件未满足时，依赖任务必须保持 blocked，不得以同机测试或规划文件存在替代证据。

## 1. 前置证据与决策门

- [ ] 1.1 收集 P0 完成、.NET/Unity 测试和受影响门禁的实际证据，核对 P0 的 identity/queue/snapshot seam 可供接入；验证：证据指针、命令输出和失败原因记录齐全；失败：缺任一实现或门禁证据则阻断 2-8，不把 P0 proposal 当完成证据。
- [ ] 1.2 完成 D1 transport spike，比较候选 TCP/UDP/库的 framing、生命周期、取消、背压、诊断、宿主成本和两 PC 可行性；验证：版本/许可证、可重复 workload、同机与两 PC 结果矩阵；失败：未获 owner 确认前不得创建 production transport adapter。
- [ ] 1.3 完成 D2 connection entry UX 决策，确认 host listen、client endpoint、错误呈现及手工 LAN 地址输入是否作为可选方案；验证：owner decision artifact 明确“可选/未批准”边界；失败：不得实现发现或把手工地址当强制产品承诺。
- [ ] 1.4 完成 D3 host/disconnect 与 exit UI/save blocker 决策；验证：集成验收所需的 host close、remote loss、cancel、Dispose、UI/save/leave 语义表；失败：保持 integration acceptance blocked，不从 socket close 推导成功流程。
- [ ] 1.5 完成 D4 benchmark workload 与阈值决策；验证：指标 schema、采样方法、p50/p95/p99/队列/错误率/内存阈值记录；失败：阈值为 `UNSET` 时不得将“looks fine”作为通过或 gate。

## 2. Transport-neutral session contract

- [ ] 2.1 建立 session descriptor、epoch、protocol/config identity、能力声明和连接生命周期状态；验证：L03/L04/L05 的 future .NET contract 能构造合法及过期输入，输出 descriptor/状态 trace；失败：未知版本、epoch 或状态跳跃被静默接受则阻断。
- [ ] 2.2 实现 session authority 的 connection-to-player binding，忽略客户端自报身份作为授权来源；验证：L02 输入/动作/断言为“foreign PlayerId/foreign session → 拒绝、before/after state 相等、结构化身份错误”，runner 为 future .NET contract，输出拒绝日志；失败：payload 身份可覆盖 binding 则阻断。
- [ ] 2.3 实现 bounded ingress 的 framing/大小/取消/队列容量/关闭/Dispose 失败分类；验证：L07 注入 malformed、oversize、queue-full、cancel、post-dispose 命令，断言无越权 mutation，future .NET contract 输出 error taxonomy、queue/dispose trace；失败：无界入队、异常泄漏或关闭后执行则阻断。
- [ ] 2.4 实现 host-local 与 remote adapter 的共同 ingress seam，不让 adapter 直接修改模拟；验证：L01 输入为两来源同批次命令，动作走同一 handler，断言状态/事件等价；future .NET contract 输出 handler-path evidence、final snapshot；失败：出现第二套规则或直接 mutation 则阻断。

## 3. Handshake and baseline/delta synchronization

- [ ] 3.1 实现协议版本、config hash、能力/策略和 session metadata 的兼容握手；验证：L03 用兼容与不兼容输入执行 handshake，成功输出 bound descriptor，失败输出稳定 compatibility report 且无 gameplay binding；失败：不兼容客户端先入局则阻断。
- [ ] 3.2 实现 baseline-before-delta 生命周期与完整 baseline 安装；验证：L04 输入合法 join、baseline、同 epoch delta，动作按阶段投递，future StateSync runner 输出 baseline/delta trace；失败：baseline 前 delta 直接重建状态则阻断。
- [ ] 3.3 实现 epoch/sequence/reference 校验、重复/旧/跳序拒绝和缺口诊断；验证：L05 输入 old epoch、旧/重复/跳序 snapshot，断言新状态不被覆盖、无重复 mutation，并输出 rejection/等待-baseline log；失败：客户端宣称 synchronized 或静默跳过缺口则阻断。
- [ ] 3.4 如需新增 cooking protocol 源，按 `Protocols/README.md` 分离 Catalog/Wire Schema 并生成派生文件；验证：源文件、manifest/生成 DTO 与 `compile-protocol-catalogs.ps1 -Check`/wire strict check 一致；失败：手改生成文件、重复 system advertisement DTO 或兼容字段无 owner 决策则阻断。

## 4. Shared authoritative command queue and dedup

- [ ] 4.1 将 P0 交互命令接入 session-scoped shared authoritative queue，并以显式模拟批次/稳定排序键执行；验证：L01、重复入队顺序测试输出相同最终 snapshot/event trace；失败：依赖接收时间、线程、字典或 transport 到达顺序则阻断。
- [ ] 4.2 实现 session/player/command identity 作用域内一次性命令去重；验证：L06 输入相同 command identity 多次，动作断言一次 mutation/一次 event，future .NET contract 输出 dedup counter；失败：重复事件或跨 session/player 错误去重则阻断。
- [ ] 4.3 在 disconnect/cancel/Dispose 时停止新 ingress，并定义已入队命令的生命周期结果；验证：L07 与 D3 状态表对照，输出 completion/cancellation/resource-unbind trace；失败：关闭后仍能提交或状态未可诊断则阻断。

## 5. Transport framing and fault-layer tests

- [ ] 5.1 为选定 stream transport 建立 framing decoder 测试，覆盖同一 byte stream 的多种 segmentation/coalescing；验证：L08 输入/动作/断言由 future .NET codec runner 执行，输出 byte vectors 与 decoded message artifact；失败：依赖 TCP packet boundary 或改变消息顺序则阻断。
- [ ] 5.2 建立应用层 fake channel fault injection，覆盖延迟、丢失、重复、乱序及恢复/unsynchronized 结果；验证：L09 输出 fault matrix、sequence/baseline outcome 和状态标记；失败：把 raw TCP packet reorder 暴露给应用或继续声称同步则阻断。
- [ ] 5.3 根据 D1 将真实 transport adapter 限定为收发/生命周期边界，禁止网络线程直接 mutation；验证：L08/L09 与 L01 的 handler-path evidence 交叉审查；失败：adapter 拥有玩法规则或绕过 queue 则阻断。

## 6. LAN integration and benchmark exits

- [ ] 6.1 建立同机多实例 host/client 集成 runner，覆盖 listen、join、handshake、baseline、command、delta、duplicate 与 disconnect；验证：L10 输入/动作/断言产出双方日志、endpoint 配置和 snapshot trace；失败：任一协议/身份/关闭失败阻断，不宣称真实 LAN。
- [ ] 6.2 执行两台物理 PC LAN 验收，记录机器、网卡、地址/防火墙前提及 host/client 角色；验证：L11 真实跨 PC 完成身份、baseline、command、delta、loss detection，产出双方日志与 message trace；失败：transport/地址/防火墙不可用时标记 P1 LAN exit blocked，不能降级为同机通过。
- [ ] 6.3 建立固定 workload benchmark，分别采集同机与两 PC 的 ingress-to-commit、decode/apply、队列深度、吞吐、p50/p95/p99、fault error rate、内存/分配；验证：L12 输出 JSON/CSV 报告并标记阈值状态；失败：无可复现数据或以“looks fine”通过则阻断。
- [ ] 6.4 D4 owner 批准阈值后才把 benchmark 转为 P1 gate，并补齐 runtime-contracts/必要 regression 入口；验证：门禁配置、future test project、命令和产物路径可审阅；失败：阈值未批准、P0 证据缺失或 host exit blocker 未解除则不得进入 P2。

## 7. Cross-host validation and handoff

- [ ] 7.1 检查 .NET Compile Include、Unity asmdef、协议生成输出与应用 package 边界一致；验证：相关文件审查及 future build/test 命令覆盖两个宿主；失败：只验证单一宿主、修改自动生成 `.csproj` 或把规则放入通用框架则阻断。
- [ ] 7.2 按影响范围执行已批准门禁并保存实际结果；验证：`runtime-contracts`、必要的 `core-stability`/`regression`、协议 check 和 Unity/两 PC 产物均明确 pass/skip 原因；失败：规划测试名称不得冒充运行结果。
- [ ] 7.3 在所有 decision gate、P0 依赖、L01-L12 成功与失败输出齐全且 blocker 清零前，保持 change Draft/NOT ready；验证：最终审查清单逐项链接 proposal/spec/design/tasks 与证据；失败：不得勾选实现完成或申请 P2。
