# Design：Cooking Level ET 生命周期与单一固定加工时钟

> 状态：planning。本文描述推荐实现设计，不授权开始编码。

## 1. Design goals

1. 建立产品语义正确的 canonical Level lifecycle，不再让旧 Match 命名承担 Level 语义。
2. 以最小 ET Component seam 驱动 Level，而不提前复制 Item/Process/Order 权威状态。
3. 让一次 Running host Tick 对应一个原子、稳定、可测试的逻辑帧。
4. 保留旧 lifecycle/direct AdvanceTicks/UDP 调用方作为兼容回归，避免扩大成全仓 MatchId/CookingScope 迁移。
5. 为后续 ET gameplay authority、checkpoint 和跨 Level 迁移提供正确边界，但不在本任务实现。

## 2. Canonical identities

### 2.1 Level scope

推荐纯 C# contract：

```csharp
public readonly record struct RestaurantRuntimeId(long Value);

public sealed record CookingLevelScope(
    CookingScope MatchScope,
    RestaurantRuntimeId RestaurantRuntime,
    LevelId Level,
    long LevelEpoch);
```

约束：

- `MatchScope.Match` 继续表示兼容层里的 parent Match identity，不再被解释为 Level identity。
- `RestaurantRuntimeId.Value > 0`，`LevelEpoch > 0`，`LevelId.Value` 非空。
- failed retry：MatchScope/RuntimeId/LevelId 不变，LevelEpoch 增加。
- success successor：MatchScope/RuntimeId 不变，LevelId 改变，LevelEpoch 增加。
- exact type/name may follow project conventions, but all four identity dimensions must remain explicit.

### 2.2 Host frame identity

`CookingLevelEtHost` owns a host-lifetime `HostFrameSequence`. For each Running Tick it calculates `candidate = checked(current + 1)`, passes the candidate into command/fixed-step result construction, and publishes it only after the fixed step returns successfully.

- first successful Running Tick publishes 1;
- successful empty or non-empty frame increments once;
- Pause/Resume, Retry and successor do not reset it;
- a fixed-step preflight/invariant failure publishes no frame, consumes no sequence, emits no tick event and leaves HostFrameSequence unchanged;
- after such a failure the host is faulted, so the same candidate is never retried by that host.

Level-local watermarks reset only when a new LevelEpoch is installed.

### 2.3 Phase A simulation scope adapter

`CookingRecipeFixture`, `CookingRecipeSnapshot` and legacy UDP contracts continue to use `CookingScope` in this task. The simulation does not store a second Level scope.

Add a pure adapter owned by the canonical Level boundary:

```text
CookingLevelSimulationBinding
├── CookingLevelScope LevelScope
└── CookingScope LegacySimulationScope = LevelScope.MatchScope
```

Rules:

- host/lifecycle validate full MatchScope + RuntimeId + LevelId + LevelEpoch before queue admission and before fixed Tick;
- `CookingRecipeSimulation` validates the bound legacy `CookingScope` equals `LevelScope.MatchScope` and receives explicit LevelEpoch/RuntimeId/LevelId only in the new tick request/result metadata;
- recipe command DTO passed to existing `Submit` retains legacy `CookingScope`; the host rejects an envelope whose canonical LevelScope does not match the active binding before simulation submission;
- `CookingRecipeSnapshot.Scope` remains legacy `CookingScope`; LevelScope is carried by `CookingLevelFrameResult`/tick event wrapper, not injected into the old snapshot or UDP schema;
- retry/successor install a new binding; old binding and simulation close before new admission;
- no adapter may reinterpret `CookingScope.Match` as `LevelId`.

This preserves wire/snapshot compatibility while making Level generation validation an explicit host/application responsibility in Phase A. A future protocol migration may add LevelScope to canonical snapshots under a separate task.

## 3. Canonical Level lifecycle

### 3.1 Contracts

Create a complete Level-named family:

```text
CookingLevelPreparation
CookingLevelState
CookingLevelOutcome
CookingLevelLifecycleReason
CookingLevelLifecycleEvent
CookingLevelLifecycleResult
CookingLevelSuccessorResult
CookingLevelLifecycleSnapshot
CookingLevelLifecycleSnapshotApplier
ICookingLevelGameplayFactory
CookingLevelLifecycle
```

State graph:

```text
Created -> Preparing -> Ready -> Running <-> Paused -> Ending -> Ended
```

Transition contract:

| Operation | Accepted source | Target / immutable effects |
| --- | --- | --- |
| `BeginPreparation(preparation)` | Created | Preparing; store copied preparation candidate |
| `CompletePreparation()` | Preparing | validate all references/config; Ready |
| `Start()` | Ready | create one gameplay; Running |
| `Pause()` | Running | Paused; close new admission, preserve pending queue/watermarks |
| `Resume()` | Paused | Running |
| `BeginEnd(Success)` | Running | set immutable Outcome=Success; Ending |
| `BeginEnd(Failed)` | Running | set immutable Outcome=Failed; Ending |
| `BeginEnd(Aborted)` | Created/Preparing/Ready/Running/Paused | set immutable Outcome=Aborted; Ending |
| `CompleteEnd()` | Ending with Outcome | close gameplay/resources; Ended |
| `CreateRetry(newEpoch)` | Ended+Failed | new Created generation, same LevelId, higher epoch |
| `CreateSuccessor(newLevelId,newEpoch)` | Ended+Success | new Created generation, nonblank different LevelId, higher epoch |

Paused cannot become Success/Failed directly. Outcome is assigned once by accepted `BeginEnd`; repeated/different outcome attempts reject without mutation. `CompleteEnd` requires an assigned Outcome. Every unlisted transition rejects without changing state, outcome, version, events, preparation or gameplay.

Retry/successor exact checks:

- same MatchScope and RestaurantRuntimeId as source;
- retry requires `newEpoch > source.LevelEpoch`, retains LevelId;
- successor requires `newEpoch > source.LevelEpoch`, nonblank `newLevelId`, and `newLevelId != source.LevelId`;
- source gameplay is closed before result publication;
- source lifecycle remains Ended and owns its immutable history;
- new lifecycle is a separate Created instance with reset lifecycle version/event sequence and explicit new LevelScope;
- rejection returns source snapshot unchanged.

### 3.2 Gameplay ownership

- `ICookingLevelGameplayFactory.Create` receives the explicit Level scope and configuration.
- Start creates exactly one `CookingRecipeSimulation` for that Level generation.
- Pause keeps gameplay alive but closes admission and fixed Tick.
- Ending closes admission before pending disposition.
- Ended closes simulation lifecycle and exposes no gameplay admission.
- Retry/successor return a new canonical lifecycle instance; the old lifecycle remains terminal.

## 4. Obsolete Match compatibility facade

The existing Match-named family remains source-compatible where practical but delegates to the canonical Level implementation.

Design constraints:

- one underlying lifecycle state machine;
- one gameplay simulation owner;
- one event/version sequence;
- no mirrored mutable fields;
- old snapshot/result conversion is pure projection;
- old `Restart(newScope,newEpoch)` translates to a compatible successor Level generation using legacy scope information;
- `[Obsolete("Use CookingLevelLifecycle ...")]` directs new callers to canonical APIs.

Concrete facade structure:

```text
CookingMatchLifecycle (obsolete facade)
└── readonly CookingLevelLifecycle _inner     // only mutable lifecycle owner
```

The facade may retain only immutable constructor compatibility metadata and conversion helpers. It must not retain its own `_state`, `_version`, `_events`, `_preparation`, `_gameplay`, event counter or writable snapshot watermark.

Projection rules:

- legacy `Preparing` represents canonical Created or Preparing;
- legacy `Ready` represents canonical Ready;
- legacy `Started` represents canonical Running only;
- legacy `Ended` represents canonical Ended;
- canonical Paused/Ending are unsupported legacy observations: legacy mutating operations return structured `InvalidState`/compatibility rejection rather than pretending Started/Ended;
- legacy event/version values are projected from the same canonical event/version source;
- legacy Snapshot is built on demand from canonical Snapshot and legacy `CookingScope` metadata;
- legacy `Restart(newScope,newEpoch)` is accepted only from canonical Ended with a terminal legacy-compatible outcome, requires same Session/World, different legacy MatchId and higher epoch, and creates one canonical successor lifecycle; it never maps to failed retry because the historical contract requires a different MatchId.

Shared-owner tests must compare object identity of `TryGetGameplay` results, canonical/legacy version progression and event counts before and after every legacy operation.

## 5. Minimal ET Phase A tree

```text
ET Scene
└── [ComponentOf(Scene)] CookingApplicationComponent
    └── [ComponentOf(CookingApplicationComponent)] CookingMatchRegistryComponent
        └── [ChildOf(CookingMatchRegistryComponent)] CookingMatchEntity
            └── [ComponentOf(CookingMatchEntity)] CookingRestaurantRuntimeComponent
                ├── [ComponentOf(CookingRestaurantRuntimeComponent)] CookingKitchenComponent
                └── [ComponentOf(CookingRestaurantRuntimeComponent)] CookingLevelComponent
                    └── [ComponentOf(CookingLevelComponent)] CookingLevelDriverComponent : IUpdate
```

Exact creation APIs:

```text
scene.AddComponent<CookingApplicationComponent>()
application.AddComponent<CookingMatchRegistryComponent>()
registry.AddChild<CookingMatchEntity>()
match.AddComponent<CookingRestaurantRuntimeComponent>()
runtime.AddComponent<CookingKitchenComponent>()
runtime.AddComponent<CookingLevelComponent>()
level.AddComponent<CookingLevelDriverComponent>()
```

Because current `MatchId` is a string-valued legacy contract and global identity migration is out of scope, Phase A does **not** force `CookingMatchEntity.Id == MatchId`. ET allocates the process-local Match Entity Id; `CookingMatchIdentityComponent` stores the legacy `CookingScope`. That ET Id never enters domain/wire/save DTOs. Future strong-long identity migration follows the reference design under a separate task.

Analyzer acceptance must verify each exact Attribute parent, not only suffixes. Runtime tests must verify direct parent/component lookup and recursive release.

### 5.1 Installation sequence

1. Host constructs `EtRuntimeHost` and Scene.
2. Install `CookingApplicationComponent`.
3. Install `CookingMatchRegistryComponent` and create one compatibility `CookingMatchEntity` with `registry.AddChild<CookingMatchEntity>()`. ET allocates its process-local long Id; `CookingMatchIdentityComponent` stores the legacy string-valued `CookingScope` business identity.
4. Install `CookingRestaurantRuntimeComponent` with a RuntimeId field.
5. Install `CookingKitchenComponent` as Phase A authority reference holder only.
6. Prepare canonical Level lifecycle.
7. Install `CookingLevelComponent` with LevelScope/lifecycle reference.
8. Install `CookingLevelDriverComponent` before Start opens gameplay admission.
9. Start creates gameplay and binds the driver through non-throwing reference assignment.

If any ET installation fails, Level must not become Running and gameplay factory must not leave an admitted simulation.

### 5.2 Phase A authority boundary

`CookingKitchenComponent`/`CookingLevelComponent` may hold references to lifecycle/simulation/queues, but may not contain independently writable Item/Station/Process/Order copies. Those remain in `CookingRecipeSimulation` for this task.

The common `EtRuntimeHost` stays domain-neutral. Do not change its Tick signature or add Cooking timing concepts.

## 6. Host API and state

Conceptual host:

```text
CookingLevelEtHost
- owner thread id
- EtRuntimeHost
- application/match/runtime/kitchen/level/driver references
- CookingLevelLifecycle
- bounded pending envelope queue
- committed batch watermark
- HostFrameSequence
- fault state
- FinalDispositionHistory
```

Recommended operations:

```text
Prepare(LevelPreparation)
Start()
TryEnqueue(command)
Tick()
Pause()
Resume()
BeginEnd(outcome)
CompleteEnd()
CreateRetry(higherEpoch)
CreateSuccessor(newLevelId,higherEpoch)
Dispose()
```

Business/lifecycle rejection returns immutable operation/admission results. Wrong thread, reentry, disposed and faulted are exceptions.

## 7. Pause and generation transitions

### Pause

Atomic ordering:

```text
close new admission
  -> preserve existing live queue/frozen batch
  -> transition Running -> Paused
  -> stop ET/fixed Tick advancement
```

Paused `TryEnqueue` returns `LevelPaused`, does not occupy capacity. Pause/Resume preserves:

```text
LevelScope / LevelEpoch
HostFrameSequence
LogicalTick / StateVersion
committed batch watermark
snapshot/event watermark
pending queue and original SimulationBatch
```

Resume reopens admission. Preserved commands receive ordinary stable sorting and current-state validation when executed.

### Failed retry

```text
close admission
  -> cancel all old-epoch pending/frozen envelopes with ordered dispositions
  -> BeginEnd(Failed)
  -> complete pending cancellation/close
  -> CompleteEnd()
  -> remove old LevelDriver/Level Component
  -> rebuild failed Kitchen supplies (deferred; task only verifies seam)
  -> create Level Component with same LevelId, higher Epoch
  -> reset Level-local Tick/batch/snapshot/event watermarks
  -> preserve HostFrameSequence
```

### Success successor

```text
close admission
  -> settle successful level (deferred)
  -> BeginEnd(Success)
  -> complete settlement seam/close
  -> CompleteEnd()
  -> remove old LevelDriver/Level Component
  -> preserve Kitchen state (deferred; task only verifies seam)
  -> create Level Component with new LevelId, higher Epoch
  -> preserve HostFrameSequence
```

## 8. Frame pipeline

```text
Check owner / Running / idle / not faulted
  -> freeze pending envelopes
  -> choose minimum SimulationBatch
  -> retain larger batches in live queue
  -> group by Session/Player/Command
  -> collapse identical fingerprints
  -> reject conflicting fingerprint groups before mutation
  -> stable sort representatives by PlayerId then CommandId
  -> submit commands to CookingRecipeSimulation
  -> simulation.AdvanceFixedTick(levelScope, hostFrameSequence)
  -> collect command dispositions + fixed-tick result
  -> return immutable frame result
```

Empty queue still advances fixed Tick. After B commits, admission rejects `SimulationBatch <= B`; batch gaps are allowed. Enqueues during Tick fail through owner-thread reentry semantics.

## 9. Domain fixed-tick transaction

Conceptual API:

```csharp
public CookingRecipeTickResult AdvanceFixedTick(
    CookingLevelScope levelScope,
    long hostFrameSequence);
```

### 9.1 Concrete staged plan

Add the new method beside the existing direct `AdvanceTicks`; do not replace or delegate the legacy path.

Inject a pure product identity seam through an optional constructor argument:

```csharp
public interface ICookingProductIdAllocator
{
    ItemId GetProductId(long productSequence);
}
```

Production maps a positive sequence to `product-{sequence}`. The allocator is pure: it does not mutate simulation state or retain the committed counter. Tests may inject fixed/recording allocators to force deterministic collisions and verify call order without reflection.

Private staged structure:

```text
FixedTickPlan
├── before/after LogicalTick, StateVersion, EventSequence
├── next committed ProductSequence
├── ItemUpdates
├── NewProducts
├── ProcessUpserts
├── ProcessStationRemovals
├── ProcessIdIndexRemovals
├── ordered ProcessResults
└── TickEvent
```

Preflight order:

1. validate lifecycle open, host frame > 0 and `LevelScope.MatchScope == fixture.Scope`;
2. checked arithmetic for all scalar candidates;
3. enumerate active Process states by ordinal ProcessId;
4. validate recipe, station, input item, input location and both process indexes;
5. stage elapsed progress or completion;
6. for completion, call allocator with staged sequence and reject blank/current/staged duplicate product IDs;
7. build complete immutable results/event in locals;
8. execute a final no-validation/no-external-call commit block.

The final commit assigns scalar watermarks/counter, applies item updates/products, process removals/upserts/index changes and appends the tick event. All dictionary collisions/capacity/invariants must be resolved before commit; the commit path is designed not to throw.

Atomicity is scoped to the fixed-step delta. Existing command representatives execute before this method and are not rolled back if the fixed step later faults. Collision tests asserting whole simulation equality therefore use a frame with zero accepted mutating command representatives. Full command+tick transactional staging is a successor refactor.

`ICookingOrderPort` is never called by `AdvanceFixedTick`. `SubmitOrder` remains in the existing command lane before fixed Tick; its external side effects are outside fixed-step atomicity. No preflight call probes the port.

### 9.2 Failed frame publication

```text
candidateFrame = checked(HostFrameSequence + 1)
  -> run command lane
  -> AdvanceFixedTick(scope, candidateFrame)
  -> on success publish HostFrameSequence = candidateFrame
```

If fixed-step preflight/commit fails:

- no tick event/result is published;
- candidate HostFrameSequence is not consumed;
- fixed-step LogicalTick/state version/product counter/process/item/tick-event history remain unchanged;
- already committed command-lane effects remain visible;
- host becomes faulted and cannot Tick again.


Recommended result contracts:

```text
CookingRecipeTickEvent
CookingRecipeTickResult
CookingRecipeProcessTickResult
CookingLevelFrameResult
CookingLevelAdmissionResult
CookingLevelOperationResult
CookingPendingCommandDisposition
```

No result references ET types.

## 10. Command arbitration and compatibility

### 10.1 Legacy driver boundary

`CookingRecipeTickHost` and its internal `CookingRecipeDriver : Entity` remain an isolated legacy compatibility seam for existing tests only. They are not attached to or reused by `CookingLevelEtHost`.

- new host creates only `CookingLevelDriverComponent`;
- legacy host creates only `CookingRecipeDriver`;
- one simulation instance is owned by exactly one host/driver path;
- constructors/tests must reject or prevent sharing the same simulation between both hosts;
- no common ET UpdateSystem invokes both drivers;
- existing `CookingVerticalSliceTests` lock the legacy seam; new `CookingLevelEtHostTests` lock the canonical seam;
- removal/conversion of the legacy driver is a later migration after callers are zero.

This task may extract pure non-ET helper code for arbitration, but it must not generalize the legacy Entity into the canonical driver or allow duplicate updates.

### 10.2 Typed arbitration contracts

Pure C# contracts:

```text
CookingLevelCommandEnvelope
  LevelScope
  canonical effective CookingRecipeCommand
  SourceConnectionId (diagnostic)
  CorrelationId (diagnostic)

CookingLevelCommandGroupKey
  LevelScope + PlayerId + RecipeCommandId

CookingCommandFingerprint
  SHA-256 of canonical semantic bytes

CookingLevelAdmissionResult
CookingLevelPendingDisposition
CookingLevelCommandLedgerEntry
CookingLevelFrameResult
```

The host canonicalizes authenticated scope/player before fingerprinting. Fingerprint bytes use explicit field order, length-prefixed ordinal strings, explicit null markers and invariant numeric/enum encoding over:

```text
MatchScope Session/World/Match
RestaurantRuntimeId / LevelId / LevelEpoch
SimulationBatch / Player / Command / Operation
Recipe / Process / Item / Station / Container / Order
ExpectedItemVersion / TickCount
```

Connection, correlation, arrival order and ET identities are excluded. A shared canonicalizer replaces new ad-hoc JSON fingerprinting; legacy simulation fingerprint behavior remains unchanged for compatibility.

Grouping key is `(LevelScope, PlayerId, RecipeCommandId)`.

- one distinct fingerprint: choose representative by ordinal `(SourceConnectionId, CorrelationId)`; execute once; remaining envelopes are duplicate dispositions using the representative result with empty events;
- multiple fingerprints: reject every envelope as `CommandIdentityConflict`; execute none; seed no simulation ledger entry; record terminal host conflict history for that epoch;
- no result order depends on queue arrival.

Frame dispositions sort by:

```text
PlayerId
CommandId
Fingerprint
SourceConnectionId
CorrelationId
```

all ordinal. Process tick results sort by ProcessId.

### 10.3 Host ledger, capacity and stale batches

Host ledger states:

```text
Pending | Executed | Conflicted | Cancelled | Stale
```

- capacity counts unique live Pending group keys, not raw duplicate envelopes;
- first unique pending group consumes one slot;
- identical duplicate or conflicting payload for an existing group consumes no additional slot;
- lifecycle/scope/player/malformed/reserved-clock/stale rejection consumes no slot;
- executing, conflict-finalizing or cancelling a group releases its live slot;
- future batches count while pending; a frozen batch counts until terminal dispositions are materialized;
- cancelled/stale/conflicted identity is terminal for the current LevelEpoch and returns its cached disposition on re-ingress; it is not represented by a null result;
- retry/successor creates a new generation ledger, so the same command ID may be used only under the new LevelEpoch.

`LastCommittedSimulationBatch` is Level-local. Before the first commit any positive batch is admissible. After committing B, `SimulationBatch <= B` is `BatchStale`; batch gaps are allowed and the minimum queued future batch executes next. Old LevelEpoch rejection happens before capacity accounting.


## 11. Error and disposal

- domain rejections do not fault host;
- invariant/programming exceptions inside ET update are captured and rethrown by `Tick`, then host stays faulted;
- no post-fault operation may report success;
- idle owner-thread Dispose is allowed after fault;
- End/Retry/Successor operation results include each pending command exactly once;
- Dispose writes pending dispositions to immutable `FinalDispositionHistory` before clearing queue;
- cancelled commands never reach simulation ledger;
- normal closure removes LevelDriver then Level Component, closes/removes Runtime only when requested, then disposes Match/application tree at host Dispose.

## 12. Compatibility constraints

- no Cooking Unity files, scenes, GameObjects, MonoBehaviours, Editors or asmdef gameplay integration;
- no global `CookingScope/MatchId` migration;
- no LAN/UDP schema change;
- no checkpoint implementation;
- no ET gameplay Entity authority migration;
- no ECS retirement;
- no change to generic ET runtime licensing/distribution boundary.

## 13. Validation design

### Canonical Level lifecycle tests

- valid preparation to Ready;
- illegal transitions mutation-free;
- Start creates exactly one gameplay;
- Running/Pause/Resume watermarks and admission;
- Failed End + retry same LevelId/higher Epoch;
- Success End + successor new LevelId/higher Epoch;
- old scope/epoch/snapshot rejected;
- gameplay closed after Ended.

### Compatibility tests

- existing `CookingMatchLifecycleTests` remain green;
- facade and canonical lifecycle share one gameplay owner/state/version;
- old Restart projects to canonical successor without duplicating state;
- obsolete API cannot observe unsupported Paused/Ending state as a false success.

### Fixed-tick domain tests

- empty Tick increments LogicalTick/version once;
- one/multiple Process progression;
- stable same-frame completion/product/event order;
- staged collision is mutation-free;
- direct legacy AdvanceTicks unchanged.

### ET host tests

- correct ComponentOf/ChildOf tree and recursive release;
- failed install prevents Running/factory leak;
- one minimum batch per frame;
- duplicate/conflict arbitration;
- command-before-clock ordering;
- Pause rejects new commands and preserves old queue/watermarks;
- retry cancels old epoch queue and resets local watermarks only;
- successor identity isolation;
- owner thread/reentry/capacity/fault/Dispose history.

## 14. Planned gates

Before the dedicated gate exists, the authoritative task-local evidence contract is:

```text
artifacts/cooking-et-level-fixed-tick/cooking-level-fixed-tick.trx
  <- complete AbilityKit.Game.Cooking.Tests project

artifacts/cooking-et-level-fixed-tick/et-level-fixed-tick.trx
  <- complete AbilityKit.ET.Runtime.Tests project
```

Both full-project runs are mandatory and any failed test blocks delivery. Focused `Trait("Gate", "CookingLevelRuntime")` filters are supplemental diagnostics only. Implementation must add the P1 gate below before final delivery; after registration the gate becomes the canonical repeatable entry and executes the same two complete projects.

Implementation gate definition:

```text
name: cooking-et-level-runtime
owner: Cooking Game Runtime
scope: cooking, et-runtime, level-lifecycle, fixed-tick
requiredBefore: merge-cooking-level-lifecycle-or-et-runtime-change
steps:
  dotnet-test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj
  dotnet-test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj
```

Focused tests also use `Trait("Gate", "CookingLevelRuntime")` for reporting, but the authoritative gate runs both complete projects so an empty filter cannot pass silently.

```text
dotnet build src/AbilityKit.Game.Cooking.EtRuntime/AbilityKit.Game.Cooking.EtRuntime.csproj
dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj
dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-udp
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate precheck
```

`cooking-udp` is legacy compatibility evidence only. Unity compile is not acceptance because Cooking Unity remains prohibited. Run `core-stability` only if shared runtime/core behavior changes. Report skipped/blocked/failing gates truthfully.
