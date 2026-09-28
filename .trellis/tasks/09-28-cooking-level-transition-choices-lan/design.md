# Cooking 跨关选择与状态承接 LAN 纵切：技术设计

需求见 [prd.md](prd.md)。设计只覆盖生产 `CookingSessionHost`/`CookingSessionClient` 的跨 Level seam，不重新实现已经落地的领域交接逻辑。

## Architecture boundary

```text
Host control plane
  -> validate current Level + resolved choices
  -> export success handoff
  -> persist major checkpoint
  -> install next Level identity/generation
  -> publish complete session snapshot

Client
  -> validate snapshot scope/generation/sequence
  -> replace projection only on accepted full baseline
  -> reset old-Level command/pending state
```

- `CookingSessionHost` 继续拥有权威仿真和固定 Tick；跨关提交在同一 simulation lock 下完成。
- `CookingSessionClient` 继续只做投影，不本地裁决装修、解锁、Buff 或 Level identity。
- `CookingLanProtocol` 新增 control-plane message payload 和带 scope/generation 的 session snapshot envelope；旧 Recipe command payload 保持兼容字段，但必须带当前 Level scope。
- `CookingRecipeSimulation` 的成功交接、`CookingMajorProgress` 的选择校验和 `CookingMajorCheckpointStore` 的完整性校验作为领域边界复用，不复制迁移逻辑。

## Runtime model

新增一个不可变的 session-level projection（名称以现有命名为准），至少包括：

- `CookingLevelScope`；
- `Generation` 和 `SnapshotSequence`；
- `CookingMajorProgress` 的只读 canonical 投影；
- `CookingRecipeSnapshot`；
- transition metadata（最近一次 transition 的 source/target、保留加工数量、清除订单/结算数量），仅用于观察，不参与业务再执行。

该 projection 提供稳定 canonical text 和 SHA-256。字段排序、解锁 ID 排序和 decoration replacement 排序必须稳定；厨房 canonical 复用现有 `CookingRecipeSnapshot`/checkpoint canonical，不另造第三套物品序列化。

## Transition transaction

1. 在 Host lock 内检查当前 scope、generation、状态和请求 idempotency。
2. 复制/导出旧厨房 checkpoint 与旧 major progress 作为 rollback guard。
3. 在不暴露半成品状态给 Client 的情况下校验并应用已确认的 decoration、unlock、cook-faster 选择。
4. 调用现有成功交接边界，得到清除订单/结算上下文的新厨房。
5. 以锁定 progress + 交接厨房调用 `CookingMajorCheckpointStore.Write`。
6. 只有写入成功后，更新 Host 的 Level scope、generation、sequence、latest projection 和 transition ledger。
7. 广播新的完整 baseline。
8. 任一步失败均回滚 simulation、progress、scope、generation、sequence 和 transition ledger；不发送 transition success。

如果现有领域对象缺少无副作用 clone/restore seam，优先复用 `ExportCheckpoint`/`RestoreExportedCheckpoint` 或增加最小内部 restore seam，不用人工逐字段回滚。

## Protocol

- `CookingLanMessageKind` 增加 transition request/result 和 session snapshot kind。
- Request/result 携带 `CorrelationId`、当前 `CookingLevelScope`、请求幂等键和结构化 reason。
- Session snapshot 携带完整 scope、generation、sequence、progress projection、kitchen snapshot、SHA-256。
- Client acceptance order：
  1. scope Match/Runtime 必须匹配；
  2. Level/epoch 必须等于当前 scope，或是被明确宣布的更高 generation；
  3. 新 Level 只接受完整 baseline；
  4. 同 scope 只接受严格递增 sequence；
  5. canonical/hash 校验失败不替换当前 projection。

旧单 Level `RecipeSnapshot` 消息可保留用于兼容测试，但生产跨关路径只使用 session snapshot。

## Failure and reconnect

- checkpoint write failure stays on the source stable state and leaves the client on the source projection.
- stale command rejection is performed before mutating the recipe simulation.
- reconnect handshake returns the current session snapshot, not historical packets.
- reconnect token remains Match-scoped; Level change does not rotate it.

## Compatibility and rollback

- 不改变已有纯 C# checkpoint 格式和已有 `CookingMajorCheckpointStore` 文件格式。
- 不修改 Unity 自动生成项目文件。
- 如果跨关协议测试暴露旧 Client 依赖 `RecipeSnapshot`，保留旧消息解码但让新 Client 优先等待/校验 session baseline。
- 回滚点是新增 session projection/protocol 与 host transition coordinator；既有单 Level command path 和领域 handoff 测试不应改变。

## Explicit non-goals

- 不把 `CookingSessionHost` 重构为 ET Entity 宿主。
- 不把 transition metadata 当成 durable save result。
- 不引入 KCP、主机迁移、客户端投票或 Unity 接入。
