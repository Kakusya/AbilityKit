结论：changes-required。这个精确候选暂不能进入合并步骤。

request_id：AK-I6-COOK-FINAL-20261007-02
flow_id：6d3174fd-2b01-4136-8ea5-0b0cd0e2845d
reviewed_sha：2bcb6d8165fd5abaa9469d9bc1462a7f3781d2dc
implementation_sha：8497d3a673bd84be079d99a58df97b96676dea88

已经确认的进展

候选直接继承 8497d3a；新增30个任务报告/记录文件，工具实现 Git tree 完全相同。当前 PR head 也与请求一致。
Slice 15 报告及匹配的 worker_done 记录了290/290、两组真实 Cooking gate 通过，历史失败仍保留。
独立 capture 文件消除了原共享追加点；实例双向覆盖、增量跳过和 producer receipt 绑定都有实质改善。
下面的发现来自只读代码审阅，没有被我执行复现，也不把 Slice 15 的既有通过结果改写成失败。
阻塞：实际编译输入仍可能被静默遗漏
事件读取器在190–212行直接跳过相对路径，以及回放时已经不存在的文件。Csc 的 ItemSpec 可以是相对路径；当前真实夹具的生成源码也采用这种写法。
具体触发：在 capture 之后、Csc 之前加入一份相对路径生成源码。capture 没记录它，reader 又忽略它；其他绝对路径引用让输入集合保持非空，汇总便没有机会发现遗漏。已消费但在回放前消失的输入也存在同类问题。
现有遗漏控制从 reader 已经筛选过的 inputs 中挑选删除对象，所以覆盖不到这个盲区。

依据：reader(https://github.com/Kakusya/AbilityKit/blob/2bcb6d8165fd5abaa9469d9bc1462a7f3781d2dc/tools/test-gate-compiler-events.cs#L190-L212)。

最小修复：保留文件参数类别和原始 ItemSpec，依据所属项目/任务的明确路径上下文解析；必需的已消费文件无法定位、读取或验证时失败，不能 continue。不要把非文件标量参数一概当文件。
新增控制：正常相对源码正例、capture 后加入相对源码的真实反例、相对输入遗漏/替换、消费后回放前文件缺失。先保存现版反例，再修复。

阻塞：Csc 可以错误归属到同项目实例的另一个 target
Get-GateEventContextKey 没包含 targetId，而 Csc 与 CoreCompile 的匹配都使用这个键。因此，同一项目实例内其他 target 的成功 Csc，可能替一个没有 Csc 的 Finished CoreCompile 满足校验。
只改 Csc.context.targetId，目前匹配逻辑就看不出差异。现有 context-mismatch 控制改的是 projectInstanceId。

依据：上下文键及匹配(https://github.com/Kakusya/AbilityKit/blob/2bcb6d8165fd5abaa9469d9bc1462a7f3781d2dc/tools/test-gate-result-contract.ps1#L1028-L1033)，相关使用位于1079–1089及1127–1133行。

最小修复：Csc 必须同时匹配所属 CoreCompile 的 targetId；独立 capture target 仍按项目实例关联，不能错误要求 capture 与 CoreCompile 的 targetId 相同。
新增控制：仅替换 targetId 的反例，以及真实“同项目兄弟 target 执行 Csc”的夹具；保留正常并行、多实例、增量跳过正例。

阻塞：候选包含尚未闭合的通用接任机制改动
Owner 对本次 successor 的授权成立。问题在于一并准备合入的可复用 skill 代码：authority-succession.json 缺失时，guard 回退为允许原主控身份；reconstruct/copies 又没有把 succession 纳入重建和副本冲突检查。即使历史已记录接任，缺失或不同的 handover 仍不能被可靠识别。
这是中断恢复的一致性问题，不要求实现新的锁服务，也不是质疑本次接任许可。

依据：guard(https://github.com/Kakusya/AbilityKit/blob/2bcb6d8165fd5abaa9469d9bc1462a7f3781d2dc/.agents/skills/cooking-dot-workflow/scripts/records.py#L143-L173)；副本比较位于306–322行。

推荐最小处理：把 records.py、test_controls.py、recovery.md 的这次接任功能改动从本次待合并差异中分离，保留为待审改动和历史证据，不删除当前资源或接任记录。
若要保留在本候选，则须单独补齐：与不可变接任历史绑定；已接任后缺失/更换文件必须 Blocked；重建与副本比较识别冲突；补缺失、有效内容替换、副本分歧的反例。文档要准确说明 helper 检查的是结构及外部证明引用，不能声称它自行证明独占权限。

验收证据还缺当前轮的主控独立审计
Slice 15 报告提供了本地文件路径和 hash，但候选中的 final-main-review-envelope.json 仍对应旧 b8c53e8 和旧 run，不能验收本轮 f0d51933-b320-40b1-bde8-8d332aa22cae、7b6a2c34-e1f8-4b22-8418-c9705f7630db。
我读到了报告和生命周期回执，没有直接读到其本地完整 controls、两份 summary、五份 TRX、binlog 和二进制归档。
下一轮请由主控独立核验最终运行的完整归档，发布紧凑机器可读审计：实际文件 hash/native退出、控制名称与期望、完整叶子覆盖、TRX ID/数量/程序集/filter、编译实例及输入闭包、完整 build→loaded-before/after→test.inputs 身份链。原始敏感日志和配置内容继续留在受控本地，无需公开。

allowed_next_action
主控记录本次 changes-required，在当前有效授权和写入隔离检查后，派发一个有界修复 worker。编译修复限于：

tools/test-gate-compiler-events.cs
tools/test-gate-result-contract.ps1
tools/tests/test-gate-result-contract.tests.ps1
tools/tests/fixtures/gate-result-model.ps1
必要时仅调整 fake-dotnet 夹具协议
本 task 的反例、修复报告和验收证据
业务、其他示例、Unity、依赖和 SDK 不变。接任机制按第3项独立处置，不夹带扩大。

保留原290项及其期望，新增具名控制；新实现提交冻结后，由独立 verifier 跑完整290＋N控制，再串行跑原始 cooking-et-level-runtime 和 cooking-kitchen-loop 命令。随后补主控独立审计，以新完整 SHA 再申请最终审阅。
本次要求重跑是因为需要修复实现；单纯追加证据提交本身不构成重跑理由。所有已有失败与通过记录继续保留。当前不合并、不关闭 Issue、不归档；后续即使技术接受，仍需实时 PR/check 复核和实际合并源码验收。
