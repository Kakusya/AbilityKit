# 固定 Flow：compete-one-item / v1（Draft）

初始化一个全新 Cooking ET fixture，两名空手、有能力、合法可达角色 A/B，一份目标 item，固定稳定 ID与初始 item version。初始化是 fixture 装配；之后行为只能走正式命令。

1. 验证 request、Flow/规则版本与预算/新输出目录；再创建本次资源。
2. offline Prepare/Start 后只读 capture；network 先使独立 server/client 的 Join→完整 baseline→精确 ACK/Ready 完成。两客户端观察相同初始 item version。
3. barrier 放行两条不同业务 ID 的 Pickup。offline 将两条 command 入 owner 队列后驱动 Tick；network 各 client 使用正式 SendCommandAsync，server 只 ProcessOwnerFrame。barrier 是 harness 条件，不保证网络到达同一 frame。
4. 等待两份正式业务处理终态。admission Accepted、传输 ACK、Task completed 分别保存，均不能单独证明拾取成功。不假定 A 或 B 必胜。
5. 检查唯一合法持有者、手槽/location 一致、另一拒绝且无该动作副作用/成功 event；保留首次不符合事实。
6. 同 stable ID/同 payload 重试获胜操作，检查不重复效果/event；再通过获胜者正式 Drop 到已配置合法空槽，验证组合后的不同目标与有限分支，不另造 gameplay。
7. network 有界等待各客户端完整目标投影收敛；只检查当前 scope/代次必要字段，不以 ACK 替代。
8. 停止动作/扰动，保存失败现场，关闭本次输入/订阅/连接/world，冲刷 collector，再原子发布 result；异常路径也走归位。

每一步记录 step/action/host/hostSequence/command关联与游戏 tick/墙钟。不能固定线程胜者，不声称并发完全重放。未知动作、未批准类别探索、丢失必要事实和归位失败分别明确状态。

独立小 Flow 候选 `pickup-drop-one-item / v1`：单角色正式 Pickup→Drop→只读观察目标在指定空槽，复用 adapter/规则/结果与归位；dot 审议是否用参数变体即可，S0 不实现。
