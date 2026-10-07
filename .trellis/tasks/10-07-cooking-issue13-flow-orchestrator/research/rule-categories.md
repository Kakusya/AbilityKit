# Owner 规则类别审批卡（全部 Draft）

类别审核是 Issue #13 的产品决定；dot 只能给技术建议。版本均 1.0，Owner decision/ref 均 null。Approved 后同语义/范围/参数实例无需重复审批；语义、适用范围、允许阈值放宽、新例外须重审。未批准规则探索结果最多 Finding/未判定，不是 Passed。

| ID | 含义/适用范围/判定时点 | 参数范围/例外 | 正例 / 故意反例 |
|---|---|---|---|
| C13-OWN | 竞争 flow；正式提交后同一 stable item 只在一个合法位置，手槽与 item location 一致，最多一个角色持有 | 仅两个空手、具资格、可达角色及一份可拾取 item；合法交接只在提交点判断；不检查跨 host 接收先后 | 任一胜者且另一空手 / 两角色手槽同时引用同 item 或 location 不一致 |
| C13-REJECT | 竞争与合法拒绝 flow；拒绝操作不产生其未声明业务效果/扣料，失败方手保持空，拒绝不新增该动作成功 event | 以该动作正式终态及其效果投影判断，不用整个世界 hash 不变；允许另一个合法命令/fixed-step 同时改变世界 | stale/已持有目标拒绝 / 拒绝方获得物品或出现成功 event |
| C13-IDEMP | 同 scope/participant/stableId/相同 payload 重试不产生第二次业务效果或新增业务 event | 不把新 stableId 当重试；不同 payload 按现有冲突语义拒绝；允许原结果重放，不固定 delivery 次数 | retry 返回已有终态且 event 水位不因 retry 增加 / 再次执行转移或新增 event |
| C13-CONVERGE | network；正常通信可用且两个正式业务终态后，各必要客户端完整已提交投影在预算内匹配目标 ownership/state version | 仅必要 ownership/手槽/version/scope，显式允许无关 transient 差异；提议 deadline 10s、允许配置 1–10s，仅更严格不重审，放宽须重审；不是传输 ACK | Ready/正确 scope 的完整目标投影 / 只收到 ACK、旧 baseline、错 scope、预算耗尽 |
| C13-COMPLETE | 所有受支持固定 flow；目标步骤/观察点实际达到、必要事实完整、归位完成才允许整体 Passed | 提议 step≤10s、overall≤120s、reset≤10s；invalid/取消/未执行/缺必要证据/无批准规则不能 Passed；不把正常小关自然完成改成业务失败规则 | 两终态/权威目标/必要投影/退出记录完整 / host 早退、缺事件、截断、不完整 result、残留 writer |

阈值为测试 harness 提案，不是正式玩法数值，也不是已批准或已实测性能。拒绝副作用的归因靠 command ID/业务事件与最小状态，在同 frame 多命令时不能误把其他合法提交视为拒绝副作用。物品总数守恒不作为烹饪通用规则。

首轮 S1 推荐启用 OWN/REJECT/IDEMP/COMPLETE，S2 再消费同一次批准的 CONVERGE。Owner 可一次批准五卡与分阶段消费，也可明确收窄；当前不预填批准。
