import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { loadCatalog } from '../common/catalog.js';
import { loadScenario } from '../scenario/load.js';
import { DT, EPS } from './common.js';
import { createSim } from './sim.js';

// WO-02 transcript precision and safety-stop allowance.
const TRANSCRIPT_DECIMALS = 3;
const SAFETY_DAYS = 5;
export const roundedJSON = value => JSON.stringify(value, (key, v) =>
  typeof v === 'number' ? Number(v.toFixed(TRANSCRIPT_DECIMALS)) : v, 2) + '\n';

export function runHeadless(scenario, catalog, script = [], { pure = false } = {}) {
  const sim = createSim(scenario, catalog, { pure });
  const commands = script.map((entry, index) => ({ ...entry, index })).sort((a, b) => a.t - b.t || a.index - b.index);
  const transcript = { scenario: scenario.id, seed: scenario.seed, snapshots: [], messages: [], result: null };
  let index = 0, snapshotDay = -1;
  const drain = () => { for (const message of sim.drain()) transcript.messages.push({ t: message.day ?? sim.t, message }); };
  while (!sim.done && sim.t <= scenario.plan.deadlineDay + SAFETY_DAYS) {
    while (index < commands.length && commands[index].t <= sim.t + EPS) {
      const { seat, cmd } = commands[index++];
      const reply = sim.apply(seat, cmd);
      transcript.messages.push({ t: sim.t, message: { t: reply.ok ? 'ack' : 'nack', v: 1, id: cmd.id, ...reply } });
      drain();
    }
    const day = Math.floor(sim.t + EPS);
    if (day > snapshotDay) { transcript.snapshots.push(sim.view('facilitator')); snapshotDay = day; }
    drain();
    if (sim.speed === 'paused') {
      transcript.safetyStop = { t: sim.t, reason: 'paused without a same-time resume' };
      break;
    }
    sim.tick(DT);
    drain();
  }
  transcript.result = sim.result;
  if (!sim.done && !transcript.safetyStop) transcript.safetyStop = { t: sim.t, reason: 'time limit' };
  return transcript;
}

function main(args) {
  const [path, ...options] = args;
  if (!path || path.startsWith('--')) {
    console.log('Usage: node headless.js <scenario.json> [--script <script.json>] [--out <transcript.json>] [--pure]');
    return 2;
  }
  let scriptPath, out, pure = false;
  for (let i = 0; i < options.length; i++) {
    if (options[i] === '--pure') pure = true;
    else if (options[i] === '--script' && options[i + 1]) scriptPath = options[++i];
    else if (options[i] === '--out' && options[i + 1]) out = options[++i];
    else throw new Error(`Unknown or incomplete option: ${options[i]}`);
  }
  const catalog = loadCatalog(), scenario = loadScenario(path, { catalog });
  const script = scriptPath ? JSON.parse(readFileSync(scriptPath, 'utf8')) : [];
  const transcript = runHeadless(scenario, catalog, script, { pure });
  if (out) {
    mkdirSync(dirname(resolve(out)), { recursive: true });
    writeFileSync(out, roundedJSON(transcript));
    console.log(`${scenario.id}: ${transcript.result?.lost ?? 'finished'} t=${transcript.result?.finishDay ?? transcript.snapshots.at(-1)?.t}`);
  } else process.stdout.write(roundedJSON(transcript));
  return transcript.safetyStop ? 1 : 0;
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  try { process.exitCode = main(process.argv.slice(2)); }
  catch (error) { console.error(error.message); process.exitCode = 1; }
}
