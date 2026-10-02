> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S01 单机逻辑移动与交互目标

> 最新执行授权：2026-10-02 owner 后续明确实施单机与联网、worktree并行、验证后合并master；Unity后置。 下面初稿未执行表述为登记时历史；具体执行设计及证据按总任务 execution.md 与本 Task 后续修订。

状态：planning / 规则已细化、待最终审阅 / 主分支未实施。停止分支中的工作不作为本 Task 的交付。

本次审议结论：现有主分支可用的原子拿放和容器操作保留；补充位置、朝向、障碍、统一目标解析与本地逻辑玩家碰撞。产品细节沿 owner 的授权由助手提出并记录，具体规则和验收矩阵见 design.md；当前没有执行许可。S02/S03 依赖这里的统一可达性，S08 再扩展可变布局，U01 负责可见高亮及手感验证。

## 目标与范围

A02–A07/A10，网格放置与连续移动分离。以单机→网络→单机 Unity→网络 Unity 顺序推进。

## 前置

依赖：现有单机基线。必须读取 [总 plan](../../spec/cooking/gameplay-menu-plan.md)、[注册表](../10-02-cooking-gameplay-menu-plan/task-register.md)、相关生效 spec 与历史 check。

## 验收

朝向/距离/障碍改变候选，空手/持物解析稳定；拒绝零变更；普通手槽与台面单物件。拒绝零变更、幂等、canonical/hash、scope 与恢复覆盖随实际新增状态一起验证。

## 范围外

本轮不改产品代码，不执行该 Task；单机 Task 不做传输与 Unity，网络 Task 不做 Unity；额外机制只审议不默认纳入基础。

## 执行门

这是可审议的初始记录，不是最终实施批准。开始前细化具体 API/错误矩阵/测试与版本策略，重新核查依赖及环境，审阅最终 planning summary 后由 owner 明确批准。
