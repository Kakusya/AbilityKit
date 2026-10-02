# S14 generation transaction ET increment

Integration base 16fdc55a2. Reviewer ownership: only CookingLevelEtHost.cs and new CookingGenerationTransactionEtTests.cs. Root supplied CreateGenerationTransactionCopy and PrepareGenerationStateAdoption domain APIs and owns their tests. No commit by reviewer.

## Contract and implementation

Successor and retry construct unpublished candidates before generation replacement. Source kitchen copy uses the root-provided private allocator which throws on any allocation and has no external wash port; no source factory, timer, actor or ledger runs. A privately restored front finishes its existing work and resets using existing rules; only its copy is changed. Success handoff retains objects, portions, unfinished work, stock/origins, remaining delivery waits and ID allocation watermarks while clearing old generation orders/events/dedup and worker claims.

Next factory supplies the actual trusted next-scope initial poses. Next preparation policy projects its InitialLayout using those poses, checks handoff references with the next poses, installs geometry before AcceptSuccessHandoff, and derives trusted front flow when present. It never uses source live positions for next seeds. Candidate standard objects are replaced by success carry, not added to it. Missing supplier identity, anchors, invalid policy, factory failure or a factory returning the source object reject before live publication. Spatial preparation factories require the explicit CreateSuccessor(level, epoch, nextPreparation) overload; legacy non-preparation factories retain old signature compatibility.

Candidate lifecycle adopts the validated kitchen before ET installation. Ownership and both simulation-publication injection points are checked before replacing source. ET LevelCreated/DriverCreated/BeforeLevelPublish failures restore source binding/tree/driver references, leave source lifecycle uncommitted and return a structured rejection without faulting the old host. Queues and terminal state are cancelled/reset only after successful lifecycle commit. Committed source kitchen ownership is then released/closed. No potentially failing factory is called after commit; frozen staged policy is reused by next BeginPreparation.

Legacy injected front uses the prepared adoption Action supplied by root, retaining its existing external object identity and callbacks rather than replaying FinishInProgress after commit. Retry creates standard supply and configured supplier balances, applies confirmed choices, and carries no failed scene processes, portions, delivery/origin state or allocator watermark.

## Actual evidence

- Initial new-test run 0/11: fixture omitted mandatory layout customer entrance/exit. Added these real targets; production layout validation unchanged.
- Second run 0/11: fixture moved player right before using a container behind its facing. Removed that unnecessary move; production reach validation unchanged.
- Third run 10/11: retry fixture attempted two objects on one ordinary world slot and a conflicting unlock. Corrected to one standard raw object and a confirmed cooking buff; retained single-slot behavior rather than weakening the rules.
- Combined host/closed-loop/front/preparation/layout/observation/generation run: 85/86 passed, zero skips. The sole failure is the historical Successor_install_failure_rolls_back_partial_tree_keeps_old_binding_and_faults_host test expecting an exception/faulted host; root explicitly approved the new structured rejection/retryable contract and owns updating that old test. All other legacy tests in the filter passed, including external front identity/reset behavior.
- Replay fixture initially used an incorrect DeliveryId property name, then missed actual facing toward receiving. Both corrected in tests; compile/failure evidence preserved.
- Final new generation focused run: 14/14 passed, zero skips, no compiler warning/error. Host diff check passed.
- New successful carry scenario actually procures a package, cooks a three-portion RetainInputs container, serves one allocator-generated product, leaves another pan at manual elapsed one, stops its worker and creates a pending finite delivery. Next generation preserves remaining two portions, lock/process progress, stock balance, pending RemainingTicks and allocator counters; it installs the next factory seed (3500) instead of source pose (500), even though source pose remains legal in that geometry. Real next preparation ticks continue delivery waits and old-scope ingress rejects.
- Five success publication failure points each preserve complete Observe canonical, source lifecycle identity/version, driver identity, command/disposition counts and unfaulted retryability. Factory, missing receiving anchor, incompatible supplier config and same-source factory output also reject with unchanged source observation and later succeed.
- Two retry publication failure controls preserve the failed generation, driver and confirmed cooking buff, then retry successfully. Successful retry has standard raw stock, initial supplier balance, no deliveries/origins/processes/portions and zero product allocator watermark.
- Next-generation checkpoint restore is compared with uninterrupted execution through pending receipt, manual continuation/completion, cup clearing and another real ServePortion allocation. Final full checkpoint canonical is identical and next product counter strictly increases.

Evidence directory: local/Logs/cooking-execution/generation-transaction-et/ contains all failure, compile and final logs/TRX. Final 14/14 is focused verification, not full gate or completed S14.

## Remaining integration boundaries

Root must update the single obsolete fault expectation and independently review/run combined gates. This increment does not implement durable major-baseline store/load, Ready menu/material permission admission or all-87 natural operating fixture. Carry objects are not deleted or granted manufacturing permission; the next Ready permission gate remains the separately assigned follow-up. Choice-specific narrowing checks and full operating exit evidence remain with that integration. Network and Unity are outside this change.

Source and integration .NET window released to root after final focused run; unrelated source changes preserved.

### Ownership review correction

Root review found candidate mutation preceding host ownership acquisition. A real foreign-owned simulation control failed against the old implementation: cup version changed from 1 to 3 before acquisition rejection. Host now acquires immediately after factory return, before standard supply, geometry, or handoff; cleanup closes only an acquired, uncommitted candidate. The unused CreateRetryKitchen entry was removed. The control verifies unchanged complete foreign checkpoint and unchanged source observation, then a real foreign Move remains Accepted and source successor retry succeeds.

Final actual focused verification: CookingGenerationTransactionEtTests 15/15 passed, zero skipped (cooking-generation-ownership-green.log/trx). Earlier red and fixture-facing failure logs are preserved alongside the final evidence under local/Logs/cooking-execution/generation-transaction-et/. git diff --check passed; no full gate claimed for this correction. Host/new-test source frozen and integration .NET window released; no commit.

### Candidate match scope correction

Wrong_match_retry_candidate_is_rejected_before_mutation_or_generation_commit genuinely failed red: CreateRetry accepted a factory kitchen from another match. Host now checks Snapshot.Scope equals candidate.Scope.MatchScope immediately after acquiring ownership and before any standard supply/geometry/handoff mutation; mismatch returns GameplayInitializationFailed and candidate ownership is released by existing cleanup. Control proves original Observe, lifecycle and driver remain unchanged, followed by a successful retry with corrected factory.

Final actual focused generation suite: 16/16 passed, zero skipped; cooking-generation-scope-red/green log and TRX preserved in the same evidence directory. git diff --check passed. Earlier root 598/726/253 gate predates this correction and is not final evidence for it. Source frozen and integration .NET window released; no commit.
