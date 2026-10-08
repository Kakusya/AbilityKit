# Issue13 acceptance routing

Current authorization: [Owner continuation](owner-approval-20261007.md). S1/S2/S3 consume the accepted API; this map adds no categories, thresholds or test scope. Main inspects worker evidence and uses unchanged passing checks instead of repeating them. Tests below are proposed coverage, not Passed claims.

| Issue13 checklist | Evidence owner/stage | Observable exit |
|---|---|---|
| Same gameplay offline/network | S1 / S2 | Both fixed flows real offline; real server + two external clients for network |
| Correct facts pass; intentional violation detected | S1 rule tests / S2 convergence controls | Real positive, explicitly synthetic negative, concrete rule/step/call evidence |
| Formal commands and results | S1 / S2 + main source inspection | Actual ET enqueue/tick or network SendCommandAsync, independent authority probe |
| Fixed steps, valid parameters | S1 | No runtime model/DSL, reject unsupported request before resources |
| Incomplete/early-exit/timeout cannot pass | S1 / S2 / S3 | Separate execution, verdict, evidence, cleanup; missing terminal/result nonzero |
| Single collector and bounded evidence | S1 / S2 | One events writer, host sequence/run fences, overflow fail closed |
| Either contender may win | S1 / S2 | One formal Accepted + one formal Rejected, no scheduling winner assertion |
| First failure before cleanup | S1 / S2 | Failure cuts remain available; cleanup failure independently recorded |
| Fresh sessions/no old result | S1 / S2 | Two independent runs/resources, existing/unknown output rejected |
| Exit0 only whole Passed | S1 / S3 | Real CLI exit plus complete matching terminal result |
| Approved categories reused | S1 catalog/main scope | Five cards version1.0 with locatable authorization; request cannot self-approve |
| Useful success/failure report | S1 / S3 | Small local report and failure pack, purpose/coverage/first cause/NotRun clear |
| One submit/wait/terminal handoff | S3 | Real success, failure and no-result caller checks; actual runner limits stated |
| Only authorized Cooking changes | main independent diff | Exact stage files, no product/Unity/examples/dependency drift |
| Keep attempts and old boundaries | all stages | Original failures retained; no claim of closing old #6/physical LAN exits |

S1 independent inspection targets: exact API declarations; all contenders enqueued before pump; frozen replay; admission without terminal; deduplicated terminal evidence; independent hand/index versus item DTO; staged frame failure semantics; bounded collector and cleanup; report ordering. S2 adds real transport provenance, external process ownership, installed-baseline fence before later mutation, role protocol identity/sequence validation, known sent-command cancellation semantics. S3 inspects the actual tool return/failure-pack handoff without introducing a daemon.

Required runtime evidence is NotRun until linked actual outputs exist. Unity, physical two-PC LAN and automatic Orca callback are outside this implementation; no same-machine result upgrades those facts.
