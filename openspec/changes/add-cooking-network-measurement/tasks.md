## 1. 前置证据与测量基线

- [ ] 1.1 核对阶段 1–6（P0–P5）的可运行垂直链路与实际实现、测试和门禁证据：P0 `add-cooking-interaction-foundation` 为传递依赖，P1 `add-cooking-lan-session` 与阶段 2–5 recipe/config/match/persistence 为本阶段直接依赖；验证：记录 baseline/delta、sequence、结算 identity、长期 revision 的证据指针；仅有 change 文档时阻断后续优化任务。
- [ ] 1.2 明确并记录 owner 决策门：workload、采样窗口、拓扑覆盖、指标阈值、插值/预测/纠正范围、失败回退及重连语义；验证：批准项写入 decision artifact，未决项为 `UNSET`/`Draft/Blocked`，不得使用默认 30Hz 或毫秒目标。
- [ ] 1.3 建立不可变 workload 与报告 schema，包含版本、配置/协议身份、fault profile、机器/网卡/拓扑、采样窗口和指标定义；验证：N01 可生成带完整元数据的 JSON/CSV 报告，当前为 future/未执行。

## 2. 分层网络与故障测量

- [ ] 2.1 建立 command ingress-to-commit、snapshot decode/apply、队列深度、吞吐、p50/p95/p99、错误率、内存/分配采样点；验证：N01 同一 workload 前后报告字段和采样定义一致，缺失数据可诊断。
- [ ] 2.2 建立同机多实例 host/client measurement runner；验证：N01/N02 产出 endpoint、实例角色、session/baseline/command/delta trace，并明确不构成真实两 PC LAN 证据。
- [ ] 2.3 建立两台物理 PC LAN runner，记录机器、网卡、地址、防火墙、host/client 角色及环境差异；验证：N02 完成跨 PC 流程或明确标记连接失败，不能降级为同机通过。
- [ ] 2.4 建立 stream framing 测试，覆盖同一 byte stream 的 segmentation/coalescing；验证：N03 decoded message 边界和顺序一致，不测试或宣称应用可见 TCP packet reorder；future .NET codec tests，未执行。
- [ ] 2.5 建立应用层 fake channel fault harness，注入 delay/jitter/loss/duplicate/reorder/disconnect；验证：N04/N05 按 epoch/sequence/baseline 产出 fault matrix、unsynchronized/recovery-required 状态和错误计数，不能产生重复权威 mutation。

## 3. 基线与可选表现优化

- [ ] 3.1 建立 baseline-only 客户端报告和回放路径，确认不依赖预测/插值即可保持快照顺序与权威正确性；验证：N01/N04/N05 的基础状态、结算 identity 和长期 progress revision 一致，future tests 未执行。
- [ ] 3.2 建立独立策略开关和 owner evidence binding；验证：无批准 artifact、范围或回退条件时 interpolation/local-prediction/correction 均保持关闭并报告 Blocked，不默认启用。
- [ ] 3.3 在批准范围内接入快照插值，仅改变表现取样；验证：N06 前后 authority、command/event、settlement 和 progress revision 逐项一致，样本不足时执行批准回退；future Unity/.NET projection tests，未执行。
- [ ] 3.4 在批准范围内接入局部预测，仅覆盖具备历史/恢复证据的本地输入和状态；验证：N07 预测输入可追踪，权威快照最终决定状态；未批准对象/远端对象不预测，future tests 未执行。
- [ ] 3.5 建立证据门控的 reconciliation；验证：N08 仅在完整 Provider、历史、恢复和可重演输入证据存在时执行局部 rollback，否则等待/request baseline 或回到确认状态，不实施 blanket rollback。

## 4. 前后对照与安全回退

- [ ] 4.1 以同一 workload/环境运行 baseline 与每项优化并生成可比报告；验证：N01/N09 对照延迟分布、decode/apply、队列、错误、内存/分配及权威最终结果，报告阈值为批准值或 `UNSET`。
- [ ] 4.2 增加权威污染检测，比较命令提交、事件、Match settlement identity 和长期 progress revision；验证：N06-N09 断言表现层不能写入或改变权威/结算/存档。
- [ ] 4.3 实现数据不足、回归、fault 错误异常或权威不一致时的策略关闭与 baseline 回退；验证：N09 记录原因、标记优化不可用、恢复确认快照状态，不保留未经证明的预测/插值。
- [ ] 4.4 在阈值仍 `UNSET` 或 owner 未批准时交付诊断报告但阻塞优化 gate；验证：N10 可检查 decision checklist，不能以“looks fine”或报告存在宣称完成。

## 5. 跨宿主验证与交付

- [ ] 5.1 检查拟建 cooking .NET benchmark、Unity runtime/projection 与 `.csproj` Compile Include/`.asmdef` 边界；验证：未来 build/test 命令覆盖两个宿主，不修改自动生成 `.csproj`，当前未执行。
- [ ] 5.2 保存 N01-N10 的实际 JSON/CSV、日志、fault matrix、before/after diff 和 owner decision artifact；验证：每项标记 pass、fail 或 skip 原因，规划测试不得冒充运行结果。
- [ ] 5.3 按影响范围评估并运行 `runtime-contracts`、必要的 `core-stability`/`regression`、协议 check 和 Unity/两 PC 出口；验证：交付报告列出真实命令与结果/跳过原因，当前阶段不运行测试。
- [ ] 5.4 在阶段 1–6（P0–P5）前置证据、同机/两 PC 报告、批准阈值、优化前后对照和回退证据齐全前保持 `Draft/NOT ready`；验证：最终 checklist 明确所有 blocker，所有任务保持未勾选。
