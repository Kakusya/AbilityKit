"""Read-only, scoped artifact inspection; stdout is the durable JSON report.

This is not a YAML parser or a substitute pass for unavailable quick_validate.
No live runtime authority checks or product tests are claimed.
"""
import ast
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys


ROOT = Path.cwd()
SKILL = ROOT / ".agents/skills/cooking-dot-workflow"
SOP = ".trellis/spec/abilitykit/supervised-issue-delivery.md"
commands = []
checks = []


def git(*args):
    cmd = ["git", *args]
    result = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8")
    commands.append({"command": cmd, "exit": result.returncode, "stdout": result.stdout, "stderr": result.stderr})
    if result.returncode:
        raise RuntimeError(result.stderr)
    return result.stdout


def check(name, actual, expected):
    checks.append({"check": name, "expected": expected, "actual": actual,
                   "status": "Passed" if actual == expected else "Failed"})


source = git("rev-parse", "HEAD").strip()
branch = git("branch", "--show-current").strip()
check("required branch", branch, "Kakusya/cooking-dot-skill-polish")
skill_text = (SKILL / "SKILL.md").read_text(encoding="utf-8")
header = skill_text.split("---", 2)[1].strip().splitlines()
fields = dict(line.split(": ", 1) for line in header)
check("simple two-scalar frontmatter keys", sorted(fields), ["description", "name"])
check("frontmatter name", fields["name"], SKILL.name)
check("description present and bounded", 0 < len(fields["description"]) <= 1024, True)
broken = []
links = 0
for path in sorted(SKILL.rglob("*.md")):
    for target in re.findall(r"\]\(([^)]+)\)", path.read_text(encoding="utf-8")):
        if "://" not in target and not target.startswith("#"):
            links += 1
            if not (path.parent / target.split("#", 1)[0]).is_file():
                broken.append({"file": str(path.relative_to(ROOT)), "target": target})
check("local linked resources exist", broken, [])
check("three concise templates", sorted(p.name for p in (SKILL / "assets").glob("*.md")),
      ["checkpoint-operation.md", "dot-decision.md", "dot-request.md"])
syntax = []
for path in sorted((SKILL / "scripts").glob("*.py")):
    ast.parse(path.read_text(encoding="utf-8"), filename=str(path))
    syntax.append(str(path.relative_to(ROOT)))
check("skill-local Python AST parses", len(syntax), 2)
old = git("show", f"{source}:{SOP}")
new = (ROOT / SOP).read_text(encoding="utf-8")
start = "## 显式 Cooking dot 工作流（2026-10-05 Owner 批准）"
end = "## 职责与权威"
check("SOP before opt-in section unchanged", new.split(start)[0], old.split(start)[0])
check("SOP after opt-in section unchanged", new.split(end, 1)[1], old.split(end, 1)[1])
check("AGENTS and openai.yaml unchanged", git("diff", "--name-only", source, "--", "AGENTS.md",
      ".agents/skills/cooking-dot-workflow/agents/openai.yaml").strip(), "")
helper = ast.parse((SKILL / "scripts/records.py").read_text(encoding="utf-8"))
imports = sorted({(node.module or "").split(".")[0] for node in ast.walk(helper) if isinstance(node, ast.ImportFrom)} |
                 {alias.name.split(".")[0] for node in ast.walk(helper) if isinstance(node, ast.Import) for alias in node.names})
check("record helper imports only approved stdlib", sorted(set(imports) - {
    "__future__", "argparse", "dataclasses", "datetime", "hashlib", "importlib", "json", "pathlib", "re", "uuid"}), [])
hashes = {str(p.relative_to(ROOT)).replace("\\", "/"): hashlib.sha256(p.read_bytes()).hexdigest()
          for p in sorted(SKILL.rglob("*")) if p.is_file() and "__pycache__" not in p.parts}
hashes[SOP] = hashlib.sha256((ROOT / SOP).read_bytes()).hexdigest()
result = {"status": "Failed" if any(c["status"] == "Failed" for c in checks) else "Passed",
          "source_sha": source, "dirty": bool(git("status", "--porcelain")), "branch": branch,
          "python": sys.version, "links_checked": links, "syntax_files": syntax, "checks": checks,
          "commands": commands, "file_sha256": hashes,
          "scope": "simple frontmatter/local links/Python syntax/SOP boundary; no PyYAML or live integration proof"}
print(json.dumps(result, indent=2, ensure_ascii=False))
raise SystemExit(0 if result["status"] == "Passed" else 1)
