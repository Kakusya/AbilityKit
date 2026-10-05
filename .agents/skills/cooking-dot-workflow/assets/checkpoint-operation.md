# Flow checkpoint / operation

Fill actual values or null + reason; these describe version-1 fields, not runnable placeholder input. [Recovery](../references/recovery.md) owns paths/sequence/authority/order.

- Binding (`flow.json`): version=1, skill=cooking-dot-workflow, generated flow_id, canonical_root, execution_host/worktree_id, task_path, verified repository, branch/target_branch, delivery_mode, original_session/terminal, run_id or null, approval_ref, created_utc.
- Checkpoint (unique evidence file): source SHA/dirty; invocation/approval and per-file Cooking consumer; Run/host; workers' Task/Dispatch/owner/model/effort/turn-start/liveness/retention/raw refs; request/budget/quota; candidate SHA + delivered SHA-256 map; checks/results/counts/commands/exits/raw/binary identities; unresolved operations; pause fact and next bounded action/blocker/owner. No second task status.
- Intent: version, flow_id, seq, op_id=`flow_id:NNNNNN`, kind=intent, recorded_utc, action, target, authorization_ref, payload_ref, preconditions (head/candidate/request/Run/Task/Dispatch/host).
- Receipt: version, flow_id, seq, same op_id, kind=receipt, recorded_utc, intent_hash, outcome=applied|not-applied|unknown, raw_ref, result (command/exit/identities/postconditions/stages/replay; resolves if reconciliation).

Intent/readback → external effect → raw evidence + immutable receipt/readback → derived index/readback. Missing receipt reconciles before retry. Read-only reconstruction has no write permission; canonical writes need verified original sole coordinator **and** record-write authority.
