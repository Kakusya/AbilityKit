# Physical two-PC handoff ? concrete commands and remaining tool gap

2026-10-03. Source/document-only; sole owned file. No build/.NET/socket/physical execution. Hardware/second endpoint unavailable: **NOT_VERIFIED**. Read physical-lan-runbook.md and physical-concurrency-control-execution.md; their same-machine passes are not LAN. Current required suite includes spatial21-phase controls plus rich F01+D31 four recovery cutpoints and natural completion; do not silently replace the latter with ordinary service.

## Freeze and copy before either endpoint starts

On one approved build machine, build the concurrency project and existing NetworkAcceptance project once, under root's .NET window. Freeze separate complete bin directories: DLLs, EXE, deps/runtimeconfig, bundled cooking/menu JSON, all dependency binaries. Copy those exact directories to both physical PCs without rebuilding on PC2. Retain a relative-file SHA256 manifest, source full HEAD/dirty patch status, runner/tool script hashes, protocol3, current Level format/Recipe5, runtimeconfig requirement, valid ET/Session MVIDs and build log. Verify copied file sets and hashes on each PC. Runtime must satisfy net10 runtimeconfig; SDK alone is not runtime proof. Wrapper NoBuild expects its project bin path in a checkout-shaped deployment; preserve that relative shape, or invoke the frozen DLL directly with identical CLI metadata. Never point NoBuild at an unrelated old output.

Record two genuinely distinct physical PCs, OS/runtime/architecture, local PID/start/exit, actual Host LAN IPv4 and remote endpoint, operator topology attestation, build manifest and run ID. Same hostname or different PIDs alone cannot prove physical topology. Assign UDP ports reachable on the intended LAN; record actual connectivity configuration rather than changing machine-wide firewall policy as part of this document. Host binds 0.0.0.0; Client uses actual Host IPv4, never localhost. Use actual READY port/PID, then launch Client promptly (20s join budget). Timestamps label artifacts only; monotonic durations remain endpoint-local, never subtract PC clocks.

## Spatial concurrency run, twice with fresh run IDs

Replace HOST_LAN_IPV4 and FULL_FROZEN_SOURCE_HEAD before executing. Use the same explicit RunId/Source/Dirty/Order on both PCs. Example first pair:

```powershell
# PC1
powershell -ExecutionPolicy Bypass -File tools/run-cooking-network-concurrency-acceptance.ps1 -Mode Host -BindIp 0.0.0.0 -Port 18091 -RunId lan-controls-remote-first-01 -Source FULL_FROZEN_SOURCE_HEAD -Dirty clean -Order remote-first -NoBuild
# PC2, after actual PC1 READY
powershell -ExecutionPolicy Bypass -File tools/run-cooking-network-concurrency-acceptance.ps1 -Mode Client -RemoteIp HOST_LAN_IPV4 -Port 18091 -RunId lan-controls-remote-first-01 -Source FULL_FROZEN_SOURCE_HEAD -Dirty clean -Order remote-first -NoBuild
```

`clean` is only valid if the frozen source was actually clean; otherwise use truthful dirty provenance and saved patch. Repeat fresh pair with RunId lan-controls-local-first-01 and Order local-first. Scheduling label does not guarantee winner; inspect actual admissions/ordinals and both terminal results. Both roles currently emit SeparateHostsRequiresPairedEvidence and physicalTwoPc NOT_VERIFIED, deliberately requiring external paired review. Endpoint180s / wrapper210s, stage10s / initial20s. Each wrapper retains local stdout/stderr/report in its stamped directory; retain shell exit code explicitly. No wrapper controls or kills a remote PC. Failed/timeout pair retains all logs and stays failed/blocked. Cleanup only launched PID with matching start-time; never global dotnet termination.

Collect both directories into a paired artifact folder without changing originals. Reconcile:

- Same fixture/run ID/config/source/dirty/tool versions/protocol/format, exact binary/content manifest sets and SHA256, valid matching ET/Session MVIDs. Distinct actual physical endpoints/PIDs and both local exit0, both passed true, no absent report.
- Exactly phases1?21 on both reports. Verify actual Client wire equals Host received wire and actual Host outbound terminal equals Client received result, per correlation/current physical channel/instance/generation/sequence. Phases1/4 genuinely have two accepted authority admissions in one owner prefix and exactly one domain winner; phase3 both independent operations succeed. Winner ownership/loser retained item, single slot, full contents/versions, allocators and Removed tombstones are preserved. Phases20/21 replay BOTH successful and rejected original Pickup/Drop with same domain/Outcome/Reason/StateVersion and IsDuplicate, no receipt overwrite or mutation.
- Incompatible PutIn rejects preserving material then compatible recovery succeeds; wrong scope/instance high sequence followed by lower current legal command succeeds; repeated sequence/conflicting same identity/old generation reject. Actual close retires old owner, token reconnect gives generation2 and a validated current-generation full image/exact Ready grant before legal new work. Old scope/generation grants do not authorize current command.
- Final full business hash/capture canonical, exact scope/instance match. Each combined baseline hash recomputes independently; recipient/session views need not have identical combined hashes. Host immutable preCloseProjection has TWO Connected/Ready/noCleanup rows. Host remoteIssued identity exactly equals Client final baseline identity and actual ACK/Ready. Client writes report while still live then holds5s; Host proves actual close of that same channel afterward. Do not derive pre-close synchronization from a later disconnected report.

SameMachine branch implements most comparison logic, but **external Host/Client branches do not currently run an offline paired verifier or certify physical topology**. Apply the above checks manually now; a small read-only `compare-cooking-physical-pair.ps1` accepting explicit report directories/manifests/topology attestation is a useful proposed tool, not implemented or approved by this document. It must compare deep fields and original exits, cannot manufacture missing evidence or flip endpoint NOT_VERIFIED merely because addresses differ.

## Existing rich companion: runnable, but not all four recovery cutpoints

Use a separately frozen existing NetworkAcceptance output and retain its wrapper/script manifest. Example ordinary service run:

```powershell
# PC1
powershell -ExecutionPolicy Bypass -File tools/run-cooking-network-process-acceptance.ps1 -Mode Host -BindIp 0.0.0.0 -Port 18090 -NoBuild
# PC2 after READY
powershell -ExecutionPolicy Bypass -File tools/run-cooking-network-process-acceptance.ps1 -Mode Client -RemoteIp HOST_LAN_IPV4 -Port 18090 -NoBuild
```

Existing wrapper has no RunId/Source/Dirty flags, so store explicit external paired provenance/shell exit manifests alongside both reports. Host/Client external modes run foreground and do not automatically retain wrapper stdout/stderr; redirect each entire invocation to its own log and preserve `$LASTEXITCODE` separately. Retain checkpoint output. Do not pass unsupported concurrency flags to this wrapper.

Existing rich Program/ProcessServiceFixture covers finite procurement, Preparing manual handoff, unattended/shared processing, completed unbound drink handoff/bind/delivery, natural two submissions/one unmet departure/zero-star cleanup success, actual durable successor and token rebind in successor Created. Compare complete gameplay hash/scope/instance/config/content provenance, exact accepted orders/unmet departure/zero stars/cleanup, successor checkpoint/generation and both process exits. Inspect every named check, not merely final hash. Final Session views can differ after remote exits; unlike new controls runner, this old companion has no newest-image pre-disposal freeze handshake.

**Genuine remaining capability:** current rich independent-process runner has no selectable four-cutpoint mode. Preparing released-manual work is not Running lifecycle Pause/disconnect/partner continuation. It does not disconnect during actual automatic processing, does not explicitly execute the rich unbound-cup live recovery cut, and does not genuinely discard matching Submit CommandResult then rebind/cached retry. Its Created successor reconnect does not substitute. Local rich4 ET tests prove their framed InProcess scope only; synthetic ProcessMeasurement lost-reply controls are not the full finite F01+D31 physical rich case. Therefore ordinary companion plus concurrency21 alone cannot certify the owner-requested full four-breakpoint physical suite.

Smallest local follow-up proposal: isolated new application `NetworkRichRecoveryAcceptance`, reusing the frozen real menu fixture/planner source through an explicitly reviewed test/application-only source linkage or new owned adaptation, without editing the accepted ordinary runner or production. Host real ET/Session plus local/UDP passive peers; selectable manual-running-pause, automatic-disconnect, unbound-cup-handoff, lost-submit-rebind. Use actual Definition/manual step and finite supplies, legal scheduler/lifecycle controls, complete image/grants and current-generation retries; application-level matching response discard must be labelled application reply loss, not UDP loss. Each fresh case must naturally deliver2/unmet1/zero-star success/cleanup and actual durable successor, retaining full items/container/process/receipt/allocator/tombstone invariants and both endpoint reports. A trusted scheduler barrier may coordinate cuts, never sidefile gameplay authority or fake capture. Preserve actual source/fixture catalog hashes and explicit changed timing. Design/review/implementation then same-machine paired proof must precede physical deployment; this document neither authorizes code nor assumes feasibility/pass.

Physical completion thus requires actual two-PC paired controls AND rich ordinary service/successor evidence plus all required rich four cutpoints with the missing tool supplied. Until then N02/N03 physical exit remains open; no Unity or numeric performance PASS follows. Meaningful local work still exists: concrete rich-cutpoint tool design and offline paired verifier design, instead of declaring the overall work blocked solely by unavailable hardware.
