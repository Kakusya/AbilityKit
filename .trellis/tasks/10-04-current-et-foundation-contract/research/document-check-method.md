# Documentation verification method

This task-scoped checker is an audit aid, not a production architecture gate. It checks UTF-8 decoding, replacement/lossy text, local links and explicit contract anchors, balanced fences, exact archived AGENTS text, documentation-only paths and git diff --check. Existing baseline errors are reported separately. External URLs are not claimed HTTP-verified; Issue and labels were read through authenticated gh. Tables/source semantics receive independent manual review.

Controls: existing-link positive, deliberately missing-link negative, unclosed-fence negative. Runtime build/test/Unity NotRun.

Actual command: `python local/verify_issue5_docs.py`. Exit/results are saved in doc-validation.json. To reproduce, save the following UTF-8 source as local/verify_issue5_docs.py and run from the repository root while this task path exists.

```python
from pathlib import Path
from urllib.parse import unquote, urlsplit
import subprocess, re, json, hashlib

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
diff = subprocess.run(['git', 'diff', '--check'], capture_output=True, text=True)
if diff.returncode:
    introduced.append(['git', 'diff-check', diff.stdout + diff.stderr])
report = {'status': 'Passed' if not introduced else 'Failed', 'baseline': BASE, 'files': results, 'introducedErrors': introduced, 'preexistingErrors': preexisting, 'historicalTextExact': history_equal, 'outOfScope': out_of_scope, 'gitDiffCheckExit': diff.returncode, 'checkerControls': controls, 'runtime': {'dotnet': 'NotRun', 'unity': 'NotRun', 'game': 'NotRun'}, 'command': 'python local/verify_issue5_docs.py'}
(TASK / 'research/doc-validation.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'status': report['status'], 'files': len(results), 'links': sum(x['localLinksChecked'] for x in results), 'introducedErrors': introduced, 'preexistingErrors': preexisting, 'historyExact': history_equal, 'gitDiffCheckExit': diff.returncode}, ensure_ascii=False, indent=2))
raise SystemExit(bool(introduced))

```
