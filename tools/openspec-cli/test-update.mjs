import assert from 'node:assert/strict';
import { mkdtempSync, cpSync, readdirSync, readFileSync, existsSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { resolve, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
const root = fileURLToPath(new URL('../../', import.meta.url));
const scratch = mkdtempSync(join(tmpdir(), 'abilitykit-update-'));
function snapshot(dir) {
  const result = {};
  function visit(current, relative = '') {
    for (const e of readdirSync(current, { withFileTypes: true })) {
      const key = join(relative, e.name);
      if (e.isDirectory()) visit(join(current, e.name), key);
      else result[key] = readFileSync(join(current, e.name)).toString('base64');
    }
  }
  visit(dir); return result;
}
try {
  for (const dir of ['.agents/skills', '.zcode/commands', 'openspec']) cpSync(join(root, dir), join(scratch, dir), { recursive: true });
  const original = snapshot(join(scratch, '.agents/skills'));
  let first;
  for (let i = 0; i < 2; i++) {
    const result = spawnSync('powershell', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', resolve(root, 'tools/openspec-cli/openspec.ps1'), 'update', scratch, '--force'], { encoding: 'utf8' });
    assert.equal(result.status, 0, result.stdout + result.stderr);
    assert.equal(existsSync(join(scratch, '.zcode/skills')), false);
    assert.equal(readdirSync(join(scratch, '.agents/skills')).filter(n => n.startsWith('openspec-')).length, 6);
    assert.equal(readdirSync(join(scratch, '.zcode/commands/opsx')).filter(n => n.endsWith('.md')).length, 6);
    const current = snapshot(join(scratch, '.agents/skills'));
    for (const [name, bytes] of Object.entries(original)) if (!name.startsWith('openspec-')) assert.equal(current[name], bytes, name);
    const all = { ...current, commands: snapshot(join(scratch, '.zcode/commands')), specs: snapshot(join(scratch, 'openspec')) };
    if (first) assert.deepEqual(all, first, 'Repeated update must be idempotent');
    first = all;
  }
  console.log('PASS: two official updates; 6 shared skills, 6 ZCode commands, no duplicates, maintenance skills unchanged, idempotent');
} finally { rmSync(scratch, { recursive: true, force: true }); }
