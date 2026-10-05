# Bounded operator documentation pilot — worker report

Artifact complete, awaiting main's independent inspection and final candidate review. This report describes documentation delivery by `task_0e2318dcd203` / `ctx_31a62bbd882d`, worker `term_04f2f37b-9f35-46d6-81cc-b26500d3c821`; main remains `term_4d2338db-ef10-45a0-9316-90733d30e4cb`, flow `a2c3dd65-4a91-43f6-9c33-ad891f7563c4`, Run `run_840202cc5621`. No commit/push, archive, canonical write, helper/runner change, external publication, nested worker, Run rebind or cleanup was performed by this worker.

## Actual load and changes

Manually read the project-path SKILL, recovery/dialogue and all three templates, helper, owning task/manifests and full AK-CDF-LIVE-PILOT-PLAN-01. Loaded repaired source is [4a4f36e6435f1315ddcec1d462c924e16b44ec5f](https://github.com/Kakusya/AbilityKit/commit/4a4f36e6435f1315ddcec1d462c924e16b44ec5f); original plan review SHA is `32b400a5c6d375be366faa606e867ef68037cfa8`. `pilot-repaired-manual-load.json`, `repair-freeze-receipt.json` and `independent-control-repair-01/results.json` were read. Source Git blobs and frozen working bytes were independently compared during document validation. This proves explicit manual reading, not host automatic invocation.

| Loaded file | Source SHA-256 |
| --- | --- |
| `SKILL.md` | `e1e37dbac50e1eab475126f5cca7e33f2e9fa765c4eb4a31caff1aebdac7b8c5` |
| `references/recovery.md` | `04dc2272e7eb380a2d7ec943c14e9528c23ad604dd923fd5ea9cf47ce654f384` |
| `references/dot-dialogue.md` | `c5f3c244676c3ec92e0adacda531bf9fe999f222a1f4d2b4eba96b02227a2fd8` |
| `scripts/records.py` | `4c186d749daafe8c62505d455fcc7660d0ca550d9b779e67d889fce8c1fcb3c0` |

Exactly three owned files changed: `.agents/skills/cooking-dot-workflow/references/operator-example.md` supplies one normal branch-only case and one stopped read-only recovery case; `.agents/skills/cooking-dot-workflow/SKILL.md` adds exactly one conditional worked-example link; this `research/pilot-report.md` records actual evidence. Cooking coordinators/workers consume these documents. Other dirty/untracked records, old failures and `__pycache__` predated dispatch and were preserved.

Git/Orca inventories showed three trees: this branch, `D:/MyWorkTree/AbilityKit` and stopped `issue6-test-gate-results`. Related AGENTS, task/research and actual diffs were inspected read-only; issue6 remains blocked with isolated example/tool changes. No other tree changed. Bootstrap returned no active session task pointer, but the dispatch explicitly names this approved in-progress owning task. Phase loader reported `Phase Index section not found in workflow.md`; this is a loader limitation, not Passed workflow validation.

## Real startup, failures and pending completion

`pilot-worker-start.json` and canonical `evidence/pilot-worker-start.json` match this Run/Task/Dispatch; `prompt.stages` contains `input_accepted` and `turn_started`, and `turnStart` is `observed`. Requested/effective agent is codex; model/effort are null, so actual values remain unknown. Canonical operation 000006 is applied **startup**, not worker success or skill acceptance. Read-only inspect returned last_seq 6, twelve record fingerprints, unresolved `[]`.

At report writing this worker's `worker_done` has not yet been sent/observed. After final inbox check it will send exactly one outcome message and idle; main must capture its actual message ID/outcome/raw payload and accounting in later main evidence. No future receipt, dot acceptance or archive is prefilled here. New uncommitted load/start/recovery/report artifacts must receive real immutable Git-SHA links in main's next evidence envelope; this report does not fabricate their publication.

Original main evidence was read, not overwritten: preliminary `records.atomic` API failure; 000003 preflight `agent_unconfigured` / not-applied; original independent 91/92 and 92/93 results with one Failed each; historical whitespace audit failure; repaired main run 93/93 Passed and worker local helper 190/190 Passed. The repaired check is main executing auditor-authored expectations on exact source, **not a fresh Orca auditor review**. These are existing bounded reports, not reruns claimed by this documentation worker.

Actual 000005 push exit0 / immediate readback exit128 remains in `evidence/publish-repair-raw.json`. Main's `publish-repair-recovery.json` records read-only `git ls-remote origin refs/heads/Kakusya/cooking-dot-skill-polish`, exit0 and exact repaired SHA; main recovered a missing receipt without repush. Original readback stderr was not retained, so its cause stays unknown. This is same-original-main effect reconciliation, not crash/takeover proof.

## Worker verification

Environment: Windows PowerShell; Python 3.14.8, `C:\Python314\python.exe`, executable SHA-256 `434b361fe0c5960d404974feed493db47f2603a1ace2574312104fbed0182c6f`; Git `2.56.0.windows.1`; Orca `1.4.220`. HEAD remains repaired SHA on `Kakusya/cooking-dot-skill-polish`, dirty main evidence plus these document edits. No product binary was built; helper identity is the hash above. Raw final assertion results are printed by the reproducible inline command below and summarized after execution.

| Check / actual command | Exit / declared coverage |
| --- | --- |
| `python -B .agents/skills/cooking-dot-workflow/scripts/records.py inspect .trellis/tasks/10-05-cooking-dot-skill-polish/research/cooking-dot-flow` | 0, Passed: existing live records read-only, last_seq6/unresolved[]; no writes |
| `git diff --check -- .agents/skills/cooking-dot-workflow/SKILL.md` | 0, Passed: tracked conditional-link change |
| Inline document API/hash/link validation via `python -B -` | First attempt Failed exit1: harness expected 17 binding fields instead of actual 16; second Failed exit1: harness used system GBK default for UTF-8 document. Only harness corrected, no helper/document defect inferred; both failures preserved here |
| `python -B C:/Users/Administrator/AppData/Roaming/orca/codex-accounts/81627c2b-efc2-4498-aeb6-64c652c0acab/home/skills/.system/skill-creator/scripts/quick_validate.py .agents/skills/cooking-dot-workflow` | Failed exit1: `ModuleNotFoundError: No module named 'yaml'`; validator coverage Blocked by dependency, no install/model/environment changes |
| Final reproducible command below, extracted from this report and executed with `python -B -` | 0, Passed: 46/46 assertions, including two actual fixture CLI subprocesses exiting0; full stdout enumerated every assertion as Passed |
| `git diff --no-index --check -- NUL <file>` separately for all three owned paths | Each exit1, stdout/stderr empty; file-versus-empty diff exits are recorded without calling them exit0 checks. Explicit whitespace/fence inspection separately covers all three documents |
| Inline UTF-8 whitespace/conflict/fence scan via `python -B -` | 0, Passed: three documents, no trailing whitespace/conflict markers, balanced code fences |

Final validator raw stdout excerpt (no live effects, no runtime/production tests):

```json
{
  "status": "Passed",
  "assertions": 46,
  "actual_fixture_subprocesses": 2,
  "delivered_sha256": {
    ".agents\\skills\\cooking-dot-workflow\\references\\operator-example.md": "fe18cbd47c948267de18943f9a10ea59c2948e84f4b5cfa4c9c1994bae4578f1",
    ".agents\\skills\\cooking-dot-workflow\\SKILL.md": "6923716ce2e64cd4c45bd3526882bf79fe911eece9342d721daaebad9c35eabc"
  }
}
```

The 46 outcomes comprise 17 snippet/API/fixture checks, 19 frozen Git/working-byte checks, one single-link-only check, and nine existing local/immutable-source link checks, all Passed. This validates Git object existence for source links, not a new remote browser load. Main must bind delivered bytes to its next actual candidate; these working-byte hashes do not pre-accept that candidate. At `2026-10-05T13:47:11.845452Z` the tree remained on the repaired SHA with main's ongoing evidence plus the three owned document paths; completion observation was still pending.

The fixture only demonstrates local record construction/readback/fingerprinting, copy refusal and stopped-reader no-write behavior. Synthetic authority and true booleans do not prove live authority. Real cross-main takeover remains Blocked; real coordinator crash, automatic host invocation, production merge/postmerge/auto-close and CI are NotRun. Product/.NET/Unity/LAN tests are N/A to this documentation change, not Passed.

Main still owns independent final artifact inspection, live completion observation/accounting, a new frozen candidate and delivered-file manifest, exact branch publication/readback and dot final-review acceptance. Branch-only requirement completion remains open. Main may save a paused session handoff without completion/archive.

### Reproducible document check

Run from the repository root: pipe the Python block below to `python -B -`. It extracts the actual document snippets and runs them in an ephemeral `nonlive-` fixture, not canonical records. The two CLI subprocesses target that fixture. All raw assertion outcomes are printed; no durable fixture/output files are added.

```python
import ast, hashlib, inspect, json, re, shutil, subprocess, sys, tempfile
from pathlib import Path
base = Path.cwd(); skill = base / ".agents/skills/cooking-dot-workflow"
doc = skill / "references/operator-example.md"
sys.path.insert(0, str(skill / "scripts")); import records
blocks = re.findall(r"```python\n(.*?)\n```", doc.read_text(encoding="utf-8"), re.S)
checks = {}
def check(name, condition):
    if not condition: raise AssertionError(name)
    checks[name] = "Passed"
for block in blocks: ast.parse(block)
check("Python blocks compile", len(blocks) == 2)
ns = {}; exec(compile(blocks[0], str(doc), "exec"), ns)
for name, args in {"initialize": ["root", "flow", "authority"],
    "append": ["root", "record", "authority"], "fingerprint": ["value"],
    "write_index": ["root", "authority"], "reconstruct": ["root", "extra"],
    "copies": ["roots"]}.items():
    check("signature " + name, list(inspect.signature(getattr(records, name)).parameters) == args)
with tempfile.TemporaryDirectory(prefix="nonlive-") as temp:
    root = Path(temp).resolve(); ev = root / "evidence"; ev.mkdir()
    for name in ["approval", "authority", "payload", "raw"]:
        (ev / (name + ".txt")).write_text("TEACHING ONLY synthetic simulation " + name, encoding="utf-8")
    auth = records.WriteAuthority("TEACHING_ONLY_SESSION", "TEACHING_ONLY_TERMINAL", True, True, "evidence/authority.txt")
    flow, intent, receipt, index = ns["exercise_teaching_api"](root, auth)
    check("fixture reconstructed", index["last_seq"] == 1 and index["unresolved"] == [] and len(index["record_hashes"]) == 2)
    check("full binding/intent/receipt fields", (len(flow), len(intent), len(receipt)) == (16, 11, 10))
    check("exact fingerprint", receipt["intent_hash"] == records.fingerprint(intent))
    digest = lambda: {p.relative_to(root).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in root.rglob("*") if p.is_file()}
    before = digest(); records.append(root, intent, auth); records.append(root, receipt, auth)
    check("identical local replay preserves bytes", before == digest())
    d = {"root": root}; exec(blocks[1], d)
    check("stopped diagnostic read only", d["diagnostic"] == index and before == digest())
    denied = records.WriteAuthority("OTHER_SESSION", "OTHER_TERMINAL", False, False, "evidence/authority.txt")
    try: records.write_index(root, denied)
    except records.Invalid: pass
    else: raise AssertionError("stopped writer allowed")
    check("stopped write denied with bytes preserved", before == digest())
    copy = root / "old-copy"; copy.mkdir(); shutil.copy2(root / "flow.json", copy / "flow.json"); shutil.copytree(ev, copy / "evidence")
    grouped = records.copies([root, copy])
    check("old subset diagnosed", grouped[flow["flow_id"]][1]["copy"] == "old-subset")
    try: records.write_index(copy, auth)
    except records.Invalid: pass
    else: raise AssertionError("copy write allowed")
    check("copied owner cannot write", not (copy / "index.json").exists())
    for command, roots, expected in [("inspect", [root], index), ("copies", [root, copy], grouped)]:
        p = subprocess.run([sys.executable, "-B", str(skill / "scripts/records.py"), command, *map(str, roots)], capture_output=True, text=True)
        check("actual fixture CLI " + command, p.returncode == 0 and json.loads(p.stdout) == expected)
freeze = json.loads((base / ".trellis/tasks/10-05-cooking-dot-skill-polish/research/repair-freeze-receipt.json").read_text(encoding="utf-8"))
for path, expected in freeze["git_blob_sha256"].items():
    p = subprocess.run(["git", "show", freeze["candidate_sha"] + ":" + path], capture_output=True)
    check("loaded Git blob " + path, p.returncode == 0 and hashlib.sha256(p.stdout).hexdigest() == expected)
    if not path.endswith("/SKILL.md"): check("frozen working bytes " + path, hashlib.sha256((base / path).read_bytes()).hexdigest() == expected)
p = subprocess.run(["git", "show", freeze["candidate_sha"] + ":.agents/skills/cooking-dot-workflow/SKILL.md"], capture_output=True, check=True)
new = (skill / "SKILL.md").read_text(encoding="utf-8")
addition = "\nFor a first operator handoff or a complete worked example, read [operator example](references/operator-example.md).\n"
check("exactly one conditional link only", new.replace(addition, "") == p.stdout.decode("utf-8") and new.count("references/operator-example.md") == 1)
for target in re.findall(r"\]\(([^)]+)\)", doc.read_text(encoding="utf-8")):
    if target.startswith("https://github.com/Kakusya/AbilityKit/blob/"):
        sha, path = target.split("/blob/", 1)[1].split("/", 1)
        p = subprocess.run(["git", "cat-file", "-e", sha + ":" + path], capture_output=True)
        check("immutable source link " + path, p.returncode == 0 and bool(re.fullmatch("[0-9a-f]{40}", sha)))
    elif not target.startswith("https:"): check("local reference " + target, (doc.parent / target).is_file())
print(json.dumps({"status": "Passed", "assertions": len(checks), "actual_fixture_subprocesses": 2, "checks": checks,
    "delivered_sha256": {str(p.relative_to(base)): hashlib.sha256(p.read_bytes()).hexdigest() for p in [doc, skill / "SKILL.md"]}}, indent=2))
```
