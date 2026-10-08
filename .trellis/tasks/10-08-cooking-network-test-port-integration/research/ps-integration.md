# PowerShell launcher integration

Worker owns only the two new port adapter/control scripts and the four Cooking process launcher scripts. Source base remains aa5e26cc8b05d24f86e7952b7ffa495a03f535b1, dirty=true; preserve the other agents' Flow/documentation changes. No real endpoint builds, firewall/PATH mutations, host launches, worktree creation, commits or cleanup outside isolated test directories were performed by this worker.

## Implemented behavior

- `Get-CookingTestPort` uses the installed fixed-location `cooking-firewall.ps1` first, repository fallback otherwise. Windows automatic host startup calls only `GetPort`; no duplicate range scan, lease or firewall change. Explicit nonzero ports are retained/recorded. Client never allocates; BuildOnly skips selection; non-Windows keeps OS ephemeral binding.
- Selector is a hidden owned PowerShell process with retained native handle, 30-second deadline, timeout/failure cleanup and disposal. Raw stdout/stderr, selector path/SHA256/native exit/timeout, configuration provenance and chosen port are retained beside each run in `port-selection.json` and selector output files. Strict validation rejects incorrect action/status, non-integer/out-of-range ports, unsupported config, reservation/mutation/connectivity claims and selector failures before a host launches.
- `Assert-CookingTestReady` verifies the actual launched PID and selected port before starting a paired client or announcing a separate Host. Mismatches update the sidecar as Failed and instruct a fresh run; there is no bind retry. OS ephemeral selections validate actual nonzero READY/PID. Candidate selection and READY do not prove physical LAN or firewall policy.
- Primary, concurrency, rich-recovery and measurement wrappers consume the helper. Rich recovery reacquires for each case; measurement for each repeat and now permits explicit `-Port`. Primary separate Host uses the existing owned-process/readiness/finally lifecycle so its READY can be checked. Concurrency/rich separate Host cannot complete successfully without verified READY.
- Rich frozen schema2 manifests additionally include/validate `portHelperSha256`. Old manifests without the current helper identity deliberately fail and require rebuild; schema semantics and report/control schemas remain intact. Measurement linked-source provenance also hashes the adapter. Root requested `-m:1` for existing rich no-incremental builds to avoid known shared compiler-log write conflicts; this is confined to those Cooking builds.

## Verification

Passed: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/cooking-test-ports.tests.ps1`, exit0, **16 isolated controls**. Selector/platform/path APIs and process lifecycle are mocked. Controls cover installed preference/fallback/absence, saved custom ranges, strict negative responses, retained raw failures, explicit/Client/BuildOnly/non-Windows modes, exact READY and mismatches, separate per-case/repeat selections, selector timeout and wait-failure cleanup, and successful process disposal. Real process launches/firewall writes:0.

Passed: PowerShell5.1 parser over all six owned scripts, exit0, zero parser errors. The first complete control run was15/exit0; the later run added the successful selector disposal case and was16/exit0. No failing control run occurred in this increment.

NotRun by worker: real Cooking process/concurrency/rich-recovery/measurement runs, installed-selector integration, existing34 firewall controls and physical LAN. Root owns these checks and their evidence. Physical two-PC remains NOT_VERIFIED. Files are frozen for root integration/reviewer verification.
