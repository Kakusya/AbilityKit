# P1 LAN listen host/client：受限实施清单

> 归档状态：本 task 已于 2026-09-16 按 `completed-limited-scope` 归档，Trellis status 为 `completed`；完成只覆盖现有 `check.jsonl` 支持的纯 .NET 受限交付。

> 本 task 已于 2026-09-16 归档，status 为 `completed`；最终完成范围仅为已验证的 transport-neutral 纯 .NET session contract。完整 P1 LAN 产品出口未完成，并转入非活动 successor backlog。D1 spike preparation 仅添加非决策性报告模型，候选比较尚未运行，不能替代 D1 owner choice 或 LAN 验收。

## 1. 当前依赖与边界

- [x] 1.1 已核验 P0 T01-T08 的纯 .NET authority、command、snapshot 和实际 check evidence 可作为本准备范围前置。
- [x] 1.2 P0 Unity T09-T10 未运行并已移入 prohibited future scope；它们不是当前 contract、LAN successor 或其他 non-Unity 后续的前置/blocker。
- [x] 1.3 未完成 non-Unity 范围已分流至 successor backlog：D1-D4、production adapter 与 L10-L12；Unity 只指向 prohibited future scope。

## 2. Transport-neutral pure .NET session contract（implemented / verified）

- [x] 2.1 已定义 session descriptor、epoch、protocol/config identity、capabilities/policy 与 lifecycle states；L03-L05 验证 handshake/epoch/state trace。
- [x] 2.2 已定义 host-owned connection-to-player binding；L02 验证 foreign claimed PlayerId 或 scope mismatch 不授权 P0 command，且 authority snapshot/event 不变。
- [x] 2.3 已定义 compatibility handshake；L03 验证 version 不兼容时无 gameplay binding。
- [x] 2.4 已定义 baseline-before-delta、epoch/sequence/baseline reference 合同；L04-L05 验证 missing baseline、old epoch、duplicate/gap sequence 的 rejection/unsynchronized trace。
- [x] 2.5 已定义 bounded ingress、queue full、cancellation、close、dispose、resource-unbind 与 session/player/command dedup；L06-L07 验证 taxonomy、queue、dedup、mutation/event、cancel/dispose/unbind trace。
- [x] 2.6 已定义 host-local 与 remote-in-process 共用 ingress/authority queue；L01 验证没有 adapter 直写或第二套 authority 规则。
- [x] 2.7 已定义 runtime structured diagnostic schema 与 JSONL round-trip；L01-L09 机器读取关联 session/connection/player/command、epoch/sequence/baseline、state/reason/queue/direction。
- [x] 2.8 已定义 application-level transport-loss seam；L09 验证 loss 进入 disconnected/unsynchronized，后续 ingress 被拒绝且不修改 authority。

## 3. D1 transport spike（preparation only / not run）

- [x] 3.1 已定义非决策性 candidate/observation/report 模型，固定 workload、版本/license、平台、配置和输出字段；未固定候选，未做 production choice。
- [ ] 3.2 比较 framing、listen/connect、关闭/cancellation、backpressure、diagnostics 和 host cost；输出 JSON/JSONL 比较报告、限制与失败原因。**Not run.**
- [ ] 3.3 对 stream transport 仅测试 segmentation/coalescing/order 的 framing byte vectors/decoded artifacts；对 delay/loss/duplicate/reorder 仅以 application fake message channel 生成 fault matrix。**Not run.**
- [x] 3.4 未创建 cooking production adapter、正式 Catalog/Wire Schema，也未作两 PC LAN claim。

## 4. 明确 deferred / blocked 的完整 P1 工作

- [ ] 4.1 **只读历史记录，不可执行**：原 Unity projection/scene/asmdef/EditMode、connection UI/控件/可视化发现已移入 prohibited future scope；地址格式和发现协议策略属于 successor backlog。
- [ ] 4.2 **Blocked**：D2 connection entry UX；D3 host exit/remote disconnect/save/leave/settlement/reconnect/migration 产品语义；D4 benchmark workload/thresholds。
- [ ] 4.3 **只读历史记录，不是当前 blocker**：production transport adapter、L10 同机 integration、L11 两台物理 PC LAN、L12 benchmark 与正式 protocol generation 仅是未启动 successor；不得自动解锁完整 P2。

## 5. 验证与记录

- [x] 5.1 已将 P0 check evidence、abilitykit validation spec、cooking LAN/P0 specs 加入 manifests。
- [x] 5.2 已运行 focused .NET build/test，20/20 passed；写出 6 个 session diagnostics JSONL、50 条记录，并在 `check.jsonl` 记录命令和产物。D1 spike 未运行；日志不替代测试退出码或 owner 决策。
- [x] 5.3 已明确记录两 PC、Unity、benchmark、gate、production transport 未运行；当前不写为通过。
## 2026-09-16 归档范围

- [x] 已按现有 `check.jsonl` 将最终交付重划为：**L01-L09 transport-neutral 纯 .NET session contract（权威证据为 20/20；6 个 JSONL、50 条记录）**。
- [x] D1-D4、production transport/wire、同机 socket、真实两 PC LAN 与正式测量移至 `Docs/design/CookingGame/successor-backlog.md`；Unity 移至 future scope。
- [x] 已确认未运行项不再属于本旧 task 的完成条件，且没有被改写为 pass。
- [x] 治理 task 已于 2026-09-16 执行 archive；本 task status 为 `completed`。
