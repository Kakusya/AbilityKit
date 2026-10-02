# N03 readiness contract review

2026-10-03，natural_operating_implement。本轮仅修改N03 prd/design/implement及本报告；无.NET、源码、metadata、manifest或Unity操作。

已读取N01最终design与2026-10-03 baseline预算补充、N02受管组装树现有Wire/options、Client Connect/Reconnect、Host ProcessOwnerFrame/issued-ack/source cancellation，以及主树process-integration-verification实际结果和独立审阅路由。root当前rich流程仍在运行；可靠UDP SingleAwaitingAck发布guard已root审议source8d3261af1，后续启动必须读该冻结source及后继，不以主树旧source代替。

实际边界：network-sdk311/311、Authority15/Transport18/Session bounded27聚焦证据不能推导N02rich/物理LAN/master已完成。828796bytes/65672tokens旧结构budget真实拒绝及后续1600receipt增长修正已记录；本轮不预判仍运行的rich结果。

已将初稿细化为既有API与失败矩阵：live精确issuedbaseline ack+generation/token轮换，cold真正新Host/store/trustedfactory路径与旧token/ID隔离，旧scope/sequence/identity conflict，partial/all caller cancellation与paused cleanup，Unavailable/budget零业务变更，有界负载与clock/bytes/allocation口径，拓扑证据分离。完整baseline哈希覆盖authority+Session；gameplay共识单独用fullcapture，退出后Sessionview允许合法不同。

状态保持planning；N02仍inprogress/物理gate缺。Root可明确批准先做独立本地恢复/边界工程控制，但不等于整体依赖完成。文档未引入网络receipt持久化、主机迁移、平行模拟或正常unmet业务失败。

待root决定（design有具体推荐）：是否批准本地先行；2/4人、10秒预热+60秒样本、5/20/50commands/s、3重复；0/50/150ms单向延迟、0/20ms抖动、0/1/5%丢包；沿10msowner调度不改businessTick；baseline15秒/command30秒/prep120秒/scenario240秒deadline；performance阈值先UNSET。省略档须记录未执行，不视作通过。

结论：可交root进行planning review；不宣称依赖已满或任务已启动。静态git diff --check通过；无新增运行/通过证据。后续需逐项记录红/绿/blocked/skip并审阅批准范围。
