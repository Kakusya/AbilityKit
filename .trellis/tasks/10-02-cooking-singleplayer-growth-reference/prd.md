# S15 单机成长与额外机制参考池

状态：planning / draft / 未执行。

## 目标与范围

K03–K14/H03–H06/I12/投掷接取冲刺/DLC。以单机→网络→单机 Unity→网络 Unity 顺序推进。

## 前置

依赖：S14。必须读取 [总 plan](../../spec/cooking/gameplay-menu-plan.md)、[注册表](../10-02-cooking-gameplay-menu-plan/task-register.md)、相关生效 spec 与历史 check。

## 验收

先保留既有评分/伙伴 Level-local 成长，再逐项审议扩展；任何风险/失败/随机/经济规则不默认为基础。拒绝零变更、幂等、canonical/hash、scope 与恢复覆盖随实际新增状态一起验证。

## 范围外

本轮不改产品代码，不执行该 Task；单机 Task 不做传输与 Unity，网络 Task 不做 Unity；额外机制只审议不默认纳入基础。

## 执行门

这是可审议的初始记录，不是最终实施批准。开始前细化具体 API/错误矩阵/测试与版本策略，重新核查依赖及环境，审阅最终 planning summary 后由 owner 明确批准。
