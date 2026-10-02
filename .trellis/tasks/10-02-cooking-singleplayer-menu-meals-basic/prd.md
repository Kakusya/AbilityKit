# S09 候选正餐第一批

> 最新执行授权：2026-10-02 owner 后续明确实施单机与联网、worktree并行、验证后合并master；Unity后置。 下面初稿未执行表述为登记时历史；具体执行设计及证据按总任务 execution.md 与本 Task 后续修订。

状态：planning / draft / 未执行。

## 目标与范围

M1：F01–F16/F21–F27/F43–F44。以单机→网络→单机 Unity→网络 Unity 顺序推进。

## 前置

依赖：S04, S05。必须读取 [总 plan](../../spec/cooking/gameplay-menu-plan.md)、[注册表](../10-02-cooking-gameplay-menu-plan/task-register.md)、相关生效 spec 与历史 check。

## 验收

先 F01/F11/F21 验证再扩完整批，切配/煮/煎/组合/分装全部从供应可达；不等同首关菜单。拒绝零变更、幂等、canonical/hash、scope 与恢复覆盖随实际新增状态一起验证。

## 范围外

本轮不改产品代码，不执行该 Task；单机 Task 不做传输与 Unity，网络 Task 不做 Unity；额外机制只审议不默认纳入基础。

## 执行门

这是可审议的初始记录，不是最终实施批准。开始前细化具体 API/错误矩阵/测试与版本策略，重新核查依赖及环境，审阅最终 planning summary 后由 owner 明确批准。
