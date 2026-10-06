# Slice 14 compiler target compatibility repair report

Date: 2026-10-07

## Authority and scope

- Dot request: `AK-I6-COOK-COMPILER-20261007-01`.
- Exact base source: `2b258b6ea2a40a9ac354b51847c749722564e8ba`.
- This repair changes only `tools/test-gate-result-contract.ps1`, `tools/tests/test-gate-result-contract.tests.ps1`, and this report.
- Cooking scope only. Runner entrypoints, gate declarations, projects, fixtures outside the owned test script, SDK/dependencies, Unity, examples, and prior reports were not changed.
- The Slice 13 red result remains historical: `cooking-et-level-runtime` native build exit `1`, reason `NativeFailure:build:1`, with the other required leaves and `cooking-kitchen-loop` `NotRun`. This repair does not relabel or replace that result.

## Failure and implementation

Slice 11 generated `AbilityKitCaptureCompilerInputs` with an unconditional `DependsOnTargets` list containing `AddGlobalAnalyzerConfigForPackage_MicrosoftCodeAnalysisNetAnalyzers`, `AddGlobalAnalyzerConfigForPackage_MicrosoftCodeAnalysisCSharpCodeStyle`, and `GenerateMSBuildEditorConfigFile`. A valid SDK project without the first optional target failed before compilation with `MSB4057`.

The target now keeps `GenerateMSBuildEditorConfigFile` as a required dependency and uses two conditionally scheduled wrapper targets for the optional analyzer-config targets. The wrappers depend on the optional names only when their owning SDK switches (`EnableNETAnalyzers` and `EnforceCodeStyleInBuild`) are enabled; otherwise the absent target names are never resolved. Both wrappers run before the capture target, and the capture target remains `BeforeTargets="CoreCompile"`, preserving input generation and per-instance capture timing without an unconditional optional dependency.

## Native regression control

`missing-optional-analyzer-targets-native-regression` creates a real `net10.0` SDK project with `EnableNETAnalyzers=false`, restores it natively, and runs two native MSBuild builds against the same valid project:

1. A pre-repair generated target with the unconditional dependency list exits `1` with `MSB4057` for the missing `AddGlobalAnalyzerConfigForPackage_MicrosoftCodeAnalysisNetAnalyzers` target.
2. The repaired generated target exits `0`, reaches the normal compiler evidence path, and produces exactly one `CoreCompile`, one successful `Csc`, and one capture completion.

The control therefore remains red against the previous target and green only after the compatibility repair; it does not use string inspection or fake-dotnet execution.

## Verification

Focused compiler controls:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1 -ArtifactRoot local/Artifacts/issue6-cooking-slice1-s14focused07 -CompilerTraceOnly
```

Exit `0`; `34` executed, `34` passed, `0` failed; `realDotNet=Executed`. Receipt SHA-256: `69fd0accd26c3ce9cf63f67119ffc2792c1d85ffbf926b4fe9ae011e452de895`.

Complete isolated control suite:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1 -ArtifactRoot local/Artifacts/issue6-cooking-slice1-s14full01
```

Exit `0`; `290` executed, `290` passed, `0` failed; console-width coverage `Passed`; `realDotNet=Executed`; `fullControlSuiteAccepted=true`. Receipt SHA-256: `253402d86b668687db8a420b0c6a40e4e10c85da36a61185ca624a7e75244500`.

Accounting is literal `289 + 1 = 290`: the preserved `249` original controls and all prior `40` Slice 11 controls remain present, with one added control (`missing-optional-analyzer-targets-native-regression`). The full receipt has zero duplicate control names. Source SHA is `2b258b6ea2a40a9ac354b51847c749722564e8ba`; source-before and source-after input fingerprints are both `c89933497e4b6c40846e07e86bfecb6ef0a5bb01a894fd9550371457a66db595`.

`git diff --check` passed. The genuine Cooking gates were not run in this repair dispatch; their exact-SHA verifier remains responsible for those commands. Unity, physical LAN, and other out-of-scope coverage remain `NotRun` or `N/A`.
