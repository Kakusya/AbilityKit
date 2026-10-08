# Cooking fixed-flow network port integration

## Scope and behavior

Source base `aa5e26cc8b05d24f86e7952b7ffa495a03f535b1`, dirty=true. Worker owns only the FlowAcceptance helper/adapter/role host and Cooking-only runtime test files. No PowerShell tool changes, firewall/PATH mutation, Unity/example changes, dependencies, worktrees or commits. Existing project references/default Compile inputs already cover the new C# files; no csproj change needed.

- `CookingTestPortSelector.cs`: Windows parent invokes the existing read-only `GetPort` script. Installed per-user fixed path precedes repository fallback discovered from cwd/output ancestors. Bounded stdout/stderr, strict response/config/range validation, duplicate-field rejection and native failure diagnostics prevent fallback to a guessed port. Only its own process handle is killed on cancellation/failure; bounded exit confirmation completes before returning. Selector process uses the caller's StartupMs token; forced cleanup has its own bounded 2-second exit-confirmation allowance.
- `NetworkFlowAdapter.cs`: select within the existing startup budget before launching any role; pass the selected integer only through the server child's private environment. Remove inherited values from other roles. Actual READY must equal the loopback selected endpoint before client launch. `network-resources.json` adds selected port/config source/range, selector PID, script SHA256, native exit and bounded raw JSON/diagnostics. Flow result/event and control DTO schemas remain unchanged. Tool selection does not reserve a port or prove firewall/LAN connectivity; a bind conflict fails this attempt rather than retrying automatically.
- `NetworkRoleHost.cs`: Windows server role refuses missing/invalid selected-port environment, then binds that port. Non-Windows still binds OS ephemeral port0. CLI role signature and gameplay behavior remain unchanged; offline adapter does not use this helper.
- `CookingFixedFlowTests.cs`: direct external server-child protocol controls obtain the Windows port through the private selector. Real network flow controls assert tool result/range/provenance and actual server READY agree. `ReadyThenDisconnect` retains its explicit in-process port0 fixture because it verifies disconnect semantics, not ordinary external host startup.
- `CookingNetworkTestPortTests.cs`: private reflection seams exercise strict result/error/config/child environment/READY controls, installed-path preference/repo fallback, real selector-process native/JSON/output failures, bounded cancellation and native exit while an unrelated process remains alive, and startup failure/timeout before any role exists.

## Validation

Windows, .NET SDK10.0.300, Debug/net10.0. Worker coordinated builds with root; no overlapping compiler run. Commands executed at repository root:

```powershell
dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj -m:1 --filter 'FullyQualifiedName~CookingFixedFlowTests|FullyQualifiedName~CookingNetworkTestPortTests' --logger 'trx;LogFileName=flow-port-focused.trx' --results-directory local/Logs/cooking-network-test-port-integration/flow-focused
dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj -m:1 --filter 'FullyQualifiedName~CookingFixedFlowTests|FullyQualifiedName~CookingNetworkTestPortTests' --logger 'trx;LogFileName=flow-port-focused-final.trx' --results-directory local/Logs/cooking-network-test-port-integration/flow-focused
```

- Passed: first command native0,58/58 tests,0 failed/skipped,11 seconds. Original TRX retained.
- Passed: after adding successful-selector provenance proof and stronger cancellation-test cleanup, final command native0,**59/59 tests**,0 failed/skipped,11 seconds. C# compile/type checks succeeded with no reported compiler warnings/errors. Final TRX retains raw test stdout and known negative-control outcomes.
- Passed: `git diff --check` for changed tracked C# files, native0; explicit trailing-whitespace scans for both new C# files found0.
- Passed: distinct post-test read-only process inspection found none of the9 actual owned role PIDs in the final suite's3 network resource sidecars alive. The crash/OS-fault control intentionally retains null client-b exit evidence in its sidecar; the expected Incomplete negative-control assertion passed and separate OS inspection found that known crashed child absent. This does not rewrite its incomplete cleanup evidence into a normal stop.
- NotRun: standalone formatting/analyzer CLI (no separate configured C# lint command identified). No physical two-PC test; remote LAN remains NOT_VERIFIED. Non-Windows real host execution was not performed; pure controls verify its port0 branch.

Final fixed-flow raw sidecars are under `local/Logs/issue13-s2-revision/tests-8e8021d24cd341b09e610ad040cdff68/`. Primary occupied-port integration running concurrently was owned by root and untouched; Flow tests queried remaining available range ports through the tool.

## Frozen identities

Final C# worker build/test completed before marking code frozen. Main may subsequently rebuild; these hashes identify this worker's verified artifacts.

| File | SHA256 |
| --- | --- |
| CookingTestPortSelector.cs | 17B03AD263BDCADFE88F6F9E50870C3365B15AEC42CE1BC1F2B0EE29A2051A19 |
| NetworkFlowAdapter.cs | C0AC00B80D1CA2710FE0E115B742C0E17B334D984E237E151E2E4EEEDAF8A976 |
| NetworkRoleHost.cs | 7EA5CB3B1629D057CB375A213A7F13CF71F5CBBFE32163014F6455BD26F0EC0A |
| CookingNetworkTestPortTests.cs | 7F98A84175CF0306670C4E91DAFB8663BDBFF2A5DE5563DB4395B424F87B856B |
| CookingFixedFlowTests.cs | 14C7D323AAAA3384C8534D2112739A1D4B3243F1815FF09BC0BB568AC7866499 |
| FlowAcceptance/bin/Debug/net10.0/AbilityKit.Game.Cooking.FlowAcceptance.dll | 8690D4CE7D19326999926455B1F8E91110CEDF15E90B5DCE90EFEAD663FE30D0 |
| ET.Runtime.Tests/bin/Debug/net10.0/AbilityKit.ET.Runtime.Tests.dll | B37E98CA4ED037C226E0C6571A7DA3145CDE8A30D4FB9CBE714A8EFDDD830059 |
