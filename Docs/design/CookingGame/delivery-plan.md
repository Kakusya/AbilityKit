# 做菜经营游戏交付计划

> 本文是历史阶段顺序与状态 INDEX，不是第二份行为草案或 active 实施清单。2026-09-16 起，七个纯 .NET 受限增量按已有 check evidence 作为 `completed-limited-scope` 归档；剩余范围按 [当前进度](progress.md) 拆分为 [non-Unity successor backlog](successor-backlog.md) 与 [prohibited Unity future scope](future-scope.md)。不虚构日期，也不宣称完整 P0–P6 产品出口完成。

## 总览

| 阶段 | archived verified delivery | non-Unity successor（未启动/未批准） | prohibited Unity scope |
|---|---|---|---|
| **P0 交互基础** | T01-T08：纯 .NET pickup/drop authority、identity/location、稳定排序、原子失败、幂等与 snapshot；`10/10` | 无 successor | T09-T10 projection、scene/config fixture、EditMode |
| **P1 会话权威** | L01-L09：transport-neutral binding/handshake/ingress/baseline/sequence/diagnostics；权威摘要 `20/20` | D1-D4、production transport/wire、同机 socket、真实两 PC LAN、正式 benchmark | projection/scene/asmdef、连接 UI 与 Unity cross-host 验证 |
| **P2 配方循环** | R01-R06：单输入/单工序/3 Tick fixture、plate、injected order port、幂等；`27/27` | 正式 recipe/order/score/settlement/failure UX、production session 与真实 LAN R07 | scene/projection/UI/EditMode |
| **P3 配置校验** | 当前 definition 范围：候选批次、全量诊断、原子提交、canonical identity/hash；focused `6/6`、当时完整 `34/34` | Process graph、Level/Map schema、正式内容、host/client compatibility、migration policy | Unity 加载与 EditMode smoke |
| **P4 Match 生命周期** | fixture-only `Preparing → Ready → Started → Ended`、restart/epoch、隔离、snapshot watermark；focused `6/6`、当时完整 `40/40` | 正式 Room/Level/Map、房间/退出/恢复/迁移/保存决策、production session 与真实 LAN | authoring→layout export、projection、scene smoke |
| **P5 持久化技术合同** | in-memory settlement/progress/ledger/integrity/staged-store/restart；focused `12/12`、当时完整 `52/52` | durable file/database/cloud store、process-crash proof、存档 owner/timing/exit/migration/recovery/encryption | Unity/app-host restart smoke |
| **P6 网络测量基线** | `InProcess` workload/report、queue/throughput/percentiles/allocation、logical fault trace 与 blocked optimization gate；focused `5/5`、完整 `57/57` | production network measurement、stream decoder、真实两 PC LAN、批准 workload/threshold、非 Unity 优化与 fallback | Unity projection 及表现侧 interpolation/prediction/correction 验证 |

## 验收边界

- 归档只说明表中 `archived verified delivery` 已由历史 `check.jsonl` 支持；未运行项目不因 scope 重划变成 pass。
- **同机或 InProcess ≠ 真实两 PC LAN**。真实网卡、地址、防火墙和双方日志仍不存在；若 owner 将来选择该工作，必须从 successor backlog 新建 task。
- **in-memory staged store ≠ durable storage**。没有真实介质或 process-crash durability 证明。
- **fixture/schema subset ≠ 正式内容**。正式 Recipe/Process/Level/Map/Order、评分、结算与失败 UX 尚未批准或完成。
- Cooking Unity 执行长期禁止、不可领取、非 blocker；只有 owner 明确重新授权并新建 task 后才可重新审议。
- 后续工作不得恢复七个归档 task；必须重新读取当时契约、当前环境和 owner 决定，并记录新的真实验证结果。
