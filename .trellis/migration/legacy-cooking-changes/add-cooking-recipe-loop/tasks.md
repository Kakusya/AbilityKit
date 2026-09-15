## 规划状态

以下任务均为未执行的实施规划，当前保持未勾选；checkbox 应在未来实施并完成对应验证后更新，不是本 change 的永久验收条件。

## 1. 领域数据与命令边界

- [ ] 1.1 在应用层建立 Recipe/Process/Appliance/Container/Order 及实例状态契约，复用阶段 1 identity/location/lifecycle seam；验证：未来 .NET contract test 可构造最小 fixture，并以 DefinitionId/InstanceId 分离配置与局内状态。
- [ ] 1.2 实现取料、开始工序、逻辑 Tick 推进和完成产物的统一验证/原子提交；验证：R01-R02 覆盖合法链路、缺输入、能力不符、未完成阈值，拒绝路径 before/after 快照相等。
- [ ] 1.3 将装盘与订单提交实现为两个独立的权威原子命令及结构化失败结果；验证：R01 断言装盘成功后有独立装盘状态可保留/拿取，R03 证明订单拒绝时只拒绝本次提交且不回滚已装盘菜品，成功提交才原子消费提交物并更新订单。
- [ ] 1.4 实现一次性命令幂等和稳定事件/结果摘要；验证：R04 对同一 command identity 重复装盘/交单断言返回已处理结果且无二次变更，对不同 identity 重交已消费菜品断言拒绝；乱序命令不得产生重复消耗、产物、事件或提交。

## 2. 数据驱动闭环验收

- [ ] 2.1 建立最小验收 fixture 与参数化 runner，确保规则不依赖正式菜谱内容；验证：R01-R05 通过，R05 增加第二 recipe/appliance 仅修改数据即可完成同一闭环，否则阻断阶段 3/P2 收口。
- [ ] 2.2 接入阶段 1 host-local/remote-in-process seam；验证：R06 两来源经过同一 handler，最终状态、事件和错误结果等价，不引入第二套规则。
- [ ] 2.3 在阶段 2 session、snapshot 和 LAN 集成证据齐全后执行联机闭环；验证：R07 由 future two-PC runner 产出双方日志与 message/snapshot trace；阶段 2 D1-D4 或 LAN 门未满足时保持 blocked，不以同机替代。

## 3. 宿主与门禁证据

- [ ] 3.1 检查应用 package、.NET Compile Include 与 Unity asmdef 的共享源码边界；验证：文件审查确认不修改通用 AbilityKit、自动生成 `.csproj` 或把规则放入 Server/Orleans。
- [ ] 3.2 在实施时确认 future .NET/Unity 测试项目与命令，执行受影响 `runtime-contracts`/`core-stability` 门禁及 `git diff --check`；验证：交付记录真实 pass 或按环境报告 skip，不把规划名称当作已存在/已通过。
- [ ] 3.3 在正式配方、订单 owner、评分/结算和失败产品语义获 owner 确认前保持 Draft / Blocked；验证：阶段出口清单链接 owner 决策与 R01-R07 证据。
