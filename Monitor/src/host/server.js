import { createServer } from 'node:http';
import { readFile, stat } from 'node:fs/promises';
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, extname, isAbsolute, relative, resolve, sep } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { loadCatalog } from '../common/catalog.js';
import { loadScenario } from '../scenario/load.js';
import { createSim } from '../sim/sim.js';
import { plannedPolylines } from '../sim/planning.js';
import { roundedJSON } from '../sim/headless.js';
import { createPacer, STATE_MS } from './pacing.js';
import { createClients } from './clients.js';

const ROOT = fileURLToPath(new URL('../../../', import.meta.url));
const WEB = resolve(ROOT, 'Monitor/web');
const VOICE = resolve(ROOT, 'Voice');
const OUT = resolve(ROOT, 'Monitor/out');
// Docs/PROTOCOL.md default HTTP and WebSocket port.
const DEFAULT_PORT = 7331;
const MIME = { '.html': 'text/html; charset=utf-8', '.wav': 'audio/wav', '.ico': 'image/x-icon' };

export async function startHost({ scenarioPath, stage2Path, port = DEFAULT_PORT, testMode = process.env.NODE_ENV === 'test' }) {
  const catalog = loadCatalog();
  const base = process.env.INIT_CWD || process.cwd();
  let scenario = loadScenario(resolve(base, scenarioPath), { catalog });
  const stage2 = stage2Path ? loadScenario(resolve(base, stage2Path), { catalog })
    : loadScenario(resolve(dirname(resolve(base, scenarioPath)), 'stage2_texas.json'), { catalog });
  const stages = new Map([scenario, stage2].map(s => [s.id, s]));
  const runs = new Map();
  let sim, transcript, snapshotDay, result, initialMessages, inspected, runId = 0;

  function reset() {
    runId++;
    sim = createSim(scenario, catalog);
    sim.apply('facilitator', { action: 'pause' });
    initialMessages = sim.drain();
    transcript = { scenario: scenario.id, seed: scenario.seed, snapshots: [], messages: [], result: null };
    snapshotDay = -1; result = null; inspected = new Set();
    for (const message of initialMessages) transcript.messages.push({ t: message.day ?? sim.t, message });
    snapshot();
  }
  function snapshot() {
    const day = Math.floor(sim.t + 1e-6);
    if (day > snapshotDay) { transcript.snapshots.push(sim.view('facilitator')); snapshotDay = day; }
  }
  function state(seat) {
    const view = sim.view(seat);
    // Public geometry is authored; history/projections come from the existing sim.
    const { lengthFt, stationEveryFt, zones } = scenario.job;
    view.project = { title: scenario.title, job: { lengthFt, stationEveryFt, zones } };
    if (seat === 'plan') {
      view.plan = { locked: scenario.plan.locked };
      view.crews = scenario.crews.map(({ id, name, machine }) => ({ id, name, machine }));
      view.fees = { mobilizationUsd: scenario.rules.mobilizationUsd, remobUsd: scenario.rules.demobFeeUsd,
        remobDays: catalog.cities[scenario.city].remobilizationDelayDays };
      view.plannedPolylines = plannedPolylines(sim.serialize());
    }
    if (seat === 'site') {
      const raw = sim.serialize();
      view.actualLines = Object.fromEntries(Object.entries(raw.actualLines).map(([id, points]) =>
        [id, [...points, { t: sim.t, ft: raw.crews[id].pos }]]));
      const zone = scenario.zoneOf(view.truck.stationFt);
      view.trench = view.trench.map(cell => {
        if (scenario.zoneOf(cell.stationFt) === zone || inspected.has(cell.stationFt)) return cell;
        const { water, wall, pipeDamage, ...visible } = cell;
        return visible;
      });
    }
    if (seat === 'facilitator') view.stage2 = stage2.id;
    return { t: 'state', v: 1, day: sim.t, displayDay: Math.floor(sim.t + 1e-6) + 1,
      speed: sim.speed, stage: scenario.id, runId, view };
  }
  function withVoiceText(message) {
    if (message.t !== 'voice') return message;
    return { ...message, lines: message.lineIds.map(id => scenario.lines.find(line => line.id === id))
      .filter(Boolean).map(({ id, speaker, text }) => ({ id, speaker, text })) };
  }
  function drain(requester) {
    for (let message of sim.drain()) {
      if (message.t === 'result') {
        result = { ...message, previousRun: runs.get(scenario.result.overlayPreviousStage) ?? null };
        runs.set(scenario.id, { ...message, previousRun: null });
        message = result;
        transcript.result = { ...sim.result, previousRun: result.previousRun };
      }
      transcript.messages.push({ t: message.day ?? sim.t, message });
      clients.deliver(withVoiceText(message), requester);
      if (message.t === 'result') {
        mkdirSync(OUT, { recursive: true });
        const stamp = new Date().toISOString().replaceAll(':', '-');
        writeFileSync(resolve(OUT, `${scenario.id}.${stamp}.json`), roundedJSON(transcript));
      }
    }
  }
  reset();
  const server = createServer(async (req, res) => {
    try {
      if (!['GET', 'HEAD'].includes(req.method)) { res.writeHead(405).end(); return; }
      const pathname = decodeURIComponent(new URL(req.url, 'http://localhost').pathname);
      if (pathname === '/transcript.json') {
        res.writeHead(200, { 'Content-Type': 'application/json',
          'Content-Disposition': `attachment; filename="${scenario.id}.json"` });
        res.end(req.method === 'HEAD' ? undefined : roundedJSON(transcript)); return;
      }
      const voice = pathname.startsWith('/Voice/');
      const root = voice ? VOICE : WEB;
      const path = resolve(root, voice ? pathname.slice('/Voice/'.length) : `.${pathname === '/' ? '/plan.html' : pathname}`);
      const rel = relative(root, path);
      if (rel.startsWith(`..${sep}`) || rel === '..' || isAbsolute(rel) || !(await stat(path)).isFile()) {
        res.writeHead(404).end(); return;
      }
      res.writeHead(200, { 'Content-Type': MIME[extname(path)] ?? 'application/octet-stream', 'Cache-Control': 'no-store' });
      res.end(req.method === 'HEAD' ? undefined : await readFile(path));
    } catch { res.writeHead(404).end(); }
  });
  const pacer = createPacer(() => sim, () => { snapshot(); drain(); });
  const clients = createClients(server, {
    state,
    welcome(socket, seat, send) {
      for (const message of initialMessages) if (message.seats?.includes(seat)) send(socket, withVoiceText(message));
      if (result) send(socket, result);
    },
    command(socket, seat, message, send) {
      let reply;
      if (message.t === 'ctrl' && (message.action === 'reset' || message.action === 'load')) {
        if (message.action === 'load' && !stages.has(message.stage)) reply = { ok: false, reason: 'stage not loaded' };
        else {
          if (message.action === 'load') scenario = stages.get(message.stage);
          reset(); pacer.setFast(false);
          reply = { ok: true, appliedDay: sim.t };
        }
      } else if (message.t === 'ctrl' && message.action === 'fast') {
        reply = testMode ? { ok: true, appliedDay: sim.t } : { ok: false, reason: 'unknown control' };
        if (testMode) pacer.setFast(true);
      } else reply = sim.apply(seat, message);
      const response = { t: reply.ok ? 'ack' : 'nack', v: 1, id: message.id,
        ...(reply.ok ? { appliedDay: reply.appliedDay } : { reason: reply.reason }) };
      if (reply.ok && seat === 'plan') {
        const posted = (message.ops ?? []).filter(op => op.op === 'enterPermit')
          .map(op => ({ zone: op.zone, number: sim.view('plan').permits.find(p => p.zone === op.zone)?.number }));
        if (posted.length) response.permits = posted;
      }
      if (reply.ok && seat === 'site' && message.action === 'inspect') {
        const ft = sim.view('site').truck.stationFt;
        inspected.add(Math.round(ft / scenario.job.stationEveryFt) * scenario.job.stationEveryFt);
      }
      send(socket, response);
      transcript.messages.push({ t: sim.t, message: { ...response, ok: reply.ok } });
      if (reply.ok && ['reset', 'load'].includes(message.action)) {
        clients.broadcast();
        for (const initial of initialMessages) clients.deliver(withVoiceText(initial));
      }
      drain(socket);
      if (reply.ok) { clients.broadcast(); pacer.sync(); }
    },
  });
  const broadcastTimer = setInterval(clients.broadcast, STATE_MS);
  try {
    await new Promise((resolveReady, reject) => { server.once('error', reject); server.listen(port, resolveReady); });
  } catch (error) { clearInterval(broadcastTimer); pacer.close(); clients.close(); throw error; }
  return { server, port: server.address().port,
    close() { clearInterval(broadcastTimer); pacer.close(); clients.close(); return new Promise(resolveClose => server.close(resolveClose)); } };
}

async function main(args) {
  const [scenarioPath, ...options] = args;
  if (!scenarioPath || scenarioPath.startsWith('--')) throw new Error('Usage: node server.js <scenario.json> [--port 7331] [--stage2 <scenario.json>]');
  const config = { scenarioPath };
  for (let i = 0; i < options.length; i++) {
    if (options[i] === '--port' && options[i + 1]) config.port = Number(options[++i]);
    else if (options[i] === '--stage2' && options[i + 1]) config.stage2Path = options[++i];
    else throw new Error(`Unknown or incomplete option: ${options[i]}`);
  }
  const host = await startHost(config);
  console.log(`LOB host listening on ${host.port}`);
  for (const signal of ['SIGINT', 'SIGTERM']) process.once(signal, () => host.close().then(() => process.exit(0)));
}
if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  main(process.argv.slice(2)).catch(error => { console.error(error.message); process.exitCode = 1; });
}
