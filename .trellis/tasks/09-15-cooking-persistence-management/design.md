# P5 持久化经营管理：迁移设计

> 本文件是遗留规划的设计迁移，不表示设计已经批准或代码已经实现。

## 当前受限实施记录

本任务已开始受限的纯 .NET 技术实施。`CookingProgressPersistence` 仅接受带 owner scope、Match scope、settlement identity、confirmation、target progress version、config identity 与 reward lines 的 immutable settlement；它维护 unlock、upgrade、currency、business progress、revision 和 applied-settlement ledger，并明确不序列化 Match、layout、entity、position、recipe process、order 或 tick。相同 settlement identity 以全量 fingerprint 幂等；不同 identity 即便同值 reward 也独立进入 ledger。

提交使用 `prepare → commit → read` 的 in-memory fault-injection seam。首次 settlement 的 progress mutation 与 ledger entry 被编码为同一个 envelope，读写以 format version、progress version、owner、config identity、revision、长度上限和 SHA-256 integrity 进行 validate-before-install。pre-commit failure、post-commit response loss、truncation、tamper、unknown format、owner/config mismatch 都只产生结构化结果。此 double 只能验证逻辑 staging/重试合同，绝不作为文件/数据库 durability 或真实 process crash 证据。

本轮实际覆盖 P01–P09 的 in-memory technical subset 与 P10 `BlockedByOwnerDecision`。存档 owner、save timing、exit/power loss/cancel/host exit、migration/backup/quarantine/recovery、encryption/key policy、Unity/host integration 和真实 durable store 均未选择或实现，完整 P5 保持 blocked。

## Context

动机与范围见 [proposal.md](proposal.md)，行为契约见 [spec](specs/cooking-persistence-management/spec.md)。路线要求将 Round/Match 状态与长期状态分离并保证结算幂等；当前没有已确认的做菜存档 owner、保存时机、退出语义或迁移/损坏恢复策略。框架已有 Record/codec、状态存储和快照设计可作边界参考，但不能替应用决定存档所有权或产品流程。

预计应用代码放在 `Unity/Packages/` 的 cooking 应用包及对应 `src/` SDK 工程，宿主路径均为拟建，实施时必须检查 `.csproj` Compile Include 和 Unity `.asmdef`；不向 `Server/Orleans` 或通用 AbilityKit 包加入经营规则。

## Goals / Non-Goals

**Goals:**

- 以纯 C# 定义长期进度、已确认结算、结算身份和幂等结果之间的可测试边界。
- 将存档记录设计为带格式/进度版本、owner scope、修订号和完整性元数据的不可部分提交记录。
- 提供可替换的持久化 store/codec seam，使写入中断、重复请求、重启读回和损坏输入可由测试夹具验证。
- 保持 owner/保存时机/退出/迁移/恢复等产品决策为显式阻塞门，不以技术实现默认值替代。

**Non-Goals:**

- 不决定默认 host 是存档 owner，不决定自动保存、关卡结束保存或退出必保存。
- 不实现或承诺玩家间共享经济、跨玩家转账、主机迁移、自动重连或跨设备账号同步。
- 不把 Match 临时状态、Unity 场景对象或网络连接生命周期直接序列化为长期进度。
- 不在本规划阶段选择具体文件路径、数据库、序列化库或加密方案；安全/完整性接口可先形成技术契约，产品恢复动作仍需 owner 批准。

## Decisions

### 1. 结算先形成不可变、带身份的变更

Match lifecycle 输出已确认 settlement record，至少携带 session/match/player scope、唯一 settlement identity、结果版本和奖励明细。Persistence application 只接受该记录并计算长期 progress mutation；不直接读取运行中 World。选择此边界是为了让重复请求可按身份去重，并隔离临时状态。替代方案是保存整个 Match 快照，但会扩大兼容面并混入不应长期存在的状态。

### 2. 幂等账本与长期修订必须同一持久提交

以 settlement identity 记录“已应用结果”，以单调 progress revision 表达长期记录版本；二者不是两个可独立成功的写入，而是必须在同一持久提交边界内原子出现。重试读取既有结果而非再次执行奖励。实现必须通过提交前崩溃、提交后响应前崩溃故障注入证明：不能账本先落盘而奖励缺失，也不能奖励先落盘而账本缺失；重启重试同一结算最终对持久奖励恰好一次。具体记录布局和介质由实现选择，但不得用内存 dedup 或先写账本再写进度替代该边界。

通知与持久奖励分开建模：只有明确的事务 outbox 或等价可靠投递机制才能把通知声明为与提交绑定的可靠投递；没有 outbox 时，通知只记录派发尝试/结果，不能承诺跨进程 exactly-once，也不能承担去重账本职责。

### 3. 采用 staged record seam，不预设介质

定义 `serialize -> integrity verify -> prepare -> commit -> read` 的抽象生命周期。实现可使用临时记录/原子替换、事务 store 或其他方式，但 read 只能看到完整 committed revision。失败不得回退到“解析成功即已保存”。替代方案是直接覆盖单文件，无法保证中断时最后完整修订仍可读。

### 4. 版本和损坏处理采用显式结果分类

记录至少区分 unknown format/version、owner mismatch、integrity failure、truncated/incomplete、configuration incompatibility 和 storage I/O failure。迁移、备份、隔离、人工恢复或拒绝由 owner decision artifact 指定；在决策前只实现/测试分类与不安装安全边界，不推导“新建空存档”行为。

### 5. 安全最小契约不等于产品恢复决定

完整性校验、长度/资源上限、owner scope 比对和不可信输入不覆盖内存状态属于技术 MUST；是否加密、密钥归属、备份保留及用户可见恢复 UI 属于后续决策门。不得把校验存在宣称为防篡改或账号安全闭环。

### 6. Test Matrix 与决策门

| ID | 输入/动作 | 可验收断言 | Runner/产物 | 状态 |
|---|---|---|---|---|
| P01 | 已确认结算写入一次 | 长期解锁/升级/货币/经营进度与预期一致，match 临时字段未进入存档 | future .NET contract tests；progress diff 与 settlement trace | 未执行 |
| P02 | 同一 settlement identity 重复请求 | 一次持久奖励 mutation，稳定幂等结果；通知与持久奖励分开计数，无事务 outbox 时不宣称通知 exactly-once | future .NET idempotence tests；dedup/result artifact | 未执行 |
| P03 | 两个不同结算 identity 相同奖励 | 两次独立应用，不错误合并 | future .NET tests；audit identity trace | 未执行 |
| P04 | 持久提交前崩溃后重启重试同一结算 | 账本不先行，重试后账本与长期进度同一提交且持久奖励恰好一次 | future crash-injection store；commit/restart trace | 未执行 |
| P05 | 持久提交后响应前崩溃后重启重试同一结算 | 识别已提交结果，不重复奖励；账本与进度一致且持久奖励恰好一次 | future crash-injection restart harness；response/retry trace | 未执行 |
| P06 | 写入中断/截断/I/O fault 后重启读取 | 读回上一完整修订或明确不可恢复错误，不安装半写入奖励 | future fault-injection store；record bytes 与 read result | 未执行 |
| P07 | 重复保存同一 revision | 不产生重复逻辑提交，读回结果稳定 | future store tests；commit counter | 未执行 |
| P08 | 重启读取最近完整记录 | 长期结果一致，不重复发奖，不隐式恢复 Match | future .NET restart harness；before/after progress snapshot | 未执行 |
| P09 | 损坏/篡改/未知版本/owner mismatch/config mismatch | 分类诊断且不覆盖有效内存；迁移/恢复策略未批准时为 Blocked | future compatibility/security tests；error report | 未执行 |
| P10 | 未决 owner/保存/退出/恢复门 | 技术核心可独立测试；集成验收保持 Draft/Blocked | decision checklist；实际 evidence pointer | 未执行 |

## Risks / Trade-offs

- [Risk] 未决 owner 使技术设计被误读为产品承诺 → Mitigation：在 proposal/spec/tasks 逐项标记 Draft/Blocked，并把 P08 作为独立出口。
- [Risk] 结算和持久化跨两个提交单元导致重复奖励 → Mitigation：结算 identity、幂等结果和 progress revision 在同一逻辑提交单元中验证。
- [Risk] 完整性校验不足或错误分类造成数据丢失 → Mitigation：P04/P07 覆盖半写入、篡改、版本与 owner mismatch；失败不覆盖有效状态。
- [Risk] 把框架 Orleans 状态存储误当作游戏存档 → Mitigation：只引用 store/codec seam，不假定宿主或介质；应用层 owner 待决策。

## Migration Plan

新能力无既有做菜存档迁移。实施顺序为：先确认阶段 5（P4）`add-cooking-match-lifecycle` 的结算/Match 契约及阶段 2–4（P1–P3）的传递证据，再建立纯 C# progress 与 settlement/idempotence，接入 transport-neutral codec/store fault harness，最后接 Unity/宿主重启 smoke。owner 未批准迁移/恢复策略前，旧版本记录只可分类拒绝或进入待决流程。回滚时移除新增 cooking persistence adapter/fixture，不修改通用 Record、Snapshot 或既有持久化契约。

## Open Questions

以下是必须在实现集成验收前由 owner 明确的决策门，而非本设计的默认值：

- 存档 owner 是本地玩家、某个账号、共享餐厅还是其他主体？跨玩家经济是否存在？
- 何时保存、退出/断电/取消时如何处理、host 关闭是否必须完成保存？
- 存档迁移、备份保留、损坏隔离/恢复和用户可见错误如何定义？
- 是否需要加密、密钥由谁管理、完整性保护的信任边界是什么？
