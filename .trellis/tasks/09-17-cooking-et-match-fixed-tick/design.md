# Design：Cooking Match ET 宿主与单一固定加工时钟

> 状态：planning。本文描述推荐实现设计，不授权开始编码。

## 1. Design goals

1. 让 ET 成为 Cooking Match 的应用层生命周期与帧调度骨架，而不是第二套领域模型。
2. 让一个宿主 Tick 对应一个可测试的权威逻辑帧，并确保全局 `LogicalTick` 每帧最多增加一次。
3. 为后续 checkpoint、跨小关延续和 UDP ingress 固定清晰边界，但不在本任务提前实现它们。
4. 保持现有 Cooking 领域命令原子性、幂等、版本和快照合同。

## 2. Proposed architecture

```text
Caller / future thread-safe ingress
                |
                v
       CookingMatchEtHost
       - owner-thread checks
       - lifecycle operations
       - pending command queue
       - frame batch freeze/sort
       - ET runtime + restaurant scene
                |
                v
       CookingMatchDriverUpdate
                |
      +---------+----------+
      |                    |
execute stable batch   AdvanceFixedTick()
      |                    |
      +---------+----------+
                v
     CookingRecipeSimulation
     (single authority owner)
```

### Ownership

- `CookingMatchEtHost` owns:
  - one `EtRuntimeHost`;
  - one long-lived restaurant/root scene/entity;
  - one current `CookingMatchLifecycle`;
  - one current Match driver/entity projection;
  - pending command admission and per-frame results.
- `CookingMatchLifecycle` owns lifecycle state and creation/closure of the active `CookingRecipeSimulation`.
- `CookingRecipeSimulation` owns every gameplay state mutation, including the fixed processing step.
- ET Entity stores only host/lifecycle references needed for scheduling and disposal. It does not duplicate item/process/order state.

## 3. Lifecycle design

### Prepare

`CookingMatchEtHost.Prepare(preparation)` delegates to `CookingMatchLifecycle.Prepare`. After a valid Ready result, the host creates one inert current Match Entity under the restaurant/root entity, but does not attach gameplay or open command admission. If ET entity creation fails, the lifecycle remains Ready and Start is not called.

### Start

`Start()` requires the inert Match Entity to exist, then delegates to lifecycle Start. After `TryGetGameplay` succeeds, the host attaches the simulation/driver reference to the existing Entity; attaching references must be non-throwing field assignment. This sequence prevents a Started lifecycle from existing without an ET driver.

If lifecycle Start itself rejects or gameplay factory creation fails, the inert Match Entity remains available for a later valid Start attempt and command admission remains closed.

### End

`End()` delegates to lifecycle End, clears pending commands with explicit rejected/dropped results, removes the Match Entity, and leaves the restaurant/root entity alive until host Dispose.

### Restart

`Restart(newScope, newEpoch)` delegates to lifecycle Restart after End. It replaces the current lifecycle reference, confirms no old Match Entity remains, resets per-match sequence/batch state, and returns in Preparing. It does not seed supplies or restore prior gameplay.

## 4. Frame pipeline

One call to `CookingMatchEtHost.Tick()` represents exactly one authoritative frame when lifecycle is Started.

```text
Check owner / idle / not faulted
  -> freeze current pending queue
  -> choose minimum SimulationBatch as this frame's closed batch
  -> retain future batches in the live queue
  -> group by Session/Player/Command identity
  -> collapse identical fingerprints; reject conflicting groups without mutation
  -> stable sort accepted representatives by PlayerId.Value, CommandId.Value
  -> submit commands to current CookingRecipeSimulation
  -> call simulation.AdvanceFixedTick(scope, epoch, hostFrameSequence)
  -> collect command dispositions + fixed-tick result
  -> return immutable frame result
```

When the queue is empty, the frame has no command batch but still calls `AdvanceFixedTick`. A batch value smaller than the committed batch watermark is stale and is rejected at admission. Batch gaps are allowed; the minimum available future batch becomes the next executed batch.

Commands enqueued while a frame is executing are rejected by current owner-thread reentry rules; future thread-safe ingress may append to the live next-frame queue without changing the frozen batch contract.

### Tick outside Started

- Preparing/Ready/Ended: return a stable lifecycle-not-started rejection; do not call ET runtime Update and do not advance LogicalTick.
- Faulted/disposed/foreign thread/reentry: throw according to existing host lifecycle conventions.

## 5. Domain fixed-tick contract

Conceptually:

```csharp
public CookingRecipeTickResult AdvanceFixedTick(
    CookingScope scope,
    long epoch,
    long hostFrameSequence);
```

The exact name may follow surrounding naming, but the semantics are fixed:

- reject mutation-free when lifecycle is closed or the supplied scope is not the simulation scope;
- precompute the next LogicalTick, frame StateVersion and one tick-event sequence;
- snapshot active processes in `ProcessId.Value` ordinal order;
- prevalidate every referenced recipe/input/station and every generated product identity before mutation;
- stage replacement process maps, item mutations, generated products, next product counter, tick event and ordered process results in temporary values;
- if any validation/allocation/invariant step fails, discard staged values and leave all authoritative fields unchanged;
- after successful staging, replace authoritative fields once, increment `LogicalTick` once and `_stateVersion` once, then append the tick event;
- do not write to `_processedCommands`, because this is not an external idempotent command.

### Why one state-version increment

A fixed Tick is one authoritative frame commit. Incrementing state version once avoids making snapshot version depend on active process count. Per-process details belong in the tick result; the snapshot version remains a frame-level watermark.

### Result shape

Recommended records:

- `CookingRecipeTickEvent`
  - shared monotonic Sequence from the simulation event counter
  - Scope, Epoch, HostFrameSequence
  - LogicalTick, StateVersion and ordered process outcome summary
- `CookingRecipeTickResult`
  - Outcome/reason
  - Scope, Epoch, HostFrameSequence
  - BeforeLogicalTick / AfterLogicalTick
  - BeforeStateVersion / AfterStateVersion
  - `CookingRecipeTickEvent`
  - ordered list of process results
- `CookingRecipeProcessTickResult`
  - ProcessId, RecipeId, PlayerId, Input ItemId, StationSlotId
  - previous/new elapsed ticks
  - completed flag
  - generated Product ItemId when completed

These records are application/domain contracts and must not reference ET types. Tick events are stored in a dedicated read-only `TickEventHistory`; they share the same `_eventSequence` counter with command events but do not change the existing `CookingRecipeEvent` shape or UDP DTO. Consumers that need a merged timeline use Sequence ordering across the two histories.

## 6. Admission and external AdvanceTicks compatibility

Define a concrete `TryEnqueue`-style API returning an immutable admission result with command identity, accepted/rejected disposition, stable reason and queue depth. Lifecycle-not-started, scope mismatch, malformed command, queue full, stale batch and reserved clock operation are normal rejections and do not consume queue capacity. Wrong thread, reentry, disposed and faulted remain exceptions.

`CookingRecipeOperation.AdvanceTicks` remains accepted by direct legacy `CookingRecipeSimulation.Submit` calls in this task. Removing it would widen migration scope and invalidate existing historical evidence.

`CookingMatchEtHost.TryEnqueue` rejects it immediately using `ReservedClockOperation`, because accepting it alongside `AdvanceFixedTick` would create a double clock. Tick defensively validates the frozen batch again before simulation submission.

This host-level admission/frame result remains separate from `CookingRecipeCommandResult`; orchestration rejection is not added to an unrelated domain enum.

## 7. Stable command batch and conflict handling

This host intentionally reuses the established `CookingSessionAuthority.ExecuteNextBatch` policy: one Tick selects the minimum queued `SimulationBatch` and retains later batches. Within the closed batch, accepted identities are sorted by `Player.Value` then `Command.Value`, matching the existing Cooking authority contract.

Before any simulation mutation, commands are grouped by `(Session, Player, Command)`:

- one fingerprint: execute one representative; additional identical envelopes receive duplicate disposition tied to the representative result;
- multiple fingerprints: reject every envelope in that identity group as `CommandIdentityConflict`; execute none and do not seed the simulation command ledger.

This removes arrival order as a tie breaker. Queue capacity is admission state, not Cooking simulation state. The new host does not reuse `CookingSessionAuthority` directly because that authority is bound to legacy `CookingCommand/CookingSimulation`, but it must preserve its bounded-ingress, minimum-batch and conflict principles.

## 8. ET entity structure

Minimal projection:

```text
CookingRestaurantEntity
└── CookingMatchEntity
    └── CookingMatchDriver component/entity
```

Only the driver needs `IUpdate` for this task. Station/Process entities are deliberately deferred until checkpoint/migration work establishes their projection contract; creating them now would duplicate or drift from simulation state.

The common ET runtime remains unchanged. If the existing `CookingRecipeDriver` can be generalized without changing legacy behavior, keep compatibility tests; otherwise introduce separate Match-specific driver types in the Cooking ET application project.

## 9. Error, pending disposition and fault behavior

- Domain command rejections are returned in frame command results and do not fault the host.
- Programming/invariant exceptions from lifecycle, simulation fixed Tick, or ET System are captured and rethrown by `Tick`, then leave the host faulted.
- No later Tick, enqueue, lifecycle transition, or Dispose-from-wrong-context may pretend success.
- Dispose on the idle owner thread remains allowed for cleanup after fault.
- `End()` and `Restart()` return host operation wrappers containing the lifecycle result and an ordered array of pending command dispositions; each queued envelope appears exactly once with command identity and stable cancellation reason.
- `Dispose()` moves equivalent pending dispositions into immutable `FinalDispositionHistory` before clearing storage; this history remains readable after disposal while mutating operations fail.
- Cancelled/rejected pending commands are never submitted to simulation and never enter its command ledger.

## 9.1 Frame and event identity

- `CookingMatchEtHost` owns a monotonically increasing `HostFrameSequence` starting at 1 on the first Started Tick and never resetting during the host lifetime, including after Restart.
- Every frame result and tick event carries lifecycle Scope and Epoch, so results from different Match generations cannot be confused.
- Command events created during a frame consume simulation `_eventSequence` first; the one tick event consumes the next sequence after all command events.
- Existing `CookingRecipeSnapshot` and UDP DTOs are unchanged in this task. Event watermark in snapshots and transport of tick events are deferred to checkpoint/network integration.

## 10. Compatibility and migration

- Keep `CookingRecipeTickHost` and its current test during this task unless the new host fully supersedes it with equivalent coverage.
- Do not modify `EtRuntimeHost.Tick()` or add timing/domain concepts to the ET package.
- Do not expose ET types from `AbilityKit.Game.Cooking`.
- Do not edit generated Unity projects or create Cooking Unity code.
- Update `Docs/Todo.md` only after implementation evidence exists; planning alone does not check items complete.

## 11. Validation design

### Domain tests

Add fixed-tick tests under `AbilityKit.Game.Cooking.Tests` for:

- empty fixed Tick increments LogicalTick/version once;
- one and multiple processes advance once;
- same-frame multi-process completion stable product/event order;
- fixed Tick preflight failure is mutation-safe; use a public/setup fixture that pre-occupies the deterministic next product identity before a multi-process completion frame, so collision occurs during prevalidation without a production test hook;
- closed lifecycle rejection is mutation-safe;
- legacy direct `AdvanceTicks` remains unchanged.

### ET application tests

Add `CookingMatchEtHostTests` under `AbilityKit.ET.Runtime.Tests` for:

- prepare creates an inert Match Entity; failed entity creation prevents Start/factory invocation;
- start admission and ET entity/gameplay binding;
- minimum-batch-per-frame selection with future batches retained;
- stable command sorting, identical duplicate collapse and conflicting fingerprint group rejection under permuted arrival;
- frozen frame batch and command-then-clock ordering;
- host admission rejection of external AdvanceTicks without queue consumption;
- End/Restart pending disposition accounting and HostFrameSequence monotonicity;
- Dispose final history, owner thread, reentry, capacity and fault propagation.

### Regression gates

Planned commands, subject to implementation scope and actual environment:

```text
dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj
dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-udp
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate precheck
```

Run `core-stability` only if shared core/runtime behavior beyond Cooking is changed. `cooking-udp` is compatibility evidence for the legacy external `AdvanceTicks` path only; it does not verify the new Match host or automatic clock. If this task adds a dedicated `cooking-et-runtime` gate to `tools/test-gates.json`, update the gate documentation and use that as the focused acceptance exit. Unity compile is not an acceptance requirement because Cooking Unity remains prohibited; the shared ET asmdef is unchanged in the recommended design.

## 12. Deferred work

Checkpoint restore, cross-level state continuation, failure supply, upgrade migration, persistence, UDP ingress, multi-Match runtime, real-time scheduler and ECS retirement each require successor planning after this task proves the lifecycle/clock foundation.
