# 做菜经营游戏交付计划

> 本文是路线顺序与测试验收 INDEX，不是第二份行为契约，也不是实现任务清单。精确语义、协议和可执行任务唯一归属 `openspec/changes/` 与毕业后的 `openspec/specs/`。路线与约束参见 [技术路线图](technical-roadmap.md)、[AGENTS.md](../../../AGENTS.md) 和 [测试门禁规范](../../AbilityKit测试门禁与批量回归规范.md)。截至 2026-09-14，以下能力均为计划，不代表已实现或已测试；不虚构截止日期。

## 总览

| 阶段 | 依赖与前置 | 交付物 | 成功测试 | 失败测试与出口条件 | 未决项/门禁 |
|---|---|---|---|---|---|
| **P0 交互基础** | 最小配置/layout/snapshot 生命周期 seam；无真实网络 | [add-cooking-interaction-foundation](../../../openspec/changes/add-cooking-interaction-foundation/proposal.md)：纯 C# 拾取/放下、唯一所有权、稳定排序/幂等、host/remote in-process 同路径、Unity projection 与最小 fixture | T01-T10：.NET 单测、两消费者 in-process 集成、Unity EditMode/projection/scene smoke | stale/跨 session ID、越界/无资格、满槽、双玩家争抢、重复/乱序、旧 projection 必须 mutation-free；全部契约场景与受影响门禁通过 | 不选 transport、帧率、lockstep、LAN；future 测试项目路径须实施时确认；评估 `core-stability`/`runtime-contracts` |
| **P1 LAN listen host/client** | P0；最小 config/layout/snapshot lifecycle 已存在且有实现与测试证据；D1 transport、D2 连接入口、D3 host/disconnect/exit、D4 benchmark targets 均需 owner 确认 | [add-cooking-lan-session](../../../openspec/changes/add-cooking-lan-session/proposal.md)（Draft / NOT ready to apply）：host 同时服务端与本地玩家、真实远端客户端、共享权威 command queue、session-scoped identity binding、协议/config handshake、baseline-before-delta、epoch/sequence rejection、bounded ingress 与分层 LAN/transport spike 证据 | L01-L12 规划矩阵：纯 C# contract、同机多实例、真实两 PC LAN、TCP stream framing 与应用 fault injection 分层、benchmark 产物；均为 future，未执行 | P0 未完成/无测试证据、D1-D4 未确认、握手/transport 不兼容、消息重复/重排、权限越界、服务端关闭或 host exit UI/save 语义未决时阻塞；不得以同机通过替代两 PC LAN，不得以“looks fine”通过性能 | 不含 WAN/NAT/relay、严格 lockstep、save、自动重连、主机迁移或人数上限；手工 LAN 地址仅可作为待确认的可选入口假设；无明确 transport 与 D4 性能证据不得进入 P2 |
| **P2 一条完整配方** | P0；P1 只在需要联机验收时依赖稳定 session；recipe/process 数据契约先确定 | 一条正式批准的端到端配方闭环（食材、处理站、容器/成品、订单或结算边界） | 纯 C# happy path 与两实例联机一致性；成功产物数量、顺序、计时和结算断言 | 缺定义/非法拓扑、错误器具能力、重复提交、容量/生命周期失败、联机争抢；任何部分提交阻止收口 | 正式配方不是路线图示例自动决定；配方、订单、得分与计时 owner 需 OpenSpec；相关 runtime/内容门禁 |
| **P3 数据配置验证** | P2 的实际数据类型；早期最小 config/layout/snapshot lifecycle 不得推迟到此阶段才建立 | Item/Ingredient/Appliance/Process/Recipe/Level 等配置校验、引用检查和启动 config hash 兼容判断 | 合法表加载、外键/能力/拓扑/等级引用通过，host/client 同 hash 启动 | 缺外键、循环/非法拓扑、能力不匹配、hash mismatch、旧 snapshot/config 拒绝或迁移策略未满足；错误必须可诊断且不启动不兼容会话 | 版本迁移策略、旧快照保留范围、工具格式与 owner；`moba-content-contracts` 不可直接代替游戏门禁，需新建/指定覆盖 |
| **P4 关卡/地图/Match 生命周期** | P0 的 identity/location/lifecycle；P1 session；P3 config/layout 校验 | Unity authoring 到逻辑 layout、Level/Map/Match/Room 生命周期、snapshot 接入；Unity 身份不作网络身份 | 最小地图加载、创建/开始/结束 Match、多实例隔离、snapshot 版本顺序和重放/恢复到批准边界 | 缺引用、重复实例、旧/乱序 snapshot、非法状态迁移、跨 Match ID 污染；不得用场景对象绕过 authority | 房间人数、房主退出、断线恢复、主机迁移仍需产品决策；Unity compile/EditMode、`runtime-contracts` |
| **P5 持久化经营管理** | P4 明确 Round/Match 与长期状态边界；先取得 save/exit owner 决策 | 解锁/升级/货币/经营进度等长期状态与 match 结算的持久化管理 | 成功结算、保存/读取、幂等奖励、版本兼容和显式退出流程 | 中断写入、重复结算、旧存档、退出未保存/取消保存、损坏数据；默认 host exit 不得自行发明 | 存档归属、保存时机、退出必需性、迁移/恢复 owner 未批准则阻塞；安全与数据格式门禁 |
| **P6 响应性与网络测量** | P1-P5 的可运行垂直链路、真实测量 harness；不是玩法正确性的前置替代 | measured responsiveness/负载/网络报告，基于证据决定插值、预测、tick 与优化；WAN 另列后续范围 | 同机多实例与真实两台 PC LAN 分开验收：前者验证流程/协议，后者验证真实网卡、发现/连接和网络条件；记录 p50/p95 等实际指标但不预设目标 | 延迟、丢包、抖动、重复/乱序、负载、断线（仅在已批准重连时）失败场景；未达批准目标或数据不足不得宣称优化完成 | 30Hz 非锁定；WAN/NAT/中继/账号平台不在本阶段；性能目标、插值/预测和重连是否启用须以批准决策为门 |

## 验收边界

- **同机多实例 ≠ 真两 PC LAN**：同机测试覆盖 adapter、进程隔离和 listen host/client 流程；不能替代两台物理电脑的网络、发现、地址、网卡与防火墙验收。P1 必须分别列出两类证据，不能把首次真实 LAN 验收推迟到 P6；P6 在此基线上扩展性能测量。
- 每个阶段同时覆盖成功与失败路径：输入、动作、状态/事件断言、runner、产物和退出条件必须进入对应 OpenSpec design/test matrix；未实现阶段的测试名称和项目路径只能标为 future，不得当作现有项目或已通过门禁。
- 后续阶段不能把最小 config、layout、snapshot、identity 和 lifecycle 前置条件拖到最后才补；阶段可以扩展它们，但不能绕过它们。
- 主机退出、存档、重连、迁移、人数、首发平台和经营/动作比例是未决产品选择；未获批准前保持 blocker，不用默认值填空。
- 本文不固定渲染帧率、模拟 Tick、严格 lockstep 或 WAN 范围。每项正式能力仍须通过新的 OpenSpec change 建立行为契约；P0 当前 change 完成后也不等于实现、门禁通过或已归档。
