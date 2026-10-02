# 现有 Cooking 规划的接续路由

2026-10-02，本轮 owner 要求记录“当前所有 plan”。这里汇合已发现入口，保留原归属，不把旧提案统一批准。

| 来源 | 保留内容 | 后续读取/归属 |
|---|---|---|
| 09-19-cooking-productization-network-slice-planning/prd.md、research/discussion-notes.md | 单机/网络共享核心、正常自然完成、固定伙伴、持续小关、成功检查点、网络纵切讨论 | S06/S14/N01；本轮四阶段推进顺序优先，旧网络方案暂不执行 |
| reference/product-lifetimes.md | Match/Connection/Participant、餐厅运行态、厨房/Level 归属 | S06/S08/S14/N01；不再问已确认身份语义 |
| reference/et-entity-tree.md | ET Component/Child、释放与所有权 | 所有 ET 变更前读；目标树不等于现有实现 |
| reference/save-storage.md | 存档位置、repository 与授权副本 | S14 若触及持久化则引用；完整 durable storage 仍单独后续 |
| successor-backlog.md 与 cooking P0–P6 spec | 正式 schema/Level/Map、durable store、网络恢复测量等未完成出口 | S04/S08/S14/N01–N03；不从 archived 状态推断完整产品完成 |
| future-scope.md | Unity 禁令与条件性验收 | U01/U02；仅列 Task，不解除禁令 |
| progress.md 与对应 check.jsonl | 既有 ET、厨房、前厅、跨关、评分、伙伴成长、双菜单等实际基线 | 全部 Task 的事实入口，具体执行结果以原始 check 为准 |
| Docs/Todo.md | 项目级 Cooking 待办与 framework 维护项 | 新总 plan 增入口；不把其他模块主动功能开发混入当前 Cooking 规划 |

## 保持的产品决定

- 小关持续递进；准备选择服务于下一 Level，不创建与小关并列的新“关间准备”产品对象。
- 只在成功小关的规定收口写产品检查点；技术同 Level 恢复测试不是任意时刻产品自动保存授权。
- 固定伙伴身份/岗位保持；不增加局内岗位指派或优先级微操。本 plan 的“接手”指玩家处理可交接工作，不改变伙伴调度决定。
- 评分已有固定基础分与星级，未满足 0 分，0 星可自然完成；不存在自动追加倒闭/业务失败的授权。
- 本轮只是记录未来 Task；旧网络讨论中代码退役/删除意图不授权本轮删除任何文件。

未发现本仓库独立 plan.py 或 plans 目录；采用现有 Trellis planning task 和 cooking spec 规划入口，不引入第二套任务系统。
