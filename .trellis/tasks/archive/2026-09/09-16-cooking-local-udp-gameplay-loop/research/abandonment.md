# 任务放弃记录：Cooking 同机 UDP 最小经营闭环

> 性质：owner 决定记录，不是完成证明，也不是能力删除声明。

## 1. 决定

2026-09-21，owner 决定放弃本任务方向：当前工作范围收敛为**单机内容**，传输层将改用 KCP 组件，同机 UDP 经营闭环不再继续推进。

放弃的直接原因：

- 当前产品与工程范围是单机玩法，本任务的 multiplayer UDP 权威闭环不在当前范围内；
- owner 对现有 UDP 内容不满意，计划引入 KCP 组件作为后续传输方向；
- 后续领域改造（容器即物品、多输入配方、两种加工完成形态、七项动作）会改变 `CookingRecipeSimulation` 的命令与配置契约，继续在旧契约上做 UDP 闭环会产生返工。

## 2. 本任务已验证的内容（历史证据仍然有效）

`check.jsonl` 记录的全部通过证据只修饰当时的受限交付，不因放弃而失效，也不得被解读为当前范围的能力：

- `AbilityKit.Game.Cooking.Udp.Tests` 23/23 通过（typed recipe loopback、畸形命令拒绝、角色隔离 JSONL 验收、负向契约）；
- `tools/run_test_gate.ps1 -Gate cooking-udp` 通过（P1）；
- 同机独立 host/client harness 运行通过，两端状态哈希收敛，artifact 见 `artifacts/cooking-udp-final-proof/`；
- 两台物理 PC LAN 明确未运行，从未被宣称。

## 3. 放弃之后的状态

- Cooking 的三个 UDP 项目（`AbilityKit.Game.Cooking.Udp`、`.UdpHarness`、`.Udp.Tests`）退出当前构建与门禁范围；源码与上述 artifacts **保留不删**；
- `cooking-udp` gate 从 `tools/test-gates.json` 退役，门禁文档同步更新；
- `Docs/design/CookingGame/progress.md` 的表述改为“退出当前构建范围”，不宣称删除，也不宣称从未存在；
- 已归档任务 `09-16-cooking-udp-lan-harness` 等历史证据不受影响。

## 4. 不成立的理解

- 不认为 UDP 能力已被删除或验证记录失效；
- 不认为本放弃授权任何 KCP 实现；KCP 接入是后续独立任务，必须重新审议传输契约、身份、快照与门禁；
- 不从本记录推导多人联机玩法已被确认或取消。

## 5. 后续入口

- 单机领域改造任务见新建的实现任务（厨房闭环契约层 → 仿真规则与闭环 fixture → 单机验收）；
- KCP 传输方向需在单机闭环稳定后另行立项。
