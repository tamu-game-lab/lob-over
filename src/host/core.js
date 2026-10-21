import { createSim } from '../sim/sim.js';
import { plannedPolylines } from '../sim/planning.js';
import { createPacer, STATE_MS } from './pacing.js';
import { createClients } from './clients.js';
import { createAutoplan } from './autoplan.js';
import { createGuide } from './guide.js';
import { STEP_DAYS } from './pacing.js';
import { EPS } from '../sim/common.js';

// WO-02 transcript precision, shared by browser downloads and Node recordings.
const TRANSCRIPT_DECIMALS = 3;
export const roundedJSON = value => JSON.stringify(value, (key, v) =>
  typeof v === 'number' ? Number(v.toFixed(TRANSCRIPT_DECIMALS)) : v, 2) + '\n';

export function createHost({ scenario, catalog, pacing = {}, autoplan = [], stages = [],
  guide = null, allowFast = false, onTranscript = () => {} }) {
  const stageMap = new Map([scenario, ...stages].map(s => [s.id, s]));
  let director = createAutoplan(autoplan);
  const runs = new Map();
  let sim, transcript, snapshotDay, result, initialMessages, inspected, guideEngine, runId = 0;

  function guideMessage(seat) { return guideEngine?.message(seat) ?? { t: 'guide', v: 1, beat: null }; }
  function publishGuide() { clients.sendEach(guideMessage); clients.broadcast(); }
  function rulesKit() {
    return { t: 'kit', v: 1, stage: scenario.id, title: scenario.title,
      job: { sign: scenario.job.sign, lengthFt: scenario.job.lengthFt, zones: scenario.job.zones },
      permits: { cards: scenario.permits.cards }, inspections: { cards: scenario.inspections.cards, checklist: scenario.inspections.checklist },
      manualPages: scenario.kit.manualPages };
  }
  function resultFor(seat) {
    if (seat === 'rules') return null;
    // The guided debrief is shown by Plan; Site never receives the money or plan.
    if (guideEngine && seat === 'site') return { t: 'result', v: 1, stage: scenario.id };
    return result;
  }

  function reset() {
    guideEngine?.close();
    runId++;
    sim = createSim(scenario, catalog);
    sim.apply('facilitator', { action: 'pause' });
    initialMessages = sim.drain();
    transcript = { scenario: scenario.id, seed: scenario.seed, snapshots: [], messages: [], result: null };
    snapshotDay = -1; result = null; inspected = new Set();
    director.reset();
    guideEngine = guide?.scenario === scenario.id ? createGuide(guide, {
      pause: () => sim.apply('facilitator', { action: 'pause' }),
      resume: () => { sim.apply('facilitator', { action: 'resume' }); pacer.sync(); },
      changed: publishGuide, connected: () => clients.connected(),
      setTimer: pacing.setTimer, clearTimer: pacing.clearTimer,
    }) : null;
    for (const message of initialMessages) transcript.messages.push({ t: message.day ?? sim.t, message });
    snapshot();
  }
  function snapshot() {
    const day = Math.floor(sim.t + 1e-6);
    if (day > snapshotDay) { transcript.snapshots.push(sim.view('facilitator')); snapshotDay = day; }
  }
  function state(seat) {
    if (seat === 'rules') {
      const view = { stage: scenario.id, displayDay: Math.floor(sim.t + EPS) + 1, speed: sim.speed };
      return { t: 'state', v: 1, ...view, day: sim.t, runId, view };
    }
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
      if (message.t === 'result' && guideEngine) clients.sendEach(resultFor);
      else clients.deliver(withVoiceText(message), requester);
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
      observeGuide();
    });
    if (changed) clients.broadcast();
  }
  function observeGuide(options) { if (guideEngine) guideEngine.observe(sim.serialize(), options); }
  const pacer = createPacer(() => sim, () => { snapshot(); drain(); observeGuide(); runAutoplan(); }, pacing);
  function handleCommand(socket, seat, message, send) {
    let reply;
    if (message.t === 'cmd' && message.action === 'ready') {
      reply = guideEngine?.markReady(seat) ? { ok: true, appliedDay: sim.t } : { ok: false, reason: 'no guide beat' };
    } else if (message.t === 'ctrl' && message.action === 'continue') {
      reply = guideEngine?.proceed() ? { ok: true, appliedDay: sim.t } : { ok: false, reason: 'no guide beat' };
    } else if (message.t === 'ctrl' && (message.action === 'reset' || message.action === 'load')) {
      if (message.action === 'load' && !stageMap.has(message.stage)) reply = { ok: false, reason: 'stage not loaded' };
      else {
        if (message.action === 'load') scenario = stageMap.get(message.stage);
        reset(); pacer.setFast(false);
        reply = { ok: true, appliedDay: sim.t };
      }
    } else if (message.t === 'ctrl' && message.action === 'fast') {
      reply = allowFast ? { ok: true, appliedDay: sim.t } : { ok: false, reason: 'unknown control' };
      if (allowFast) pacer.setFast(true);
    } else {
      const travelWhilePaused = guideEngine && sim.speed === 'paused' && seat === 'site' && message.action === 'moveTo';
      reply = sim.apply(seat, message);
      if (reply.ok && travelWhilePaused) {
        // Use the sim's authored travel deadline, including a fractional final tick.
        const arrival = sim.serialize().truck.arrivesT;
        sim.apply('facilitator', { action: 'resume' });
        while (!sim.done && arrival - sim.t > EPS) {
          sim.tick(Math.min(STEP_DAYS, arrival - sim.t)); snapshot(); drain(socket);
          observeGuide({ defer: true });
        }
        sim.apply('facilitator', { action: 'pause' });
      }
      if (reply.ok && message.t === 'ctrl' && ['start', 'resume'].includes(message.action)) {
        observeGuide({ start: true });
        if (guideEngine?.paused) sim.apply('facilitator', { action: 'pause' });
      }
    }
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
      publishGuide();
      clients.sendEach(seat => seat === 'rules' ? rulesKit() : null);
      for (const initial of initialMessages) clients.deliver(withVoiceText(initial));
    }
    drain(socket);
    observeGuide();
    if (reply.ok) runAutoplan();
    if (reply.ok) { clients.broadcast(); pacer.sync(); }
  }
  const clients = createClients({
    state,
    welcome(socket, seat, send) {
      for (const message of initialMessages) if (message.seats?.includes(seat)) send(socket, withVoiceText(message));
      if (seat === 'rules') send(socket, rulesKit());
      if (guideEngine) send(socket, guideMessage(seat));
      if (result && resultFor(seat)) send(socket, resultFor(seat));
    },
    command: handleCommand,
    changed: () => guideEngine?.connectionsChanged(),
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
    continue: () => control('continue'),
    setGuide(definition) { guide = definition; return control('reset'); },
    setFast: value => { if (allowFast) pacer.setFast(value); },
    setAutoplan(script, enabled = true) {
      director = createAutoplan(script, { enabled });
      clients.broadcast();
    },
    enableAutoplan(value) { director.setEnabled(value); clients.broadcast(); },
    get transcript() { return transcript; },
    close() { guideEngine?.close(); clearInterval(broadcastTimer); pacer.close(); clients.close(); },
  };
}
