# Cooking non-Unity successor backlog

> 状态：**non-active / not approved / no schedule**。本文记录七个受限交付之外仍可能有价值的纯 .NET、网络环境或宿主无关工作；它不是 Trellis task、实施批准、阶段 blocker、owner 承诺或时间表。

## 使用规则

- P0 没有 successor：其获批范围按现有纯 .NET authority/interaction evidence 收口；Unity 条目统一见 [future scope](future-scope.md)。
- P1–P6 的条目只有在 owner 另行批准并新建 Trellis task 后才能实施。
- 后续 task 必须重新审议依赖、产品语义、环境与验收命令；不得恢复旧 task，也不得把旧 `check.jsonl` 当作新范围通过证据。
- Unity 工作不进入本 backlog；它长期禁止且只在 [future scope](future-scope.md) 保留来源和不变量。
- 本 backlog 不改变当前事实：没有 production transport、真实两 PC LAN、正式内容、durable storage 或完整 P0–P6 产品出口。

## P1 会话与真实网络

- 运行 D1 候选 transport spike，记录 framing、listen/connect、关闭/cancellation、backpressure、诊断、配置、平台/license 与 host cost；由 owner 单独决定 production choice。
- 定义 D2 连接入口/地址或发现策略，D3 host/disconnect/exit/save/leave/settlement/reconnect 产品语义，D4 workload 与 benchmark thresholds。
- 实现 production transport adapter、正式 wire codec/schema、同机真实 socket 集成和分层诊断。
- 在两台物理 PC 上执行 LAN 验收，记录机器、网卡、地址、防火墙、双方日志和 message trace；不得由 in-process 或同机证据替代。

## P2 正式配方与订单

- 设计并批准正式 Recipe/Process/Appliance/Container/Order 内容与 timing，而不是沿用单输入/单工序/3 Tick fixture。
- 明确 order owner、评分、结算、失败处理与产品 UX，并维持装盘、提交、幂等和原子失败不变量。
- 在 production session 可用后执行跨进程及真实 LAN recipe-loop 验收；不得把 R06 in-process equivalence 当作 R07。

## P3 正式配置与兼容

- 建立 Process graph、Level/Map schema、正式内容和完整跨表关系；补齐循环、断裂、不可达终点等拓扑验证。
- 将 config identity/hash 接入真实 host/client gameplay binding，验证兼容与不兼容启动门。
- 另行决定旧 config/snapshot migration、拒绝、隔离或恢复策略；当前只保留 blocked result，不存在已批准转换。

## P4 正式 Match 与房间生命周期

- 将 fixture-only Level/Map/Layout 替换为获批正式 schema，并补齐产品 Room/Match 编排。
- 明确房间人数、host exit、disconnect recovery、reconnect、host migration、save/settlement 等产品行为。
- 在 production session 上执行同机跨进程和真实两 PC lifecycle 验收，保留 Match identity、epoch、config identity 与 stale input 隔离。

## P5 durable persistence 与经营策略

- 选择并验证真实文件、数据库或云端 durable store；通过 non-Unity app-host/process restart harness 证明 atomic commit、process-crash recovery、restart read-back 和 ledger/progress 一致性。
- 明确存档 owner、保存时机、退出/断电/cancel/host exit、migration、backup、quarantine/recovery 与用户可见错误。
- 审议 encryption/key policy、完整性信任边界以及是否需要 transaction outbox；没有 outbox 时不得声称通知跨进程 exactly-once。

## P6 真实测量与可选优化

- 建立 production transport 的 socket/stream decoder 测量与两 PC LAN harness；stream segmentation/coalescing 与 application fault 必须分层。
- 批准 workload、采样窗口、拓扑覆盖和延迟/吞吐/队列/内存/分配阈值；`UNSET` 不构成通过标准。
- 仅在真实报告和 owner approval 齐备后另行实施非 Unity 的同步/纠正策略；没有完整历史、恢复和重演证据时不得 blanket rollback。
- 执行 baseline/优化前后对照、authority pollution 检查和明确 fallback；报告存在本身不表示优化完成。
