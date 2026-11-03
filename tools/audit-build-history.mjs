// Read only: extract executed main-project builds from exported Codex JSONL logs.
// Prints a sanitized ledger to stdout; never copies prompts or raw tool output.
import fs from 'node:fs';
import crypto from 'node:crypto';
import readline from 'node:readline';
const files = process.argv.slice(2);
const calls = new Map(), output = new Map(), cells = new Map(), continuations = new Map(), unmatched = [];
for (const file of files) {
  for await (const line of readline.createInterface({input:fs.createReadStream(file),crlfDelay:Infinity})) {
    if (!line.trim()) continue;
    let row; try { row = JSON.parse(line); } catch { continue; }
    const p = row.payload;
    if (!p || row.type !== 'response_item') continue;
    if (p.type === 'custom_tool_call_output' || p.type === 'function_call_output') {
      const id = continuations.get(p.call_id) ?? p.call_id;
      const text = typeof p.output === 'string' ? p.output : JSON.stringify(p.output);
      output.set(id, (output.get(id) ?? '') + text);
      const cell = text.match(/Script running with cell ID ([\w-]+)/);
      if (cell) cells.set(cell[1], id);
      continue;
    }
    if (p.type !== 'custom_tool_call' && p.type !== 'function_call') continue;
    const source = p.input ?? p.arguments ?? '';
    const wait = source.match(/"?cell_id"?\s*:\s*"?([\w-]+)/);
    if (wait && /wait$/.test(p.name)) {
      const id = cells.get(wait[1]); if (id) continuations.set(p.call_id,id);
    }
    if (!/dotnet\s+(?:build|msbuild)|(?:MSBuild|msbuild)\.exe/.test(source)) continue;
    const commands = [...source.matchAll(/(?:\bcommand|\bcmd|"command"|"cmd")\s*:\s*("(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|`[^`]*`)/g)]
      .map(m => { try { return JSON.parse(m[1]); } catch { return m[1].slice(1,-1); } });
    const builds = commands.flatMap(command => command.split(/\r?\n|;/))
      .filter(command => /(?:dotnet\s+(?:build|msbuild)|(?:MSBuild|msbuild)\.exe)/.test(command))
      .filter(command => /StagePhysicsEvents[\\/]+StagePhysicsEvents\.csproj|\bStagePhysicsEvents\.csproj/.test(command));
    if (builds.length) calls.set(p.call_id, {time:row.timestamp, count:builds.length, source, commands:builds});
    else if (/StagePhysicsEvents/.test(source)) unmatched.push({time:row.timestamp, commands, source:source.slice(0,600)});
  }
}
const results = [], review = [];
for (const [id, call] of calls) {
  const result = output.get(id) ?? '';
  const evidence = result.replace(/\\r|\\n/g, '\n');
  const launched = /MSBuild version|Determining projects to restore|復元対象のプロジェクト|StagePhysicsEvents\.csproj\]|StagePhysicsEvents ->|Build succeeded|Build FAILED|ビルドに成功|ビルドに失敗|error (?:MSB|CS|NETSDK|NU)|error :|Access to the path .*StagePhysicsEvents/.test(evidence);
  const succeeded = /Build succeeded|ビルドに成功/.test(evidence);
  const failed = /Build FAILED|ビルドに失敗|error (?:MSB|CS|NETSDK|NU)|error :/.test(evidence);
  const key = crypto.createHash('sha256').update(id).digest('hex').slice(0,16);
  if (!launched || call.count !== 1 || (succeeded && failed)) {
    review.push({key,time:call.time,count:call.count,commands:call.commands,output:result.slice(0,2000)}); continue;
  }
  results.push({time:call.time, evidence:key, result:succeeded?'succeeded':failed?'failed':'started'});
}
results.sort((a,b)=>a.time.localeCompare(b.time));
console.log(JSON.stringify({attempts:results.map((r,i)=>({number:i+1,...r})),review,unmatched},null,2));
