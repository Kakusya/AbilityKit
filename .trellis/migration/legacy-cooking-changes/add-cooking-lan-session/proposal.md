## Draft / NOT ready to apply

> 本提案仅为 P1 规划草案；在以下决策门与前置证据完成前，不得进入实施：P0 `add-cooking-interaction-foundation` 已实现且相关测试/门禁有证据；D1 transport spike 与 owner 确认；D2 连接入口 UX；D3 host/disconnect 语义；D4 benchmark 目标；host exit UI/save 语义在集成验收前解除 blocker。4/4 规划文件存在不代表可实施。

## Why

P0 只建立不依赖真实网络的交互与 host/remote in-process seam；近期路线需要把同一权威命令路径扩展到真正的 LAN listen host 与远端客户端。现在需要先形成可审查的会话、身份、兼容握手、baseline/delta 和失败边界，避免把传输选择、客户端自报身份或未决定的退出/重连行为偷偷固化到实现中。

## What Changes

- 新增 LAN listen session 规划：host 同时承担服务端和本地玩家，真实远端客户端加入同一局；同机多实例只能作为辅助，P1 必须有两台 PC LAN 出口。
- 规划共享权威 command queue：本地 host 与远端命令走同一验证/稳定排序/原子提交路径；一次性命令按 session-scoped dedup 只产生一次 mutation。
- 规划 session-scoped identity binding：连接加入后由 host 分配/绑定身份，绝不信任客户端提交的 `PlayerId`；未绑定、越权、跨 session 身份均拒绝。
- 规划 protocol/config compatibility handshake、session baseline 后再应用 delta，以及带 `session epoch`/snapshot sequence 的旧会话与旧快照拒绝。
- 规划受控 ingress：结构化错误、取消、关闭/Dispose、边界 payload/队列行为可诊断；丢失 transport 时不得声称仍同步，也不隐含自动重连、主机迁移或 host exit 产品流程。
- D1 保持 TCP/UDP（及候选库）的 transport spike 为显式 owner 决策；生产实现不得在决策前开始。测试按层覆盖 TCP 字节分段/合并/顺序与应用层 fault injection，不宣称应用能观察原始 TCP 包重排。
- 规划同机多实例与真实两 PC LAN 的分层验收、成功/失败测试矩阵及性能指标采集；性能阈值保持 unset，必须先作 D4 决策，不以“看起来正常”作为通过。

## Capabilities

### New Capabilities
- `cooking-lan-session`: 定义做菜经营游戏 LAN listen session 的身份绑定、握手兼容、权威命令入队、baseline/delta 同步、会话/快照新旧拒绝、受控 ingress 与分层验收边界。

### Modified Capabilities
- 无；P0 仍是前置 change，未毕业规格不在本 change 复制。

## Impact

- 未来影响纯 C# 应用层 session/identity/command queue、协议 catalog/wire schema、配置 hash 与 snapshot baseline/delta seam，以及 Unity host/client 入口；不把规则塞入通用框架，不假设当前已有 cooking session 实现。
- 依赖 P0 完成、测试与门禁证据；依赖 ADR-0001 的 listen host 角色和 ADR-0002 的权威固定 Tick/状态同步基线。
- 明确不包含 WAN/NAT/relay、严格 lockstep、save、自动 reconnect、host migration、产品人数上限、默认退出流程或固定 tick/性能目标；host exit UI/save 语义在集成验收前保持 blocker。
