# Cooking 生产级局域网会话与协议抽象提取实施计划 (Implement)

## 实施步骤

1. **项目依赖配置更新**
   - 检查并使 `AbilityKit.Game.Cooking` 引用必要的 `AbilityKit.Network.Transport.LiteNet` 与 `LiteNetLib`（或按解耦依赖结构配置）；
2. **提取协议层到生产项目 (`Session/CookingLanProtocol.cs`)**
   - 包含封包定义与 `CookingLanCodec`；
3. **提取 Host 会话运行时 (`Session/CookingSessionHost.cs`)**
   - 移植断线手持物自动掉落、命令幂等拦截与全量/增量快照广播逻辑；
4. **提取 Client 会话运行时 (`Session/CookingSessionClient.cs`)**
   - 移植异步命令等待、断线重连、快照投影更新；
5. **重构测试 Harness 与用例回归**
   - 清理测试项目中的重复实现，将 `LoopbackUdpTopology` 指向生产级会话运行时；
   - 运行全部测试门禁确保 206 项用例 100% 通过；
   - 记录 `check.jsonl`。
