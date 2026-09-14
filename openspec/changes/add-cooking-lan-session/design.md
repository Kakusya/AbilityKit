## Draft / NOT ready to apply

> 这是规划草案。实现前置条件：P0 `add-cooking-interaction-foundation` 完成并有 .NET/Unity/相关门禁证据；D1-D4 owner 决策完成；host exit UI/save 语义在集成验收前解除 blocker。不得把规划文件齐全当作 implementation-ready。

## Context

动机与范围见 [proposal.md](proposal.md)；行为契约见 [spec](specs/cooking-lan-session/spec.md)。当前路线明确 listen host、权威固定 Tick/状态同步方向，但不提供本游戏现成 session/transport 实现；Coordinator 文档也明确不能假定历史 `SessionCoordinator` 或 Local/Remote/Hybrid 实现存在。P0 尚未实现，故本设计只定义后续接入边界，不编辑业务代码。

现有框架设计可借鉴 `Docs/design/07-NetworkSynchronization/01-FrameSync.md` 的输入入队与 Host 驱动边界、`02-StateSync.md` 的权威快照基线，以及 `05-SessionCoordination.md` 的会话身份/数据面分层；这些文档不替本应用决定领域协议。协议源必须遵循 `Protocols/README.md` 的 Catalog/Wire 分离及生成检查规则。

## Goals / Non-Goals

**Goals:**

- 以应用层 session facade 持有连接、绑定身份、命令 ingress、baseline/delta 消费和生命周期诊断；simulation 只接受 host 统一 command queue。
- 把 handshake、配置 hash、协议/能力版本、session epoch、snapshot sequence 作为可验证的 session contract。
- 让 host-local 与 remote transport adapter 只负责接入/收发，禁止 adapter 直接 mutation；一次性命令使用 session/player/command 作用域 dedup。
- 在 transport 决策前保持 framing/codec/queue 等可独立测试；区分 TCP stream 行为与应用层 fault injection。
- 交付同机多实例、真实两 PC LAN、纯 C# contract 和 Unity 入口的分层测试计划与可审阅产物；性能阈值保持 unset，决策后才转成 gate。

**Non-Goals:**

- 不包含 WAN、NAT 穿透、relay、账号平台、严格 lockstep、save、自动 reconnect、host migration 或产品人数上限。
- 不从 `Dispose`、socket close 或窗口关闭推导 host exit UI、存档、离开房间或结算行为；这些在集成验收前是 blocker。
- 不把 P0 fixture 的两实例/两玩家视为产品上限，不承诺 30Hz、响应毫秒数、吞吐或“看起来流畅”。

## Decisions

### D1 — Transport spike 必须先于生产实现（Owner confirm required）

先做可重复 spike，比较候选 TCP/UDP（以及实际可用库）的 framing、连接建立、关闭/cancel、背压、诊断、配置与 Unity/.NET 宿主成本；输出候选矩阵、版本/许可证、同机和两 PC LAN evidence。owner 必须确认一个 transport 与 fallback/否决理由后，才允许实现生产 adapter。设计不把 transport API 名称写入领域 contract。

替代方案是直接选 TCP：可简化可靠有序 stream，但会把未验证的 framing/生命周期和性能假设藏入实现；直接选 UDP 则会提前决定可靠性、排序和丢失恢复。两者均不作为当前默认。

### D2 — 连接入口 UX 先锁定最小可验收流程（Owner confirm required）

本 change 只要求 host listen 与客户端通过显式入口加入；地址/端口手工输入可作为**可选提案假设**，不是批准的发现方案。D2 必须确认入口字段、错误展示、配置 hash/protocol mismatch 的用户可见结果、同机与两 PC 如何提供 endpoint，以及是否需要发现；未确认前不实现产品 UI 或把发现当作必需能力。

### D3 — Host/disconnect 语义保持阻塞，不猜退出流程（Owner confirm required）

session 内部定义 host authoritative close、remote transport loss、cancel/dispose 的结构化状态和禁止继续声称 synchronized 的边界；不定义 host 退出后的 UI、save、leave、结算、重连、迁移。D3 必须在集成验收前确认：host exit 的产品流程、远端断开显示、是否允许当前局安全结束。未决时相关验收不能通过。

### D4 — Benchmark 目标在证据后由 owner 决定（Owner confirm required）

先建立固定 workload 与采样格式，记录 command ingress 到 authoritative commit、baseline/delta decode/apply、队列深度、吞吐、p50/p95/p99、丢失/重复/乱序 fault 下的错误率和内存/分配；同机多实例与两 PC LAN 分开记录。阈值保持 `UNSET`，不得使用“looks fine”作为 acceptance。owner 根据 spike 数据批准阈值后，才能把指标变成 P1 gate；30Hz 不预设为目标。

### 1. Session / identity / ingress ownership

应用层建立 session descriptor（session id、epoch、protocol/config identity、capabilities）和 connection binding。host 在 handshake 成功后分配绑定 player identity；wire payload 的 claimed PlayerId 仅作一致性检查，权威以 connection binding 为准。ingress 先做 framing/大小/取消/生命周期/绑定校验，再将不可变 command envelope 放入共享队列；simulation step 统一排序、dedup、验证、提交。

### 2. Handshake and synchronization lifecycle

采用明确阶段：`Connecting -> Handshaking -> Bound -> BaselinePending -> Synchronized -> Disconnected/Rejected/Disposed`。只有 `Bound` 后的 gameplay command 可入队；只有完整 baseline 安装成功后才允许 delta。baseline/delta 载荷携带 session id/epoch、snapshot sequence、baseline reference/config identity；序号缺口、旧 epoch、重复/旧 sequence 和无法应用的 delta 均拒绝或请求 baseline，不静默修复。

### 3. Shared queue and one-shot dedup

host-local 和 remote adapter 调用相同 ingress facade；队列中的 command 以显式模拟批次和稳定键排序，不能使用接收时间、线程调度、字典枚举或 transport 到达顺序裁决。dedup 记录保留 command identity 与结果摘要，跨 session/player 不共享；重复投递返回原结果或等价幂等结果，不重发 mutation/event。

### 4. Transport-neutral protocol ownership

若新增 cooking Catalog/Wire Schema，源文件分别放入 `Protocols/Catalogs` 与 `Protocols/WireSchemas`，不把 catalog handshake 动态广告重复建成业务 DTO；生成文件只由脚本生成。具体字段 ID、最大 payload、可靠性与版本窗口在 D1/D2 确认后再落盘，当前 spec 只固定可观察兼容语义。

### 5. Layer-aware fault and LAN verification

纯 C# 测试使用 fake message channel 注入 message-level delay/loss/duplicate/reorder；stream decoder 测试使用同一 byte stream 的 segmentation/coalescing，验证 framing 恢复顺序，不模拟应用可见 TCP packet reorder。真实两 PC 测试必须记录两台机器、host/client 角色、地址/防火墙前提、握手/baseline/command/delta/断开证据；同机多实例证据不能替代它。

## Test Matrix

以下测试均为规划中的 future tests，不能当作已存在或已通过。每个 ID 的 runner、输出和成功/失败断言都必须在实施任务中落地。

| ID | 输入 | 动作 | 成功断言 | 失败/出口断言 | Runner / 输出 |
|---|---|---|---|---|---|
| L01 | P0 最小 fixture；host local + remote command | 两来源提交同批次可接受命令 | 同一 queue/handler；状态与事件遵守 P0 | 任一来源绕过 handler 或产生第二套规则则失败 | future .NET contract；命令日志、最终 snapshot、handler path evidence |
| L02 | 客户端自报 foreign PlayerId/foreign session | 已绑定连接提交命令 | 命令拒绝、无 mutation、身份错误可诊断 | 若以 payload 身份授权则阻断 | future .NET contract；拒绝结果与 before/after state |
| L03 | 协议版本/config hash/能力不兼容 | 执行 handshake | join rejected，无 gameplay binding | 若可先入局再报错则阻断 | future .NET handshake；compatibility report |
| L04 | 合法 join + baseline + 同 epoch delta | 按阶段投递 | baseline 先安装，连续 delta 正确应用 | delta-before-baseline 被缓存/拒绝，不直接应用 | future .NET StateSync；baseline/delta trace |
| L05 | old epoch、旧/重复/跳序 sequence | 投递到已更新客户端 | 不覆盖较新状态；缺口明确报告/等待 baseline | 若声称 synchronized 或重复 mutation 则阻断 | future .NET projection/session；sequence rejection log |
| L06 | 相同 command identity 重复消息 | 投递两次/多次 | 一次 mutation/一次事件，结果幂等 | 第二次改变状态或重复事件则阻断 | future .NET contract；dedup counter + event trace |
| L07 | malformed/oversize、队列满、cancel、dispose 后 ingress | 注入边界输入并关闭 session | 结构化拒绝、无越权 mutation、handler/resource 解绑 | 无界入队、异常泄漏或关闭后继续执行则阻断 | future .NET contract；error taxonomy、queue/dispose trace |
| L08 | TCP byte stream 的多种 segmentation/coalescing | 喂给 framing decoder | 解码消息边界/顺序一致 | 若依赖 packet boundary 则阻断 | future .NET codec; byte vectors and decoded message artifact |
| L09 | message-level delay/loss/duplicate/reorder | fake channel fault injection | 按 sequence/baseline 规则等待、拒绝或 unsynchronized | 不得宣称原始 TCP packet reorder 可见；不得隐式同步 | future .NET fault harness；fault matrix and state outcome |
| L10 | 同机 host/client 多实例 | listen、join、handshake、baseline、command、delta | 流程与协议证据完整 | 仅同机通过不得代替 LAN evidence | future integration runner；logs, endpoint config, snapshot trace |
| L11 | 两台物理 PC、显式地址/入口 | host 与 remote client 完成同上流程并断开 | 真 LAN 连接、身份、baseline、命令、delta、loss detection 可复现 | 防火墙/地址/transport 不可用时阻断 P1 LAN exit，不降级为同机通过 | future two-PC manual/integration harness；双方日志、环境表、packet/message trace |
| L12 | 固定 workload 与 fault profile | 采样 ingress/commit/decode/queue/内存指标 | 产出 p50/p95/p99 等实测数据，阈值仍标 UNSET | 无数据或“looks fine”不得通过；D4 未批准不得 gate | future benchmark harness；JSON/CSV report + decision input |

## Risks / Trade-offs

- [Risk] 未决 D1 导致 codec、连接 API 和协议字段返工 → [Mitigation] 先以 transport-neutral fake channel 和 spike 输出作为 gate，D1 前禁止 production adapter。
- [Risk] 客户端自报身份造成越权 → [Mitigation] binding 是唯一 authority，所有 command 做连接身份一致性检查，覆盖 L02。
- [Risk] baseline/delta 乱序造成幽灵状态 → [Mitigation] epoch + sequence + baseline reference 三重校验，覆盖 L04/L05。
- [Risk] 把 TCP 包边界误当消息或误测 packet reorder → [Mitigation] L08/L09 明确分层；TCP 只测试 stream segmentation/coalescing/order。
- [Risk] 同机测试掩盖真实网卡/防火墙问题 → [Mitigation] L10 与 L11 分离，真实两 PC 是 P1 必需出口。
- [Risk] 把 Dispose/断线误称为重连或安全退出 → [Mitigation] 明确 unsynchronized/disconnected，D3 前不添加 reconnect/migration/save/exit claim。
- [Risk] 性能目标被随意设定 → [Mitigation] D4 保持阈值 unset，先提交可复现指标和 owner decision。

## Migration Plan

这是新应用能力，实施前先验证 P0 交付证据与 D1-D4。按“transport spike/协议测试 seam → 纯 C# session/identity/queue → handshake/baseline/delta → host/client adapter → 同机集成 → 两 PC LAN”顺序推进；任一阶段失败时只保留诊断和测试产物，不向下一阶段接入。回滚删除新增 cooking session/adapter/protocol 源文件与 fixture，不修改既有 AbilityKit 通用同步契约；生成协议文件若有变更必须随源文件通过正式生成检查回退。

## Open Questions

以下问题不是实现默认值，必须在相应门禁前由 owner 回答：

- D1 选 TCP、UDP 或候选库及生产配置是什么？
- D2 是否批准手工 LAN 地址输入作为可选入口？是否需要局域网发现？
- D3 host exit UI/save/leave/结算和 remote disconnect 展示语义是什么？
- D4 benchmark workload 与 p50/p95/p99、队列、错误率、内存阈值是什么？
