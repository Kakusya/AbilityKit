# P1 LAN listen host/client：受限实施清单

> 顶层任务现为 `in_progress`，仅实施已获授权的 transport-neutral 纯 .NET session contract；完整 P1 LAN exit 仍 `blocked`。D1 spike preparation 仅添加非决策性报告模型，候选比较尚未运行，不能替代 D1 owner choice 或 LAN 验收。

## 1. 当前依赖与边界

- [x] 1.1 已核验 P0 T01-T08 的纯 .NET authority、command、snapshot 和实际 check evidence 可作为本准备范围前置。
- [x] 1.2 P0 Unity T09-T10 对当前 contract 实现为 `deferred`，不要求先实现；对后续 Unity/LAN integration exit 仍是前置。
- [x] 1.3 保持完整 P1 blocked：D1 production choice、D2 entry UX、D3 host/disconnect/exit 产品语义、D4 benchmark threshold、production adapter、L10-L12 和 Unity 都未解除。

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

- [ ] 4.1 **Deferred**：P0 Unity T09-T10、P1 Unity projection/scene/asmdef/EditMode、connection UI、地址输入/发现。
- [ ] 4.2 **Blocked**：D2 connection entry UX；D3 host exit/remote disconnect/save/leave/settlement/reconnect/migration 产品语义；D4 benchmark workload/thresholds。
- [ ] 4.3 **Blocked**：production transport adapter、L10 同机 integration、L11 两台物理 PC LAN、L12 benchmark、正式 protocol generation、cross-host validation 与 P2 解锁。

## 5. 验证与记录

- [x] 5.1 已将 P0 check evidence、abilitykit validation spec、cooking LAN/P0 specs 加入 manifests。
- [x] 5.2 已运行 focused .NET build/test，20/20 passed；写出 6 个 session diagnostics JSONL、50 条记录，并在 `check.jsonl` 记录命令和产物。D1 spike 未运行；日志不替代测试退出码或 owner 决策。
- [x] 5.3 已明确记录两 PC、Unity、benchmark、gate、production transport 未运行；当前不写为通过。
