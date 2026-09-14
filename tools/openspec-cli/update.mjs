import { AI_TOOLS } from './node_modules/@fission-ai/openspec/dist/core/config.js';
import { UpdateCommand } from './node_modules/@fission-ai/openspec/dist/core/update.js';

const args = process.argv.slice(2);
if (args.some(arg => arg.startsWith('-') && arg !== '--force') || args.filter(arg => !arg.startsWith('-')).length > 1) {
  console.error('Usage: openspec.ps1 update [path] [--force]');
  process.exit(1);
}
// ZCode owns its commands, but shares the agents skill root in this workspace.
const zcode = AI_TOOLS.find(tool => tool.value === 'zcode');
if (!zcode) throw new Error('OpenSpec ZCode adapter missing; review the pinned dependency before updating.');
zcode.skillsDir = '.agents';
await new UpdateCommand({ force: args.includes('--force') }).execute(args.find(arg => !arg.startsWith('-')) ?? process.cwd());
