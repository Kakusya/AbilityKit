> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# 规划记录设计

状态：planning-only。任务只维护规划与证据入口。

## 归属

- 稳定玩法规划和推进入口：`.trellis/spec/cooking/gameplay-menu-plan.md`。
- 本轮功能事实/证据/Task 注册：本 task 的 `research/` 与 `task-register.md`。
- 应用层新增能力如何沿现有架构接入：`Docs/design/CookingGame/gameplay-plan-architecture.md`；不替代唯一 technical-roadmap。
- 菜单原始来源：reference/menu-v0.1，保留原字节；整合规则：reference/menu-integration.md。
- 长期目标、路线、进度、Todo 与 AGENTS 只增加精简入口和 owner 最新顺序，不复制全清单。

## 信息流

外部候选资料 → 只读来源副本/身份核对 → 功能与 schema 差异 → 规划规则及后续 Task → 将来独立审阅批准 → 实施/实际 check。当前停在后续 Task 登记。

每个子 Task 用 task.py create --no-start 建立 planning 初稿，不激活；独立 meta 记录 plan_id、stage、依赖、授权状态与 Unity/网络执行门。根 Task 的子列表保存真实目录链接。实施前再细化设计，而非现在为全产品编造未测量 API 或固定数值。

## 兼容与风险

保持当前生产代码、DefinitionId、v2 schema、checkpoint 与历史归档记录。旧 KCP/LiteNet、仲裁来源冲突在网络阶段消解，当前保留稳定行为。Unity 顺序不解除禁令。原始附件保留了作者未经生产校验的闭合声明，README 必须明确本轮检查限于结构和编号。

规划修订通过更新 owner 记录、入口和对应未执行 Task 完成；不重写旧 check，不重开受限交付归档。任务初稿的复用应防止再次创建同义 backlog。
