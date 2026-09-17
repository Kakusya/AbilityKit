# 实际验证记录

日期：2026-09-17。范围：规划前的既有 Cooking 领域实现基线，无产品代码改动。

## 实际通过

命令：

```text
dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj --logger "trx;LogFileName=cooking-baseline.trx" --results-directory artifacts/cooking-et-roadmap/baseline
```

退出码 0；net10.0；57 total / 57 passed / 0 failed / 0 skipped。控制台报告测试用时 321 ms。原始 TRX 已复制到同目录 `cooking-baseline.trx`，解析 Counters 同为 total=57、executed=57、passed=57、failed=0、notExecuted=0。

原运行产物：`artifacts/cooking-et-roadmap/baseline/cooking-baseline.trx`。

## 未执行

UDP gate、同机跨进程 harness、两物理 PC LAN、core-stability、runtime-contracts、regression、ET 构建/新运行时测试、Unity 编译/EditMode、持久化重启恢复均未运行。本轮仅规划，未修改上述运行时代码；Cooking Unity 仍禁止。

## 结论限制

本结果只说明现有领域测试基线通过。不能证明新增业务玩法、ET 提炼、同步适配、完整状态还原、磁盘存档或 ECS 退役通过。完整反向依赖清零也未完成。


## 追加：ET 内核生命周期验证（2026-09-17 第二轮）

新增 `src/AbilityKit.Demo.ET.Core.Tests/`（xunit，引用 Demo.ET.Share/Logic/App）。

实际执行与结果：

1. 临时 probe（已删除 /tmp/et-probe）：仅编译 ET Core Share 的 Entity+ETTask 子集失败，260 个错误；缺 MongoDB Bson、MemoryPack、StaticField、IPool/ObjectPool、GenerateType、Log 等闭包类型。证明 ET Entity 内核不是自包含的，提炼边界比"两个目录"大。
2. `dotnet test src/AbilityKit.Demo.ET.Core.Tests/AbilityKit.Demo.ET.Core.Tests.csproj` 退出码 0：1/1 通过。测试按 Program.cs/DemoEntry.Init 顺序真实启动 ET（CreateLocalSmokeDefaults + CodeTypes 扫描），经反射调用 internal Fiber 构造，验证 Scene 创建、InstanceId 生成、组件 Add/Get、Dispose 后父子递归销毁与 IsDisposed。
3. 迭代中确认的事实：Entity.RegisterSystem 强依赖 iScene.Fiber.EntitySystem 与 EntitySystemSingleton.TypeSystems（CodeTypes 程序集扫描）；Fiber 构造 internal。独立宿主不能"只拿 Entity 类"运行，需要完整单例链——与 ADR-0003 提炼前必须核对编译闭包的判断一致。

## 门禁执行记录（区别通过/失败/跳过）

- 实际通过：Cooking 领域 57/57（两次）；ET Core lifecycle 1/1。
- `regression` gate：失败于 `HFSM deterministic runtime contract tests`（2/47 失败，DefinitionJsonTests）。已用 git stash -u 复现：移除本轮全部改动后同样失败，属既有失败，与本轮工作无关。本轮未修复该门禁，也不记为通过。
- 误操作说明：stash 复原时三个生成 DLL（Analyzer.Plugin、Moba.CodeGen、ET.SourceGenerator）被构建触碰，已 `git checkout` 还原为已提交状态。
- 未运行：cooking-udp gate（未改 UDP 代码）、Unity 编译/EditMode（未改 Unity 资产与共享 UPM 源，且本机无 Editor 环境验证）、Moba/Shooter 专项门禁。

## 追加：第 1 步 System 执行与生命周期闭环（第四轮）

范围：仅扩展现有 `src/AbilityKit.ET.Share.Tests`，不修改 ET 源码、Cooking、Unity、协议或存档。

### 实际通过

- `dotnet test src/AbilityKit.ET.Share.Tests/AbilityKit.ET.Share.Tests.csproj --logger "trx;LogFileName=share-systems-step1-final.trx" --results-directory artifacts/cooking-et-roadmap/share`：exit 0，5/5 通过（含既有 2 条公开生命周期 + 3 条新测试），TRX Counters total=5 executed=5 passed=5 failed=0。
- 覆盖：Awake 注册一次；`Thread.Sleep(30)`+仅 LateUpdate 不推进 Update；`manager.Update()` 每次 Tick 恰好 +1；实体 `Dispose()` 幂等、Destroy 恰好一次、EntityRef 失效、已销毁实体不再收到后续 Update；双 Fiber 队列互不串扰、Remove 一个 Fiber 后另一个继续 Tick 且各自记录 Fiber.Id；`World.Dispose()` 后全部单例实例清空、实体 Destroy、旧引用失效；重建 World 后全新单例/队列/实体，旧实体状态不变，新 Fiber 上下文回调可执行而旧回调计数保持 0（仅证明新调度器未承接旧队列，非框架主动取消旧回调）。

### 失败与边界

- `core-stability` 门禁 exit 1：`HFSM deterministic runtime contract tests` 2/47 失败（DefinitionJsonTests：CRLF/LF 换行差异；v1 迁移 `isGhostState` 未知属性）。与第四轮改动无关、本轮未修复 HFSM；ET 聚焦测试的通过不能代表门禁通过。
- 已知测试自证限制：`Fiber.Instance` 与 SynchronizationContext 由测试宿主在 finally 中显式恢复，不据此宣称框架自动还原；旧回调非执行不能等价于取消。
- 构建再次触碰三个既有生成 DLL，已 `git checkout` 还原；规划文档本轮未改动。

### 结论

第 1 步验收成立：不依赖 DemoEntry、不反射构造 Fiber，可完成"启动—注册—显式 Tick—销毁—重启"闭环，System 按 `[EntitySystem]` 扫描注册、按宿主显式 `Update` 推进。独立内核提炼（第 2 步）、Cooking 纵切（第 3 步）与清退（第 4 步）仍未开始。

## 追加：Demo 依赖下的 Cooking 集成探针（第三轮）

本节为第三轮结果；前述“仅规划/ET 未运行”只描述第一轮。当前范围已明确收敛为 Demo 宿主集成探针，不是独立 ET 内核或完整 Cooking 迁移。第四轮见上方。

### 实际通过

- `dotnet build src/AbilityKit.Game.Cooking.EtBridge.Tests/AbilityKit.Game.Cooking.EtBridge.Tests.csproj --verbosity normal`：exit 0，0 errors，56 warnings。日志 `artifacts/cooking-et-roadmap/bridge-build-normal.log`。
- `dotnet test src/AbilityKit.Game.Cooking.EtBridge.Tests/AbilityKit.Game.Cooking.EtBridge.Tests.csproj --no-restore --verbosity normal --logger "trx;LogFileName=bridge-final.trx" --results-directory artifacts/cooking-et-roadmap/bridge`：exit 0，4/4 passed，0 skipped；TRX Counters 已解析确认。日志 `artifacts/cooking-et-roadmap/bridge-final.log`。
- 单独筛选 `FullyQualifiedName~Scene_Disposal_Recursively_Disposes_Projected_Items` 并使用 `--no-build --no-restore --logger "console;verbosity=detailed"`：1/1 passed，没有失败断言；`artifacts/cooking-et-roadmap/bridge/recursive-destroy-isolated.trx`。不据此修改 ET 内核。
- Cooking 领域 `dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj --no-restore --verbosity minimal --logger "trx;LogFileName=cooking-probe-regression.trx" --results-directory artifacts/cooking-et-roadmap/baseline`：57/57 passed。
- ET lifecycle `dotnet test src/AbilityKit.Demo.ET.Core.Tests/AbilityKit.Demo.ET.Core.Tests.csproj --no-build --no-restore --verbosity minimal --logger "trx;LogFileName=et-lifecycle-regression.trx" --results-directory artifacts/cooking-et-roadmap/baseline`：1/1 passed，使用已构建二进制。

### 失败与修正

新增探针首次编译因缺少 ET ProjectReference 失败。随后修正不存在的 CookingFixtures、Pickup 命令签名、AddChild(string) 和 EntityRef 构造/访问 API；最后构建与测试通过。期间一次 Python 读取 `/tmp` 日志路径失败属于 Windows/Git Bash 路径差异，不是测试失败，后改用仓库 artifacts 相对路径并独立核验退出码/TRX。

### 检查结论和限制

覆盖物品创建、Pickup 更新同一实体、版本更新、移除引用失效、相同快照重放不重复创建且不改变权威状态、Scene 递归销毁。ET Id 自动生成，与领域 ItemId 分离。树只承载 Scene/Registry/Item 生命周期，未实现正式餐厅/小关所有权。

探针只接受同 scope、上游已校验的顺序完整物品快照；未验证乱序/epoch/非法输入、对象池复用、多实例隔离、系统执行、固定 Tick、跨阶段加工或磁盘恢复。依赖 DemoEntry.Init 与反射 Fiber；独立内核验收未完成。项目未接入 Unity/生产装配或门禁配置。

依赖告警仍在：MongoDB.Driver 2.17.1（NU1903）、SharpCompress 0.30.1（NU1902），以及 Entitas 版本回退/旧 Framework 兼容性告警。未静默升级或抑制。本轮未再跑 regression/core-stability/runtime-contracts/cooking-udp/Unity/两机 LAN；之前 regression 的 HFSM 2/47 失败仍未解决，不能把聚焦通过当全局门禁通过。

本轮构建再次触碰三个既有生成 DLL；未擅自还原未知二进制状态，保留并在交付说明披露。未提交代码；finish-work 因任务源码未提交而阻止归档，避免脚本自动提交越权。
