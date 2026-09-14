测试 ID 的输入、断言和执行层级见 [design.md 的 Test Matrix](design.md#test-matrix)。以下均为未执行任务。

## 1. 共享纯 C# 契约与状态

- [ ] 1.1 在 `Unity/Packages/` 的应用边界内建立 Session/World/Match/Player/Item/Definition/Location/生命周期与配置 fixture 契约；验证方式：T01、T02、T10 的纯 C# 单元测试能构造同一最小 fixture，并以明确身份区分跨 session/过期实例。
- [ ] 1.2 实现 Item 唯一位置、owner、手/站点容量与有效性不变量；验证方式：T01、T04 通过，成功后仅有一个位置/所有者，容量拒绝前后权威状态相等。

## 2. 权威命令处理

- [ ] 2.1 实现 pickup/drop command envelope、session/player/command 作用域和稳定排序入口；验证方式：T06、T08 在重复运行中选出相同赢家，最终快照与事件序列一致，同一封闭模拟批次内不依赖入队顺序（不要求跨批次回滚）。
- [ ] 2.2 实现统一验证与原子提交，覆盖生命周期、资格、配置范围、当前位置、可用性和容量；验证方式：T03、T04 对每个拒绝原因断言状态结构相等且无部分事件。
- [ ] 2.3 实现一次性命令幂等记录与过期/未知 ID 拒绝；验证方式：T02、T07 断言重复命令至多提交一次，跨 session/player 同值 ID 不错误去重。

## 3. Host/Remote in-process adapters

- [ ] 3.1 添加 host-local 与 remote-in-process adapter seam，二者只投递同一 command handler，不直接写状态；验证方式：T05 通过可观察 handler seam/调用计数证明两者共用路径，结果和事件等价。
- [ ] 3.2 为两消费者争抢、重复、重排和失败场景补充集成 fixture；验证方式：T05-T08 全部通过，并保存可审阅的测试输出；不得引入真实网络端口或传输库。

## 4. Unity projection 与最小 fixture

- [ ] 4.1 建立只读权威快照到 Unity 表现对象的 projection，处理版本水位、未知/旧输入和纠正；验证方式：T09 EditMode 测试断言旧/未知输入不覆盖新视图且不改变 authority。
- [ ] 4.2 建立最小 scene/config fixture（一个 movable item、hand、station slot、两名逻辑玩家、显式容量/资格/范围）；验证方式：T10 EditMode scene smoke 能加载并解析 fixture，且不依赖公共网络、存档或完整配方。
- [ ] 4.3 检查 Unity asmdef 与 .NET Compile Include 同时消费共享源码；验证方式：文件路径和引用审查通过，随后按影响范围运行计划中的 Unity 编译/EditMode 与 .NET 测试；不得修改自动生成 `.csproj`。

## 5. 测试接入与交付门禁

- [ ] 5.1 将 T01-T08 接入实施时确认的 .NET 测试项目，补齐可重复 fixture、稳定排序和 mutation-free 状态比较；验证方式：相关 `dotnet test` 全绿并输出测试结果；项目路径当前仅为 future placeholder，不得提前宣称存在。
- [ ] 5.2 将 T09-T10 接入实施时确认的 Unity EditMode 测试程序集和 fixture 场景；验证方式：Unity EditMode/scene smoke 全绿并保存命令、日志、XML 产物；Editor 占用时按 AGENTS 规则跳过并报告原因。
- [ ] 5.3 根据实际受影响程序集选择并运行现有门禁（至少评估 `core-stability`/`runtime-contracts`，必要时 `regression`），同时执行 `git diff --check`；验证方式：实际命令、结果或跳过原因记录在交付报告中。本规划阶段不运行构建/测试。
