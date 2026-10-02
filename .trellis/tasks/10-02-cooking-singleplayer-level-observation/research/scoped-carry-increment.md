# S14 scope-narrowed carry ET acceptance

Status: focused verified 1/1, 0 failures/skips; awaiting independent review and root composite gates. Production files are unchanged.

## Contract and fixture

The new CookingScopedCarryEtTests.cs uses an isolated, scope-aware copy of the catalog action planner. The reviewed natural fixture remains untouched. Source policy selects F01+D31 from loaded F01+D31 provenance; successor selects only F01 and derives its material grants from that dependency closure. This scope unit fixture does not bind Front; the separately reviewed natural fixture owns current-menu Front and operating-end acceptance. Catalog test durations are six ticks with recomputed trusted catalog/provenance identity.

Every source material enters through public finite RequestSupply/ReceiveDelivery. The carry contains a fully formed D31 cup, a stopped partially completed D31 manual step, a completed cheese-cap batch with remaining portions, an unstarted correctly assembled old recipe and another approved tea delivery. Factory target poses differ from source live poses and are derived from actual target Spatial.InitialPoses.

The new scope must pass real Ready despite forbidden carried stock. Existing receipt, manual continuation and transport/cleanup remain available. New tea procurement, PutIn, StartProcess and ServePortion reject MenuNotAuthorized with unchanged items/processes/supply/allocator. Fixed ET clock advancement is explicitly distinguished from business mutation. Running codec dispose/restore repeats the same commands and must match complete final checkpoint and Observe canonical text, including clock.

Success handoff has no Front and uses BeginEnd/CompleteEnd as a scope unit-test shortcut. This is not natural business completion proof; the separately owned natural operating acceptance provides that evidence. Network and Unity are out of scope.

## Verification

Actual command:

```powershell
dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj --filter FullyQualifiedName~CookingScopedCarryEtTests --logger 'trx;LogFileName=cooking-scoped-carry-owner-final.trx'
```

Final: 1/1 passed, 0 failed/skipped, test about one second; command 4.1 seconds. Compilation/type checking passed with no warning/error in this focused run. Source and .NET window released to root. No production bug found by this control.

Initial evidence is preserved: compile typo ReceiveDelivery was corrected to the actual ReceiveSupply enum. A copied natural fixture's unfinished Front correctly rejected BeginEnd (ServiceNotFinished). The dedicated scope unit fixture now has no Front interface, as permitted by the scoped unit contract; copied Front-only methods/assertions were removed. This does not weaken the independent real natural-operating proof. A leftover Front assertion then raised a fixture null reference and was removed. The next two runs passed, including final explicit policy/grant/checkpoint-identity controls.

All logs and TRX, including these fixture failures, are under `local/Logs/cooking-execution/scoped-carry/`. The final source includes approved delivery duplicate reception idempotence and exact saved allocator checks. Root's reviewed checkpoint tombstone correction was imported before the passing run; this producer neither edited nor commits those root-owned prerequisite files.

No broad gate or master result is claimed by this producer. Root will route the evidence and run the composite gates. S14 completion and network/Unity status are not inferred from this one control.

## Independent review follow-up

Root static review found no behavioral blocker and requested direct authority identity proof. Added Assert.Same(fixture Simulation, Host.Driver.Simulation) after initial Begin, before carry checks, after successor, after codec restore and at both continuation entries. All passed in cooking-scoped-carry-owner-final.trx: 1/1, zero failures/skips, command 3.96 seconds. No reference rebinding or production mutation was needed. This run predates root's separate manual tick timing correction; no result for that subsequent source is claimed.
