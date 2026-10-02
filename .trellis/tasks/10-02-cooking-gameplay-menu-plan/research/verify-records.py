"""Read-only verification of this planning record; no product tests or mutation."""
from pathlib import Path
import json, re, hashlib

root = Path.cwd()
task = root / '.trellis/tasks/10-02-cooking-gameplay-menu-plan'
records = [task] + sorted(root.glob('.trellis/tasks/10-02-cooking-*'))
records = list(dict.fromkeys(records))
assert len(records) == 21, len(records)
by_id = {}
for directory in records:
    data = json.loads((directory/'task.json').read_text(encoding='utf-8'))
    assert data['status'] in {'planning','in_progress','completed'}, directory
    # This only validates bookkeeping, never proves product implementation.
    plan_id = data['meta'].get('plan_id')
    if plan_id in {'U01', 'U02', 'S15'}:
        assert data['meta']['planning_only'] is True, directory
        assert data['meta']['implementation_authorized'] is False, directory
    else:
        assert data['meta']['implementation_authorized'] is True, directory
    for file in ['prd.md','design.md','implement.md','implement.jsonl','check.jsonl']:
        assert (directory/file).exists(), (directory,file)
    if directory != task:
        assert data['parent'] == task.name, (directory, data['parent'])
        by_id[data['meta']['plan_id']] = data
    for manifest in ['implement.jsonl','check.jsonl']:
        for line in (directory/manifest).read_text(encoding='utf-8').splitlines():
            entry = json.loads(line)
            if 'file' in entry:
                assert (root/entry['file']).exists(), entry
assert len(by_id) == 20
visiting, done = set(), set()
def visit(id):
    assert id not in visiting, id
    if id in done: return
    visiting.add(id)
    for dependency in by_id[id]['meta']['dependencies']:
        assert dependency in by_id, dependency
        visit(dependency)
    visiting.remove(id)
    done.add(id)
for id in by_id: visit(id)
parent = json.loads((task/'task.json').read_text(encoding='utf-8'))
assert set(parent['children']) == {p.name for p in records if p != task}
for id in ['U01','U02']:
    assert by_id[id]['meta']['execution_gate'] == 'prohibited-unity'
audit = (task/'research/feature-audit.md').read_text(encoding='utf-8')
found = re.findall(r'^\| ([A-K]\d{2}) \|',audit,re.M)
counts = [10,14,14,10,12,10,12,8,12,8,14]
expected = {f'{g}{i:02}' for g,count in zip('ABCDEFGHIJK',counts) for i in range(1,count+1)}
assert len(found) == 124 and set(found) == expected
source = json.loads((task/'research/source-audit.json').read_text(encoding='utf-8'))
for file in source['files']:
    target = root / 'Docs/design/CookingGame/reference/menu-v0.1' / file['name']
    original = Path('D:/MyDownload') / file['name']
    assert hashlib.sha256(target.read_bytes()).hexdigest() == file['sha256']
    assert target.read_bytes() == original.read_bytes()
assert len(source['menu_ids']) == 87
assert set(source['menu_ids']) == set(source['excel_menu_ids'])
# Only links in new/changed planning docs, excluding supplied source text.
docs = list(task.rglob('*.md')) + [p for d in records if d != task for p in d.glob('*.md')]
docs += [root/p for p in ['AGENTS.md','.trellis/spec/cooking/gameplay-menu-plan.md',
    'Docs/design/CookingGame/gameplay-plan-architecture.md',
    'Docs/design/CookingGame/reference/menu-integration.md',
    'Docs/design/CookingGame/reference/menu-v0.1/README.md']]
links = 0
for doc in docs:
    for target in re.findall(r'\]\(([^)]+)\)',doc.read_text(encoding='utf-8')):
        if target.startswith(('http:','https:','sandbox:','#')): continue
        path = target.split('#')[0].strip('<>')
        assert (doc.parent/path).resolve().exists(), (str(doc),target)
        links += 1
print(json.dumps({'status':'pass','tasks':21,'children':20,'feature_ids':124,
    'menu_ids':87,'checked_links':links,'dependency_graph':'acyclic',
    'original_files':'byte-identical','product_tests':'not-run'},ensure_ascii=False))
