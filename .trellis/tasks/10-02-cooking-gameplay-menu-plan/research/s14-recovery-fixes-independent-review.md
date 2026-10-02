# S14 recovery fixes: independent read-only review

Reviewed baseline: main ac3769465 plus the initial six root-owned WIP files supplied in the dispatch. Source was read only; no .NET or production mutation was performed. Root and technical producers may make subsequent corrections; those require their own focused/composite evidence.

## Accepted bounded fixes

- CookingRecipeCheckpoint.cs hand grouping (~717) and installation (~727) both exclude Removed items. Historical disposed cup hand locations remain in the item tombstones/canonical checkpoint but do not claim a live hand. This removes the actual resurrection/duplicate-occupancy inconsistency without deleting history or changing schema. The two added OrderBinding regression cases exercise removed disposable hand ownership; runtime owners remain unchanged.
- CookingRecipeLoop.cs IsSupportedProcessDuration (~1338) accepts exactly the configured base duration or the current trusted CookingMajorProgress.CookTicks result. Fixed tick validation (~1368) and restore validation (Checkpoint ~503) use the same predicate. Arbitrary saved durations remain rejected. Base-six work begun before a modifier remains six; trusted accelerated soup work may retain three. Positive recipe durations and remaining elapsed bounds are still checked through the existing validation path. No saved payload grants a buff.
- CreateGenerationTransactionCopy installs the existing private trusted progress reference before checkpoint validation. It still uses the throw-on-allocation staging allocator and copies effective geometry/menu policy, with no published second driver or external wash callback.
- Host durable staging and load attach validated confirmed progress before AcceptSuccessHandoff. Same-Level restore resolves the external confirmed provider once, rejects malformed/duplicate choices and current menu unlocks exceeding confirmed choices, constructs locked progress through CreateProgress, and applies it before Recipe RestoreCheckpoint. Provider data is application input, not saved grants. Existing acquisition/scope/release boundaries and canonical/schema formats remain unchanged by this increment.

The four CookingEffectiveProcessTimingTests source cases explicitly cover accelerated versus original-start duration, missing grant and arbitrary-duration atomic rejection, exact continuation, and private-copy validation order. Dispatch reports actual three-red/one-green before correction and forty-five green after correction. The hand regression reports two red then thirty-five green. This reviewer inspected their source paths, did not rerun them or independently read the original run artifacts; these numbers are producer evidence, not a new reviewer execution claim.

## Concrete remaining findings in the initial WIP

### Public ApplyRetryChoices rejection can mutate process bindings

CookingRecipeLoop.cs ApplyRetryChoices (~864) calls MigrateStations before planning/validating every unlock. MigrateStations (~783-795) commits process station indexes and clears ActiveWorker. A valid active process replacement followed by an unknown unlock causes ApplyRetryChoices to return UnknownChoice (~887) after those changes. This contradicts the method's documented whole-call rejection/no-change contract. It is a process-binding/claim defect, not a geometry installation claim. New private Host candidates protect the prior live source, but that does not repair the public API contract.

Recommendation: root-owned minimal transactional preflight or isolated staged state for all decoration and unlock placement before any process-index/claim mutation. Preserve the strict public two-argument UnknownChoice contract and add a real process-binding rollback control.

### Host global confirmed unlocks need a current-level eligible placement subset

The initial Host retry passes all progress.Unlocks to ApplyRetryChoices; that method requires each definition in current StandardInitialSupply and fixture.Items. An ever-unlocked definition absent from this level therefore rejects retry even though global confirmed choices are allowed to persist outside current LevelAllowed. If a definition exists in the loaded content but is disallowed in this level, its mere global confirmation must not spawn new stock or grant manufacturing.

Recommendation: keep full global progress, but derive an explicit internal current-level eligible physical-placement subset from trusted registered/current supply and actual material grants. Filter/reapply only those definitions once. Do not weaken the existing strict public API or clear global confirmations. Root has acknowledged this finding and plans a separate Host/internal correction.

## Rejected hypothesis: clean-pool count loss

The initial retry filter removes confirmed definitions from a transient content object's StandardInitialSupply before ApplyStandardInitialSupply. An early concern that this removes clean-pool counts was incorrect. ContentCatalog.BuildFixture (~154-164) and simulation construction already seed the pool from the ORIGINAL trusted content before this filter; ApplyStandardInitialSupply (~170-177) always skips cleanPool entries because construction owns them. The transient filter does not rebuild the fixture. Therefore there is no source-backed pool-loss finding from this change. Root reports a real confirmed-cup retry count-one control; retaining pool entries in the transient list is semantic clarity, not a demonstrated required fix.

## Boundaries and verification

- No architecture, owner, checkpoint schema or arbitrary-duration grant introduced by the accepted changes.
- No lint/type-check/test execution by this read-only reviewer. Producer actual focused results require root evidence routes and combined gates after the remaining fixes.
- Accepted scoped-carry commit 9cabcd347 remains a no-Front scope unit control: catalog F01+D31 to F01-only, real ET procurement/production/receipt/recovery, owner reference assertions, full Running codec continuation canonical equality. Its own actual focused result is 1/1 with zero failures/skips, predating these separate timing changes.
- Natural full-flow/durable acceptance and other S14 exits are separately owned. Static acceptance of these recovery predicates is not full S14 completion; network and Unity remain outside this review.

## Follow-up review of the root eight-file retry transaction increment

Read-only source follow-up after root copied the increment to integration; no .NET or production commit by this reviewer.

The two prior findings are structurally closed:

1. Public ApplyRetryChoices now delegates to CookingRetryChoiceTransaction.ApplyRetryChoicesAtomic. It checks owner/lifecycle/reentry, creates a private generation copy, applies all station/unlock changes there, and only restores the accepted staged checkpoint into the live kitchen. An unknown unlock or placement rejection does not commit process binding or worker changes. The public two-argument overload passes null eligibility and retains strict UnknownChoice behavior. Root reports actual two red controls followed by composite 47/47 green; not independently rerun here.
2. Host freezes scoped Front/menu before retry stock planning, derives eligibleUnlocks from current registered content, standard supply and (Base union Confirmed) intersect Allowed, invokes the internal ForLevel overload, and leaves the complete global progress object intact. Absent or disallowed global confirmations no longer make eligible placement mandatory. Standard confirmed world/station entries are omitted from base stock and reapplied once. Clean-pool entries remain in the transient list for clarity, while constructor ownership of pool counts is unchanged.

### New source-backed consistency edge: commit validation uses old progress

ApplyRetryChoicesAtomic calls receiver RestoreExportedCheckpoint(staged.ExportCheckpoint()) before assigning receiver _majorProgress = progress. The staged core has already attached the NEW progress, but no validation is performed against that intended progress before live restore.

Concrete counterexample: an unbound live kernel has an active accelerated soup process RequiredTicks=3 under old confirmed CookFaster=true. Call the public API with a valid empty new progress CookFaster=false. The private copy restores correctly using old true, the core attaches false without changing the active process, the live restore validates RequiredTicks=3 using receiver's old true and accepts, then receiver progress becomes false. Next fixed-tick validation rejects the now unsupported duration. This is an ordinary invalid accepted state, not an arbitrary saved grant. Fresh Host retries normally lack active processes, but the public guarded API permits this existing active-kernel case.

Recommendation: validate the complete staged checkpoint under the staged/new progress before any live installation (or explicitly reject removing a modifier while its shortened work exists). Preserve the old receiver progress on failure. Root notified; this finding has not been executed or fixed by the reviewer. Prior fixed-tick/restore timing predicates remain accepted; this follow-up cannot yet declare the new atomic retry transaction closed until intended-progress commit consistency is verified.

## Final follow-up: intended-progress guard closes the new edge

Read-only review of the latest root guard confirms CookingRetryChoiceTransaction now exports the accepted private staged state, restores/validates it on the staged owner itself (which already holds intended NEW progress), and only then restores it into the receiver and assigns the progress reference. New false choices with an old accelerated RequiredTicks=3 process therefore reject before touching the receiver. No old-grant validation can authorize the incompatible committed state.

The new Retry_choices_cannot_remove_the_grant_required_by_active_work regression creates real trusted accelerated work, attempts empty locked replacement choices, asserts rejection and complete checkpoint equality, then advances the original kernel successfully to elapsed one. This proves both rejection atomicity and retention of the original timing grant.

Independently read `local/Logs/retry-atomic-grant-green.log` and parsed `local/Logs/retry-atomic/retry-atomic-grant-green.trx`: actual 48 total/executed/passed, zero failed/error/timeout/aborted/notExecuted; the named regression is Passed. The preceding actual one-case red was independently read at `local/Logs/retry-grant-red.log` and its TRX `local/Logs/retry-atomic/retry-grant-red.trx` parsed: one executed/failed, zero passed/skipped, with Assert.False actual True for the named grant-removal regression. No .NET was executed here.

All three source-backed findings raised in this report are now closed for the reviewed increment: public whole-call retry rejection atomicity, current-level eligible physical unlock placement with preserved global choices, and intended-progress commit timing consistency. No additional blocker was found in this bounded re-review. Full S14 acceptance still depends on final integrated/master gates and separately owned natural/technical controls; static closure and the observed 48-domain focus do not replace those exits.

## Bounded follow-up: trusted factory seed fulfills exact standard entries

Reviewed the latest Host SeedProvides block read-only. Before inspection, the next factory kitchen has already been acquired and checked against the candidate match scope. The comparison uses that trusted fresh factory snapshot; neither saved source inventory nor baseline payload is treated as permission to fulfill retry stock.

For each world/station standard entry, only exact definition/location/count matches qualify. Every matching item must have version one, no product/dirty/completed/remaining-portion/binding/supply-origin state, no anchored process and empty contents if it is a container. Matched entries are excluded from the missing stock list; unfulfilled entries follow the existing standard application and eligible unlock placement. The same missing list is used for the private unlock core, preventing re-adding a factory-seeded confirmed tool. Clean pools remain constructor-owned. Global confirmed progress remains complete; current-menu intersection still controls eligible new unlock placement. No carried failed-generation stock is intentionally reused.

This closes the concrete duplicate-pristine-tool case: a trusted catalog factory has already seeded one empty tool at world:h0 and declared that same one-item standard entry; the Host no longer attempts a second single-slot insertion. A partial, mismatched or non-pristine occupant does not silently authorize fulfillment; the existing candidate staging may reject rather than repair arbitrary factory contents. This is exact entry fulfillment, not a universal trusted factory-state validator.

An initial wording question about "no process" was clarified by root: the contract is matching objects having no anchored active process, not global process absence. Source implements that object-local condition. No global process prohibition is inferred or requested by this review.

Root reports actual natural/catalog two-case red from duplicate seed followed by latest four-case green, including complete 87-menu retry and known-but-current-forbidden unlock, while previous natural/durable continuation controls remain green. These four results are producer evidence pending their routed artifacts and final combined gates, not tests executed by this reviewer. No additional bounded blocker found in SeedProvides. Source, owner, canonical/schema and allocation authority remain unchanged. Full S14 and arbitrary trusted factory seed semantics are not concluded from this local acceptance.
