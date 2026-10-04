# Independent review: Issue #5

This is the historical first local review, before dot's review of 4fe2ff9f2. Its supply-closing conclusion was corrected under R1; current R1-R3 findings and source verification are in [revision-review-round1.md](revision-review-round1.md). Preserve the initial evidence without treating its original acceptance as current dot approval.

2026-10-04. Result: Passed for the authorized documentation and rule scope, after two local documentation fixes. This does not accept runtime migration, network exits, performance, Unity, dependency closure or release safety. .NET/Unity/game/network/protoc: NotRun.

## Findings fixed

- Newly authored root/task prose was initially replaced by literal question marks. Parent regenerated new text in UTF-8; reviewer inspected the readable final AGENTS/progress/history/reference and task context. Historical corrupted source was deliberately left unchanged. The previously broken test-strategy link now resolves.
- Contract C HostFrameSequence row previously described a complete successful frame. Actual ExecuteFrameCore assigns the counter immediately after AdvanceFixedTick and before front-of-house Step. Reviewer corrected the row to explicitly preserve this boundary and possible later failure.
- Contract E mapped checks but did not explicitly identify review/evidence responsibility. Reviewer added Orca evidence collection, dot review, and the separate future #6/#7/#8/#9 responsibility routes, without claiming an assigned executor or unlocking blocked work.

## Requirement-by-requirement audit

| Requirement | Result | Current evidence |
|---|---|---|
| A actual and proposed trees | Passed | A diagrams match Host component relationships; Kitchen/Level siblings, Driver under Level, ordinary Simulation still owns stores. Target names explicitly proposed. |
| A all ten state families | Passed | Item; hand/container; Station; Process; Order/settlement; inventory/supply; Session; dedup; event watermark; version/allocator rows each specify current file/symbol, proposed owner/System, creation/carry/disposal/rebuild/index, consistency closure, withdrawn writer, consumer adaptation and positive/negative controls. Shared owner/reentry/scope/candidate/disposal checks supplement per-family controls. |
| A identity and migration semantics | Passed | Parent is release ownership, stable IDs model relationships; InstanceId/EntityRef remain incarnation checks. DTO/event/command entries are not mechanically entities. M1-M5 require complete state/reference transaction groups and old writer removal. |
| B five message classes/all axes | Passed | Matrix separates intent, admission/terminal, committed facts, full State+Session projection, controls. Producer/consumer, owner context, freeze, identities, scope, order, dedup, late/cancel/error, capacity and observations are explicit. Context fields need not be added universally. |
| B ordinary/retry sequences | Passed | Six ordinary steps trace SendCommandAsync, ingress, Map, Adapter, TryEnqueueNetwork, Submit/terminalize, fixed step, Complete/capture/Publish, install/exact ACK/Ready. Retry distinguishes submitted reply loss, CloseOwner cancellation/cleanup, credentialed Join generation increment, full rebind baseline, cached same-payload terminal and conservation checks. No Close/Rebind wire enum invented. |
| C staged transaction | Passed | Inspected ExecuteFrameCore lines 1720-1811 and test Fixed_step_failure_after_accepted_command_preserves_the_command_effect_and_final_history lines 577-595: Accepted effects/event/Executed retained, both clocks zero on fixed-step failure. Front Step happens after counter assignment. Target whole-frame atomicity conflict explicitly recorded without runtime change. |
| C clocks/completion/idempotence | Passed | LogicalTick, HostFrameSequence, batch/ordinal, network deadline/elapsed and separate Task/terminal/ACK/projection facts defined. Same stable+payload and old-incarnation rules retained; no deadline loosening. |
| D capabilities and protocols | Passed | Project Compile Include inspected; EtRuntimeHost installs EntitySystem, not EventSystem, single-active guard confirmed. Ledger distinguishes source wiring from fresh runtime proof. JSON Wire3, catalog 75 MemoryPack/13 custom-binary/0 protobuf, ET proto MemoryPack and exporter-only protobuf remain distinct. |
| D provenance/dependency boundary | Passed with explicit unknowns | Four recorded local SHA256 values independently match bytes. core/sourcegenerator packaging identity, trimmed modules, local patches, license/internal-only, host/consumer/retirement routes recorded. Unknown upstream commit, full patch/tool hashes, restore closure and responsible maintainers explicitly remain #7 work, not guessed. No upgrade/release or legal conclusion. |
| E concise rules/history/safety | Passed | Readable AGENTS routes current state only to progress, retains owner/lifecycle/message/transaction before-after, readonly/DTO/stateless exceptions, environment and evidence distinctions, internal-only/positive-negative requirements, Orca/Trellis/worktree/Unity safety and Issue authority limits. |
| E gate/check mapping | Passed | Eight E rows map current limited checks, future bounded checks, manual review and positive/negative controls; added responsibility routes. No new checker claimed implemented; AKET001/002 scope limited. |
| Preserve accepted history and pending exits | Passed | progress/contract retain historical S01-S14/N01 only within SHA/scope, N02/N03 incomplete, physical unavailable, Unity/S15 deferred, performance not passed. Blocked #1-4/#6-9 are not unlocked. ADR-0003 remains Proposed. |
| Scope and validation | Passed for inspected documents | Git tracked diff lists AGENTS/progress/ET reference only; new contract/history/task files are documentation. Parent final staged-scope check still required before commit. No runtime commands executed. |

## Actual document checks

- Python strict UTF-8 decoding of AGENTS, progress, contract, history and ET reference: Passed.
- Local Markdown links outside fenced source blocks across these five documents: zero missing targets.
- Markdown fences: balanced (0/0/4/2/58 delimiters respectively).
- Four contract SHA256 entries: all matched current file bytes.
- Historical fenced original, normalized only for line endings/surrounding whitespace: equals `git show 5312c6e4bf2b612260297e2d8623aa362a9051e6:AGENTS.md`. Original byte hash `3d75d0c11590abaade4446a4fb3fcdb069615cad34f682800ba5bd9541c853c5` matches stated provenance.
- git diff --check: exit 0; Git emitted only CRLF-to-LF normalization notices, no whitespace errors.

## Findings not fixed

No remaining in-scope defect identified in reviewed documents. Future runtime/CI/dependency/protocol gates explicitly pending in #6-#9 are not defects this documentation task is authorized to implement. Remote Issue report/commit or PR and final source-scope review are main-session closeout actions, not completed by this reviewer.

## Verification

- Documentation lint/link/encoding/diff: Passed.
- TypeCheck: NotRun (documentation-only; no code changes).
- Runtime tests: NotRun (documentation-only).

## Final host-closure supplement

Inspected Runtime/AbilityKit.ET.Runtime.asmdef: references empty, noEngineReferences true, allowUnsafeCode true, autoReferenced false. Inspected LiteNet SDK csproj: Network.Runtime and Network.Host project references plus LiteNetLib NuGet 2.1.4. Inspected LiteNet UPM package.json: both framework dependencies 0.1.0 and explicit requirement to install LiteNetLib.dll separately. The final D paragraph matches all three source files; it correctly does not claim actual restore, DLL identity or Unity runtime verification.
