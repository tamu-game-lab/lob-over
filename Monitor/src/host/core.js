import { createSim } from '../sim/sim.js';
import { plannedPolylines } from '../sim/planning.js';
import { createPacer, STATE_MS } from './pacing.js';
import { createClients } from './clients.js';
import { createAutoplan } from './autoplan.js';

// WO-02 transcript precision, shared by browser downloads and Node recordings.
const TRANSCRIPT_DECIMALS = 3;
export const roundedJSON = value => JSON.stringify(value, (key, v) =>
  typeof v === 'number' ? Number(v.toFixed(TRANSCRIPT_DECIMALS)) : v, 2) + '\n';

export function createHost({ scenario, catalog, pacing = {}, autoplan = [], stages = [],
  allowFast = false, onTranscript = () => {} }) {
  const stageMap = new Map([scenario, ...stages].map(s => [s.id, s]));
  let director = createAutoplan(autoplan);
  const runs = new Map();
  let sim, transcript, snapshotDay, result, initialMessages, inspected, runId = 0;

  function reset() {
    runId++;
    sim = createSim(scenario, catalog);
    sim.apply('facilitator', { action: 'pause' });
    initialMessages = sim.drain();
    transcript = { scenario: scenario.id, seed: scenario.seed, snapshots: [], messages: [], result: null };
    snapshotDay = -1; result = null; inspected = new Set();
    director.reset();
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
    if (seat === 'facilitator') {
      view.stage2 = stages.at(-1)?.id ?? scenario.id;
      view.autoplan = director.status;
    }
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
        onTranscript(transcript);
      }
    }
  }
  reset();
  function runAutoplan() {
    if (sim.speed === 'paused' || sim.done) return;
    let changed = false;
    director.advance(sim.t, (seat, cmd) => {
      const reply = sim.apply(seat, cmd);
      changed ||= reply.ok;
      transcript.messages.push({ t: sim.t, message: {
        t: reply.ok ? 'ack' : 'nack', v: 1, id: cmd.id, ...reply,
      } });
      drain();
    });
    if (changed) clients.broadcast();
  }
  const pacer = createPacer(() => sim, () => { snapshot(); drain(); runAutoplan(); }, pacing);
  function handleCommand(socket, seat, message, send) {
    let reply;
    if (message.t === 'ctrl' && (message.action === 'reset' || message.action === 'load')) {
      if (message.action === 'load' && !stageMap.has(message.stage)) reply = { ok: false, reason: 'stage not loaded' };
      else {
        if (message.action === 'load') scenario = stageMap.get(message.stage);
        reset(); pacer.setFast(false);
        reply = { ok: true, appliedDay: sim.t };
      }
    } else if (message.t === 'ctrl' && message.action === 'fast') {
      reply = allowFast ? { ok: true, appliedDay: sim.t } : { ok: false, reason: 'unknown control' };
      if (allowFast) pacer.setFast(true);
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
    if (reply.ok) runAutoplan();
    if (reply.ok) { clients.broadcast(); pacer.sync(); }
  }
  const clients = createClients({
    state,
    welcome(socket, seat, send) {
      for (const message of initialMessages) if (message.seats?.includes(seat)) send(socket, withVoiceText(message));
      if (result) send(socket, result);
    },
    command: handleCommand,
  });
  const broadcastTimer = setInterval(clients.broadcast, STATE_MS);
  function control(action, fields = {}) {
    let response;
    // Public controls are local to the host, so do not claim a facilitator seat.
    handleCommand(null, 'facilitator', { t: 'ctrl', v: 1, action, ...fields }, (_, message) => { response = message; });
    return response;
  }
  return {
    attach: clients.attach, state,
    start: () => control('start'), pause: () => control('pause'), resume: () => control('resume'),
    reset: () => control('reset'), load: stage => control('load', { stage }),
    setFast: value => { if (allowFast) pacer.setFast(value); },
    setAutoplan(script, enabled = true) {
      director = createAutoplan(script, { enabled });
      clients.broadcast();
    },
    enableAutoplan(value) { director.setEnabled(value); clients.broadcast(); },
    get transcript() { return transcript; },
    close() { clearInterval(broadcastTimer); pacer.close(); clients.close(); },
  };
}
