# Natural operating acceptance increment

Owner: native `natural_operating_implement`; isolated Orca-managed worktree `cooking-natural-operating-s14`, originally base `c60bc8446`, fast-forwarded to reviewed production `fa2f60e17`. Ownership is two new ET test files and this report; no production, Host, existing fixture, checkpoint or source-menu edits.

## Fixture contract

The trusted catalog copy changes step durations to six ticks, loads through `CookingMenuCatalog.Load`, then projects the F01/D31 closure. Thus the source catalog SHA, content provenance and runtime recipe durations agree. Bundled originals remain untouched. Initial live objects are empty processing/storage/serving vessels. Each actual raw definition has a finite supplier (8-unit package, 16 external units, 3 delivery ticks). Requests, reception, package relocation, unpacking, processing, portion serving and delivery all use real ET commands and fixed ticks.

Both configured players can work all configured stations. Catalog dependency recursion determines inputs/output/carrier; it does not simulate a second kitchen. One manual task pauses with retained progress, moves the original worker aside and resumes with the other player. Automatic processing is observed active without a worker before leaving its station. Finished D31 remains unbound until the other player takes the cup and binds an actual customer order. Front owns all order creation; no `OpenOrder` or `BeginEnd` call exists in the fixture.

Trusted layout uses compact 12-column bands (eight unique anchor cells per two-row band), actual occupied equipment, actor radius40, interaction radius800 and movement speed1000; BFS plans legal cell-centre paths which execute through continuous `Move` commands. Suppliers share one purchasing interaction anchor and one receiving slot; each received physical package is moved to its distinct storage slot before the next reception. Preparation and service use the same Host-owned simulation. D31 and F01 cut components are prepared before service; F01 final assembly/plating occurs during Running. Front requests D31 first, then F01. Front schedule: 3 tables, service160, arrival interval60, inquiry2, wash2, dining3, waiting150. Both products are submitted; a third unmet customer naturally leaves, tables clear, washing finishes and `TryFinishService` accepts normal Success. The F01 serving plate comes from a real finite clean pool of one; D31 uses a disposable cup. Fixture score thresholds1000/2000/3000 prove zero-star success without inventing a new scoring rule.

Every frame hashes the full exported checkpoint canonical text, full frozen Observe canonical text and actual dispositions. This bounds retained trace memory while comparing the entire frame, including command receipts, lifecycle, Front, supply and geometry. Fresh replay submits recorded exact commands or empty ticks, preserving the Preparing→Running transition index. Separate recovery branches export, serialize, decode, dispose and restore the Host at Preparing paused manual work and Running with a finished unbound cup plus its actual customer order, then continue. Full final Ended domain checkpoint and Observe are compared directly; Level checkpoint export remains legitimately unavailable at Ended.

The actual optional `ICookingMenuGameplayFactory`/`ICookingScopedFrontOfHouseGameplayFactory` interfaces provide the trusted catalog, F01/D31 selection, all actual material definitions including physical package definitions, empty confirmed unlocks, binding enabled and the matching two-template Front. No substitute interface or caller-supplied policy is used. Required nullable Menu identity and Level format8 come from the reviewed production commit.

## Actual checks so far

- `git diff --check`: passed.
- First focused .NET command compiled, then failed1/1 because the fixture layout had default ActorRadius250 while trusted geometry policy required40. Corrected the fixture's layout radius; no production rule changed. `local/Logs/natural-operating-first.log` and `natural-operating/natural-operating-first.trx` preserve the red.
- Second focused attempt compiled and ran. It was intentionally aborted after >110 seconds CPU because the original long-strip layout plus 3300-tick service created excessive movement receipts and per-frame serialization cost. Only the owned testhost PID27512 was stopped; this is aborted, not pass. Log `local/Logs/natural-operating-layout-fixed.log` retained. Fixture was compacted as above; subsequent result pending.
- Compact focused .NET run passed1/1, failed0, skipped0, exit0, actual test duration24seconds. Command: `dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj --filter FullyQualifiedName~CookingNaturalOperatingEtTests --no-restore --logger "trx;LogFileName=natural-operating-compact.trx" --results-directory local/Logs/natural-operating`. Log `local/Logs/natural-operating-compact.log`, TRX `local/Logs/natural-operating/natural-operating-compact.trx`. This green covered continuous, exact-input replay and Preparing manual recovery. Later strengthened exact unmet/clean tables/final Ended canonical and additional Running unbound-cup recovery await rerun; do not extend the earlier proof to those additions.

### Final reviewed-production focused proof

Command: `dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj --filter FullyQualifiedName~CookingNaturalOperatingEtTests --no-restore --logger "trx;LogFileName=natural-operating-first-cup-order-recovery.trx" --results-directory local/Logs/natural-operating`.

Actual result: **exit0, passed1, failed0, skipped0, test duration14seconds**, production base `fa2f60e17`. The single Fact executes all four branches: continuous, exact recorded-input replay, Preparing paused-manual export/codec/dispose/restore, Running actual D31 order plus finished unbound cup export/codec/dispose/restore. Every frame compares the full checkpoint/Observe/dispositions hash; full final Ended canonical text also matches. Actual Host Ready and runtime policy interfaces are exercised. Procurement asserts every received delivery, each remaining finite balance8 and original live supply units8×supplier count. End asserts two settlements, exactly one unmet order, all tables Free, empty customer and wash queues, no manual work claims, zero stars and normal Ended Success.

Log: `local/Logs/natural-operating-first-cup-order-recovery.log`; TRX: `local/Logs/natural-operating/natural-operating-first-cup-order-recovery.trx` in the isolated managed worktree.

### Preserved intermediate failures and corrections

| Log stem under local/Logs | Actual result | Cause / correction |
|---|---|---|
| natural-operating-host-ready-four-branches | failed1/1,29seconds | Manufacturing all F01 components during Running exceeded fixture waiting150; prepared cut components through the catalog DAG before Ready, retaining Running final assembly. |
| natural-operating-host-ready-prepared-components | failed1/1,9seconds | The manually supplied serving plate had washability but no clean-pool capacity. Replaced it with actual standard finite clean-pool supply1; removed the washability override. |
| natural-operating-clean-pool-four-branches | failed1/1,26seconds | Running codec rejected the oversized checkpoint; diagnosis added without weakening assertions. |
| natural-operating-running-codec-diagnostic | failed1/1,26seconds | Confirmed RecordTooLarge: 1,684,155 characters at frame1078. |
| natural-operating-bounded-checkpoint | failed1/1,14seconds | Compact shared procurement geometry reduced size to1,122,163 characters/frame709, still above the unchanged1,048,576-character limit. |
| natural-operating-nearby-appliances | failed1/1,16seconds | Moving appliances first worsened total raw-transfer routes:1,242,719 characters/frame788. Reverted this placement experiment. |
| natural-operating-compact-instance-ids | failed1/1,17seconds | Compact stable fixture instance/anchor IDs preserved all receipts but alone did not satisfy the limit:1,231,716 characters/frame788. |

Final bounded scenario uses the better compact geometry and requests/serves the already prepared D31 first. Running restoration happens at its first actual order before binding, followed by all remaining F01 manufacture/delivery and natural closure. No command receipts or physical stock are removed to create the checkpoint; no codec size guard or production rule is changed. The proof covers the two actual accepted restore points, not arbitrary unlimited-duration saves or serialization acceptance at every later frame.

## Remaining exits

Full integration/master gates and independent root review of these new test files remain to be performed by the coordinator. Success/Failed successor and durable baseline controls remain separately pending; this increment alone does not close S14, network or Unity.
