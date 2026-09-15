# P4 关卡/地图/Match 生命周期：迁移设计

> 本文件是遗留规划的设计迁移，不表示设计已经批准或代码已经实现。

## Context

见 [proposal.md](proposal.md) 与 [spec](specs/cooking-match-lifecycle/spec.md)。阶段 1/P0 规划了 identity/location/lifecycle seam，阶段 2/P1 规划了 session、epoch 和快照边界，阶段 4/P3 规划了配置与逻辑布局校验；这些 change 仍是规划，当前没有 cooking 实现或 durable specs。现有 StateSync 设计表明业务层负责快照内容和导入导出，不能把通用 `WorldStateSnapshot` 直接当成完整业务状态。

## Goals / Non-Goals

**Goals:**

- 以显式状态机支持准备、开始、结束、重新开局，并保证非法迁移和旧局输入可诊断拒绝。
- 将 Level/Map 解析、配置身份、session/Match identity、epoch 和业务状态快照组合为可复用生命周期边界。
- 提供单机纯 C#、同机多实例和阶段 2 证据满足后的 LAN 集成测试矩阵；核心验收必须覆盖完整生命周期而非只测 snapshot。
- 隔离多个 Match 的实体、recipe/订单进度、计时器和版本。

**Non-Goals:**

- 不决定 host exit、断线恢复、主机迁移、人数上限、存档归属或长期经营结算；这些沿 owner 指针保持 Draft / Blocked。
- 不实现 P5 阶段 6 持久化管理或 P6 阶段 7 网络测量，不假定对应文件、测试宿主或门禁已经存在。
- 不让 Unity 场景对象直接推进权威状态，不把通用快照对象误作完整业务恢复协议。

## Decisions

### 1. 生命周期使用显式单向状态与新局代际

每个 Match 维护 `Preparing/Ready/Started/Ended` 状态、Match identity、session identity、config identity 和 epoch。重新开局创建新的 Match instance/epoch，而不是清空旧对象复用旧身份；这样旧命令和快照可确定拒绝。替代方案是原地 reset，容易残留物品、订单进度和快照水位。

### 2. 地图加载先验证逻辑布局再开放命令

从 Level/Map 配置生成与 Unity 表现无关的逻辑 layout，先执行阶段 4 校验，再创建准备上下文；只有 Ready/Started 才按相应规则接收命令。Unity authoring 可产生输入，但 GameObject identity 不进入权威身份。替代方案是直接以场景层级作为 Match 状态，会破坏跨端与测试隔离。

### 3. 生命周期转换和内容初始化使用原子提交

每次 transition 先检查当前状态、session binding、config identity 和必需布局，再一次提交状态、epoch/版本和事件；失败不改变状态。开始时初始化阶段 3 recipe loop 所需局内实例，结束时封闭命令入口，但不自动生成存档或长期奖励。

### 4. 快照包含业务生命周期元数据

应用层快照至少携带 Match identity、session/epoch、config identity、lifecycle state、logical version 和必要的 layout/业务引用；通用 StateSync 仅作为传输/缓存抽象。客户端严格按版本与 Match identity 应用，旧/乱序/未知输入不得覆盖新状态。

### 5. 阶段依赖和出口矩阵

| 工作项 | 纯 C# 前置 | 联机前置 | 阶段出口 |
|---|---|---|---|
| Map/Level 解析与准备 | 阶段 4/P3 config/layout 校验 | 可选 session binding | P4 准备态与失败诊断 |
| 开始/结束/重新开局 | 阶段 1/P0 identity/lifecycle + P3 | 阶段 2/P1 session | 完整四态序列与新代际 |
| 多 Match 隔离/快照 | P0 + P3 | P1 snapshot seam | 隔离、旧输入拒绝、版本证据 |
| LAN lifecycle | 上述全部 | P1 稳定 session、LAN 集成和 D1-D4 门 | 两 PC 证据；同机不替代 |
| 后续阶段 6/P5 | P4 结束/结算边界 | 按未来 change | 不在此实现或假定 |
| 后续阶段 7/P6 | P1-P5 可运行链路 | 按未来 change | 不在此实现或假定 |

### 6. Test Matrix 与证据计划

| ID | 输入/动作 | 断言 | Runner/证据（future） |
|---|---|---|---|
| M01 | 有效 Level/Map 加载并准备 | layout、config/session identity 正确，状态 Ready | .NET lifecycle contract；layout manifest/state trace |
| M02 | 缺 Map、非法 layout、hash mismatch | 不进入 Ready/Started，无 gameplay 实例 | .NET validator integration；diagnostic + before/after |
| M03 | Ready→Started→Ended→重新开局 | 依次完成四段，new Match identity/epoch，旧局关闭 | .NET lifecycle test；transition/event trace |
| M04 | 非法重复开始/Ended 后 gameplay | 拒绝，状态/事件不变 | .NET negative test；rejection log |
| M05 | 两 Match 并行命令/Tick | 实体、recipe、计时、事件和 snapshot 隔离 | multi-instance future integration；双状态 trace |
| M06 | 旧局命令/快照进入新局 | 拒绝/过期，new state 不变 | session/snapshot future test；sequence log |
| M07 | lifecycle snapshot 顺序/乱序 | 按序应用，旧/未知不覆盖 | Unity EditMode/projection future；watermark trace |
| M08 | 同机 host/client 完整流程 | 流程与协议证据完整，但标记非 LAN 替代 | future integration runner；双方 logs |
| M09 | 两台 PC LAN 完整流程 | 准备、开始、结束、重开和断开证据可复现 | future two-PC runner；环境表、logs、message trace |

## Risks / Trade-offs

- [Risk] “重新开局”复用旧身份导致幽灵状态 → [Mitigation] M03/M06 强制新 instance/epoch 与旧输入拒绝。
- [Risk] 仅接入 snapshot 而遗漏完整 lifecycle → [Mitigation] M01-M04 是 P4 核心出口，缺任何状态转换不得收口。
- [Risk] Unity 场景对象绕过 authority → [Mitigation] M01/M07 检查逻辑 layout、只读 projection 和 authority 不变。
- [Risk] 退出/断线语义被默认实现 → [Mitigation] owner 决策未确认时只输出 blocked，不生成 save/reconnect/migration 结论。

## Migration Plan

无既有 cooking Match 数据迁移。实施顺序：复核 P0/P1/P3 实际证据→逻辑 layout/准备→四态 transition→多实例隔离与业务快照→同机测试→满足 P1 LAN 门后两 PC 测试。失败时移除新增生命周期应用代码/fixture，不修改通用 StateSync、旧 changes、路线或主 specs；P5/P6 由后续 change 另行规划。

## Open Questions

跨阶段退出、断线、迁移、存档和测量事项仅链接 [ADR/long-term-goals.md](../../../ADR/long-term-goals.md) 与 [delivery-plan.md](../../../Docs/design/CookingGame/delivery-plan.md) 的 owner 入口；其未决状态不改变本 change 必须覆盖的准备、开始、结束、重新开局核心契约。
