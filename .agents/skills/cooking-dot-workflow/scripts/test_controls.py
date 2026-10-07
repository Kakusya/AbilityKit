"""Observable isolated controls. All effects are local simulations, not API replay.

Subprocesses exit only themselves at preset failure points. Never touches a live
Run, worker, GitHub, branch, task lifecycle or process belonging to another agent.
"""
from __future__ import annotations

import argparse
from copy import deepcopy
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import traceback
from uuid import uuid4

import records as r


SCRIPT = Path(__file__).resolve()
START = "2026-10-05T00:00:00Z"  # controlled clock, not observed execution time
SHA = "a" * 40
events = []
processes = []


def save(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2), encoding="utf-8")


def expect(name, actual, expected):
    passed = actual == expected
    events.append({"control": name, "expected": expected, "actual": actual,
                   "status": "Passed" if passed else "Failed"})
    print(json.dumps(events[-1]), flush=True)
    return passed


def rejected(fn):
    try:
        fn()
        return False
    except r.Invalid as exc:
        events.append({"diagnostic": str(exc)})
        return True


def fixture(root, run_id="simulation-run"):
    for name in ("approval", "authority", "payload", "raw"):
        save(root / "evidence" / f"{name}.json", {"simulation": True, "name": name})
    flow = {"version": 1, "skill": r.SKILL, "flow_id": str(uuid4()),
            "canonical_root": str(root.resolve()), "execution_host": "simulation-host",
            "worktree_id": "simulation-worktree", "task_path": ".trellis/tasks/simulation-only",
            "repository": "simulation-repository", "branch": "simulation-branch",
            "target_branch": "simulation-target", "delivery_mode": "branch-only",
            "original_session": "simulation-original", "original_terminal": "simulation-terminal",
            "run_id": run_id, "approval_ref": "evidence/approval.json", "created_utc": START}
    r.initialize(root, flow, authority())
    return flow


def authority(session="simulation-original", sole=True, write=True):
    return r.WriteAuthority(session, "simulation-terminal", sole, write, "evidence/authority.json")


def intent(root, seq=1, action="mock-effect", preconditions=None):
    flow = r.binding(root)
    return {"version": 1, "flow_id": flow["flow_id"], "seq": seq,
            "op_id": f'{flow["flow_id"]}:{seq:06d}', "kind": "intent", "recorded_utc": START,
            "action": action, "target": "simulation-only", "authorization_ref": "evidence/approval.json",
            "payload_ref": "evidence/payload.json", "preconditions": preconditions or {}}


def receipt(i, outcome="applied", result=None):
    return {k: i[k] for k in ("version", "flow_id", "seq", "op_id", "recorded_utc")} | {
        "kind": "receipt", "intent_hash": r.fingerprint(i), "outcome": outcome,
        "raw_ref": "evidence/raw.json", "result": result or {"simulation": True}}


def process(args, expected_exit=0):
    command = [sys.executable, "-B", *map(str, args)]
    result = subprocess.run(command, capture_output=True, text=True, timeout=30)
    processes.append({"command": command, "exit": result.returncode, "expected_exit": expected_exit,
                      "stdout": result.stdout, "stderr": result.stderr})
    expect(f"subprocess exit {len(processes)}", result.returncode, expected_exit)
    return result


def mock(root, stage):
    """Only this test adapter performs a simulated local side effect."""
    root = Path(root)
    if stage == "timing-read":
        request = r.read(root / "evidence/saved-request.json")
        reply = r.read(root / "evidence/saved-reply.json")
        print(json.dumps(r.reply_decision(request, reply, "2026-10-06T00:00:00Z")), flush=True)
        return
    if stage == "supplement-intent":
        r.append(root, intent(root, action="dot-supplement", preconditions={"request_id": "simulation-request-1"}), authority())
        print(json.dumps({"simulation": True, "stage": "supplement-intent-read-back-no-receipt"}), flush=True)
        os._exit(74)
    i = intent(root)
    if stage == "recover":
        derived = r.reconstruct(root)
        ledger = r.read(root / "effects.json") if (root / "effects.json").exists() else {}
        if derived["unresolved"]:
            # Query unique operation ID; do not invoke simulated effect again.
            outcome = "applied" if i["op_id"] in ledger else "not-applied"
            r.append(root, receipt(i, outcome, {"simulation": True, "queried_op": i["op_id"]}), authority())
        r.write_index(root, authority())
        print(json.dumps({"simulation": True, "ledger": ledger, "index": r.reconstruct(root)}), flush=True)
        return
    r.append(root, i, authority())
    print(json.dumps({"simulation": True, "stage": "intent-read-back", "op_id": i["op_id"]}), flush=True)
    if stage == "pre-effect":
        os._exit(71)
    save(root / "effects.json", {i["op_id"]: 1})
    print(json.dumps({"simulation": True, "stage": "effect-applied", "invocations": 1}), flush=True)
    if stage == "lost-receipt":
        os._exit(72)
    r.append(root, receipt(i), authority())
    print(json.dumps({"simulation": True, "stage": "receipt-read-back"}), flush=True)
    if stage == "stale-index":
        os._exit(73)
    r.write_index(root, authority())


def crash_controls(base):
    for name, exit_code, count in (("pre-effect", 71, 0), ("lost-receipt", 72, 1), ("stale-index", 73, 1)):
        root = base / name
        fixture(root)
        r.write_index(root, authority())
        process([SCRIPT, "--child", name, "--root", root], exit_code)
        read_result = process([SCRIPT.parent / "records.py", "inspect", root])
        derived = json.loads(read_result.stdout)
        expect(f"{name}: actual subprocess read last_seq", derived["last_seq"], 1)
        expect(f"{name}: unresolved before reconciliation", len(derived["unresolved"]), 0 if name == "stale-index" else 1)
        expect(f"{name}: persisted stale index", r.read(root / "index.json")["last_seq"], 0)
        process([SCRIPT, "--child", "recover", "--root", root])
        ledger = r.read(root / "effects.json") if (root / "effects.json").exists() else {}
        expect(f"{name}: no duplicate effect invocation", sum(ledger.values()), count)
        expect(f"{name}: resolved after actual subprocess reconciliation", r.reconstruct(root)["unresolved"], [])


def record_controls(base):
    root = base / "records"
    fixture(root)
    i = intent(root)
    r.append(root, i, authority())
    r.append(root, receipt(i, "unknown"), authority())
    expect("unknown receipt blocks subsequent external intent",
           rejected(lambda: r.append(root, intent(root, 2), authority())), True)
    old = root / "records/000001.receipt.json"
    original = old.read_bytes()
    expect("immutable unknown receipt cannot be overwritten",
           rejected(lambda: r.append(root, receipt(i), authority())), True)
    expect("rejected overwrite preserves receipt bytes", old.read_bytes() == original, True)
    correction = intent(root, 2, "reconcile", {"resolves": i["op_id"]})
    r.append(root, correction, authority())
    r.append(root, receipt(correction, result={"resolves": i["op_id"]}), authority())
    expect("new reconciliation pair resolves prior uncertainty", r.reconstruct(root)["unresolved"], [])
    r.write_index(root, authority())
    (root / "index.json").write_text("{corrupt", encoding="utf-8")
    before = (root / "index.json").read_bytes()
    result = process([SCRIPT.parent / "records.py", "inspect", root])
    expect("corrupt index ignored by actual subprocess read", json.loads(result.stdout)["last_seq"], 2)
    expect("read-only reconstruction does not repair shared index", (root / "index.json").read_bytes() == before, True)
    r.write_index(root, authority())
    expect("authorized index repair matches immutable records", r.read(root / "index.json"), r.reconstruct(root))
    copy = base / "old-copy"
    shutil.copytree(root, copy)
    i3 = intent(root, 3)
    r.append(root, i3, authority())
    r.append(root, receipt(i3), authority())
    group = r.copies([root, copy])
    expect("same flow copies dedup", len(group), 1)
    expect("old copy recognized against canonical", next(iter(group.values()))[1]["copy"], "old-subset")
    expect("old copy cannot write index", rejected(lambda: r.write_index(copy, authority())), True)
    conflict = r.read(copy / "records/000001.intent.json")
    conflict["target"] = "conflicting-target"
    save(copy / "records/000001.intent.json", conflict)
    save(copy / "records/000001.receipt.json", receipt(conflict, "unknown"))
    expect("conflicting valid copies refuse election", rejected(lambda: r.copies([root, copy])), True)
    expect("sequence gap rejected", rejected(lambda: r.append(root, intent(root, 5), authority())), True)
    for field, value in (("version", 2), ("flow_id", str(uuid4())), ("op_id", "wrong-id"),
                         ("payload_ref", "../outside.json")):
        bad = intent(root, 4)
        bad[field] = value
        expect(f"invalid record {field} rejected", rejected(lambda: r.append(root, bad, authority())), True)
    run = base / "run-binding"
    fixture(run, None)
    create = intent(run, action="run-create")
    binding_before = (run / "flow.json").read_bytes()
    r.append(run, create, authority())
    r.append(run, receipt(create, result={"run_id": "simulation-derived-run"}), authority())
    expect("effective Run derived from immutable receipt", r.reconstruct(run)["effective_run_id"], "simulation-derived-run")
    expect("flow binding remains immutable", (run / "flow.json").read_bytes() == binding_before, True)
    expect("mismatched operation Run refused",
           rejected(lambda: r.append(run, intent(run, 2, preconditions={"run_id": "wrong-run"}), authority())), True)
    duplicate = intent(run, 2, "run-create")
    r.append(run, duplicate, authority())
    expect("conflicting effective Run refused", rejected(lambda: r.append(run,
           receipt(duplicate, result={"run_id": "another-run"}), authority())), True)
    guard = base / "authority"
    f = fixture(guard)
    before_files = {str(p.relative_to(guard)): p.read_bytes() for p in guard.rglob("*") if p.is_file()}
    for name, denied in (("new main", authority(session="simulation-new")),
                         ("unproven sole authority", authority(sole=False)),
                         ("record write unapproved", authority(write=False))):
        expect(f"{name} refuses dispatch predicate", r.mutation_allowed(f, denied), False)
        expect(f"{name} refuses canonical record write", rejected(lambda: r.append(guard, intent(guard), denied)), True)
        expect(f"{name} refuses index write", rejected(lambda: r.write_index(guard, denied)), True)
    after_files = {str(p.relative_to(guard)): p.read_bytes() for p in guard.rglob("*") if p.is_file()}
    expect("denied recovery leaves all shared files unchanged", after_files == before_files, True)
    correlated = base / "receipt-identity"
    fixture(correlated)
    action = intent(correlated, preconditions={"request_id": "expected-request", "dispatch_id": "expected-dispatch"})
    r.append(correlated, action, authority())
    for key, value in (("action", "wrong-action"), ("request_id", "wrong-request"), ("dispatch_id", "wrong-dispatch")):
        expect(f"mismatched receipt {key} rejected", rejected(lambda: r.append(correlated,
               receipt(action, result={key: value}), authority())), True)


def copy_binding_controls(base):
    root = base / "copy-binding-canonical"
    flow = fixture(root)
    first = intent(root)
    r.append(root, first, authority())
    r.append(root, receipt(first), authority())
    old = base / "copy-binding-old"
    shutil.copytree(root, old)
    second = intent(root, 2)
    r.append(root, second, authority())
    r.append(root, receipt(second), authority())
    shutil.copytree(root, base / "copy-binding-current")
    current = base / "copy-binding-current"
    expect("equal bindings and records remain one flow",
           r.copies([root, current])[flow["flow_id"]],
           [{"root": str(root.resolve()), "copy": "equal"},
            {"root": str(current.resolve()), "copy": "equal"}])
    expect("equal binding old subset remains evidence",
           r.copies([old, root])[flow["flow_id"]],
           [{"root": str(old.resolve()), "copy": "old-subset"},
            {"root": str(root.resolve()), "copy": "equal"}])
    separate = base / "copy-binding-separate-uuid"
    separate_flow = fixture(separate)
    expect("legitimate separate UUIDs remain separate groups",
           sorted(r.copies([root, old, separate])),
           sorted([flow["flow_id"], separate_flow["flow_id"]]))

    # Both declarations are valid against their own reachable canonical root.
    # The shared UUID, rather than local validity, makes these conflicts.
    changes = {"canonical_root": None, "execution_host": "other-host",
               "worktree_id": "other-worktree", "task_path": ".trellis/tasks/other",
               "repository": "other-repository", "branch": "other-branch",
               "target_branch": "other-target", "delivery_mode": "production",
               "original_session": "other-session", "original_terminal": "other-terminal",
               "run_id": None, "approval_ref": "evidence/authority.json",
               "created_utc": "2026-10-04T23:59:00Z"}
    for field, value in changes.items():
        conflict = base / f"copy-binding-conflict-{field}"
        shutil.copytree(root, conflict)
        changed = flow | {"canonical_root": str(conflict.resolve())}
        if field != "canonical_root":
            changed[field] = value
        save(conflict / "flow.json", changed)
        expect(f"self-canonical {field} fixture is individually valid",
               r.copies([conflict])[flow["flow_id"]],
               [{"root": str(conflict.resolve()), "copy": "equal"}])
        before = {str(p.relative_to(base)): p.read_bytes()
                  for directory in (root, current, old, conflict)
                  for p in directory.rglob("*") if p.is_file()}
        for order, roots in (("forward", [root, conflict]),
                             ("reverse", [conflict, root]),
                             ("third entry", [root, current, conflict])):
            expect(f"same UUID conflicting immutable {field} rejected {order}",
                   rejected(lambda: r.copies(roots)), True)
        after = {str(p.relative_to(base)): p.read_bytes()
                 for directory in (root, current, old, conflict)
                 for p in directory.rglob("*") if p.is_file()}
        expect(f"conflicting {field} discovery leaves all inputs unchanged", after == before, True)


def request_controls(base):
    root = base / "supplement"
    flow = fixture(root)
    request = {"flow_id": flow["flow_id"], "request_id": "simulation-request-1", "type": "final-review",
               "candidate_sha": SHA, "wait_started_utc": START, "deadline_utc": "2026-10-05T00:30:00Z", "paused": False,
               "conversation_id": "simulation-conversation", "prior_message_boundary": "simulation-prior",
               "request_message_id": "simulation-posted-request"}
    reply = {"flow_id": request["flow_id"], "request_id": request["request_id"], "reviewed_sha": SHA,
             "full": True, "decision": "accept-candidate", "time_reliable": True,
             "reply_posted_utc": "2026-10-05T00:25:00Z", "generating": False,
             "conversation_id": request["conversation_id"], "message_id": "simulation-final-reply",
             "after_request_verified": True, "absent_at_prior_boundary": True}
    for name, changed, time, expected in (
        ("timely", {}, "2026-10-05T00:26:00Z", "timely"),
        ("ambiguous", {"decision": "ambiguous"}, "2026-10-05T00:31:00Z", "ambiguous"),
        ("old SHA", {"reviewed_sha": "b" * 40}, "2026-10-05T00:31:00Z", "wrong-SHA"),
        ("old request", {"request_id": "old"}, "2026-10-05T00:31:00Z", "wrong-request"),
        ("late-read timely", {}, "2026-10-06T00:00:00Z", "timely-late-read"),
        ("late", {"reply_posted_utc": "2026-10-05T00:40:00Z"}, "2026-10-06T00:00:00Z", "late"),
        ("unknown time", {"time_reliable": False, "reply_posted_utc": None}, "2026-10-06T00:00:00Z", "unknown-time")):
        result = r.reply_decision(request, reply | changed, time)
        expect(f"reply {name} classification", result["classification"], expected)
        expect(f"reply {name} valid", result["valid_resolution"], expected in {"timely", "timely-late-read"})
    paused = request | {"paused": True}
    late_read = r.reply_decision(paused, reply, "2026-10-06T00:00:00Z")
    expect("timely late-read preserves prior pause", [late_read["preserve_pause"], late_read["can_apply"]], [True, False])
    stale_pause = r.reply_decision(request, reply, "2026-10-06T00:00:00Z")
    expect("expired deadline stale paused=false retains technical reply validity", stale_pause["valid_resolution"], True)
    expect("expired deadline stale paused=false refuses automatic apply/continue",
           [stale_pause["can_apply"], stale_pause["pause_required"], stale_pause["preserve_pause"]], [False, True, True])
    expect("late read cannot enable dispatch/shared-write eligibility even for original main",
           stale_pause["can_apply"] and r.mutation_allowed(flow, authority()), False)
    proof = {k: reply[k] for k in ("flow_id", "request_id", "conversation_id", "message_id", "reviewed_sha")}
    proof |= {"verified": True, "clock_trusted": True, "clock_jump": False, "full": True, "generating": False,
              "prior_message_boundary": request["prior_message_boundary"], "full_snapshot_ref": "evidence/full-snapshot.json",
              "prior_snapshot_ref": "evidence/prior-snapshot.json", "time_source": "simulation-trusted-UTC",
              "observed_utc": "2026-10-05T00:25:00Z", "uncertainty_ms": 0}
    unknown = reply | {"time_reliable": False, "reply_posted_utc": None, "timing_proof": proof}
    result = r.reply_decision(request, unknown, "2026-10-05T00:26:00Z")
    expect("null posted UTC full observed in budget is timely", result["classification"], "timely")
    expect("observation timing basis explicit", result["timing_basis"], "complete-observation-upper-bound")
    expect("observation proof does not fabricate posted UTC", unknown["reply_posted_utc"], None)
    for name, changes, now in (
        ("fragment before deadline full first seen later", {"full": False}, "2026-10-06T00:00:00Z"),
        ("full first seen after deadline", {"observed_utc": "2026-10-05T00:40:00Z"}, "2026-10-06T00:00:00Z"),
        ("clock interval crosses deadline", {"observed_utc": "2026-10-05T00:29:59Z", "uncertainty_ms": 2000}, "2026-10-05T00:30:01Z"),
        ("clock jump", {"clock_jump": True}, "2026-10-05T00:26:00Z"),
        ("unverifiable raw proof", {"verified": False}, "2026-10-05T00:26:00Z")):
        bad = unknown | {"timing_proof": proof | changes}
        expect(f"observation refuses {name}", r.reply_decision(request, bad, now)["valid_resolution"], False)
    for name, changes in (("wrong conversation", {"conversation_id": "wrong"}),
                          ("wrong request", {"request_id": "wrong"}),
                          ("wrong SHA", {"reviewed_sha": "b" * 40}),
                          ("old reply at boundary", {"absent_at_prior_boundary": False})):
        expect(f"timely observation rejects {name}",
               r.reply_decision(request, unknown | changes, "2026-10-05T00:26:00Z")["valid_resolution"], False)
    restored = r.reply_decision(paused, unknown, "2026-10-06T00:00:00Z")
    expect("durable earlier full observation adopted on restore", restored["valid_resolution"], True)
    expect("restored earlier full proof retains pause/no authority", [restored["can_apply"], restored["preserve_pause"]], [False, True])
    save(root / "evidence/full-snapshot.json", {"simulation": True, "reply": reply, "full": True, "generating": False})
    save(root / "evidence/prior-snapshot.json", {"simulation": True, "boundary": request["prior_message_boundary"], "reply_absent": True})
    save(root / "evidence/saved-request.json", paused)
    save(root / "evidence/saved-reply.json", unknown)
    durable = process([SCRIPT, "--child", "timing-read", "--root", root])
    expect("actual subprocess reads durable prior full timing proof", json.loads(durable.stdout)["valid_resolution"], True)
    expect("actual subprocess timing read does not enable continuation", json.loads(durable.stdout)["can_apply"], False)
    expect("observation rule does not change original deadline", request["deadline_utc"], "2026-10-05T00:30:00Z")
    expect("one supplement available within budget", r.supplement_allowed(request, [], "2026-10-05T00:05:00Z", False), True)
    process([SCRIPT, "--child", "supplement-intent", "--root", root], 74)
    supplement = r.read(root / "records/000001.intent.json")
    expect("supplement intent persisted despite actual subprocess exit", len(r.reconstruct(root)["unresolved"]), 1)
    expect("lost supplement receipt still consumes quota", r.supplement_allowed(request, [supplement], "2026-10-05T00:06:00Z", False), False)
    for name, time, generating in (("generating", "2026-10-05T00:05:00Z", True),
                                  ("unknown generation", "2026-10-05T00:05:00Z", None),
                                  ("deadline", "2026-10-05T00:30:00Z", False)):
        expect(f"supplement refused {name}", r.supplement_allowed(request, [], time, generating), False)
    new = request | {"request_id": "simulation-request-2", "parent_request_id": request["request_id"],
                     "wait_started_utc": "2026-10-06T00:00:00Z", "deadline_utc": "2026-10-06T00:30:00Z"}
    expect("timeout does not grant fresh budget", r.reopen_allowed(paused, new), False)
    expect("explicit Owner reopening permits new linked request", r.reopen_allowed(paused, new, owner_resume=True), True)
    revision = reply | {"decision": "needs-revision", "observed_utc": "2026-10-05T00:26:00Z"}
    expect("valid revision permits later candidate request", r.reopen_allowed(request, new, revision_decision=revision), True)
    expect("late-read revision stale pause flag cannot refresh budget", r.reopen_allowed(request, new,
           revision_decision=revision | {"observed_utc": "2026-10-06T00:00:00Z"}), False)
    expect("old timeout/pause retained", paused["paused"], True)


def delivery_controls():
    delivery = {"mode": "branch-only", "paused": False, "candidate_sha": SHA, "accepted_sha": SHA}
    for key in ("scope_checked", "independent_check", "workers_accounted", "required_checks_passed",
                "candidate_accepted", "hashes_unchanged", "branch_push_verified", "required_pilot_passed", "effects_reconciled"):
        delivery[key] = True
    expect("complete declared branch-only delivery eligible", r.delivery_allowed(delivery), True)
    expect("paused session rejects complete/archive", r.delivery_allowed(delivery | {"paused": True}), False)
    for key, value in (("required_pilot_passed", False), ("accepted_sha", "b" * 40),
                       ("hashes_unchanged", False), ("workers_accounted", False), ("effects_reconciled", False)):
        expect(f"branch delivery refuses missing {key}", r.delivery_allowed(delivery | {key: value}), False)
    production = delivery | {"mode": "production", "merge_verified": True,
                             "integration_passed": False, "issue_required": True,
                             "issue_close_verified": False, "premerge_verified": True}
    expect("simulated postmerge failure rejects archive/close completion", r.delivery_allowed(production), False)
    complete = production | {"integration_passed": True, "issue_close_verified": True}
    expect("pure production delivery with all evidence eligible", r.delivery_allowed(complete), True)
    expect("pure production optional Issue is N/A", r.delivery_allowed(complete | {"issue_required": False, "issue_close_verified": None}), True)
    expect("pure production unverified Issue close refused", r.delivery_allowed(complete | {"issue_close_verified": False}), False)
    checks = {k: True for k in ("authorized", "head_base_current", "candidate_accepted", "required_checks_passed",
              "scope_checked", "sole_coordinator_verified", "closing_links_inspected", "closing_links_clear", "effects_reconciled")}
    checks["mode"] = "production"
    expect("pure premerge all conditions", r.premerge_allowed(checks), True)
    expect("branch-only mode always refuses merge", r.premerge_allowed(checks | {"mode": "branch-only"}), False)
    for name, changed in (("automatic close association", {"closing_links_clear": False}),
                          ("unknown server associations", {"closing_links_inspected": None}),
                          ("missing authority", {"sole_coordinator_verified": False})):
        expect(f"pure premerge refuses {name}", r.premerge_allowed(checks | changed), False)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", type=Path)
    parser.add_argument("--child", choices=("pre-effect", "lost-receipt", "stale-index", "recover", "supplement-intent", "timing-read"))
    parser.add_argument("--root", type=Path)
    args = parser.parse_args()
    if args.child:
        mock(args.root, args.child)
        return 0
    if not args.output_dir:
        parser.error("--output-dir required")
    require_new = args.output_dir / "controls-output.json"
    if require_new.exists():
        parser.error("use a new output directory to preserve original results")
    args.output_dir.mkdir(parents=True, exist_ok=True)
    snapshots = {}
    with tempfile.TemporaryDirectory(prefix="cooking-flow-controls-") as temp:
        base = Path(temp)
        for group, fn in (("crash", lambda: crash_controls(base)), ("record", lambda: record_controls(base)),
                          ("copy-binding", lambda: copy_binding_controls(base)),
                          ("request", lambda: request_controls(base)), ("delivery", delivery_controls)):
            try:
                fn()
            except Exception:
                events.append({"control": group, "status": "Failed", "raw_failure": traceback.format_exc()})
                print(traceback.format_exc(), flush=True)
        snapshots = {str(p.relative_to(base)): p.read_text(encoding="utf-8") for p in base.rglob("*") if p.is_file()}
    failed = sum(e.get("status") == "Failed" for e in events)
    passed = sum(e.get("status") == "Passed" for e in events)
    result = {"status": "Failed" if failed else "Passed", "passed": passed, "failed": failed,
              "observed_utc": datetime.now(timezone.utc).isoformat(), "python": sys.version,
              "simulation": "local mock effect reconciliation; no GitHub/Orca idempotency claim",
              "fault_model": "process-crash only", "events": events, "subprocesses": processes}
    save(args.output_dir / "controls-output.json", result)
    save(args.output_dir / "controls-fixtures.json", snapshots)
    print(json.dumps({k: result[k] for k in ("status", "passed", "failed", "simulation")}), flush=True)
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
