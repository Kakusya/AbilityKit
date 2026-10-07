# Issue13 S1 bounded revision: main independent check

Status: **Passed** for source, API, scope and raw-evidence inspection. Exact candidate acceptance remains pending dot review. Prior candidate `b387179fd4ae60339c3aa7f3a6efb3f12f7a976a` and its timely needs-revision decision remain historical evidence.

Only five approved delivery files changed: FlowRuleEvaluator.cs (B1), FlowEventCollector.cs (B2), FlowOrchestrator.cs (B3), CookingFixedFlowTests.cs and fixed-flow.md. The 47 public API declarations match the accepted original design plus amendment. No DTO, catalog, rule version, product, dependency, unrelated example or Unity change. User journal changes are excluded.

## Behavior inspection

- B1: independently proved rejected-command effects or contradictory ownership still fail. Missing formal terminals alone yield Undetermined; the competing-result cardinality check requires both formal terminals. Timeout evidence is Failed at harness level, product Undetermined, first cause CommandTimeout, without a fabricated ProductFailure.
- B2: validate the exact event-kind payload union before serialization, enqueue, required-event indexing or event-window inclusion. Command and observation kinds require their respective sole payload; RuleChecked requires Check; lifecycle kinds have no payload. Wrong-kind controls remain outside the window/index.
- B3: observe caller cancellation before final status and after publication/readback before accepting completion. Independent cleanup/publication tokens remain. Late cancellation produces Failed/1, preserves actual execution/product/evidence facts, moves an unaccepted success marker to incomplete-result.json, and writes a bounded cancellation pack. Cancellation after accepted completion does not retroactively invalidate it. The original product first cause survives cleanup failure and late cancellation.

## Fresh worker validation, independently inspected

Actual source was `92313027d67706cf2c45f3e948f30ced6e66da01`, **dirty=true**, Debug. The frozen candidate is a later packaging commit; these results are not described as a clean build of that future SHA. SDK 10.0.300, runtime 10.0.8, MSBuild 18.6.3+caa81fa49, Windows win-x64, Orca 1.4.221. Source fingerprints match the revision manifest. Git-normalized fingerprints separately identify committed blobs.

Commands below retain actual options; fresh machine-local output paths are represented by placeholders. Exact commands and native outputs remain in the retained local evidence, not published private paths.

```text
dotnet build src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj -c Debug
dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj -c Debug --no-build --no-restore --filter FullyQualifiedName~CookingFixedFlowTests&FlowStage=S1 --logger trx;LogFileName=flow-s1.trx --results-directory <fresh-validation>
dotnet src/AbilityKit.Game.Cooking.FlowAcceptance/bin/Debug/net10.0/AbilityKit.Game.Cooking.FlowAcceptance.dll run --request Docs/design/CookingGame/testing/requests/compete-offline.json --output-root <fresh-validation>/compete-cli
dotnet src/AbilityKit.Game.Cooking.FlowAcceptance/bin/Debug/net10.0/AbilityKit.Game.Cooking.FlowAcceptance.dll run --request Docs/design/CookingGame/testing/requests/pickup-drop-offline.json --output-root <fresh-validation>/pickup-drop-cli
```

One build, one focused test window and two fresh real offline CLI runs; all four native exit codes are **0**. Build: 0 errors, 2095 existing dependency documentation warnings, no changed-file warning. TRX: total/executed/passed **10/10/10**, failed/notExecuted **0/0**, all actual case names verified. Existing cases incorporate the new negative controls rather than increasing the test count.

Both CLI results have Passed status and product verdict, execution/evidence complete, confirmed Complete cleanup and 13 contiguous events with matching run identity. Run IDs: `run-49f60805b08f4b59a5d3ae94aee6a208` and `run-a60d5307ce7b4549bc3f335b76effc37`. Raw result, event, request, source, exit and binary records were inspected independently.

FlowAcceptance DLL SHA-256: `525F65CBD950F135FD090837E540E76EC0C0D9ECFB9D1BBDB01A48AE0FED1DA1`.
ET.Runtime.Tests DLL SHA-256: `711BE0D227583AD69A20D589DEBF45D75FB709C3C31242BEC29B70D22B0D53F5`.

Timeout, late-cancellation marker/pack, and product-plus-cleanup-plus-cancellation raw control artifacts were also inspected. Returned Failed/1 on late cancellation is supported by the actual Passed TRX case and source assertion; it is **not** a separate external CLI cancellation run. Partial terminals, contradictory hands, wrong-kind and publisher cancellation controls use declared synthetic/test seams and are labelled accordingly.

Main ran a scoped `git diff --check` (exit 0) and independent static/raw-result inspections. Main did not repeat the passing build/tests/CLI runs. Some machine metadata has a UTF-16 BOM; the read-only inspector decodes its actual encoding without rewriting raw evidence.

## Limits and references

Real external network S2, actual toolcaller S3 failure/no-result exercises, Unity, physical LAN and automatic Orca wake remain **NotRun**. S1 convergence is **N/A**. Branch-only delivery does not authorize merge, postmerge integration, Issue close or cleanup.

See [inspection](s1-revision-inspection.json), [all 17 delivered-file hashes](s1-revision-delivered-manifest.json), [accepted API](accepted-api-design.md), [precedence amendment](accepted-api-amendment.md), [prior full review](dot-s1-review-reply-raw.txt) and the deferred [S2 bootstrap](accepted-s2-bootstrap.md).
