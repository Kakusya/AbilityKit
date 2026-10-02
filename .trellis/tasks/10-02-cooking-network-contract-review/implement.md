> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: network contract review; S01-S14 accepted at 4dadd25c8 with evidence routed by master-singleplayer-exit-verification.md. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# N01 待执行清单

- [ ] 核对依赖、历史 check 与实际代码；解决本 Task 的契约差异。
- [ ] 补齐具体设计、验收场景与影响的 snapshot/checkpoint/schema。
- [ ] 最终规划审阅及 owner 批准；当前不可 start。
- [ ] 批准后按独立纵切实现：KCP/LiteNet、先到先得/稳定仲裁冲突。
- [ ] 实际运行聚焦 .NET 测试与适用 gate；记录 pass/fail/blocked/skip。
- [ ] 核对 更新权威决定与唯一传输方向，确定身份、命令/快照契约和真实 LAN 验收；不以旧纪要自动执行。

当前所有实现与测试步骤均未执行。命令选择见总任务 implement.md；Unity 必须解除禁令并确认环境后补齐场景命令。

## Current execution sequence after review

Owner authorizes audit then implementation. Follow the reviewed design and parent research/final-review.md; historical planning-only text is superseded. Check actual dependency evidence before starting, preserve stopped worktree edits, fix review findings before accepting prior implementation, update payload/fingerprints/config identity/canonical/checkpoint together, run focused behavioral and applicable integration gates, record actual pass/fail/blocked/skip and commit evidence. Never declare this Task complete from metadata or directory counts.

## N01 当前具体实施准备（2026-10-03，仍 planning）

- [x] 读取master86c3eb41d Host入队、Tick、Observe、checkpoint/export与排序边界，补齐design可审阅契约。
- [x] 区分Session bounded event FIFO与Host现有Player/Command排序；记录必须审阅的窄ordering扩展，未伪称现API已满足ordinal。
- [x] 记录ET程序集依赖方向、authority port/composition、Paused不可ExportCheckpoint及断线pending取消触点。
- [x] 定义typed full Observation/checkpoint wire、版本拒绝、外部可信policy、幂等/错误矩阵与实际出口。
- [ ] root审阅补充设计；统一正式ADR/spec历史适用边界和具体manifests。
- [ ] root核对S14完整退出并批准进入N02/N03；保留旧dirty worker，逐diff移植，勿直接merge。
- [ ] 后续实现再运行focused/SDK/ET/双进程实际验证；第二物理主机缺证据保持blocked，Unity不执行。

上述勾选只代表设计落盘，没有生产实施或测试通过。当前N01 metadata不由此变更。

## N01 阻塞修订记录（2026-10-03，等待 root + layout 再审）

- [x] 明确wire batch=0、server frozen-prefix统一batch、首次映射重传复用、stable businessidentity与connection sequence分离。
- [x] 给出pure-domain port/Host capture/cancellation拟签名，保留唯一ET authority、默认单机排序与Recipe5/Level8恢复契约。
- [x] 增补Created/Paused full readonly Recipe/Front/layout capture；Observation本身不宣称包含tombstone/allocator。
- [x] 固定wire/collection/connection/business/control/close tombstone/receipt上限与显式超限恢复反馈。
- [x] ack绑定实际issued baseline全部identity；断线StopProcess/StopFrontWork owner-cleanup以及paused/resume顺序。
- [x] 明确旧SessionAuthority/legacy ctor历史路径分离及逐caller迁移范围。
- [ ] root + layout复审最终契约；N01仍planning，无生产执行、测试或metadata改动。

## 四项源代码边界最终修订

- [x] 用CaptureResult(accepted/reason/state?)替换直接capture返回；frame/control独立捕获失败，closed control enum与最小结果DTO已定义。
- [x] source局部取消使用caller history，不安装logical terminal毒化有效重复caller；全caller取消返回CancelledNoExecution。
- [x] 拟Host terminal notification outbox，冲突影响所有旧caller，不能只reject新包。
- [x] cold新serverSessionInstance一律拒绝旧instance，不从Recipe receipt hash重建batch/command；仅live rebind承诺映射复用。
- [ ] root+layout最终审阅，随后正式规范同步与实施/实际验证；当前无生产修改或.NET运行。

- [x] 固定instance+完整scope+participant+wireStableID的length-prefixed SHA256 domain ID，69ASCII bytes；不碰旧receipt、不扩大持久化。
- [x] 同步删除前文冲突/冷重传歧义；pending真实payload交Host，terminalConflict不执行，outbox只含已admitted旧caller，新冲突caller即时返回。
- [ ] layout复读冻结正文与root最终接受；设计落盘不代表接口已有或测试通过。
