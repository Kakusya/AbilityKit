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