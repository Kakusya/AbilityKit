# 依赖与同步研究

2026-09-17，只读摘读。下列为有界元数据/合同盘点，不是完整 C# 引用审计，不构成可安全删除证明。路径相对仓库根目录。

## 已核对的直接依赖

- `src/AbilityKit.World.ECS/AbilityKit.World.ECS.csproj:12-27`：共享 world.ecs 源码，依赖 Core/Diagnostics。
- `src/AbilityKit.World.Entitas/AbilityKit.World.Entitas.csproj:12-25`：共享 world.entitas 源码，Core/World.DI，Entitas 1.5.0。
- ECS 直接工程消费者：Ability、Combat.Projectile、Demo.Moba.Console、Demo.Moba.Share、Game.Flow.Core、Record.MemoryPack（各自 src 同名 csproj）。Unity 元数据还涉及 base.editor、demo.moba.editor/runtime/view.runtime。删除前必须重新完整核实。
- `src/AbilityKit.World.Snapshot/AbilityKit.World.Snapshot.csproj:13-24`：Core/Host/World.DI/FrameSync/NetworkFragments。
- `src/AbilityKit.World.StateSync/AbilityKit.World.StateSync.csproj:12-24`：Core/Snapshot/MemoryPack。
- `src/AbilityKit.World.FrameSync/AbilityKit.World.FrameSync.csproj:13-27`：Core/Deterministic/World.DI/MemoryPack。
- `src/AbilityKit.Demo.ET.Share/AbilityKit.Demo.ET.Share.csproj:29-65`：框架依赖加 ET Core/Loader/Excel/MemoryPack/StateSync Compile Include，并引用 MongoDB、NLog 等；不是可直接改名的独立 Entity 内核。
- `src/AbilityKit.Demo.ET.Logic/AbilityKit.Demo.ET.Logic.csproj:20-33`：引用旧 Host/FrameSync/Snapshot/Ability/Moba 与 ET generator；不存在直接 World.ECS 引用不意味着不存在间接耦合。

Entitas 全部反向消费者尚未穷尽；不得据此记录为依赖清零。

## 同步关键证据

- `ADR/decisions/0002-authoritative-fixed-tick-state-sync.md:14-20,30-32`：固定模拟 Tick、网络线程只入队；首阶段状态同步；业务状态和 codec 由应用提供。严格 lockstep 需另证排序、随机、数值、缺输入、恢复与 hash。
- `Docs/design/07-NetworkSynchronization/01-FrameSync.md:12-27,57`：帧输入与 PreTick/PostTick 管线是复用边界，不保证业务确定性。
- `Docs/design/07-NetworkSynchronization/02-StateSync.md:98-129`：业务负责实体排序、codec/hash/写回；通用 full snapshot 元数据不等于完整世界恢复，Timestamp/hash 需检查稳定性。
- `Unity/Packages/com.abilitykit.world.snapshot/Document/SnapshotRoutingBoundary.md:4-10,23-32,44-64`：路由解码与分发，不负责连接可靠性、线程或业务组包。
- `Unity/Packages/com.abilitykit.world.statesync/Runtime/StateSync/Client/IPredictableEntity.cs:10-46,69-82`：业务提供稳定 int EntityId、状态槽与应用服务器状态的逻辑。
- `Unity/Packages/com.abilitykit.world.statesync/Runtime/StateSync/Core/IRollbackable.cs:2-8`：long EntityId、SnapshotKey、创建与还原状态；不同身份接口仍需映射。
- `src/AbilityKit.Demo.ET.Logic/Model/Driver/ETMobaBattleDriver.cs:23-45,70,170-187`：ET 组件持有 World/Host/WorldManager 与 Units 字典，Tick 转发并收集快照；证明集成存在，不证明 Cooking 导入/导出已实现。
- `src/AbilityKit.Demo.ET.Logic/Model/Driver/ETMobaBattleRuntimeDriver.cs:6-59`：薄宿主/时钟适配，不提供实体恢复。

## 结论

同步需求会影响实体选择与迁移成本，不能作为支持 ET 的独立证据。ET 为已批准目标，但必须验证稳定领域身份映射、spawn/despawn、引用重建、排序、唯一时钟、快照恢复和网络版本语义。Entitas 不自动确定性；少量实体仍可能需要查询。树形所有权与同步范围并非同一概念。
