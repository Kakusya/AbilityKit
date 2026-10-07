# Owner 规则类别审批卡（全部 Draft）

类别审核是 Issue #13 的产品决定；dot 只能给技术建议。版本均 1.0，Owner decision/ref 均 null。Approved 后同语义/范围/参数实例无需重复审批；语义、适用范围、允许阈值放宽、新例外须重审。未批准规则探索结果最多 Finding/未判定，不是 Passed。

| ID | 含义/适用范围/判定时点 | 参数范围/例外 | 正例 / 故意反例 |
|---|---|---|---|
| C13-OWN | 竞争 flow；正式提交后同一 stable item 只在一个合法位置，authority独立ItemInHand索引与item location一致（不使用同源HeldItem），最多一个角色持有 | 仅两个空手、具资格、可达角色及一份可拾取 item；合法交接只在提交点判断；不检查跨 host 接收先后 | 任一胜者且另一空手 / 两角色手槽同时引用同 item 或 location 不一致 |
| C13-REJECT | 竞争与合法拒绝flow；拒绝不产生该command未声明效果/扣料，失败方手空，无该动作成功event | 用正式终态及归因事实，不要求全世界不变；其他合法命令/Tick可改变世界；fixture列真实合法原因集合，不固定ItemStale，可能先空间校验TargetOutOfRange；协议拒绝不冒充领域拒绝 | 正式业务Rejected无该command成功效果 / 拒绝方持物或新增该command成功event |
| C13-IDEMP | 同 scope/participant/stableId/相同 payload 重试不产生第二次业务效果或新增业务 event | 不把新 stableId 当重试；不同 payload 按现有冲突语义拒绝；允许原结果重放，不固定 delivery 次数 | retry返回原结果且该command业务event身份/数量不增；无关Tick的全局水位可变化 / 再次执行转移或新增 event |
| C13-CONVERGE | network；当前session/scope/generation的实际安装完整baseline，在预算内version≥fence且必要item/手投影匹配 | Pickup后、下次改变item前检验；允许更高version/无关暂态差异；提议10s、允许1–10s，放宽须重审；不是ACK或IsSynchronized采样baseline的精确ACK证明 | 当前完整目标投影 / 只有ACK、旧scope/generation或目标缺失/预算耗尽 |
| C13-COMPLETE | 所有受支持固定 flow；目标步骤/观察点实际达到、必要事实完整、归位完成才允许整体 Passed | 提议 step≤10s、overall≤120s、reset≤10s；invalid/取消/未执行/缺必要证据/无批准规则不能 Passed；不把正常小关自然完成改成业务失败规则 | 两终态/权威目标/必要投影/退出记录完整 / host 早退、缺事件、截断、不完整 result、残留 writer |

阈值为测试 harness 提案，不是正式玩法数值，也不是已批准或已实测性能。拒绝副作用的归因靠 command ID/业务事件与最小状态，在同 frame 多命令时不能误把其他合法提交视为拒绝副作用。物品总数守恒不作为烹饪通用规则。

首轮 S1 推荐启用 OWN/REJECT/IDEMP/COMPLETE，S2 再消费同一次批准的 CONVERGE。Owner 可一次批准五卡与分阶段消费，也可明确收窄；当前不预填批准。

## Dot要求的修订（优先于上方初稿歧义；Owner状态仍Draft）

六、五张规则卡的修订建议，均不代替 Owner 批准

C13-OWN：
保留提交点唯一归属；authority 使用独立手索引＋item location。fixture 必须证明两角色最初空手、具资格、合法可达。客户端只验证自己的正式投影，不冒称校验服务端内部索引。

C13-REJECT：
不固定失败原因为 ItemStale。当前空间校验可能先得到 TargetOutOfRange。类别表达“合法竞争失败不产生该命令成功副作用”；具体 fixture 的合法原因集合由源码对应路径列明。全局状态/version 改变不算拒绝方副作用。

C13-IDEMP：
删除“全局 event 水位不得变化”的歧义。固定 Tick 自己也会推进全局 sequence/version。检查原 command 的业务事件身份/数量、重复返回不产生新业务事件、目标 item/独立手槽不因 retry 再变。重试必须保留原 batch/version/payload；新操作不能借原 ID 冒充 retry。

C13-CONVERGE：
用当前 session/scope/generation 的实际安装完整 baseline，recipe version 至少达到目标 fence，并匹配所选 item/手持字段。允许更高 version，不要求全局快照逐字相等。
必须在 pickup 后、下一次改变该物品之前验证 pickup 收敛。不能用最终 Drop 的收敛补证明 pickup。
IsSynchronized 不是当前采样 baseline 的精确 ACK/Ready 身份证明。初始连接按现有 Join/baseline/Ready 路径；首版不新增目标 ACK 追踪能力，结果写“已安装目标投影收敛”。

C13-COMPLETE：
这是执行完整性/收尾条件，与产品规则分开记录。已确认产品失败＋后续崩溃可同时存在；没有结果、证据不全或归位不明不能 Passed。
测试 flow 完成不等于餐厅自然经营成功。最小拾取测试结束按测试会话释放，不调用 TryFinishService 冒称完成整个关卡。

规则审批卡仅需 ID/version、语义、适用 fixture/mode、允许参数范围、正反例、Owner 决定引用。S1 使用 OWN/REJECT/IDEMP/COMPLETE；CONVERGE 在 S1 标不适用，S2 才消费。现在全部仍是 Draft。


默认预算采用dot提案：startup/step/convergence10s、overall120s含归位/发布，预留reset10s+publish2s。精确允许参数范围由Owner决定，不代填Approved。
