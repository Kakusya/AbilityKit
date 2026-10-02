# Independent durable Host baseline review

Boundary: integration cooking-integration-s06-s14 HEAD d83dc5cf596ca27fe4e6433d76d9e5e30c10d048 plus durable Host/core WIP inspected on 2026-10-03. Host file SHA256 81EC429604A13F0F5B1C858B1459CF98C101360FD38FFDDEC6E1EE268461307D. Read-only assignment: no production edit or .NET command executed.

## Findings (fixed)

No reviewer fixes. Prepared generation commit validates and allocates lifecycle event storage before IO; its idempotent commit matches legacy lifecycle behavior. Current baseline format3 integrity covers required HostFrameSequence, avoiding a reset of the saved Host clock. Legacy typed2 is explicitly rejected rather than silently accepted.

## Findings (not fixed)

LoadMajorBaseline's prepublication catch filters only ArgumentException, InvalidOperationException and OverflowException. External factory/provider calls are not constrained to those exception classes. An ordinary IOException from factory.Create, preparation, menu, Front or confirmed-choice provider escapes instead of returning InitializationFailed. The finally cleanup remains present, but the structured load contract is incomplete for ordinary initialization failure. Reported to coordinator; recommend an actual IOException factory control followed by successful valid load and an explicit ordinary-versus-catastrophic exception policy. No source edits by this reviewer.

## Reviewed behavior

- Config identity, scoped preparation/Front/menu identities and independently reconstructed locked choices are validated; rehashed payload choices are not grants. Menu unlocks must be confirmed choices. Wrong trust checks precede kernel creation where possible.
- Load acquires ownership before any kernel mutation and checks match scope. Foreign-owned kernels are neither mutated nor released/closed; cleanup disposes the unpublished Host tree. Fresh target factory seed poses, not saved coordinates alone, drive projected geometry; both saved seed and normalized Kitchen poses must equal those trusted seeds.
- Success stages carried stock, portion state, manual elapsed process, pending delivery remaining time and allocator via AcceptSuccessHandoff. New geometry is installed before state acceptance. Load returns Created with saved HostFrameSequence and one owned kitchen; normal Prepare/Start reuse it.
- All explicit publication injection points and geometry/Front/factory validation precede WriteBaseline. Store validates, writes .next, rereads and atomically replaces the named file through File.Move overwrite. IO rejection restores source tree and retains previous bytes.
- Immediately after successful file replacement, baselineCommitted suppresses ordinary failure rollback and staged-kernel cleanup. Prepared lifecycle commit has no normal validation failure. Subsequent bookkeeping can still allocate or invoke owner cleanup; do not claim the entire post-rename path is allocation-free or that catastrophic failure preserves a live in-memory source. The committed file is the recovery authority after that point. No fsync/power-loss guarantee.
- Retry API never receives the store and does not rewrite successful baseline.

## Verification

Whitespace: integration git diff --check passed, normalization warnings only. Lint/type-check/tests: not independently run, as instructed; coordinator reports48 focused passes comprising20 durable +16 generation +12 Ready. Reviewed test assertions include complete Created Observe and final continuation canonical equality, pending receipt, resumed manual work and subsequent allocator use, five precommit publication failures, IO .next rejection preserving file bytes, invalid record/legacy/config/menu, rehashed choice and legal alternate seed rejection, foreign ownership and legacy no-preparation policy.

Full natural service, replay, broad integration gates, network and Unity completion are not established by this report. The factory-exception control above remains an open finding at this exact source boundary.

## Final amendment: resolved at frozen producer71a9bcbd5

Reviewed frozen commit71a9bcbd5d0dbca064ab0e0b4801d12c4a7c03b9, Host SHA25603363C9904C77992E9B7A65C28B7EEDC379BD5DE8D382DC309C9BD42C1420F0C. The earlier factory exception finding is resolved: unpublished Load catches factory exceptions and returns InitializationFailed; cleanup stays in finally, with no change to post-rename transaction handling. No open blocking source finding remains at this reviewed boundary.

Final producer evidence is50/50, zero failures/skips:22 durable +16 generation +12 Ready, cooking-major-baseline-host-io-final.trx/log. Relative to the previously dispatched48/48 combination, the two added controls are (1) legacy factory durable transition/load uses target trusted seed without preparation policy and normal Start reuses that single loaded kernel, and (2) actual factory IOException returns structured InitializationFailed, preserves file bytes and permits a subsequent valid Created load. The producer report's earlier49/49 paragraph is explicitly superseded by its final freeze amendment. These results are attributed producer evidence, not independently executed tests. Independent git diff --check passed again. Coordinator broad gate remains pending; natural Ended-to-durable-successor/load integration remains separate acceptance.

The post-rename bookkeeping allocation/cleanup caveat above remains a documented limitation, not an unresolved ordinary rollback defect or power-loss guarantee.
