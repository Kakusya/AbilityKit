# Cooking 单机与联网执行记录

## 当前目标

2026-10-02 owner 在最终规划汇总后明确要求：列 Task，按实际情况用 worktree 并行实施，检测验证，通过后回并 master；先单机和联网，Unity 后做，活用 Orca。该回复是对上一轮 plan 的实施授权；此前助手获授权补足未提细节。当前不再受初版 planning-only 限制。

完整范围为 S01–S14 的基础单机玩法、87 款候选内容分批导入与闭环、N01–N03 的共享网络链路/恢复测量；S15 只是后置扩展参考审议。U01/U02 未授权。不能只做容易通过的小片段便标整个目标完成。

## Task 与并行波次

| 线 | Task | 工作边界与顺序 | 状态 |
|---|---|---|---|
| 厨房核心 | S01→S02→S03，之后 S05 | 同一 worktree 单 owner；所有命令/DTO/canonical/checkpoint 和 ET 指纹协调修改 | 研究完成，待启动 Orca worker |
| 菜单内容 | S04，再 S09–S13 | 独立 worktree；先完整源映射与依赖审计，核心契约合入后导入内容与校验 | 待启动 Orca worker |
| 网络 | N01，再 N02→N03 | 独立 worktree；先对齐已确认 LiteNet/通用 Transport 边界，再接入新增玩法及恢复测量 | 待启动 Orca worker |
| 单机经营整合 | S06/S07/S08/S14 | 协调会话 worktree；依赖核心和菜单，逐批整合，不建立平行 authority | 待前置 |

S04/N01 的只读研究及相互独立设计工作可以前置，生产整合仍遵守依赖。后续 Task 的 ready 不以文档存在判断，必须确认前置实际验证。

## 核心设计收敛

- S01：既有 RecipeSimulation 拥有逻辑 pose；地图锚点把 World/Station ID 映射连续位置，固定 Tick 命令移动/朝向，障碍和角色碰撞；preview 只读，所有实际拿放/容器/加工/交单再次使用同一几何验证。无空间配置的历史 fixture 保持旧语义，配置存在时缺锚点拒绝，不能任意可达。
- S02：配方分 Automatic/Manual；Manual 进程最多一个 active worker，停止/离开暂停保留，另一玩家继续；设备仍自动，端锅继续。legacy 多 tick 不能绕过 manual。success handoff 保留进度但清 worker；同 Level 恢复保留工作状态。
- S03：默认产量1，批量输出显式份数；新增 ServePortion、ClearContents、DiscardItem 走同一提交 lane。保留 Pour 的整体转移语义；按份取用守恒、重复不生成第二份、容器清空不销毁。完成批次拒绝改料/重启；目标空间和 ID allocator 在提交前完整验证。
- 核心 owner 统一协调 schema/checkpoint 新字段及版本，只升一次，显式拒绝不支持旧格式；既有默认一份/自动工序测试继续成立。ET 手写 CookingCommandFingerprint 必须覆盖新增 payload，网络 DTO 必须 round-trip。
- 核心三个任务改现有 owner，允许拆为同一 partial class 的文件，不另造未接入新系统。菜单映射不是生产实现，网络研究不是实际 LAN 通过。

## 已读取环境证据

- 根工作树 master，开始时仅保留上一轮规划变更；初始 HEAD 9bfdebc6e5ed423fa96c5bf48bb79e1001e08efd。
- `orca status` app/runtime/graph ready；AbilityKit 已注册。优先 Orca 受管工作树、监督 worker、消息与清理生命周期。
- `orca host list` 只有 local，`orca environment list` 为空。当前无法证明两台物理 PC LAN；本机实际 UDP、独立进程、恢复/测量可执行，但不能替代双机验收。
- .NET SDK 实际 10.0.300，未有 global.json 固定 SDK 的主张。

## 合并门

各 worker 独立提交 feature 分支；主会话审核 diff 与范围，执行相应 cooking-kitchen-loop/cooking-et-level-runtime，网络/通用包变化附加 runtime-contracts 和必要协议检查。只合并测试已通过的增量；合并后再跑覆盖新增整合行为的验证。master 不直接接收未审查或混入用户改动的代码。不推远端。

Orca worker/dispatch、worktree 路径、提交 SHA、实际测试结果及合并 SHA 后续按真实回执追加。超时不等于 worker 退出；同一活跃 dispatch 持续观察，不重复启动。
