# Issue #5 源码核验记录

2026-10-04。审计范围是静态源与项目接线，未执行 .NET/Unity/game/network/performance。工作区基线5312c6e4bf2b612260297e2d8623aa362a9051e6；`git diff --stat a2cd7284e12d50a10bbb7abee6fc265577b9aa7c 5312c6e4bf2b612260297e2d8623aa362a9051e6` 实际结果仅 reclone-handoff 新增48行，源结论未因交接SHA新增运行通过。

## 读取与核验

完整读取保存的 Issue 正文 research/issue-5-snapshot.md（分段0–199、200–459、460–结尾），包括A–E、全部报告附录/来源及AGENTS proposal。读取before-dev技能、注入task PRD/design/implement/manifest、abilitykit/cooking/guides indexes；注入保存文件重新读取。输出读取使用UTF-8，已有任务中文中的字面问号不得猜造。

实际源检查：

- CookingLevelEtHost.cs 的实体关系、Host字段、TryEnqueueNetwork、CaptureReadOnlyFullState、ExecuteFrameCore、FreezeNextBatch、TerminalizeRemainingIdentity、CancelAdmittedAfterFault、InstallGeneration/InstallLevel/RemoveLevel与mutation gates。
- CookingRecipeLoop.cs 的完整状态字段、Submit/SubmitCore/ExecuteValidatedCommand、BuildFixedTickPlan/CommitFixedTick、物品/容器/Process/订单、Commit与allocator字段；CookingRecipeCheckpoint.cs 的ExportCheckpoint/RestoreCheckpoint/ExportSuccessHandoff/AcceptSuccessHandoff与foreign-key验证；CookingRecipeSupply.cs 的供应状态、candidate allocation与provenance验证。
- CookingNetworkSessionHost.cs 的Connection/Participant/Mapping、ProcessOwnerFrame、Map、Complete、Publish、Join、CloseOwner、CompleteAck/FlushDeferredAcks；CookingNetworkSessionClient的SendCommandAsync/TryInstallBaseline/ACK/Ready；WireCodec DTO/Options/Encode/TryDecode/Freeze。
- CookingNetworkAuthorityAdapter ConsumeFrame的同Host提交、cleanup、admission与fault drain；旧CookingSessionHost/CookingSessionAuthority的独立类型与legacy消费者边界。
- ET Runtime csproj的Compile Include/IsPackable/framework；EtRuntimeHost的single-active、owner-thread、world安装和Tick；runtime notice的裁剪来源与省略模块；实际reference目标树§7–9和门禁名称。
- CookingLevelEtHostTests实际fixed-step-failure测试体，断言effects/event/Executed保留且两时钟0。

## 核心发现与处理

| 当前事实 | 目标/未决 | 文档处理 |
| --- | --- | --- |
| ET树已组织生命周期，Simulation仍持有领域字典；Session/Host另持各自职责的ledger | 全面ET owner目标，迁移System未存在 | 分实际/目标树，10族逐列一致性闭包、旧writer撤销、消费者和正负controls |
| ExecuteFrameCore先Submit/Terminalize后fixed-step；已接受命令在step失败后保留 | reference §8提出whole-frame atomic commit，与实际行为冲突；改rollback未批准 | 固定分阶段行为，明确reference是目标且不能静默改变；不修改runtime、不提升Proposed ADR |
| Wire3 JSON，baseline full State+Session；精确ACK/Ready独立于terminal | 更换编码/protobuf或合并不同ledger需后案 | 五类全部轴、真实ordinary/retry时序，Close/Rebind明确不是新增enum |
| core3.0.3/sourcegenerator3.0.1为包装来源，裁剪0.1.0，internal-only | 完整upstream commit/hash/patch/工具binary闭包未齐 | 以仓库固定SHA识别已知副本，缺口列#7，不猜造上游身份/授权 |
| Compile Include与调用存在不证明本轮fresh build；EventSystem编入未安装 | .NET/Unity/IL2CPP运行验证后续 | capability表把接线、编入未接线、源工具、提案分开，本轮全部运行NotRun |
| Unity缺DLL exit0可被父gate标Passed | #6治理检查未实现 | 映射现有/待建gate+人工check与正负controls，不宣称机器强制已落地 |

## 交付与验证边界

详细合同位于 Docs/design/CookingGame/current-et-foundation-contract.md；实现仅该文档与本研究记录。根AGENTS/progress/history由主会话负责。链接、UTF-8、Markdown、git diff --check由主会话整合后统一执行并记录。没有运行时、版本、协议、CI或发布改动；本记录不替代独立check或Issue逐项验收。
