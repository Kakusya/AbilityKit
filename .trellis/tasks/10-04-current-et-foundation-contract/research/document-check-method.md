# Documentation verification method (R3 revision)

This task-scoped audit aid is not the #6 production test runner or an implemented architecture gate. It checks declared documentation coverage: strict UTF-8, lossy/replacement text, local links and explicit contract anchors, balanced fences, exact historical AGENTS text and documentation-only scope. Pure documentation checks may pass their own declared coverage; .NET/Unity/game/network/protoc tests remain NotRun, never tests Passed.

## Reproduce committed and local difference checks separately

Save the UTF-8 code below as local/verify_issue5_docs.py, then run from the repository root:

```text
python local/verify_issue5_docs.py --reviewed-head <reviewed-head> --output local/issue5-final-doc-validation.json
git diff --check 5312c6e4bf2b612260297e2d8623aa362a9051e6 <reviewed-head>
git diff --check
git diff --cached --check
```

The first diff checks the committed PR change against its approved baseline even after the worktree is clean. The second diff checks unstaged changes; the third checks the index versus HEAD. A clean-tree zero from a local-only diff is not evidence for the committed PR difference. The checker resolves reviewed-head to an exact commit and records all three actual argv/exit codes, checkedOutHead, gitStatusBefore and committedContentProven (true only for a clean tree at the reviewed commit). The final reviewed SHA is reported in the Issue response rather than recursively committed into its own record.

Markdown content is scanned from the working tree, so committed-content proof requires clean git status and checked-out HEAD equal to reviewed-head. For a different revision, use an authorized checkout of that revision; supplying its SHA alone does not check its file content. Precommit scans explicitly record dirty paths and the old committed head. The --output local/... option keeps final SHA receipts outside tracked documents, avoiding self-SHA commit loops.

Controls: valid existing-link positive, deliberately missing-link negative, unclosed-fence negative. Previous baseline link issues are reported separately. External URLs are not blanket HTTP-verified; Issue/review/labels are read with gh. Source/table semantics require independent review. The checker does not assert CI execution; check-runs/status counts must be queried separately. Empty statuses and pending aggregate do not prove a running CI job.

Original first-round commands/results in verification.md and prior Issue response remain historical evidence; this method supersedes their local-only reproduction recipe. No runtime/CI/protocol code is changed.

## Checker source

```python
from pathlib import Path
from urllib.parse import unquote, urlsplit
import subprocess, re, json, hashlib, argparse

parser = argparse.ArgumentParser(description='Task-scoped documentation audit; not a runtime test gate.')
parser.add_argument('--reviewed-head', default='HEAD', help='Committed PR revision to check against BASE.')
parser.add_argument('--output', default='.trellis/tasks/10-04-current-et-foundation-contract/research/doc-validation.json')
args = parser.parse_args()
reviewed_head = subprocess.check_output(['git', 'rev-parse', '--verify', args.reviewed_head + '^{commit}'], text=True).strip()
checked_out_head = subprocess.check_output(['git', 'rev-parse', '--verify', 'HEAD'], text=True).strip()
tree_status = subprocess.check_output(['git', 'status', '--porcelain'], text=True).splitlines()
committed_content_proven = not tree_status and checked_out_head == reviewed_head

ROOT = Path.cwd()
BASE = '5312c6e4bf2b612260297e2d8623aa362a9051e6'
TASK = Path('.trellis/tasks/10-04-current-et-foundation-contract')
files = [Path('AGENTS.md'), Path('Docs/design/CookingGame/progress.md'), Path('Docs/design/CookingGame/reference/et-entity-tree.md'), Path('Docs/design/CookingGame/current-et-foundation-contract.md'), Path('Docs/design/CookingGame/history/agents-notices-2026-10-04.md')]
files += sorted(TASK.rglob('*.md'))

def prose(text):
    out, fence = [], None
    for number, line in enumerate(text.splitlines(), 1):
        match = re.match(r'^\s*(`{3,}|~{3,})', line)
        if match:
            kind = match[1][0]
            if fence is None:
                fence = kind
            elif kind == fence:
                fence = None
            continue
        if fence is None:
            out.append((number, line))
    return out, fence

def issues(path, text):
    lines, fence = prose(text)
    errors, links = [], 0
    if fence:
        errors.append(('fence', 'unclosed'))
    for number, line in lines:
        for match in re.finditer(r'(?<!!)\[[^\]]*\]\(([^\n]*?)\)', line):
            target = match[1].strip().split(' "', 1)[0].strip('<>')
            if urlsplit(target).scheme:
                continue
            rel, _, fragment = unquote(target).partition('#')
            if not rel:
                continue
            links += 1
            resolved = (path.parent / rel).resolve()
            if not resolved.exists():
                errors.append(('link', target))
            elif fragment and resolved.is_file():
                content = resolved.read_text(encoding='utf-8-sig')
                if fragment.startswith('L') and fragment[1:].isdigit():
                    if int(fragment[1:]) > len(content.splitlines()):
                        errors.append(('line-anchor', target))
                elif fragment in ['a', 'b', 'c', 'd', 'e'] or fragment.startswith('rule-'):
                    if f'id="{fragment}"' not in content:
                        errors.append(('anchor', target))
    return errors, links

positive = issues(Path('AGENTS.md'), '[valid](Docs/design/CookingGame/progress.md)')[0]
negative_link = issues(Path('AGENTS.md'), '[invalid](issue5-deliberately-missing.md)')[0]
negative_fence = issues(Path('AGENTS.md'), '```text\nunclosed')[0]
assert not positive, positive
assert ('link', 'issue5-deliberately-missing.md') in negative_link, negative_link
assert ('fence', 'unclosed') in negative_fence, negative_fence
controls = {'validLocalLink': 'Passed', 'missingLinkRejected': 'Passed', 'unclosedFenceRejected': 'Passed'}
results, introduced, preexisting = [], [], []
for p in files:
    raw = p.read_bytes()
    try:
        text = raw.decode('utf-8-sig')
    except UnicodeDecodeError as exc:
        introduced.append([p.as_posix(), 'utf8', str(exc)])
        continue
    if '\ufffd' in text:
        introduced.append([p.as_posix(), 'replacement-char', 'U+FFFD'])
    errors, count = issues(p, text)
    previous = subprocess.run(['git', 'show', BASE + ':' + p.as_posix()], capture_output=True)
    known = set()
    if previous.returncode == 0:
        old = previous.stdout.decode('utf-8-sig')
        known = set(issues(p, old)[0])
    for kind, target in errors:
        row = [p.as_posix(), kind, target]
        (preexisting if (kind, target) in known else introduced).append(row)
    # Literal repeated question marks in newly authored prose catch lossy shell encoding.
    for n, line in prose(text)[0]:
        if '???' in line:
            if previous.returncode == 0 and line in old:
                continue
            introduced.append([p.as_posix(), 'lossy-text', n])
    results.append({'file': p.as_posix(), 'utf8': 'Passed', 'localLinksChecked': count, 'bytes': len(raw)})

original = subprocess.check_output(['git', 'show', BASE + ':AGENTS.md']).decode('utf-8-sig')
archived = Path('Docs/design/CookingGame/history/agents-notices-2026-10-04.md').read_text(encoding='utf-8')
preserved = archived.split('```markdown\n', 1)[1].rsplit('```', 1)[0]
history_equal = preserved == original + ('' if original.endswith('\n') else '\n')
if not history_equal:
    introduced.append(['history', 'fidelity', 'Mismatch'])

tracked = subprocess.check_output(['git', 'diff', '--name-only', BASE], text=True).splitlines()
untracked = subprocess.check_output(['git', 'ls-files', '--others', '--exclude-standard'], text=True).splitlines()
allowed = {'AGENTS.md', 'Docs/design/CookingGame/progress.md', 'Docs/design/CookingGame/reference/et-entity-tree.md', 'Docs/design/CookingGame/current-et-foundation-contract.md', 'Docs/design/CookingGame/history/agents-notices-2026-10-04.md'}
out_of_scope = [p for p in tracked + untracked if p not in allowed and not p.startswith(TASK.as_posix() + '/')]
if out_of_scope:
    introduced.append(['scope', 'out-of-scope', out_of_scope])
diff_checks = []
for label, argv in [('committedPR', ['git', 'diff', '--check', BASE, reviewed_head]),
                    ('unstaged', ['git', 'diff', '--check']),
                    ('staged', ['git', 'diff', '--cached', '--check'])]:
    diff = subprocess.run(argv, capture_output=True, text=True)
    diff_checks.append({'scope': label, 'argv': argv, 'exitCode': diff.returncode})
    if diff.returncode:
        introduced.append(['git', label, diff.stdout + diff.stderr])
report = {'status': 'Passed' if not introduced else 'Failed', 'baseline': BASE, 'reviewedHead': reviewed_head, 'checkedOutHead': checked_out_head, 'committedContentProven': committed_content_proven, 'scannedTree': 'working tree', 'gitStatusBefore': tree_status, 'files': results, 'introducedErrors': introduced, 'preexistingErrors': preexisting, 'historicalTextExact': history_equal, 'outOfScope': out_of_scope, 'diffChecks': diff_checks, 'checkerControls': controls, 'runtime': {'dotnet': 'NotRun', 'unity': 'NotRun', 'game': 'NotRun'}, 'command': 'python local/verify_issue5_docs.py --reviewed-head ' + args.reviewed_head + ' --output ' + args.output}
Path(args.output).write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'status': report['status'], 'reviewedHead': reviewed_head, 'checkedOutHead': checked_out_head, 'committedContentProven': committed_content_proven, 'files': len(results), 'links': sum(x['localLinksChecked'] for x in results), 'introducedErrors': introduced, 'preexistingErrors': preexisting, 'historyExact': history_equal, 'diffChecks': diff_checks}, ensure_ascii=False, indent=2))
raise SystemExit(bool(introduced))

```
