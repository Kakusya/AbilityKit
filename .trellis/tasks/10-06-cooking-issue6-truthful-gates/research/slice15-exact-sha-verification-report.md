# Slice 15 exact-SHA verification report

Date: 2026-10-06 UTC (2026-10-07 Asia/Shanghai)
Target commit: `8497d3a673bd84be079d99a58df97b96676dea88`
Dispatch: `task_f2c0e4cd89f8` / `ctx_705bac462252`

## Outcome

Passed. The isolated contract controls completed exactly `290/290`, and both genuine Cooking gates completed with native CLI exit `0`, complete required leaves, and nonzero TRX execution. No required check was Failed, Blocked, Skipped, or NotRun.

This is exact-SHA verification evidence only. It does not merge, close Issue #6, claim Unity or physical-LAN coverage, or replace any preserved historical failure.

## Preflight and execution boundary

- `git rev-parse HEAD` equaled `8497d3a673bd84be079d99a58df97b96676dea88` before execution.
- `git status --porcelain` was empty before execution and before each genuine gate.
- `local/Artifacts/issue6-cooking-slice1-s15-verifier-controls` did not exist before the first command.
- The three required commands ran serially in the requested order. No failed command was rerun; no source, configuration, tests, dependency, SDK setting, Unity content, or example content was repaired or changed.
- Issue #6 was read-only checked before execution: it remained OPEN with label `blocked`; the current body authorizes these two Cooking gates and does not authorize other product/example work.

## Commands and receipts

| Check | Exact argv | UTC start | UTC end | Process exit / status | Structured receipt |
|---|---|---|---|---|---|
| Contract controls | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1 -ArtifactRoot local/Artifacts/issue6-cooking-slice1-s15-verifier-controls` | `2026-10-06T21:51:03.8653542Z` | `2026-10-06T22:06:37.4658598Z` | `0 / Passed` | `local/Artifacts/issue6-cooking-slice1-s15-verifier-controls/controls.json`, 2,729,465 bytes, SHA-256 `73DC3325347D0165AFFE3B6E9803B4BEC99D681CC1A61528A9266F1707B77EC9` |
| `cooking-et-level-runtime` | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime -Configuration Debug -CI` | `2026-10-06T22:15:29.8502580Z` | `2026-10-06T22:43:31.4806679Z` | `0 / Passed` | `local/Logs/test-gates/f0d51933-b320-40b1-bde8-8d332aa22cae/gate-summary.json`, 20,542,196 bytes, SHA-256 `45DDEEFF200A57BEC9FD17B3312206C199AA136BFFBB709E805446E52D7DB1A5` |
| `cooking-kitchen-loop` | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop -Configuration Debug -CI` | `2026-10-06T22:45:39.1051561Z` | `2026-10-06T23:20:33.6584993Z` | `0 / Passed` | `local/Logs/test-gates/7b6a2c34-e1f8-4b22-8418-c9705f7630db/gate-summary.json`, 27,277,010 bytes, SHA-256 `10B3A3541756A5C9264F6DCDD93D873DD9B4DF9CAF958FE8C6DF55A0BDA4FB7F` |

Raw console receipts were preserved separately:

| Check | stdout | stderr |
|---|---|---|
| Controls | `C:\Users\ADMINI~1\AppData\Local\Temp\slice15-task_f2c0e4cd89f8-controls.stdout.txt`, 26,806 bytes, SHA-256 `83FAAE1ACC7604133DA139E2B2372774933BDB82EBABD3A9938728C9E1DB3269` | 0 bytes, SHA-256 `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855` |
| ET gate | `C:\Users\ADMINI~1\AppData\Local\Temp\slice15-task_f2c0e4cd89f8-et.stdout.txt`, 228 bytes, SHA-256 `7A2E5BC4BD86B624EB49F7FF4114661EDADA8910850E1803B72A313865E77D3F` | 0 bytes, SHA-256 `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855` |
| Kitchen gate | `C:\Users\ADMINI~1\AppData\Local\Temp\slice15-task_f2c0e4cd89f8-kitchen.stdout.txt`, 224 bytes, SHA-256 `0E872055B5A241BC4A07D056AA32E26B0EB73FDAC01248F53DE2C3A0C50F7658` | 0 bytes, SHA-256 `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855` |

## Contract controls

The receipt reports `status=Passed`, `total=290`, `executed=290`, `passed=290`, `failed=0`, `fullControlSuiteAccepted=true`, `contractControlsAccepted=true`, and `realDotNet=Executed`. Console-width coverage is Passed for requested and observed widths 40, 80, and 120. Unity and Issue acceptance are `NotRun` in this isolated control receipt, and `example=true` correctly prevents the controls from being treated as genuine gate evidence.

All 290 case names are unique. Preservation accounting is literal `249 + 40 + 1 = 290`: Slice 13 established the original 249 names plus all 40 prior approved additions at the parent source, and the source diff from `2b258b6ea2a40a9ac354b51847c749722564e8ba` to the target adds only `missing-optional-analyzer-targets-native-regression` without removing or renaming any prior control. That new native regression control Passed with native exit `0`, while its embedded pre-repair build retained the expected MSB4057 red behavior.

Control source-before and source-after both record SHA `8497d3a673bd84be079d99a58df97b96676dea88`, `dirty=false`, and input fingerprint `e7ddb75407ff5f509ee8a57616e0192859e4251f265d750a9562d94ee0aaed83`.

## `cooking-et-level-runtime`

Gate run ID `f0d51933-b320-40b1-bde8-8d332aa22cae`; gate result ID `d6db0ca0-f320-40c0-9f49-8268722fd040`. The summary records `status=Passed`, `cliExitCode=0`, `fullGateAccepted=true`, `example=false`, all four declared required bindings completed, and no missing or optional bindings.

| Required leaf | Result ID | Status / native exit | Actual coverage |
|---|---|---|---|
| ET relation analyzer build | `a6c53e74-2e68-4968-aa06-e837caf0723a` | Passed / `0` | Build-only; test counts N/A |
| Cooking ET runtime build with relation analyzer | `11896312-9c15-4edc-a633-5a76d5ae7e8c` | Passed / `0` | Build-only; test counts N/A |
| Cooking domain and Level lifecycle tests | `bd507715-df3d-4884-b23f-98418fb62a83` | Passed / `0` | `867` total, executed, and passed; `0` failed/skipped/not-executed |
| Cooking ET Level host and runtime tests | `fb3d1c77-82af-43c9-865e-739cadf74914` | Passed / `0` | `328` total, executed, and passed; `0` failed/skipped/not-executed |

Actual TRX receipts:

- `local/Logs/test-gates/f0d51933-b320-40b1-bde8-8d332aa22cae/bd507715df3d/test-results.trx`, 1,384,416 bytes, SHA-256 `C8E6D694438C7E34E6FD14C9F545B4B06EB437E74076D33C9964207BACEE280F`, TRX run ID `4b7356a0-10b3-4382-a735-fe79156fea17`.
- `local/Logs/test-gates/f0d51933-b320-40b1-bde8-8d332aa22cae/fb3d1c7782af/test-results.trx`, 577,945 bytes, SHA-256 `0C3FE49CF43623E40B4B94848AB623E93A20838C09CDF1771350AD0B7126E1AB`, TRX run ID `9bbbb523-dcdf-4879-9734-3bf54735345d`.

## `cooking-kitchen-loop`

Gate run ID `7b6a2c34-e1f8-4b22-8418-c9705f7630db`; gate result ID `fbbba649-246a-4ba0-a8ab-db8567a8acd3`. The summary records `status=Passed`, `cliExitCode=0`, `fullGateAccepted=true`, `example=false`, all five declared required bindings completed, and no missing or optional bindings.

| Required leaf | Result ID | Status / native exit | Actual coverage |
|---|---|---|---|
| Cooking domain build | `a40cc9a3-4044-4afe-be97-c2e122d80537` | Passed / `0` | Build-only; test counts N/A |
| Cooking ET runtime build with relation analyzer | `62e79500-9213-4364-9be0-78bc5092ec50` | Passed / `0` | Build-only; test counts N/A |
| Kitchen-loop focused contract tests | `9f5c7db6-09fe-40e2-a8b4-8a471444145c` | Passed / `0` | `644` total, executed, and passed; `0` failed/skipped/not-executed |
| Cooking domain and Level lifecycle regression | `9424707d-2ab1-44c7-ad36-1c5970e60311` | Passed / `0` | `867` total, executed, and passed; `0` failed/skipped/not-executed |
| Cooking ET Level host and runtime regression | `01ba1f6c-122c-495d-9c15-b57a238682fc` | Passed / `0` | `328` total, executed, and passed; `0` failed/skipped/not-executed |

Actual TRX receipts:

- `local/Logs/test-gates/7b6a2c34-e1f8-4b22-8418-c9705f7630db/9f5c7db609fe/test-results.trx`, 1,019,327 bytes, SHA-256 `370A42AD916E3B5D613700AF44AEB95293685DA892CADBDF9B6B224A96300822`, TRX run ID `04f31f88-d732-432b-80d2-2a3b359299ea`.
- `local/Logs/test-gates/7b6a2c34-e1f8-4b22-8418-c9705f7630db/9424707d2ab1/test-results.trx`, 1,382,640 bytes, SHA-256 `60BB3BDB70F898E1030810715C5417F73F8A06B596B1693ABBC8296F1CC41E45`, TRX run ID `618e4a6b-e03e-432e-b320-ce25ca26bfe1`.
- `local/Logs/test-gates/7b6a2c34-e1f8-4b22-8418-c9705f7630db/01ba1f6c122c/test-results.trx`, 577,945 bytes, SHA-256 `569BF9202E80628E811FE0ED8BDAAE6AB5436B5009B14B773FA8F0996EDF769B`, TRX run ID `4349b98e-9f25-40b5-9f88-73b54b3bf696`.

## Binary and source identity

The executed build/test receipts pin the primary binaries below. Repeated paths in test output carry the same byte count and hash.

| Binary | Bytes | SHA-256 |
|---|---:|---|
| `AbilityKit.ET.RelationAnalyzer.dll` | 9,216 | `57D4B8C598212EAD68C3B0DDB12ECAF709BE39664FAEE1FCCD3E6B2814635E35` |
| `AbilityKit.Game.Cooking.dll` | 1,478,656 | `01060D555B4980FABB01A2639E030A94615054EAB5577D6028DDE882454804C6` |
| `AbilityKit.Game.Cooking.EtRuntime.dll` | 142,336 | `2F39BFF036B38335EE18209F7E68BECAAFE1F419497439D83F6782D89CC7A89F` |
| `AbilityKit.Game.Cooking.Tests.dll` | 859,648 | `F0D29C47AD573F44936FA0228062358DC80B4C049CEF2F9BE4BF769668F82F91` |
| `AbilityKit.ET.Runtime.Tests.dll` | 545,792 | `8CAE21BC7547CA732B3EB0D4C01F4D4583C4D3F451C86A056DDD050DDD5C1252` |

Both gate summaries record source SHA `8497d3a673bd84be079d99a58df97b96676dea88`, `dirty=false` before and after, and identical input fingerprint `8481fb08f809de7808e7f063dfef106dc3cec79a6a871d15f9f98ceb9fec1a94`. All eight control/helper inputs had Git-normalized working blob IDs exactly equal to the target commit blobs. Their exact working-byte SHA-256 values were:

- `tools/run_test_gate.ps1`: `575469C4238E29CF41B5826A43748D38C9C81578793261835B6E51DC3A886E8F`
- `tools/test-gate-compiler-events.cs`: `1170F99EED343BBEC54560F1B27F5A08DD462EEF936620721B2557CDA3D5689A`
- `tools/test-gate-result-contract.ps1`: `4EE9D89CA78DBA112D6F3B8BDC69FC57130BA07EF740AC7B4FF765CD2318742B`
- `tools/test-gates.json`: `85111D00C4A3A61C87F61DA3E43D159DA60E07D058406F7049E17495E7E520A1`
- `tools/tests/fixtures/gate-result-fake-dotnet.ps1`: `D90A92A5674357FDBE11B78EF2C77AD788F0F028DC56FB3BDFD18DD0B243A957`
- `tools/tests/fixtures/gate-result-model.ps1`: `2E0C20C5D10B38FBE3BF20E0C053CA5A66C01BAA89CF1438A843AADBE5974058`
- `tools/tests/fixtures/gate-result-native-probe.ps1`: `F3A1A95A96CB42F1C1C3DBC2CF77FB6DF66D13855543A6332C576F56BD355BFE`
- `tools/tests/test-gate-result-contract.tests.ps1`: `2FC7B99C73933628B5D66295FB32CC2E9C3F576F4270B93E7289C7F9F60B8FF3`

This establishes that the tested implementation inputs were the target commit, while each gate additionally archived its evaluated restore/build/compiler/test closure and generated binary identities.

## Tools, status vocabulary, and limitations

- .NET SDK `10.0.300`; Microsoft.NETCore.App runtime `10.0.8`; MSBuild `18.6.3.23102`.
- Windows PowerShell `5.1.26100.9444`, Desktop edition; CLR `4.0.30319.42000`; OS `Microsoft Windows NT 10.0.26100.0`.
- `Passed`: all three required commands and every required gate leaf above.
- `Failed`: none in this Slice 15 execution. Historical Slice 10/Slice 13 failures remain unchanged evidence and are not relabeled.
- `Blocked`: none in this execution.
- `Skipped`: none in the executed TRXs and no gate leaf.
- `NotRun`: Unity, physical two-PC LAN, non-Cooking gates, Issue acceptance/closure, merge, and post-merge verification; none was requested or authorized here.
- `N/A`: test/TRX counts for build-only leaves; those leaves are valid build coverage and do not claim test execution.

The controls are isolated contract evidence and do not by themselves establish genuine gate success; the two separate genuine gate summaries do. The run does not establish Unity/Mono/IL2CPP/AOT behavior, physical LAN behavior, merge correctness, CI required-check configuration, or final Issue acceptance.
