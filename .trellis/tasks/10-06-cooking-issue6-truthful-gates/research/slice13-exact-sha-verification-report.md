# Slice 13 exact-SHA verification report

Date: 2026-10-06 UTC (worker run)
Target commit: `2b258b6ea2a40a9ac354b51847c749722564e8ba`
Dispatch: `task_4cd508b98f86` / `ctx_b0735a8376ba`

## Preflight

- `git rev-parse HEAD` matched the target SHA exactly.
- `git status --porcelain` was empty before execution.
- Required new artifact root was absent before launch: `local/Artifacts/issue6-cooking-slice1-s13-verifier-controls`.
- The prior coordinator-specified artifact-path attempt and its exit-1 result were not modified or reinterpreted.

## Commands and results

Commands were run serially. The first two entries ran; the third was deliberately stopped because the first genuine gate failed.

| Check | Exact argv | UTC start | UTC end | Exit/status | Evidence |
|---|---|---|---|---|---|
| Contract controls | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1 -ArtifactRoot local/Artifacts/issue6-cooking-slice1-s13-verifier-controls` | `2026-10-06T20:33:12.5316468Z` | `2026-10-06T20:47:33.7790973Z` | `0 / Passed` | Receipt [`controls.json`](../../../../local/Artifacts/issue6-cooking-slice1-s13-verifier-controls/controls.json), 2,726,840 bytes, SHA-256 `E033FF6CF52FE8AF8925610923F7359455BEFE12ECD0D358607104A29BCE3192` |
| `cooking-et-level-runtime` | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime -Configuration Debug -CI` | `2026-10-06T20:47:49.7798593Z` | `2026-10-06T20:48:28.2810525Z` | `1 / Failed` | Summary [`gate-summary.json`](../../../../local/Logs/test-gates/2292eb5c-8dca-4a03-8164-61e76ed68da3/gate-summary.json), 1,682,702 bytes, SHA-256 `86493C85A26D63FC3D9318127C35CA13500735CBFC9ABBEF90E6463D75773B8E` |
| `cooking-kitchen-loop` | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop -Configuration Debug -CI` | Not run | Not run | `NotRun` (stop-after-failure rule) | No invocation or receipt |

The gate console receipt was `C:\Users\ADMINI~1\AppData\Local\Temp\slice13-gate-et-level-runtime-console.log` (SHA-256 `340B45C6824920E7560B722284B974E16464F43316A5D0D270577E6AADB54EDE`). Its native summary was: `Gate cooking-et-level-runtime: Failed; CLI 1; fullGateAccepted=False; example=False`.

## Control-contract result

The new artifact root was used as requested. The receipt reports `status=Passed`, `total=289`, `executed=289`, `passed=289`, `failed=0`, `fullControlSuiteAccepted=true`, `contractControlsAccepted=true`, `realDotNet=Executed`, `consoleWidthCoverage=Passed` for requested widths 40/80/120, and `unity=NotRun` / `issueAcceptance=NotRun`. The 289 case names were unique (289 unique names); the control harness completed its retained original-name checks, including the original 249 names. `sourceBefore.sha` and `sourceAfter.sha` both equal the target SHA, both dirty flags are false, and both input fingerprints equal `9b74aa67d97ee4fe5f79ebf84b75d4a1800ec9ebf7d6e242fde0ff495b141717`. The receipt marks this isolated control suite `example=true`; it is not evidence that the genuine Cooking gates passed.

## Genuine gate evidence

Run ID: `2292eb5c-8dca-4a03-8164-61e76ed68da3`  
Gate result ID: `58dea325-241b-43d3-a536-932a216aeead`  
Status: `Failed`; CLI exit `1`; `fullGateAccepted=false`; reason `ChildFailureOrIncompleteCoverage`.

Declared required leaves were:

1. `cooking-et-level-runtime:1:dotnet-build:AbilityKit.ET.RelationAnalyzer`
2. `cooking-et-level-runtime:2:dotnet-build:AbilityKit.Game.Cooking.EtRuntime`
3. `cooking-et-level-runtime:3:dotnet-test:AbilityKit.Game.Cooking.Tests`
4. `cooking-et-level-runtime:4:dotnet-test:AbilityKit.ET.Runtime.Tests`

Actual leaf states:

- `ET relation analyzer build`: `Failed`, process exit `1`, result ID `bc5c520f-beb6-422b-b931-b3bf847aa522`, invocation `cooking-et-level-runtime/1`. The captured build output reports MSB4057: target `AddGlobalAnalyzerConfigForPackage_MicrosoftCodeAnalysisNetAnalyzers` does not exist in `AbilityKit.ET.RelationAnalyzer.csproj`.
- `Cooking ET runtime build with relation analyzer`: `NotRun`, CLI `4`, result ID `47f160d3-90ca-42e6-8e73-1b527726b0dc`.
- `Cooking domain and Level lifecycle tests`: `NotRun`, CLI `4`, result ID `1c22c2de-3ffe-4b8f-bd4a-be86cbafc40a`.
- `Cooking ET Level host and runtime tests`: `NotRun`, CLI `4`, result ID `cbaef39d-fc78-4bfc-860f-2073758014b0`.

Coverage completed: none. All four required leaves remain missing in the failed gate summary. TRX count is zero; no test counters or build/test binary identity can be claimed for the uninvoked leaves. The failed build child stage receipt is `local/Logs/test-gates/2292eb5c-8dca-4a03-8164-61e76ed68da3/bc5c520f-beb6-422b-b931-b3bf847aa522/stage-receipt.json`, SHA-256 `E60002330C74090FA5E6B66C1FF020ABF178E23F367AE05012A63CF57ED4851F`; its build stdout is `local/Logs/test-gates/2292eb5c-8dca-4a03-8164-61e76ed68da3/bc5c520f-beb6-422b-b931-b3bf847aa522/build.stdout.txt` (925 bytes; SHA-256 `A7D3A80E4AFDBB1FE309FEF7264ED0990E999CC7810355DC92A71F8174CFC6A0`).

The gate source provenance records SHA `2b258b6ea2a40a9ac354b51847c749722564e8ba` and `dirty=false` both before and after. `realDotNet=Executed`; console-width coverage is `Passed`; Unity and Issue acceptance are `NotRun`. Because the required build failed before test leaves, test counts, TRX identities, and test binary identities are `N/A` for the failed build and `NotRun` for the three downstream leaves.

## Tool versions and limitations

- .NET SDK `10.0.300` (host/runtime `10.0.8`), MSBuild `18.6.3.23102` (`18.6.3+caa81fa49`).
- Windows PowerShell `5.1.26100.9444`, host `4.0.30319.42000`; OS `Microsoft Windows NT 10.0.26100.0`.
- No Unity or physical-LAN checks were requested or run.
- The second genuine gate was not launched because the required stop-after-failure rule applies. This report therefore records `Passed` only for the isolated controls, `Failed` for the first genuine gate, and `NotRun` for the kitchen-loop gate; it does not claim Issue acceptance.
