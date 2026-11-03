// Use this wrapper for every main-project build, including verification rebuilds.
// Test projects do not advance the UI-only build counter.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const lock = path.join(root, 'obj', 'stageflux-build.lock');
fs.mkdirSync(path.dirname(lock), {recursive:true});
let handle;
try { handle = fs.openSync(lock, 'wx'); }
catch { console.error('Another Stage Flux build owns the counter lock. Inspect it before retrying.'); process.exit(2); }
let code = 1;
try {
  const ledgerPath = path.join(root, 'docs', 'build-history.json');
  const sourcePath = path.join(root, 'EventBuildInfo.cs');
  const ledger = JSON.parse(fs.readFileSync(ledgerPath, 'utf8'));
  const source = fs.readFileSync(sourcePath, 'utf8');
  const match = source.match(/internal const int Number = (\d+);/);
  const previous = ledger.attempts.at(-1)?.number ?? 0;
  if (!match || Number(match[1]) !== previous) throw Error('Build counter and ledger disagree; refusing to reuse a number.');
  const number = previous + 1;
  const attempt = {number, time:new Date().toISOString(), result:'started'};
  ledger.attempts.push(attempt);
  fs.writeFileSync(ledgerPath, JSON.stringify(ledger,null,2)+'\n');
  fs.writeFileSync(sourcePath, source.replace(/internal const int Number = \d+;/, `internal const int Number = ${number};`));
  console.log(`Stage Flux UI build-${number}`);
  const result = spawnSync('dotnet', ['build','StagePhysicsEvents.csproj',...process.argv.slice(2)], {cwd:root, stdio:'inherit'});
  code = result.status ?? 1;
  attempt.result = code === 0 ? 'succeeded' : 'failed';
  attempt.exitCode = code;
  fs.writeFileSync(ledgerPath, JSON.stringify(ledger,null,2)+'\n');
  if (result.error) console.error(result.error.message);
} finally { fs.closeSync(handle); fs.unlinkSync(lock); }
process.exitCode = code;
