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


## 追加：第 2 步独立 ET 运行时提炼（第五轮）

范围：新建 `Unity/Packages/com.abilitykit.et.runtime/`（共享 UPM 源 + `THIRD-PARTY-NOTICES.md`、ET-Core/ET-SourceGenerator LICENSE 副本）与 `src/AbilityKit.ET.Runtime`（net10.0，Compile Include 共享源）、`src/AbilityKit.ET.Runtime.Tests`。不修改 vendored ET、Demo、Cooking、HFSM、ECS、协议或 Unity 生成工程。

### 提炼边界（实际执行）

- 复制 `cn.etetet.core@3.0.3/Scripts/Core/Share` 的 70 个文件（Entity 树、EntitySystem、Fiber/主调度、World 单例链、ETTask、日志、对象池）与 `cn.etetet.sourcegenerator@3.0.1/Runtime` 的 10 个标注文件；逐文件剥离 MongoDB/MemoryPack/CommandLineParser 的 using、`[Bson*]`/`[MemoryPack*]`/`[Option]` 标注；`Object.cs` 序列化方法裁剪为空基类；`Fiber.cs` 移除 Mailboxes/ActorId/LogInvoker，改由 Logger 取日志；未引入任何假 shim 或重写实现。
- 依赖核对：`typeof(Entity).Assembly.GetReferencedAssemblies()` 断言仅 System/netstandard；测试断言进程中无 AbilityKit.Demo 程序集。
- 许可：ET License 为限制传播许可（允许自用/任职公司使用与商用，禁止私下传播修改版；上线前需通知）。包内保留原 LICENSE 与声明文件；提炼结果标记为内部使用，不做公开发布。
- 曾生成一版重写式 EtRuntime.cs（TaskCompletionSource 伪 ETTask），经审查不符合"保留真实 ET 源码"的要求，已整体删除替换为原源码提取。

### 实际通过

- `dotnet build src/AbilityKit.ET.Runtime/AbilityKit.ET.Runtime.csproj`：exit 0，0 警告 0 错误。
- `dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj --logger "trx;LogFileName=runtime-step2.trx" --results-directory artifacts/cooking-et-roadmap/runtime`：exit 0，8/8 通过（失败: 0，通过: 8，已跳过: 0）。TRX：`artifacts/cooking-et-roadmap/runtime/runtime-step2.trx`。
- 覆盖：显式 Tick 驱动 Awake/Update/LateUpdate、销毁后不再推进、EntityRef 失效、未注册实体不入更新队列；双 Scene 队列隔离与递归销毁所有权；对象池复用 + InstanceId 代际失效与队列重挂；跨阶段实体延续与"暂停≠销毁"；宿主 Run/Tick 内 Fiber.Instance 与 SynchronizationContext 作用域切换并恢复；Shutdown/Restart 后旧回调不执行、新 Fiber 回调执行、单例静态清空、Dispose 后调用抛 ObjectDisposedException；同进程重复宿主、重复场景 id、Tick 重入、跨线程操作被拒绝；真实 ETTask `WaitFrameFinish` 在显式 Tick 帧末恢复。
- `precheck` 门禁 exit 0（P0 通过，`local/Logs/test-gates/20260917-143608-precheck`，10/10 smoke）。

### 失败与修正（记录）

- 编译迭代：缺 `/unsafe`（SortedSet）→ csproj AllowUnsafeBlocks；缺 SceneTypeSingleton → 补入；两个源文件级裁剪错误（CS0310/CS0272）来自被替换的重写稿，替换为原源码后消失。
- 测试修正：`ETTask.Coroutine()` 不存在 → 直接持有 ETTask；未标 `[EntitySystem]` 的派生系统仍被注册——经核实为 ET 原生继承式扫描语义（`UpdateSystem<T>` 基类自带标注），非提炼缺陷；测试改为断言未实现 IUpdate 的实体不入队列。

### 门禁补充

- `core-stability` exit 1：仅 `HFSM deterministic runtime contract tests` 2/47 失败（既有 DefinitionJsonTests 换行/isGhostState 问题，与本轮无关）；其余 24 步全部通过（含 Core 141、BehaviorTree 166、Network 223、World StateSync/FrameSync、Host、Analyzer、Triggering 等）。本轮不记该门禁为通过，HFSM 修复建议单独立项。

### 结论限制

- 第 2 步验收成立：独立于 Demo（无 DemoEntry/反射 Fiber/Share 引用）、无外部 NuGet/项目依赖、单进程单宿主、显式 Tick、显式销毁与重启均有测试证据。
- 未验证：Unity 侧编译（本机无 Unity Editor，`run-unity-compile-check.ps1` 会跳过且不算通过）、asmdef 引用联动、Cooking 纵切、多宿主进程、并发宿主访问（宿主强制 owner-thread）。
- 已知限制：单进程仅一个宿主（World/FiberManager 静态单例决定，测试已断言拒绝第二个宿主）；ET License 限制包外传播，仅供内部使用。

## 追加：最小 Cooking ET Tick 命令接点（第六轮）

提交：`38d822271 feat(cooking): dispatch recipe commands through ET tick`。

### 实际实现

- 新增纯 .NET 应用项目 `src/AbilityKit.Game.Cooking.EtRuntime`，引用独立 ET runtime 与既有 Cooking 领域项目；通用 ET runtime 不反向引用 Cooking。
- `CookingRecipeTickHost.Enqueue` 只把 `CookingRecipeCommand` 放入 owner-thread FIFO 队列，不直接改变权威模拟。
- ET Scene 中注册的 `CookingRecipeDriverUpdate : UpdateSystem<CookingRecipeDriver>` 在显式 `Tick` 内串行调用 `CookingRecipeSimulation.Submit`。
- ET 系统层会捕获异常，因此 host 显式保存 authority failure，并由 `Tick` 抛出 faulted 错误，避免错误报告成功。
- 回归覆盖拾取、开始加工、推进三 Tick、产物生成、装盘、提交订单、重复命令幂等、空 Tick 无结果与 Dispose 后拒绝调用。

### 红绿与最终验证

- 红测：临时空实现的 `Tick()` 返回空集合，精确测试 `Et_tick_drains_recipe_commands_through_the_authoritative_simulation` 按预期失败：`Assert.Single() Failure: The collection was empty`。证据：`artifacts/cooking-et-roadmap/step3/recipe-et-red.trx`。
- 真实 `UpdateSystem` 调度实现后，同一精确测试连续三次通过；收窄掉无关 Pickup 冒烟后再次通过；提交后 `--no-build` 再次通过。最终证据：`artifacts/cooking-et-roadmap/step3/recipe-et-final-focus.trx`。
- `dotnet build src/AbilityKit.Game.Cooking.EtRuntime/AbilityKit.Game.Cooking.EtRuntime.csproj --no-incremental`：exit 0，0 警告，0 错误。
- `dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj`：exit 0，9/9 通过。证据：`artifacts/cooking-et-roadmap/step3/runtime-single-fix-final.trx`。
- `powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-udp`：23/23 通过；这只证明既有 UDP 回归，没有证明 ET 与 UDP 已接合或两物理 PC LAN。
- `powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate precheck`：exit 0；Moba console build 0 errors（32 个既有兼容/依赖告警），Moba smoke 通过。日志：`artifacts/cooking-et-roadmap/step3/precheck.log`，门禁目录 `local/Logs/test-gates/20260917-153838-precheck`。
- 构建触碰的 Analyzer、Moba CodeGen、ET SourceGenerator 三个已跟踪 DLL 已恢复，未进入提交。

### 结论限制

本轮只完成独立 ET runtime 与最小 Cooking 权威命令 Tick 调度接点。完整阶段三仍缺 MatchLifecycle 编排、自动单时钟加工、UDP host/remote 身份绑定、checkpoint 重建继续、成功延续、失败标准供应、升级进度迁移和成功结算持久化；阶段四 ECS 清退未开始。Unity Editor 编译、同机 ET 跨进程、两物理 PC LAN 和 durable storage 均未验证，不记为通过。
