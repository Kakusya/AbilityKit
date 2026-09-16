# P5 持久化经营管理：实施清单

> 归档状态：本 task 已于 2026-09-16 按 `completed-limited-scope` 归档，Trellis status 为 `completed`；完成只覆盖现有 `check.jsonl` 支持的纯 .NET 受限交付。

> 以下未勾选段落是**只读历史规划**：不可执行、不是当前 task checklist，也不是 archived limited delivery 的 blocker。non-Unity 后续只见 successor backlog；Unity 只见 prohibited future scope。

## 迁移前置条件

- 依赖：P4 的 Round/Match 与长期状态边界。
- 阻塞：存档归属、保存时机、退出与迁移/恢复语义均待 owner 确认。
- 规划验证：P01-P07（均为 future 规划，尚未执行）
- 实施前将 `.trellis/spec/abilitykit/index.md`、`.trellis/spec/abilitykit/validation.md` 与本任务对应 cooking spec 加入 context manifests。

## 当前受限实施状态

- [x] 1.1 已复核 P0–P4 的实际 pure .NET evidence 与完整 exit blockers；P5 只使用当前 Match/config identity seam，不把前序 partial evidence 当毕业。
- [ ] 1.2 owner artifact：**blocked**。存档 owner、save timing、exit/power-loss/cancel、host exit、migration/backup/recovery、encryption/key policy 尚未确认。
- [x] 2.1 已在 `src/AbilityKit.Game.Cooking/` 建立 unlock/upgrade/currency/business progress/revision/ledger 的长期 progress 合同，明确排除 Match 临时状态。
- [x] 2.2 已建立 confirmed settlement→progress mutation 边界，校验 confirmation、owner/match scope、settlement ID、progress version/config identity；无效输入 mutation-free。
- [x] 2.3 已实现 settlement fingerprint/idempotence/conflict；无 notification/outbox/跨进程 exactly-once 声明。
- [x] 2.4 已在单个 in-memory next-state envelope 中表达 ledger + progress 逻辑 commit；P04/P05 验证故障/重试合同，但不等同真实 durable store 或 process crash 证据。
- [x] 3.1 已实现 format/progress version、owner/config identity、revision、integrity hash 和 record length seam。
- [x] 3.2 已实现 transport-neutral staged in-memory store fault double；未选择文件/数据库/原子替换机制。
- [x] 3.3 已验证 duplicate settlement/revision read-back 不产生第二个逻辑 commit。
- [x] 4.1/4.2 已验证 restart 的 long-term-only read-back、tampered/truncated/unknown-format/owner/config mismatch 分类且不安装无效记录；不实施 migration/backup/quarantine/recovery action。
- [x] 4.3 已为未决 lifecycle policies 返回 `BlockedByOwnerDecision`；不由 End/Dispose/connection close 推导保存。
- [x] 5.1 已实际运行 P01–P10 对应的 pure .NET contract tests 和 JSONL evidence。遗留 P01-P07 计划范围与 design P01-P10 的冲突已在 PRD 协调；完整矩阵采用 P01-P10。
- [ ] **只读历史、不可执行** 5.2 Unity/app-host restart smoke：**not run / future**，本轮无 Unity Cooking host。
- [ ] 5.3 全局 `runtime-contracts`/`core-stability`：**not run**，现有 gate 未覆盖独立 Cooking 项目；实际 build/test、task validation、`git diff --check` 在 check evidence 记录。
- [ ] 5.4 完整 P5 产品出口与 owner integration 未完成，已移入 successor backlog；它们不阻止本 pure .NET limited delivery 归档，也不解锁完整后续出口。


## 只读历史规划说明

- 下文仍保留的 `Draft`、`blocked`、future matrix 与未勾选 checklist 仅用于历史追溯，**不可执行、不是当前 task checklist，也不是本 limited delivery 的 blocker**。
- 未完成的 non-Unity 范围（durable store、process-crash 证明与存档产品策略）只以 [`successor-backlog.md`](../../../Docs/design/CookingGame/successor-backlog.md) 为入口；如获批准必须新建 task。
- Unity package、asmdef、scene、authoring、projection、UI、EditMode 与 Unity smoke 长期禁止；只见 [`future-scope.md`](../../../Docs/design/CookingGame/future-scope.md)，不得按下文历史指令实施。

## 迁移的原实施清单

## 1. 前置证据与决策门

- [ ] 1.1 核对 `add-cooking-recipe-loop`、`add-cooking-config-validation`、`add-cooking-match-lifecycle` 的 proposal/spec/design/tasks 及实际实现/测试证据；验证：记录 settlement、配置版本/hash、Match 完成状态和长期边界的证据指针；未完成或仅有规划文件时阻断后续接入，不假称前置完成。
- [ ] 1.2 取得存档 owner、保存时机、退出/断电/取消、迁移/备份/损坏恢复与 host 退出的 owner decision artifact；验证：决策表逐项有 Approved/Draft/Blocked 状态；未决项保持 P08 Blocked，不由实现默认值补齐。

## 2. 长期进度与结算幂等

- **只读历史、不可执行：**该 Unity package/asmdef/scene/EditMode/host 指令已由 prohibited future scope 取代；本 task 不实施，也不把它作为 blocker。
- [ ] 2.2 建立已确认 settlement record 到 progress mutation candidate 的适配边界；验证：P01/P06 用有效与未确认/foreign settlement 断言只接受合法结算且输出可审计 identity；缺证据不得宣称通过。
- [ ] 2.3 实现 settlement identity 作用域内的幂等应用与结果返回；验证：P02 重复请求只有一次持久奖励 mutation，通知与持久奖励分开计数且无事务 outbox 时不宣称通知 exactly-once；P03 不同 identity 即使奖励相同仍分别处理；future .NET tests，未执行。
- [ ] 2.4 将长期进度变更与已应用结算 ID 去重账本置于同一持久提交边界；验证：P04/P05 分别模拟提交前崩溃及提交后响应前崩溃，重启重试同一结算时账本与进度不可一先一后，持久奖励恰好一次；future crash-injection/restart harness，未执行。

## 3. 存档记录与安全写入

- [ ] 3.1 建立带格式版本、进度版本、owner scope、单调 revision、完整性元数据及长度/资源边界的 record/codec seam；验证：P07 覆盖字段缺失、未知版本、owner mismatch、完整性失败并断言不覆盖有效内存；future .NET contract tests，未执行。
- [ ] 3.2 建立 transport-neutral staged persistence store seam（serialize/verify/prepare/commit/read），具体介质与原子替换遵循已批准决策；验证：P04 注入截断、取消和 I/O fault，读回上一完整 revision 或明确不可恢复错误；不得把解析成功当保存成功。
- [ ] 3.3 实现同一 revision/保存请求的安全重复处理；验证：P05/P07 断言无重复逻辑提交、commit counter 稳定、读回内容不变；future store fault harness，未执行。

## 4. 重启、兼容与故障出口

- [ ] 4.1 建立重启 read-back 流程，在安装前执行 owner、格式/进度版本、完整性、revision 一致性与当前配置兼容性验证；验证：P06 进程重启夹具读回解锁/升级/货币/经营进度且不再次发奖；future .NET restart harness，未执行。
- [ ] 4.2 对损坏、篡改、截断、未知版本和配置不兼容建立结构化错误分类及隔离/拒绝 seam；验证：P07 输出 error report 且不静默新建空档、不覆盖有效内存；迁移/备份/人工恢复在 owner 未批准前保持 Blocked。
- [ ] 4.3 接入批准的保存/退出/恢复生命周期，并保留未决策略的阻塞状态；验证：P08 与 decision artifact 逐项核对保存、取消、失败、恢复、host exit 结果；规划文档存在不能替代实际 evidence。

## 5. 跨宿主测试与门禁

- [ ] 5.1 将 P01-P07 接入实施时确认的 .NET 测试项目，保留可重复 settlement/store/restart fixture 与 bytes/trace 产物；验证：实际 `dotnet test` 结果、失败原因或跳过原因记录，当前均为 future/未执行。
- **只读历史、不可执行：**该 Unity package/asmdef/scene/EditMode/host 指令已由 prohibited future scope 取代；本 task 不实施，也不把它作为 blocker。
- [ ] 5.3 按影响范围评估并运行 `runtime-contracts`、必要的 `core-stability`/`regression` 与 `git diff --check`；验证：交付报告列出真实命令、结果或跳过原因；不得将计划测试写成已通过。
- [ ] 5.4 在 owner 决策、前置 change 实现证据、P01-P08 测试证据和故障出口齐全前，保持本 change `Draft/NOT ready`；验证：最终 checklist 明确 blocker，所有 checkbox 保持未勾选。
## 2026-09-16 归档范围

- [x] 已按现有 `check.jsonl` 将最终交付重划为：**in-memory 纯 .NET settlement/progress/staged-store 技术合同（focused 12/12；当时完整回归 52/52）**。
- [x] 真实 durable store、process-crash 证明与存档产品策略移至 successor backlog；Unity 移至 future scope。
- [x] 已确认未运行项不再属于本旧 task 的完成条件，且没有被改写为 pass。
- [x] 治理 task 已于 2026-09-16 执行 archive；本 task status 为 `completed`。
