> Ordering applicability2026-10-03: existing singleplayer deterministic ordering is preserved. Authorized network composition uses atomically assigned ingress ordinal on the same ET Host; it does not create a second simulation. [Current decision](0002-authoritative-fixed-tick-state-sync.md) and [N01 design](../../.trellis/tasks/10-02-cooking-network-contract-review/design.md). This note does not promote this historical Proposed ADR or authorize ECS deletion.

# ADR-0003：Cooking ET 主干目标与验证门槛

- 状态：Proposed
- 日期：2026-09-17
- 关联任务：`.trellis/tasks/09-17-cooking-et-runtime-roadmap/`

## 上下文

用户批准探索后以 ET Entity 树与 EntitySystem 为应用主干的目标路线，并在迁移验证后考虑退役重复 ECS。详细业务地图与未决项见关联任务 prd.md。当前 Cooking 实现、ET Demo 和 World ECS 不是同一个已迁移运行时。

本草案不修改 ADR-0001/0002：listen host、纯 C# 权威固定 Tick、首阶段状态同步/显式快照仍为基线；Cooking Unity 仍禁止。

## 拟议决策

以 ET 为应用组织主干的目标，AbilityKit 提供独立能力。先验证内核边界与 Cooking 纵切，再按消费者和测试迁移情况决定 ECS 清退批次。ET 的生命周期所有权树不得直接替代领域关系、网络身份或存档格式。

固定 Tick、输入稳定排序、原子提交、去重、状态导出/还原、引用重建和同步版本均需业务实现与验收。DTO 解耦能减少耦合，不保证迁移无需适配。ET 与 Entitas 都不自动保证严格 lockstep；少量对象不能证明无需批量查询。

## 替代方案与影响

- 保留现有 World/ECS 并收敛生命周期：现有集成代价低，但需统一多套对象组织。
- ET 管理应用、Entitas 管理独立批处理子域：若确有独立需求可评估，禁止重复权威状态。
- ET 全面替换：业务组织统一，但需付出 Host/World、生成器、同步、旧调用方的迁移成本，不能只增加外壳。

目标路线已获批准不等于技术可行性已经通过。提炼 ET 前核对源码编译闭包、许可及宿主兼容；删除 ECS 前完成完整反向依赖、验收替代与明确删除范围审阅。当前元数据盘点不等于依赖清零。

## 验证与接受条件

关联任务 research/validation.md 记录已有 Cooking 57 项测试通过，仅是现状基线。新 ET 生命周期、跨小关业务、快照恢复与继续执行、网络故障、存档重启、保留调用方回归均未验证。

待详细设计中的产品阻塞项解决、目标纵切验证和迁移影响核实后再审议本 ADR 的 Accepted 状态。不凭本草案删除 world.ecs/world.entitas。
