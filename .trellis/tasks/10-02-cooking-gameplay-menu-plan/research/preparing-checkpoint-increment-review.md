# Preparing checkpoint increment

Base: integration 585e15982. Ownership: only CookingLevelEtHost.cs and CookingPreparingEtTests.cs; core AdoptRecoveredVersion change supplied by root. No commit by reviewer.

## Actual behavior

Export supports unpaused Running and initialized Preparing with no queued/in-flight commands and matching recipe LevelScope. Legacy Preparing without an initialized kitchen remains unsupported. Preparing export preserves State=Preparing, Outcome=null, service offset zero, frozen trusted preparation identity, and InstalledLayout=null.

Restore rejects unsupported State, non-null Outcome and any InstalledLayout before host creation. Preparing requires a trusted preparation factory/config identity and offset zero. Its trusted front service clock must remain zero. Restore calls BeginPreparation only, uses that initialized kitchen, restores recipe/front payloads and adopts version/frame watermarks without CompletePreparation or Start. Running retains its existing preparation/start route and service offset semantics.

No payload authorizes new content, trusted policy or layout. Actual layout installation/restoration remains deferred to the next serialized root increment.

## Evidence

- First focused run: 6/7 passed. Failure was a test assumption that zero ticks lacked a LevelScope binding. Inspection and actual export showed publication already binds the generation. The expectation was corrected and a dedicated zero-tick export/restore canonical control was added; production history validation was not weakened.
- Second focused run: 7/7 passed with two nullable fixture warnings. The non-null delivery id is now asserted via Assert.IsType<string>.
- Final combined Preparing, supply ET, front/delivery ET and level checkpoint run: 37/37 passed, zero skips, no compiler warning/error. Preparing suite contains eight facts.
- Continuous versus restored control snapshots Preparing with both pending finite delivery and elapsed-one partial process. Both paths receive the delivery, preserve zero front time, complete Ready, Start the same factory-created kitchen (CreateCount=1), complete the process and produce identical final Running checkpoint canonical text.
- Adversarial controls enumerate every unsupported lifecycle state and test Outcome, InstalledLayout, nonzero Preparing service offset and nonzero front clock; each rejects with factory CreateCount=0. Zero-tick Preparing restores exact canonical text and zero service time without starting service.
- git diff --check passed (Git emitted only its CRLF-to-LF normalization notice).

Actual logs/TRX: local/Logs/cooking-execution/preparing-checkpoint-increment/, preserving first failure, second run and final combined results. This is focused verification; full kitchen/ET gate is not claimed for this changed increment.

Source and integration .NET window released to root after final focused run. Other files and root's lifecycle change were preserved.

## Independent review correction: pristine Preparing front

Root identified that clock zero alone admitted forged business progress. A new real test confirmed the blocker: NextCustomerSequence=1, customer-1 WaitingForInquiry elapsed zero and table-1 Occupied were accepted by restore. The red test failed 0/1 and its log/TRX are retained as cooking-preparing-pristine-red.

Preparing restore now constructs a pristine front from the actual trusted Schedule, Flow, ManualPolicyIdentity and Menu, matching PublishKitchen/BindFrontOfHouse initialization, and compares the full front checkpoint canonical before host creation. This covers all counters, customers, work, wash/unsatisfied queues, companion experience/state, table state/clear sequence, menu and routes without hand-maintaining a subset. With no trusted front configuration, Preparing requires null front payload; legacy injected front is rejected. Recipe inventory, processes and supply payloads are unaffected.

Controls additionally forge arrival countdown, companion completed-task experience, free-table clear sequence and Closing, and test a preparation factory without trusted front configuration. Every rejection occurs with factory CreateCount=0. Existing uninterrupted/restored process-and-supply canonical control remains passing.

Final changed-source combined run: 38/38 passed, zero skips, no compiler warning/error; Preparing has nine facts. git diff --check passed with only Git's CRLF normalization notice. Logs/TRX cooking-preparing-pristine-red/final are in the evidence directory above. Earlier full root gates predate this blocker correction and are not final evidence for it. Source/.NET window again released, no commit by reviewer.
