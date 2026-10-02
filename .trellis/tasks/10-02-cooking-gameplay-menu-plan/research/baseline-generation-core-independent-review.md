# Baseline / generation staging core 独立审查

2026-10-02，只读审阅 main 新 CookingMajorBaseline、CookingMajorBaselineStoreTests、CookingGenerationTransactionCopy、其 Tests、Front PrepareGenerationStateAdoption。未改源码、未运行 .NET。

## Findings（未修复）

阻断：`ValidBaseline` 检查 Kitchen.Items entry 非 null，却未检查 item.Location；ItemLocation 是 reference record。`Kitchen.CanonicalText` 必须解引用 Location.Kind/OwnerId/SlotId。合法文件的 `payload.kitchen.items[0].location` 毒化为 null 会在 ReadBaseline 的 integrity/validation 路径抛 NullReferenceException；WriteBaseline 同样可接受该结构到 canonical 解引用处。两处 catch 均不捕获此异常，无法返回结构化 reason。已通知 root；建议在 canonical 前检查 item.Location 非null，并新增真实 read 毒化测试与非法写保留旧文件测试。不要简单吞所有异常来掩盖结构缺口。当前四例未覆盖 required 字段显式 null 与嵌套 Location null。

非阻断调用边界：PrepareGenerationStateAdoption 返回的 Action 捕获 source，实际 CopyFrom 在 commit 时才做；调用方必须保持 staged source 私有、不在验证后改变配置或重入。该 action 不是预先冻结完整 state 的不可失败 swap（CopyFrom 还会构造/复制集合）。因此暂不能将它单独宣传为整个 host durable事务完成。CreateGenerationTransactionCopy 也共享 fixture 和 _majorProgress，当前独立性证明仅针对 Recipe state，不能称任意 progress/fixture修改隔离。

## 已确认的受限能力

- 新 baseline envelope format 2，旧 store format 1 由 typed ReadBaseline 明确返回 UnsupportedLegacyBaseline；旧API并未因此获得完整restart能力。JsonRequired 包含有意可null的policy/layout字段，缺失与合法null区分。
- 哈希正文覆盖source/target scopes、config identity、preparation map/layout/stations/containers、三policy identity、installed layout及seeds、完整Kitchen canonical、locked/decoration/unlocks/CookFaster；并非仅库存摘要。哈希证明完整性，不替代 trusted内容或完整Restore外键校验。
- Decode限制1Mi字符并检查JSON形状/format/hash/match；Write先验证、序列化后Decode，再写同目录.next并回读验证，最后Move overwrite；失败清理.next且不主动删除旧file。size限制作用于已读/序列化字符串，不是磁盘流式读取内存上限。
- 四store测试有新store实际读取并AcceptSuccessHandoff恢复库存、非法写保持旧file、截断/篡改/缺required/错match/旧format拒绝、真实.next目录冲突导致WriteFailed且旧file内容不变。未证明fullHostRestart或故障点覆盖所有文件系统失败。
- 私有generation copy用禁止allocate的内部allocator、不传入external washing port，并通过完整导出/内部restore复制Recipe state、EffectiveSpatial及closing/completed。不会调用源externalallocator，也不在创建copy时驱动washport。测试实际在copy Pickup并确认source fullcheckpoint不变；尚无洗碗port spy或实际产物分配拒绝测试，静态路径提供该边界证据。
- Front adoption先核对schedule/companion/flow/manual policy，验证阶段不改源；测试私有front reset直到commit前原ownercanonical不变，commit后相等，并拒绝不同schedule。它保持原house对象和既有callbacks，不是新host发布实现。

## Verification

Root报告baseline4/4、合并focused7/7通过；本reviewer未重跑，不把数量当嵌套毒化覆盖。Lint/TypeCheck未单独执行。由于上述Location-null阻断，当前不作整体无条件通过结论；修复后应保留该初始发现并追加resolution。FullHostRestart、跨关geometry/permission事务仍不由这些core组件推导完成。

## Location-null resolution

Root 已新增真实文件 JSON `payload.kitchen.items[0].location=null` 毒化反例，并实际取得 red：local/Logs/cooking-major-baseline-null-location-red.log 1 failed，暴露 uncaught NullReferenceException。最新 ValidBaseline 在 item entry guard 中增加 `x.Location is null`，没有 broad catch NRE。独立只读确认 guard 在 Kitchen.CanonicalText 前，且读取毒化路径返回 InvalidBaseline；local/Logs/cooking-baseline-generation-core-null-location-green.log 实际 7 passed/0 failed/0 skipped。本 reviewer 未执行测试。

再次沿 canonical 所有解引用核对：Kitchen.Scope null 会先因 scope mismatch拒绝；各必需Kitchen集合及被解引用entry、process LockedInputs、container ItemIds、clean counts/poses已有前置guard；Supply/SupplyOrigins仅由JSON序列化而不在此canonical手工解引用，其业务合法性仍交完整trusted Restore校验。Preparation/Layout/Choices 引用及列表受guard，InstalledLayout沿既有CanonicalText完整非null guard。不再发现同路径可逸出NRE的具体缺口。

修复后可接受受限baseline persistence与private staging core；保留上述Action私有源稳定约束，不等同fullhost事务保证。Root此前480/608/238为修复前候选，最终fullgate将重跑，不能作为该新guard最终证据。
