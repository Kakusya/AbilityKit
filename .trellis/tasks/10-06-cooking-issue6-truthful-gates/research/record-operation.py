"""Task-local operator for the installed workflow record API; no external effects."""
import argparse
import datetime
import json
import os
from pathlib import Path
import sys

repo = Path(__file__).resolve().parents[4]
root = Path(__file__).parent / "cooking-dot-flow"
sys.path.insert(0, str(repo / ".agents/skills/cooking-dot-workflow/scripts"))
from records import WriteAuthority, append, fingerprint, read, reconstruct, write_index

def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat().replace("+00:00", "Z")

p = argparse.ArgumentParser()
p.add_argument("mode", choices=["begin", "finish"])
p.add_argument("--action")
p.add_argument("--target")
p.add_argument("--payload", default="evidence/approval.md")
p.add_argument("--preconditions", default="{}")
p.add_argument("--seq", type=int)
p.add_argument("--raw")
p.add_argument("--result", default="{}")
p.add_argument("--result-file")
p.add_argument("--outcome", default="applied")
a = p.parse_args()
flow = read(root / "flow.json")
authority = WriteAuthority(os.environ["CODEX_SESSION_ID"], os.environ["ORCA_TERMINAL_HANDLE"], True, True, "evidence/authority.json")
state = reconstruct(root)
seq = state["last_seq"] + 1 if a.mode == "begin" else a.seq
base = dict(version=1, flow_id=flow["flow_id"], seq=seq, op_id=f'{flow["flow_id"]}:{seq:06d}', kind="intent" if a.mode == "begin" else "receipt", recorded_utc=now())
if a.mode == "begin":
    assert not state["unresolved"], "Reconcile pending operation before a new effect"
    pre = json.loads(a.preconditions)
    if state["effective_run_id"]:
        pre.setdefault("run_id", state["effective_run_id"])
    base.update(action=a.action, target=a.target, authorization_ref=flow["approval_ref"], payload_ref=a.payload, preconditions=pre)
else:
    intent = read(root / "records" / f"{seq:06d}.intent.json")
    result = read(Path(a.result_file)) if a.result_file else json.loads(a.result)
    base.update(intent_hash=fingerprint(intent), outcome=a.outcome, raw_ref=a.raw, result=result)
append(root, base, authority)
write_index(root, authority)
final_state = reconstruct(root)
print(json.dumps({"operation": base["op_id"], "kind": base["kind"], "effective_run_id": final_state["effective_run_id"], "unresolved": final_state["unresolved"]}, ensure_ascii=False))
