# Design: Cooking 同机 UDP 最小经营闭环

## Scope decision

该 task 固定使用一个协作 fixture：host-local 玩家完成取料、启动加工和 3 个 logical tick 的加工推进；remote UDP client 完成装盘和订单提交。两名玩家必须对**同一订单**分别完成至少一个必要步骤。这个分工是已批准的最小协作验收，不是正式角色设计、菜谱内容或经济平衡。

## Authority integration

当前 `CookingRecipeSimulation` 与 `CookingSessionAuthority`/`CookingSimulation` 分离，无法证明 UDP gameplay 对 recipe/order 有同一份权威状态。实现以最小公共 authority seam 为目标：

```text
host-local command / remote UDP envelope
  -> CookingUdpHost single-reader dispatcher
  -> Cooking gameplay authority facade
  -> recipe/order fixture mutation + canonical gameplay snapshot
  -> command result + full baseline/delta
  -> client gameplay projection
```

- 单一 facade 负责 connection binding、command identity、串行 dispatch、recipe command 执行和 snapshot publication。
- host 与 remote 都只经此 facade；LiteNet callbacks 只复制/解码/入队。
- 复用现有 recipe fixture 的 validation、fixed tick、dedup、order port 和 canonical snapshot；不为 host/client 写平行的领域规则。
- UDP wire 仅增加经审议的 gameplay command/result/snapshot payload，继续采用 schema/protocol/scope/epoch/config/correlation 的既有 envelope 验证和 1200-byte hard cap。
- fixture 若超出 datagram cap，测试应明确拒绝/失败并留下证据；本 task 不实现分片、压缩或 resync。

## Fixture lifecycle

1. Host 构造固定 fixture order `fixture-order-a`，其最小 settlement 是稳定的 completion record（order ID、recipe ID、accepted-by player、state version）；不引入货币、评分或持久化。
2. Host-local player 取得已配置的食材、开始加工并以显式 3 tick 完成加工。
3. Remote client 接收连续 delta，提交装盘命令，再提交同一 order。
4. Authority 接受 order 后只记录一次 completion/settlement，消费 product 并发出 delta；同一 command replay 返回 duplicate 结果。
5. Client projection 与 host canonical gameplay snapshot hash 必须相等。

## Structured log contract

日志格式由 Cooking harness 侧单一 owner 定义，所有 reader 都复用同一 typed decoder。日志事件为 UTF-8 JSONL，每个角色一个 `events.jsonl`；每行含：

- `schema`: `abilitykit.cooking-harness-event.v1`
- `runId`, `role`, `sequence`, `timestampUtc`, `eventType`
- `scope`, `epoch`, `correlationId`, optional `commandId`/`orderId`
- `outcome`, `reason`, optional `snapshotSequence`/`baselineReference`/`stateHash`

角色进程只能写自己的目录。run-level `manifest.json` 与 `acceptance-summary.json` 由 PowerShell runner 在 child processes 结束后，以 temp file 后原子 move/replace 发布。

```text
same-machine-<runId>/
  manifest.json
  acceptance-summary.json
  host/
    stdout.log
    stderr.log
    events.jsonl
    ready.json
    result.json | failure.json
  client/
    stdout.log
    stderr.log
    events.jsonl
    result.json | failure.json
```

验收器按 `FileStream` 顺序逐行读取指定两个 JSONL 文件，通过 typed decoder 对当前 `runId` 的 required events 做 reducer 验证；不扫历史 run，不将完整日志目录读进内存。free-text stdout/stderr 只作排障附件。

## Acceptance and retention

成功判定同时需要：角色目录不重叠、两个 result 可解析、两个 events JSONL 均可解析、事件的 run/role/identity 关联正确、所有协作步骤被接受、order settlement exactly once、snapshot sequence 连续以及最终 hashes 一致。任一日志缺失、截断、schema 不兼容、关联缺失或 failure document 都得到 failed summary。

默认仅清理最旧的成功 runs，保留最近 10 个；失败 runs 与 `--keep-artifacts` 标记的 run 始终保留。清理在 acceptance summary 落盘后执行，绝不删除当前目录。

## Non-goals and operational safety

不变更 Unity、Orleans、generic networking、physical two-PC LAN、自动重连、认证/加密、预测/rollback、正式订单/经济规则或 durable persistence。LiteNet connection key 仍只适用于受控开发连通性。