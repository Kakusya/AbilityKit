# 失败重开带走当前进程的解锁与煮制加速：技术设计

需求见 [prd.md](prd.md)。重开仍先按标准供应新建厨房。进度对象只在新厨房安装前套用，不写检查点。

## 决定

`CreateRetry` 增加一个可选的 `CookingMajorProgress`。不传时行为与现在相同。传入时，先用候选代际的工厂建厨房并套用进度，成功后才 `InstallGeneration`。套用做三件事：没有装修则跳过；有装修则 `MigrateStations`；然后按解锁定义 `PlaceUnlock`；最后 `UseMajorProgress`。三件事任一拒绝，新厨房关闭，候选代际不安装，失败现场保持原样。

换代不能先安装再套用。`InstallGeneration` 会拆掉当前 Level 并提交候选。套用若在那之后失败，失败现场已经不在了。

装修不在本任务里发明新工位。正式内容没有第二口灶。带未知工位的进度必须整次拒绝，避免把「重开成功」写成「装修已经换到一个不存在的工位」。

## 数据流

```text
Ended + Failed
  -> 先建候选，不安装
  -> 标准供应新建厨房
  -> progress 不为空：
       装修为空：跳过
       装修不为空：MigrateStations，拒绝则关闭厨房并返回拒绝
       每个解锁：PlaceUnlock，拒绝则关闭厨房并返回拒绝
       UseMajorProgress(同一份对象)
  -> 套用成功后才 InstallGeneration
  -> 收养成功后 DropFailedScene
  -> 不写 major.checkpoint.json
```

## 边界

| 对象 | 责任 | 不负责 |
|---|---|---|
| `CookingMajorProgress` | 记住解锁、煮制加速和已有装修 | 在重开时改自己的锁定状态 |
| 新厨房 | 摆标准供应、额外摆解锁、挂上进度 | 恢复失败现场的物品和工序 |
| 宿主 | 应用失败时不收养新厨房 | 定义失败条件，写检查点 |
| 前厅 | 收养成功后丢掉旧现场 | 把旧询问或旧碗写进新厨房 |

## 契约

- 解锁例子用内容里已有的 `bread-slice`。它的标准供应是 `world:pantry` 两份，重开会再摆 `bread-slice-unlock-1` 和 `bread-slice-unlock-2`。不新增烤箱实例，因为烤箱已经在标准供应里。
- 煮制加速不改内容目录。缩短只发生在新厨房 `StartProcess` 读取这份进度时。番茄蛋花汤的所需 tick 变成原来的一半，至少为 1。切番茄不变。
- 失败现场的 canonical 与新厨房的 canonical 不相等。新厨房没有订单、没有工序。
- 套用发生在 `InstallGeneration` 之前。这时新厨房还没 `BindLifecycleGate`，`MigrateStations` 和 `PlaceUnlock` 都看得到「没有门」，不必为了重开把 `Running` 上的迁移放行。
- 拒绝时，新厨房 `CloseLifecycle` 后丢弃，候选代际不安装。宿主仍停在失败终态，不出现两间厨房。

## 不采用

- 重开成功之后再补摆解锁。补摆失败就会留下一间已经安装、但选择没带上的厨房。
- 重开时重跑整份标准供应来「包含解锁」。解锁是额外摆放，标准供应本身不含玩家这一局里才选的定义。
- 为了让装修在重开时可观察，临时加一个 `stove-b`。那是新内容，不是这条缺口。
- 把进度写进失败检查点，换个进程再读回来。09-19 只说当前进程有效。

## 风险

- `PlaceUnlock` 遇到已经存在的 `bread-slice-unlock-1` 会拒绝。标准供应的实例是 `bread-slice-1`，两者不冲突。测试不要先手工放一个同名解锁实例。
- 若应用进度时忘了把生命周期门放回去，后续 `Created` 上的合法迁移会被卡住，或者 `Running` 上的迁移会被放行。测试要看到重开后仍是 `Created`，并且未锁定进度还能再改。
