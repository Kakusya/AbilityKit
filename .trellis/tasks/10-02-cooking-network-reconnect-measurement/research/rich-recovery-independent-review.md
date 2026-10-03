# Independent rich framed recovery review

2026-10-03. Reviewed recovery commits `bae3564dd` and `571ae7ef4`, current frozen source, actual rich-final TRX/stdout and four named stage logs. No rich test or .NET command was run by this review. Newly executed DeferredAck isolation tests are a separate increment `111c4b055` and not included in rich counts. Sole main ownership: this report.

## Decision

Approve importing the four owned rich files from `bae3564dd` plus formatting-only `571ae7ef4`. No blocking production or fixture defect found in the final reviewed scope. It closes the actual framed InProcess F01+D31 four-cutpoint composed test gap, subject to master integration/gates. It does not close rich UDP, physical two-PC, fault/load or performance exits.

## Evidence inspected

Recovery tree: `C:/Users/Administrator/orca/workspaces/AbilityKit/cooking-network-recovery-current`.

`local/Logs/cooking-execution/network-rich-recovery/rich-final.trx`: executed4/passed4/failed0/notExecuted0; all four theory names present. Per-case duration automatic-active148.973s, manual-paused143.389s, unbound-cup144.146s, submitted-reply-lost142.519s. Matching stdout reports4 passed/0 failed/skipped,9m39s. All named stage logs reach successor-full-baseline-verification and completed:

- automatic `rich-stage-automatic-active-f64cd8acdd6b411ba6bc87b3b92a4bc0.log`:148953ms,811commands.
- manual `rich-stage-manual-paused-a746a52825ee4e159d4086beb03874d3.log`:143391ms,870commands.
- unbound `rich-stage-unbound-cup-033c66e963b74bada721924d336c3029.log`:144140ms,811commands.
- lost reply `rich-stage-submitted-reply-lost-0051287b66c74d09b54046e878072f5c.log`:142516ms,811commands.

Preserved earlier failures are not hidden: first/second fixture failures, actual ACK deadlock diagnostic, resumed3/4 with manual cutpoint absent, then corrected manual1/1 and final4/4. Current frozen rich source matches recovery HEAD exactly. Formatting successor removes BOM/trailing blank lines only and changes no assertions or controls. The first mistaken diff against main HEAD merely showed rich files not yet imported; the correct recovery-tree diff is empty.

## Scope verified against actual code

One actual CookingLevelEtHost is prepared using trusted CookingRichRecoveryFixture. The catalog contains actual selected F01+D31 dependency closure, with a separately hashed trusted6tick recipe copy. Factory seeds only empty configured tools; supplies are finite8unit packages requested, received and physically handled through real framed RequestSupply/ReceiveSupply/TakeOut/PutIn/movement/process/portion/binding/submission operations. Layout/equipment/poses and configured front are real, not mocked. Customer wait500 is a disclosed test pacing setting; service160/arrival60 remain. It is not production timing acceptance.

Planner consumes full passive captures and sends actual commands; no second kitchen or domain mutation in callbacks. Peer callbacks decode passive baselines and emit ACK into the framed ingress. This test harness is not the production client security-validation test and makes no UDP retransmission claim.

Preparing manual handoff now chooses a dependency Manual recipe excluding final F01 output (P01 lettuce), leaves the actual process idle and lets partner complete the original work. During Running, final F01 Manual assembly remains available for the manual-paused cutpoint. That cutpoint proves active worker, real Pause, disconnect, unchanged whole observation/frame while paused, Resume cleanup clearing worker with unchanged elapsed progress, and other participant continuing original process before completion.

Automatic cutpoint checks a genuinely active automatic process with elapsed<required and no worker before rebind. Unbound D31 cup cutpoint verifies completed product unbound and in the serving vessel, exact product record survives rebind, and a different chef ultimately binds/delivers it. Lost response drops one actual Submit CommandResult while committed complete baseline/settlement reaches the peer; rebind rotates generation/token; same stable payload returns Accepted duplicate with original domain ID and unchanged settlement/tombstone records. The rich test does not separately assert ACK completion of the pre-close lost-result baseline; its claim is receipt of committed full state and legitimate fresh rebind ACK. The independent-process runner has a stronger pre-close exact ACK gate.

ServePortion checks source remaining count decrement, destination unit increment and allocator increase. Each scenario genuinely finishes two deliveries, one unmet natural departure, zero stars and success through TryFinishService/CompleteEnd. Ended capture has clean/free tables and empty wash queue. CreateSuccessor writes the typed major baseline through trusted progress/store path; ReadBaseline succeeds and preserves allocator/supply balances/deliveries. Successor is real Created epoch2 in the same Host, and both live participants become Ready from full new-scope baseline/ACK; full Recipe and Front canonicals match Session current capture. This is durable successor generation evidence, not a new OS Host cold restart (covered separately).

The600s whole-case check is cooperative before commands/waits/frames, not preemptive cancellation of a blocked authority call. Per-response15s and projection30s remain. No receipt pruning, wider production budget, bypassed rejection or fabricated success was found.

## Remaining boundaries

Master broad regression/import is still root-owned. Real physical LAN, rich UDP recovery and separate-process lost-Submit/load controls remain separate. Formal performance thresholds are UNSET. Cold restart is not inferred from same-Host successor. New dedicated DeferredAck transition tests are separately compiled/executed5/5 and must remain independently recorded, not added to rich4/4.

## Verification

Lint: inspected formatting-only diff and owned-source changes; no new source modification required. TypeCheck/tests: actual producer build/TRX inspected; reviewer did not rerun rich or claim independent compile. Four-case acceptance is supported by real final producer artifacts and matching reviewed source, while master acceptance remains pending.
