# Cooking 技术路线：ET 主干与 ECS 退役规划

> 状态：规划已完成并经用户批准继续施工（2026-09-17）。implement.jsonl/check.jsonl 已填入真实条目；验证证据见 research/validation.md。

## Goal

以已确认业务约束推进 ET 主干路线，记录迁移与退役的证据门槛。用户已批准四阶段计划并明确要求执行，本 task 已进入 in_progress；此前“仅规划、保持 planning”的说明已过期。本轮完成规划落盘与 Demo 宿主下的生命周期/物品投影探针，不代表独立内核、完整 Cooking 纵切或清退完成。后续实施遵守各阶段验证门槛，未决产品语义不得自行补成需求。

## Confirmed product requirements

1. 首版合作动作烹饪为主，目标 LAN 2–4 玩家、约一两百现场对象；这是设计预算，不是性能实测。
2. 一个大关对应一家餐厅，小关为该餐厅的营业阶段。餐厅成长在同一大关内延续；跨大关保留规则未确认。
3. 升级、增加菜单、扩建在小关之间进行。
4. 成功进入下一小关时，食材、半成品、加工进度保留。
5. 准备阶段暂停加工，下一小关恢复；订单按本小关结算、不延续。
6. 失败重试保留成长、清空现场、重开本小关；重新供应规则尚未确定。
7. 用户选择小关完成时写盘、营业中不保存。失败是否属于写盘事件，以及准备阶段修改的保存点仍待明确，不能把助手此前推断当批准。
8. 继续遵守 ADR-0001/0002：listen host、本机与远端统一权威输入路径、固定模拟 Tick、首阶段状态同步/快照。

## Scope and requirements

- R1：保存业务决策、未知项和阶段边界，不把测试 fixture 当成正式产品内容。
- R2：把 ET 主干作为已批准的目标路线，验证状态标为未验证；退役必须以调用迁移、依赖清零和验证为前提。
- R3：研究现有同步合同、ET 集成和直接依赖；不把无直接依赖等同于无适配工作。
- R4：运行已有 Cooking 测试建立现状基线，保留原始结果；不把其通过宣称为新业务或 ET 验收通过。
- R5：产出设计、实施清单、ADR Proposed 草案和真实 context manifests。

## Acceptance criteria

- [x] R1：确认规则和未决产品语义分别记录。
- [x] R2/R3：设计包含同步适配、身份/生命周期、确定性、退役门槛及依赖证据范围。
- [x] R4：57 项既有 Cooking 领域测试通过，原始 TRX 与结果说明可追踪；见 research/validation.md。
- [x] R5：design.md、implement.md、ADR 草案与 context manifests 已建立，验证记录见 research/validation.md。
- [ ] 新运行时实施前：详细设计审阅、以下阻塞决策解决，以及所选迁移范围的完整反向依赖核实。

## Blocking decisions for implementation

- 小关完成存档的精确边界：成功/失败、准备阶段修改、启动下一关时的 checkpoint。
- 失败清空现场后的初始供应、玩家断线或房主退出，以及存档归属和恢复语义。
- 新菜单/升级/扩建如何处理既存工位、食材与未完成加工；不能随父节点删除误销毁进度。
- 需要迁移或隔离哪些旧调用方，如何维持未迁移项目的构建与测试。内部 API 可变更不等于任意删测试。

## Out of scope

当前探针不修改 Cooking 规则、不删除 ECS、不提交 Git。独立 ET 提炼属于已批准后续阶段，但尚未完成；不得以本探针替代其验收。不解除 Cooking Unity 禁止、不接 Orleans、不实现严格 lockstep/rollback、不声称真实两 PC LAN 或 durable storage 已完成。Moba/Shooter/Samples 按批准计划后续单独处置，不作为探针 blocker；清退仍须完整核实消费者。

## Evidence and ownership

长期目标与既有架构基线仍见 ADR/long-term-goals.md、ADR/README.md、ADR/decisions/0002-authoritative-fixed-tick-state-sync.md。新的产品结论先在本 task 审阅，不静默覆盖已有权威正文。研究证据见 research/dependencies-and-sync.md；架构草案见 ADR/decisions/0003-cooking-et-runtime-direction.md。
