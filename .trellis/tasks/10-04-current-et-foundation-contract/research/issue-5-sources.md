### ET sources

**证据索引 领域与当前 ET**

除 G6 补充差异外，仓库源码链接固定到 a2cd7284e12d50a10bbb7abee6fc265577b9aa7c。行号与文件名用于定位本次判断，不保证未来 master 保持同样内容。

#### E1

**提炼来源与当前编译闭包**

[runtime csproj](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.ET.Runtime/AbilityKit.ET.Runtime.csproj) · [来源 notice](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/com.abilitykit.et.runtime/THIRD-PARTY-NOTICES.md#L32-L40) · [历史提炼记录](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/.trellis/tasks/archive/2026-09/09-17-cooking-et-runtime-roadmap/research/validation.md#L89-L120) · [core 包版本](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Demo.ET.Share/src/cn.etetet.core%403.0.3/package.json)

提炼内核与完整 vendor 必须分开。当前文件计数 82；core@3.0.3/sourcegenerator@3.0.1 是包版本，Cooking 提炼包为 0.1.0。

#### E2

**真实状态 owner 与 ET 权威入口**

[Host 树与引用](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking.EtRuntime/CookingLevelEtHost.cs#L254-L524) · [Simulation 状态](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking/CookingRecipeLoop.cs#L450-L490) · [AuthorityAdapter](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking.EtRuntime/CookingNetworkAuthorityAdapter.cs#L1-L95)

支撑 Phase A 判断与首切片范围。Adapter 复用现有 host，不另建模拟/时钟；Simulation 仍有主要领域 mutable stores。

#### E3

**目标领域树与运行机制**

[ET 参考树](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Docs/design/CookingGame/reference/et-entity-tree.md#L463-L615) · [EtRuntimeHost](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/com.abilitykit.et.runtime/Runtime/EtRuntimeHost.cs#L8-L142) · [EventSystem](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/com.abilitykit.et.runtime/Runtime/Core/World/EventSystem/EventSystem.cs#L26-L160) · [事件异常语义](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/com.abilitykit.et.runtime/Runtime/Core/World/EventSystem/IEvent.cs#L20-L32)

参考树是目标；不能当已实现。当前宿主单 active/owner；EventSystem 按类型/SceneType 分派，不自动提供事务或房间实例隔离。

#### E4

**保留边界与其他消费者**

[Demo 完整依赖](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Demo.ET.Share/AbilityKit.Demo.ET.Share.csproj#L29-L72) · [旧 EtBridge](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking.EtBridge/CookingEtBridge.cs#L1-L71) · [Projectile 消费者](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Combat.Projectile/AbilityKit.Combat.Projectile.csproj#L20-L28) · [Orleans Host](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Server/Orleans/src/AbilityKit.Orleans.Host/AbilityKit.Orleans.Host.csproj#L10-L20)

不同 runtime provider 与示例链并存不等于同一产品同时加载；删除前需要反向依赖清单。

#### E5

**容易被重构改变的语义**

[命令后 fixed-step 失败测试](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.ET.Runtime.Tests/CookingLevelEtHostTests.cs#L575-L595) · [Host 提交顺序](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking.EtRuntime/CookingLevelEtHost.cs#L1720-L1803) · [FiberManager.Create](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Demo.ET.Share/src/cn.etetet.core%403.0.3/Scripts/Core/Share/World/Fiber/FiberManager.cs#L64-L108) · [现有关系 analyzer](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.ET.RelationAnalyzer/EtRelationAnalyzer.cs#L32-L94)

测试源码不是本轮运行结果。Fiber 初始化未终结 Task 是静态路径风险，当前 Cooking 不使用该路径。

#### E6

**架构决定与稳定工程入口**

[ADR 0002](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/ADR/decisions/0002-authoritative-fixed-tick-state-sync.md) · [ADR 0003](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/ADR/decisions/0003-cooking-et-runtime-direction.md) · [AGENTS](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/AGENTS.md) · [当前状态入口](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Docs/design/CookingGame/progress.md)

ADR 0003 尚为 Proposed；新方向需显式审议。AGENTS 的历史执行通知不能代替当前状态和最新授权。

### Network sources

**证据索引 协议与网络**

#### N1

**当前 Cooking 编码与调用链**

[WireCodec](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking/Session/CookingNetworkWireCodec.cs#L28-L158) · [SessionClient](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking/Session/CookingNetworkSessionClient.cs#L27-L114) · [实际装配入口](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking.NetworkAcceptance/Program.cs#L54-L64) · [Authority capture](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking/CookingNetworkAuthority.cs#L38-L74)

JSON v3、严格输入验证、Freeze、完整 State+Session capture；源码可达不等于正式产品部署已验收。

#### N2

**88 条 catalog 与 ET proto 的真实用途**

[已提交 manifest](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Protocols/Generated/protocol-manifest.json) · [ET Proto2CS](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Demo.ET.Share/src/cn.etetet.proto@3.0.2/DotNet~/Proto2CS.cs#L77-L145) · [ET MessageSerializeHelper](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Demo.ET.Share/src/cn.etetet.core@3.0.3/Scripts/Core/Share/Network/MessageSerializeHelper.cs#L8-L41) · [Orleans grain DTO](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Server/Orleans/src/AbilityKit.Orleans.Contracts/FrameSync/FrameSyncModels.cs#L1-L22)

75 MemoryPack / 13 custom-binary / 0 protobuf；ET .proto 输出 MemoryPack；Orleans grain serializer 与外部 wire 分开。

#### N3

**protobuf 可选 backend 与测试边界**

[CLI 导出入口](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/tools/AbilityKit.Protocol.CatalogCompiler/EditorWorkflowCommands.cs#L329-L379) · [backend 类型映射](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/tools/AbilityKit.Protocol.CatalogCompiler/Emit/ProtobufProtocolBackend.cs#L28-L126) · [导出测试](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Protocol.CatalogCompiler.Tests/ProtocolBackendTests.cs#L60-L168) · [schema scalar](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Protocols/wire-schema.schema.json)

package/type/import/uint8 是源码可见缺口；没有运行 protoc，不报告本轮编译失败。

#### N4

**入站预算与安全信道边界**

[Host ingress](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking/Session/CookingNetworkSessionHost.cs#L98-L122) · [Owner kind 拒绝](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking/Session/CookingNetworkSessionHost.cs#L213-L237) · [LiteNet transport](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/com.abilitykit.network.transport.litenet/Runtime/Transport/LiteNetTransport.cs#L22-L86) · [连接 listener](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/com.abilitykit.network.transport.litenet/Runtime/Transport/LiteNetChannelListener.cs#L35-L48)

先解析后判方向是待收紧路径；已有边界，未做攻击复现。ReliableOrdered 与无密钥摘要不构成加密身份信道。

#### N5

**帧结构和旧路径**

[NetworkPacketHeader](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/com.abilitykit.network.runtime/Runtime/Network/Protocol/NetworkPacketHeader.cs#L6-L49) · [NetworkFrameCodec](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/com.abilitykit.network.runtime/Runtime/Network/Protocol/NetworkFrameCodec.cs#L8-L57) · [旧 UDP codec](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking.Udp/CookingUdpProtocol.cs#L54-L95) · [旧 harness](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking.UdpHarness/Program.cs#L25-L35)

旧 UDP v1/1200-byte fixture 仍有实际引用。先迁覆盖再退役，不能直接标为死代码。

#### O1

**官方序列化约束**

[Protobuf proto3](https://protobuf.dev/programming-guides/proto3/) · [分帧说明](https://protobuf.dev/programming-guides/techniques/) · [非 canonical 说明](https://protobuf.dev/programming-guides/serialization-not-canonical/) · [MemoryPack 版本演进](https://github.com/Cysharp/MemoryPack#version-tolerant)

官方规则用于评估候选，不证明旧 vendor 已具备所有现行功能。字段号、presence、schema 演进与 canonical hash 均要有明确合同。

### Governance sources

**证据索引 治理与任务**

#### G1

**门禁与 AGENTS 的事实缺口**

[test-gates 配置](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/tools/test-gates.json) · [Unity skip 分支](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/tools/run-unity-compile-check.ps1#L13-L17) · [父 gate 状态](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/tools/run_test_gate.ps1#L339-L345) · [AGENTS](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/AGENTS.md)

根 workflow 缺失依据该提交的 git ls-tree/ls-files；未取得外部 CI 或远端 required checks 证据。项目/包计数是静态盘点。

#### G2

**声明版本与最终闭包**

[Unity manifest](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/manifest.json) · [UPM lock](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/packages-lock.json) · [Shooter csproj](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Protocol.Shooter/AbilityKit.Protocol.Shooter.csproj) · [Orleans Contracts](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Server/Orleans/src/AbilityKit.Orleans.Contracts/AbilityKit.Orleans.Contracts.csproj)

MemoryPack 1.10.0/1.21.0/1.21.4 是不同图的声明；未 restore。无 global.json/NuGet lock/中央版本文件由完整树扫描确认。

#### G3

**现有可复用能力**

[Luban 配置权威](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/LubanConfig/Moba/README.md) · [WorldContainer](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/com.abilitykit.world.di/Runtime/World/DI/WorldContainer.cs) · [Baseline 文件写入](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking/CookingMajorBaseline.cs#L89-L126) · [结算文件写入](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/src/AbilityKit.Game.Cooking/CookingLevelSettlementStore.cs#L68-L118)

配置生成、生命周期 DI、文件存储分别有真实语义；建议按缺口收敛，未断言已有掉电丢失或 DI 错误。

#### G4

**来源 许可与发布**

[当前 ET notice](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/com.abilitykit.et.runtime/THIRD-PARTY-NOTICES.md#L32-L40) · [当前 ET license](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/Unity/Packages/com.abilitykit.et.runtime/LICENSE.ET-Core#L1-L6) · [发布清单](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/tools/publish/release-manifest.json) · [发布脚本](https://github.com/Kakusya/AbilityKit/blob/a2cd7284e12d50a10bbb7abee6fc265577b9aa7c/tools/publish/release.js)

当前 allow-list 无 ET；内部使用限制须显式覆盖传递发布闭包。本报告不判断既有授权或法律违规。Node 两项只读检查通过不代表游戏验证通过。

#### G5

**平台一手说明**

[SDK global.json](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json) · [NuGet lock](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files) · [DI 指南](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines) · [SQLite 原子提交](https://www.sqlite.org/atomiccommit.html)

支持固定 SDK、最终应用恢复闭包、DI scope 语义和故障模型判断；SQLite 仍是需求驱动候选，不是本轮实施决定。

#### I1

**现有四项功能任务**

[#1 command366](https://github.com/Kakusya/AbilityKit/issues/1) · [#2 共享客户端](https://github.com/Kakusya/AbilityKit/issues/2) · [#3 rich 四断点](https://github.com/Kakusya/AbilityKit/issues/3) · [#4 P6 投影](https://github.com/Kakusya/AbilityKit/issues/4)

Issue 是动态链接。状态依据 2026-10-04 04:02 UTC 已确认操作：四项全部 blocked，保留原计划等待架构优先评估。源码与历史证据仍按各自固定 SHA 解释。

#### G6

**审计后仅文档变化**

[reclone handoff](https://github.com/Kakusya/AbilityKit/blob/5312c6e4bf2b612260297e2d8623aa362a9051e6/Docs/design/CookingGame/reclone-handoff-2026-10-04.md)

04:10 UTC 对比核实 master 5312c6e4 仅增加该文档 48 行，无代码或配置变化；旧源码结论适用范围不变，未新增构建或运行证据。

#### I2

**新架构合同任务**

[#5 当前 ET 架构合同与 AGENTS](https://github.com/Kakusya/AbilityKit/issues/5)

04:13 UTC 已发布并标记 orca-ready。范围是实际源码、目标树、消息、事务与 AGENTS 的文档和规则工作，不含运行时迁移；#1–#4 继续 blocked。

后续实施以 #5 的审阅结果组织：先确认 Cooking 领域权威、当前 LiteNet/Wire3、分阶段提交与门禁合同，再审批首个物品归属纵切。其他宿主清退、ET 全网络接管、格式替换、发布与升级均不由本报告自动启动。


---
