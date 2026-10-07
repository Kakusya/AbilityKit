"""Skill-local records and pure gates; no runtime/API effects or exclusivity service.

CLI reads only. WriteAuthority is a caller's separately verified attestation, not
a lock or proof produced by this helper. Process-crash envelope, no fsync claim.
"""
from __future__ import annotations

import argparse
from dataclasses import dataclass
from datetime import datetime, timedelta
import hashlib
import importlib.util
import json
from pathlib import Path, PurePosixPath
import re
from uuid import UUID


VERSION = 1
SKILL = "cooking-dot-workflow"
SHA = re.compile(r"[0-9a-f]{40}\Z")
HASH = re.compile(r"[0-9a-f]{64}\Z")
FILE = re.compile(r"([0-9]{6})\.(intent|receipt)\.json\Z")


class Invalid(ValueError):
    pass


def require(condition, reason):
    if not condition:
        raise Invalid(reason)


def utc(value):
    require(isinstance(value, str) and value.endswith("Z"), "UTC timestamp must end in Z")
    try:
        return datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError as exc:
        raise Invalid("invalid UTC timestamp") from exc


def fingerprint(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(",", ":"),
                                    ensure_ascii=False, allow_nan=False).encode("utf-8")).hexdigest()


def read(path):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            require(key not in result, "duplicate JSON key")
            result[key] = value
        return result
    try:
        value = json.loads(Path(path).read_text(encoding="utf-8"),
                           object_pairs_hook=unique,
                           parse_constant=lambda x: (_ for _ in ()).throw(Invalid("nonfinite JSON")))
        require(isinstance(value, dict), "expected JSON object")
        return value
    except (OSError, ValueError) as exc:
        raise Invalid(f"cannot read {path}: {exc}") from exc


def text_identity(value):
    return isinstance(value, str) and bool(value.strip())


def reference(root, value):
    require(isinstance(value, str) and "\\" not in value and ":" not in value,
            "reference must be root-relative")
    parts = PurePosixPath(value)
    require(not parts.is_absolute() and ".." not in parts.parts and
            len(parts.parts) >= 2 and parts.parts[0] == "evidence", "invalid evidence reference")
    resolved = (Path(root) / value).resolve()
    require(resolved.is_relative_to(Path(root).resolve()) and resolved.is_file(),
            "missing or escaping evidence reference")
    return resolved


def binding(root):
    value = read(Path(root) / "flow.json")
    validate_binding(root, value)
    return value


def validate_binding(root, value):
    keys = {"version", "skill", "flow_id", "canonical_root", "execution_host", "worktree_id",
            "task_path", "repository", "branch", "target_branch", "delivery_mode",
            "original_session", "original_terminal", "run_id", "approval_ref", "created_utc"}
    require(set(value) == keys, "binding fields/version mismatch")
    require(type(value["version"]) is int and value["version"] == VERSION and value["skill"] == SKILL,
            "unsupported binding version/skill")
    try:
        require(str(UUID(value["flow_id"])) == value["flow_id"], "flow_id must be canonical UUID")
    except (ValueError, TypeError, AttributeError) as exc:
        raise Invalid("invalid flow_id") from exc
    for name in keys - {"version", "run_id"}:
        require(text_identity(value[name]), f"missing identity: {name}")
    require(Path(value["canonical_root"]).is_absolute(), "canonical root must be absolute")
    task = PurePosixPath(value["task_path"])
    require(not task.is_absolute() and ".." not in task.parts and ":" not in value["task_path"],
            "task path must be repository-relative")
    require(value["delivery_mode"] in {"branch-only", "production"}, "invalid delivery mode")
    require(value["run_id"] is None or text_identity(value["run_id"]), "invalid Run identity")
    utc(value["created_utc"])
    reference(root, value["approval_ref"])


@dataclass(frozen=True)
class WriteAuthority:
    session: str
    terminal: str
    sole_coordinator_verified: bool
    record_write_authorized: bool
    evidence_ref: str


def mutation_allowed(flow, authority):
    """Pure predicate; never obtains authority. New/unverified main fails closed."""
    return (isinstance(authority, WriteAuthority) and
            authority.session == flow["original_session"] and
            authority.terminal == flow["original_terminal"] and
            authority.sole_coordinator_verified is True and
            authority.record_write_authorized is True)


def write_guard(root, flow, authority):
    require(mutation_allowed(flow, authority), "Blocked: exclusive original coordinator/record-write authority")
    require(Path(root).resolve() == Path(flow["canonical_root"]).resolve(), "copy is read-only")
    reference(root, authority.evidence_ref)


def _atomic(root, path, value, flow, authority):
    write_guard(root, flow, authority)
    # Resolve this repository's existing utility; do not copy its implementation.
    repo = next((p for p in Path(__file__).resolve().parents if (p / ".trellis/scripts/common/io.py").is_file()), None)
    require(repo is not None, "Trellis atomic utility unavailable")
    spec = importlib.util.spec_from_file_location("cooking_trellis_io", repo / ".trellis/scripts/common/io.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    path.parent.mkdir(parents=True, exist_ok=True)
    require(module.write_json(path, value), "atomic write failed")
    require(read(path) == value, "atomic readback mismatch")


def _immutable(root, path, value, flow, authority):
    write_guard(root, flow, authority)
    if path.exists():
        require(read(path) == value, "immutable record conflict")
        return
    _atomic(root, path, value, flow, authority)


def initialize(root, flow, authority):
    # Raw evidence must already exist; a helper cannot bootstrap proof of authority.
    validate_binding(root, flow)
    write_guard(root, flow, authority)
    _immutable(root, Path(root) / "flow.json", flow, flow, authority)


def validate_record(root, flow, record, filename):
    match = FILE.fullmatch(filename)
    require(match is not None, "invalid record filename")
    seq, kind = int(match[1]), match[2]
    require(1 <= seq <= 999999 and type(record.get("seq")) is int and record["seq"] == seq,
            "invalid sequence")
    base = {"version", "flow_id", "seq", "op_id", "kind", "recorded_utc"}
    extra = ({"action", "target", "authorization_ref", "payload_ref", "preconditions"} if kind == "intent"
             else {"intent_hash", "outcome", "raw_ref", "result"})
    require(set(record) == base | extra, "record fields mismatch")
    require(type(record["version"]) is int and record["version"] == VERSION and
            record["flow_id"] == flow["flow_id"] and record["kind"] == kind and
            record["op_id"] == f'{flow["flow_id"]}:{seq:06d}', "record identity/version mismatch")
    require(utc(record["recorded_utc"]) >= utc(flow["created_utc"]), "record predates flow")
    if kind == "intent":
        require(text_identity(record["action"]) and text_identity(record["target"]) and
                isinstance(record["preconditions"], dict), "invalid intent")
        reference(root, record["authorization_ref"])
        reference(root, record["payload_ref"])
    else:
        require(isinstance(record["intent_hash"], str) and HASH.fullmatch(record["intent_hash"]) and
                record["outcome"] in {"applied", "not-applied", "unknown"} and
                isinstance(record["result"], dict), "invalid receipt")
        reference(root, record["raw_ref"])


def reconstruct(root, extra=None):
    """Scan immutable records; index content is never read and nothing is written."""
    root = Path(root)
    flow = binding(root)
    records = {}
    for path in sorted((root / "records").glob("*")):
        if path.name.startswith(".") and path.name.endswith(".tmp"):
            continue  # Trellis's pre-replace residue is not a committed record.
        require(path.is_file() and not path.is_symlink(), "invalid record entry")
        records[path.name] = read(path)
    if extra:
        name, value = extra
        require(name not in records or records[name] == value, "immutable record conflict")
        records[name] = value
    pairs = {}
    for name, value in records.items():
        validate_record(root, flow, value, name)
        pairs.setdefault(value["seq"], {})[value["kind"]] = value
    require(sorted(pairs) == list(range(1, len(pairs) + 1)), "sequence gap")
    unresolved = []
    run_id = flow["run_id"]
    previous_time = utc(flow["created_utc"])
    for seq, pair in sorted(pairs.items()):
        require("intent" in pair, "orphan receipt")
        intent = pair["intent"]
        if "run_id" in intent["preconditions"]:
            require(run_id is not None and intent["preconditions"]["run_id"] == run_id,
                    "operation Run identity mismatch/unbound")
        require(utc(intent["recorded_utc"]) >= previous_time, "intent time order")
        previous_time = utc(intent["recorded_utc"])
        resolves = intent["preconditions"].get("resolves")
        if intent["action"] == "reconcile":
            require(resolves in unresolved, "reconciliation must reference prior unresolved operation")
        else:
            require(not unresolved, "unresolved effect blocks subsequent intent")
            require(resolves is None, "only reconciliation may resolve")
        receipt = pair.get("receipt")
        if receipt:
            require(receipt["intent_hash"] == fingerprint(intent), "receipt intent hash mismatch")
            require(utc(receipt["recorded_utc"]) >= utc(intent["recorded_utc"]), "receipt precedes intent")
            result = receipt["result"]
            require("action" not in result or result["action"] == intent["action"], "receipt action mismatch")
            for identity in ("request_id", "task_id", "dispatch_id", "run_id", "execution_host", "candidate_sha"):
                if identity in result and identity in intent["preconditions"]:
                    require(result[identity] == intent["preconditions"][identity], f"receipt {identity} mismatch")
            if receipt["outcome"] == "applied" and intent["action"] == "run-create":
                found_run = receipt["result"].get("run_id")
                require(text_identity(found_run) and run_id in {None, found_run}, "conflicting/missing Run binding")
                run_id = found_run
            if intent["action"] == "reconcile" and receipt["outcome"] != "unknown":
                require(receipt["result"].get("resolves") == resolves, "reconciliation receipt reference mismatch")
                unresolved.remove(resolves)
        if not receipt or receipt["outcome"] == "unknown":
            unresolved.append(intent["op_id"])
    return {"version": VERSION, "flow_id": flow["flow_id"], "effective_run_id": run_id, "last_seq": len(pairs),
            "unresolved": unresolved,
            "record_hashes": {k: fingerprint(v) for k, v in sorted(records.items())}}


def append(root, record, authority):
    flow = binding(root)
    write_guard(root, flow, authority)  # before any directory/file mutation
    require(type(record.get("seq")) is int and record.get("kind") in {"intent", "receipt"}, "invalid record identity")
    name = f'{record["seq"]:06d}.{record["kind"]}.json'
    reconstruct(root, (name, record))  # reject before writing
    _immutable(root, Path(root) / "records" / name, record, flow, authority)


def write_index(root, authority):
    flow = binding(root)
    write_guard(root, flow, authority)
    value = reconstruct(root)
    _atomic(root, Path(root) / "index.json", value, flow, authority)
    return value


def copies(roots):
    """Dedup against reachable declared canonical authority; never elect/repair."""
    groups = {}
    bindings = {}
    for root in roots:
        flow = binding(root)
        require(bindings.setdefault(flow["flow_id"], flow) == flow,
                "conflicting immutable bindings for same flow_id")
        canonical = Path(flow["canonical_root"])
        require(binding(canonical) == flow, "conflicting/missing canonical binding")
        actual = reconstruct(root)
        authoritative = reconstruct(canonical)
        require(all(authoritative["record_hashes"].get(k) == v
                    for k, v in actual["record_hashes"].items()), "conflicting/newer copy records")
        groups.setdefault(flow["flow_id"], []).append({"root": str(Path(root).resolve()),
            "copy": "equal" if actual == authoritative else "old-subset"})
    return groups


def validate_request(request):
    require(text_identity(request.get("request_id")) and text_identity(request.get("flow_id")), "missing request identity")
    require(request.get("type") in {"planning", "final-review"} and
            isinstance(request.get("candidate_sha"), str) and SHA.fullmatch(request["candidate_sha"]), "request type/SHA")
    require(type(request.get("paused")) is bool, "request pause fact required")
    require(all(text_identity(request.get(k)) for k in
                ("conversation_id", "prior_message_boundary", "request_message_id")), "request conversation/boundary required")
    start, deadline = utc(request["wait_started_utc"]), utc(request["deadline_utc"])
    require(deadline == start + timedelta(minutes=30), "original budget must be 30 minutes")
    return start, deadline


def reply_decision(request, reply, now):
    """Timing eligibility only; can_apply never grants dispatch/write authority."""
    start, deadline = validate_request(request)
    observed = utc(now)
    require(observed >= start, "observation before request")
    valid, basis = False, "unknown"
    if reply.get("full") is not True or reply.get("generating") is not False or reply.get("decision") not in {"accept-plan", "accept-candidate", "needs-revision", "blocked"}:
        classification = "ambiguous"
    elif reply.get("request_id") != request["request_id"] or reply.get("flow_id") != request["flow_id"]:
        classification = "wrong-request"
    elif reply.get("reviewed_sha") != request["candidate_sha"]:
        classification = "wrong-SHA"
    elif reply.get("conversation_id") != request["conversation_id"]:
        classification = "wrong-conversation"
    elif (reply["decision"] == "accept-plan" and request["type"] != "planning") or (
            reply["decision"] == "accept-candidate" and request["type"] != "final-review"):
        classification = "wrong-decision-type"
    elif not (text_identity(reply.get("message_id")) and reply["message_id"] != request["request_message_id"] and
              reply.get("after_request_verified") is True and reply.get("absent_at_prior_boundary") is True):
        classification = "unverified-reply-boundary"
    elif reply.get("time_reliable") is not True or reply.get("reply_posted_utc") is None:
        proof = reply.get("timing_proof") or {}
        require(isinstance(proof, dict), "timing proof must be object")
        proven = (proof.get("verified") is True and proof.get("clock_trusted") is True and
                  proof.get("clock_jump") is False and proof.get("full") is True and proof.get("generating") is False and
                  all(proof.get(k) == reply.get(k) for k in ("request_id", "flow_id", "conversation_id", "message_id", "reviewed_sha")) and
                  proof.get("prior_message_boundary") == request["prior_message_boundary"] and
                  text_identity(proof.get("full_snapshot_ref")) and text_identity(proof.get("prior_snapshot_ref")) and
                  text_identity(proof.get("time_source")) and type(proof.get("uncertainty_ms")) is int and
                  proof["uncertainty_ms"] >= 0)
        if proven:
            proof_time = utc(proof["observed_utc"])
            upper = proof_time + timedelta(milliseconds=proof["uncertainty_ms"])
            proven = start <= proof_time <= observed and upper <= deadline
        if proven:
            classification = "timely-late-read" if observed > deadline else "timely"
            valid, basis = True, "complete-observation-upper-bound"
        else:
            classification = "unknown-time"
    else:
        posted = utc(reply["reply_posted_utc"])
        require(posted <= observed, "reply posted after observation")
        if posted < start:
            classification = "old-reply"
        elif posted > deadline:
            classification = "late"
        else:
            classification = "timely-late-read" if observed > deadline else "timely"
            valid, basis = True, "reliable-posted-UTC"
    expired = observed > deadline
    return {"classification": classification, "timing_basis": basis, "valid_resolution": valid,
            "pause_required": request["paused"] or expired or (not valid and observed >= deadline),
            "can_apply": valid and not request["paused"] and not expired,
            "preserve_pause": request["paused"] or expired}


def supplement_allowed(request, intents, now, generating, valid_resolution=False):
    start, deadline = validate_request(request)
    reserved = sum(i.get("kind") == "intent" and i.get("action") == "dot-supplement" and
                   i.get("flow_id") == request["flow_id"] and
                   i.get("preconditions", {}).get("request_id") == request["request_id"] for i in intents)
    return (start <= utc(now) < deadline and generating is False and not valid_resolution and
            request.get("paused") is not True and reserved == 0)


def reopen_allowed(old, new, owner_resume=False, revision_decision=None):
    old_start, _ = validate_request(old)
    start, _ = validate_request(new)
    require(new["flow_id"] == old["flow_id"] and new["request_id"] != old["request_id"] and
            new.get("parent_request_id") == old["request_id"] and start >= old_start, "new linked request required")
    revision = (revision_decision is not None and revision_decision.get("decision") == "needs-revision" and
                reply_decision(old, revision_decision, revision_decision["observed_utc"])["can_apply"])
    return owner_resume is True or (revision and old.get("paused") is not True)


def premerge_allowed(evidence):
    return evidence.get("mode") == "production" and all(evidence.get(k) is True for k in ("authorized", "head_base_current", "candidate_accepted",
        "required_checks_passed", "scope_checked", "sole_coordinator_verified", "closing_links_inspected",
        "closing_links_clear", "effects_reconciled"))


def delivery_allowed(evidence):
    """Guard complete/archive; attested evidence, never changes task status."""
    if evidence.get("paused") is not False:
        return False
    common = (all(evidence.get(k) is True for k in ("scope_checked", "independent_check", "workers_accounted",
              "required_checks_passed", "candidate_accepted", "hashes_unchanged", "effects_reconciled")) and
              isinstance(evidence.get("candidate_sha"), str) and SHA.fullmatch(evidence["candidate_sha"]) and
              evidence.get("accepted_sha") == evidence["candidate_sha"])
    if evidence.get("mode") == "branch-only":
        return bool(common and evidence.get("branch_push_verified") is True and evidence.get("required_pilot_passed") is True)
    if evidence.get("mode") == "production":
        issue_ok = (evidence.get("issue_required") is False or
                    (evidence.get("issue_required") is True and evidence.get("issue_close_verified") is True))
        return bool(common and evidence.get("merge_verified") is True and evidence.get("integration_passed") is True and
                    evidence.get("premerge_verified") is True and issue_ok)
    return False


def main():
    parser = argparse.ArgumentParser(description="Read-only Cooking flow diagnostics")
    parser.add_argument("command", choices=("inspect", "copies"))
    parser.add_argument("roots", nargs="+")
    args = parser.parse_args()
    try:
        result = reconstruct(args.roots[0]) if args.command == "inspect" else copies(args.roots)
        require(args.command != "inspect" or len(args.roots) == 1, "inspect takes one root")
        print(json.dumps(result, indent=2))
        return 0
    except (Invalid, KeyError, TypeError) as exc:
        print(json.dumps({"status": "Blocked", "reason": str(exc)}))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
