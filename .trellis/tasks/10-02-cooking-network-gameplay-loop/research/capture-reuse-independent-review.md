# Readonly capture reuse independent review

Decision: the exact bounded source change is acceptable for focused verification. No source-backed blocker found. No optimized runtime/test result was available to this reviewer at this decision; root owns actual authority focus and rich rerun. No .NET was run by the reviewer.

## Source

Managed assembled tree `C:/Users/Administrator/orca/workspaces/AbilityKit/cooking-network-process-current`, current uncommitted diff in `src/AbilityKit.Game.Cooking.EtRuntime/CookingLevelEtHost.cs`, CaptureReadOnlyFullState. The diff moves ExportCheckpoint before standalone Recipe/Front exports and uses its already-copied Recipe and FrontOfHouse payloads when available; otherwise it exports the existing readonly projections.

NetworkUnavailable still checks owner thread, Disposed/Faulted and every existing Busy mutation flag before any read. Observe, checkpoint availability, lifecycle/preparation/layout/menu/config identities, logical/service clocks, LastCommitted batch and major display are unchanged. ExportCheckpoint performs only owner reads and copied payload creation: it rejects Paused/non-running/non-preparing/pending/no-preparation cases, exports Recipe, verifies its LevelScope binding, then exports Front. No factory invocation, gameplay command, timer, advance or actor mutation was added.

For a successful resumable owner capture the same committed checkpoint now supplies FullRecipe and FullFront. There is no intervening authority operation between those reads, so their canonical state and clock refer to the same owner boundary as before. The objects are detached checkpoint payloads, not live dictionaries or mutable simulation ownership. Sharing that detached payload between the two display positions does not create mutation authority; consumers must continue treating snapshots as readonly. No restoration or client grant is changed.

Created with no kitchen still yields null Recipe. Paused or other non-resumable states preserve the explicit standalone Recipe/Front exports and unavailable reason. A null checkpoint Front with a real front owner still takes the old fallback. ExportCheckpoint may already export Recipe before a LevelScope mismatch rejection; that non-resumable path can still perform a second export, so the reduction is specifically the successful resumable path, not every state or every frame.

## Required proof and bounded cost claim

Root should run its existing 15 authority controls, retaining Created, Paused, Faulted and reentrant Busy cases. A useful explicit assertion in a successful Running/Preparing resumable case is FullRecipe.CanonicalText == ResumableCheckpoint.Recipe.CanonicalText and corresponding FullFront canonical equality; repeated captures must leave Recipe, Front, HostFrame and command watermarks unchanged. Existing full wire roundtrip controls cover complete state serialization. These checks are about equality and nonmutation; no assertion should depend on reference identity or claim clients can modify snapshots.

Statically, a successful resumable capture reduces standalone Recipe/Front export calls from two each to one each. Observe and ExportCheckpoint validation remain; no preflight was removed. This says nothing about three captures per Session frame, serialization/reflection costs, UDP queueing, total rich duration or throughput. Actual optimized evidence must be appended by root rather than inferred from this source review.

## Actual root focused evidence

Root subsequently ran the authority focus in process-current with explicit canonical reuse assertions. Independently inspected source now asserts running FullRecipe canonical equals both the real simulation export and ResumableCheckpoint.Recipe, and FullFront canonical equals ResumableCheckpoint.FrontOfHouse before retaining the Paused readonly checks. Independently parsed `src/AbilityKit.ET.Runtime.Tests/TestResults/capture-reuse-canonical.trx`: 15 executed/passed, zero failed/notExecuted. `local/Logs/network-capture-reuse-canonical.log` confirms the actual net10.0 focused build/test and 15 passed / 0 failed / 0 skipped, approximately 440 ms.

The previously recommended successful-resumable equality assertions are therefore verified by this focused run. This closes that bounded proof gap; it is not a representative process latency/throughput result or broad network completion. The reviewer only read source/log/TRX and did not run .NET.
