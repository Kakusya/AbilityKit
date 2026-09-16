# P5 持久化经营管理：实施清单

> 以下均为迁移的**未执行**规划。任务进入 `in_progress` 前必须重新审阅依赖、阻塞决策、当前代码与验证命令；不得只因文档迁移而勾选或关闭任何项目。

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
- [ ] 5.2 Unity/app-host restart smoke：**not run / future**，本轮无 Unity Cooking host。
- [ ] 5.3 全局 `runtime-contracts`/`core-stability`：**not run**，现有 gate 未覆盖独立 Cooking 项目；实际 build/test、task validation、`git diff --check` 在 check evidence 记录。
- [ ] 5.4 完整 P5 exit/owner integration：**blocked**，不可归档或解锁后续完整出口。

## 迁移的原实施清单

## 1. 前置证据与决策门

- [ ] 1.1 核对 `add-cooking-recipe-loop`、`add-cooking-config-validation`、`add-cooking-match-lifecycle` 的 proposal/spec/design/tasks 及实际实现/测试证据；验证：记录 settlement、配置版本/hash、Match 完成状态和长期边界的证据指针；未完成或仅有规划文件时阻断后续接入，不假称前置完成。
- [ ] 1.2 取得存档 owner、保存时机、退出/断电/取消、迁移/备份/损坏恢复与 host 退出的 owner decision artifact；验证：决策表逐项有 Approved/Draft/Blocked 状态；未决项保持 P08 Blocked，不由实现默认值补齐。

## 2. 长期进度与结算幂等

- [ ] 2.1 在拟建 cooking 应用包与对应 SDK 工程中建立长期进度数据契约，区分解锁、升级、货币、经营进度与 Match 临时状态；验证：P01 纯 C# 测试断言字段边界和不包含临时实体/计时器/位置，路径实施时确认并同时检查 `.csproj`/`.asmdef`。
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
- [ ] 5.2 将 P06/P08 接入实施时确认的 Unity EditMode 或应用宿主 smoke，验证重启读回不依赖 Unity `GameObject` 身份；验证：Unity 测试日志/XML 产物与宿主路径审查，当前未执行。
- [ ] 5.3 按影响范围评估并运行 `runtime-contracts`、必要的 `core-stability`/`regression` 与 `git diff --check`；验证：交付报告列出真实命令、结果或跳过原因；不得将计划测试写成已通过。
- [ ] 5.4 在 owner 决策、前置 change 实现证据、P01-P08 测试证据和故障出口齐全前，保持本 change `Draft/NOT ready`；验证：最终 checklist 明确 blocker，所有 checkbox 保持未勾选。
