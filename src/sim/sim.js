import { makeRng } from '../common/rng.js';
import { CREW_IDS, DT, EPS, copy, draftState, emit, voice, refreshEventSpeed } from './common.js';
import { arrivalVoices } from './voices.js';
import { refreshCrews, moveCrews, updateMood } from './crews.js';
import { detectConflicts, resolveConflicts, validateConflictCommit } from './conflict.js';
import { updateEvents } from './events.js';
import { accrue } from './money.js';
import { issueOrder, updateOrders, validateOp } from './orders.js';
import { finalInspectionEntered, updateHoldPoints } from './permits.js';
import { plannedPolylines } from './planning.js';
import { applySite, updateTruck } from './site.js';
import { view } from './views.js';

// WO-05a: these authored Texas switches have no alternative implementation.
const SUPPORTED_RULES = {
  orderLagShifts: 1, heldCrewBills: true, conflictRule: 'crossWithinNextDay',
  conflictBehaviour: 'stopAndArgue', unpermittedBoundary: 'idleAndBill',
  pumpHalvesRework: true, likelihoodRollsApply: false,
};
const SUPPORTED_SHIFTS_PER_DAY = 1; // Authored scenario time.shiftsPerDay.

function initialState(scenario, catalog, pure) {
  const crews = Object.fromEntries(CREW_IDS.map(id => {
    const crew = scenario.crews.find(c => c.id === id);
    return [id, { ...crew, pos: 0, work: 0, onsite: 0, finishT: null,
      rate: catalog.machines[crew.machine]?.rateFtPerDay ?? 0, mult: 0, status: 'idle',
      mood: 0, waitDays: 0, workingDays: 0, hold: null, stop: null, arguing: false, demobilized: false, lentAway: false }];
  }));
  const state = {
    scenario, catalog, pure, rngState: makeRng(scenario.seed).state(),
    t: 0, speed: 'full', pausedSpeed: 'full', done: false, result: null, crews,
    capFt: scenario.capFt ?? null, orders: [], nextOrderId: 1, loans: [], deliveries: [], payments: [],
    protections: [], events: copy(scenario.events ?? []), deviations: copy(scenario.deviations ?? []), conflicts: [], pendingStops: {},
    permits: Object.fromEntries(scenario.job.zones.map(z => [z.id, { entered: pure || scenario.permits.heldBy === 'pm',
      signed: false, number: scenario.permits.cards.find(c => c.zone === z.id)?.number }])),
    inspections: Object.fromEntries(scenario.job.zones.map(z => [z.id, { cleared: pure || !scenario.inspections.required,
      reported: false, number: scenario.inspections.cards.find(c => c.zone === z.id)?.number }])),
    truck: { stationFt: 0, moving: false, dismounted: false }, flags: [], inbox: [], inboxDelivered: [], pmLines: [],
    messages: [], changeLog: [], spentUsd: 0, crewCostUsd: 0, finesUsd: 0, peakOpenTrenchFt: 0,
    actualLines: Object.fromEntries(CREW_IDS.map(id => [id, [{ t: 0, ft: 0 }]])),
    plannedLines: Object.fromEntries(CREW_IDS.map(id => [id, { startDay: crews[id].startDay, rate: crews[id].rate, holds: [],
      segments: [{ fromT: crews[id].startDay - 1, rate: crews[id].rate }] }])),
    reportedLines: Object.fromEntries(CREW_IDS.map(id => [id, [{ t: 0, ft: 0 }]])),
  };
  if (!pure) { voice(state, 'stageStart', ['plan', 'site']); updateEvents(state); }
  refreshCrews(state);
  return state;
}

function recordActual(s) {
  for (const id of CREW_IDS) {
    // Level 1's result ends each actual line when that crew finishes the job.
    const t = s.scenario.rules.planControlsClock ? s.crews[id].finishT ?? s.t : s.t;
    if (s.actualLines[id].at(-1).t !== t) s.actualLines[id].push({ t, ft: s.crews[id].pos });
  }
}

function report(s) {
  const feetByCrew = Object.fromEntries(CREW_IDS.map(id => [id, Math.floor(s.crews[id].pos + EPS)]));
  const notes = CREW_IDS.map(id => `${s.crews[id].name}: ${feetByCrew[id] - s.reportedLines[id].at(-1).ft} ft today`);
  for (const id of CREW_IDS) s.reportedLines[id].push({ t: s.t, ft: feetByCrew[id] });
  const item = { day: s.t, feetByCrew, spendUsd: s.spentUsd, notes };
  s.inbox.push({ ...item, from: 'Daily report', subject: `Day ${s.t}` });
  emit(s, 'report', { ...item, seats: ['plan', 'facilitator'] });
}

function finish(s) {
  const lost = s.pure ? null : s.spentUsd > s.scenario.plan.budgetUsd ? 'budget'
    : s.t > s.scenario.plan.deadlineDay - 1 + EPS ? 'deadline' : null;
  const complete = s.crews.back.finishT != null && (s.pure || finalInspectionEntered(s));
  if (!lost && !complete) return;
  recordActual(s);
  const idleDays = Object.fromEntries(CREW_IDS.map(id => [id, Math.max(0, s.crews[id].onsite - s.crews[id].work)]));
  const planned = plannedPolylines(s);
  s.done = true;
  s.result = {
    ...(lost ? { lost } : { finishDay: s.crews.back.finishT }), promisedDay: s.scenario.plan.promisedFinishDay,
    budgetUsd: s.scenario.plan.budgetUsd, spentUsd: s.spentUsd, crewCostUsd: s.crewCostUsd,
    idleDays, idleCrewDays: CREW_IDS.reduce((total, id) => total + idleDays[id], 0),
    peakOpenTrenchFt: s.peakOpenTrenchFt, finesUsd: s.finesUsd,
    deviationsCaught: s.deviations.filter(d => d.triggered && d.caught).length,
    deviationsMissed: s.deviations.filter(d => d.triggered && !d.caught).length,
    events: s.events.filter(e => e.fired).map(e => ({ id: e.id, day: e.firedT, option: e.option ?? null,
      daysLost: e.type === 'A2' ? e.durationDays * e.rateDrop + (e.reworkDays ?? 0)
        : ((e.resolvedT ?? s.t) - e.firedT) * e.rateDrop, protection: e.protection })),
    changeLog: copy(s.changeLog), lines: { planned, actual: copy(s.actualLines) },
  };
  if (!s.pure) {
    voice(s, 'stageEnd', ['plan', 'site']);
    emit(s, 'result', { stage: s.scenario.id, ...s.result, seats: ['plan', 'site', 'facilitator'] });
  }
}

export function tickState(state, dt) {
  if (state.done || state.speed === 'paused') return state;
  const s = draftState(state);
  if (!s.pure) { updateTruck(s); updateOrders(s); updateEvents(s); }
  refreshCrews(s);
  if (!s.pure) detectConflicts(s);
  moveCrews(s, dt);
  accrue(s, dt);
  if (!s.pure) updateMood(s, dt);
  // Quantizing arithmetic noise preserves exact shift boundaries across JS engines.
  s.t = Number((s.t + dt).toFixed(10));
  if (!s.pure) {
    updateTruck(s); updateOrders(s); updateHoldPoints(s); updateEvents(s);
    refreshCrews(s);
    arrivalVoices(s);
    refreshEventSpeed(s);
  }
  if (Math.floor(s.t + EPS) > Math.floor(state.t + EPS)) {
    recordActual(s);
    if (!s.pure) report(s);
  }
  finish(s);
  return s;
}

function commit(state, ops) {
  if (!Array.isArray(ops)) return { reason: 'invalid commit' };
  const conflictReason = validateConflictCommit(state, ops);
  if (conflictReason) return { reason: conflictReason };
  const s = draftState(state);
  for (const op of ops) {
    const reason = validateOp(s, op);
    if (reason) return { reason };
    // Staging on the draft validates sequential office actions without any partial commit.
    issueOrder(s, op);
  }
  resolveConflicts(s);
  for (const id of CREW_IDS) {
    if (s.crews[id].stop && !ops.some(op => ['hold', 'resume'].includes(op.op) && op.crew === id)
      && !s.orders.some(order => order.op.op === 'resume' && order.crew === id && order.appliedT == null)) {
      issueOrder(s, { op: 'resume', crew: id });
    }
  }
  s.pendingStops = {};
  updateOrders(s);
  updateHoldPoints(s);
  if (s.crews.back.finishT == null && s.crews.back.pos >= s.scenario.job.lengthFt - EPS
    && finalInspectionEntered(s)) s.crews.back.finishT = s.t;
  refreshCrews(s);
  s.changeLog.push({ day: s.t, ops: copy(ops), ...(ops.length ? {} : { note: 'no change' }) });
  if (s.speed === 'paused') s.pausedSpeed = 'full'; else s.speed = 'full';
  refreshEventSpeed(s);
  finish(s);
  return { state: s };
}

export function createSim(scenario, catalog, { pure = false } = {}) {
  // The pure oracle omits full-model switches; explicitly changed values still fail.
  for (const [key, expected] of Object.entries(SUPPORTED_RULES)) {
    const actual = scenario.rules?.[key];
    if (!(pure && actual === undefined) && actual !== expected) {
      throw new Error(`Unsupported rules.${key}: expected ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}`);
    }
  }
  const shifts = scenario.time?.shiftsPerDay;
  if (!(pure && shifts === undefined) && shifts !== SUPPORTED_SHIFTS_PER_DAY) {
    throw new Error(`Unsupported time.shiftsPerDay: expected ${SUPPORTED_SHIFTS_PER_DAY}, got ${JSON.stringify(shifts)}`);
  }
  // JSON copying also accepts the WO-01 loader's frozen scenario with derived functions.
  const source = copy(scenario), fleet = copy(catalog);
  let state = initialState(source, fleet, pure);
  return {
    get t() { return state.t; }, get speed() { return state.speed; },
    get clock() { return source.time; },
    get done() { return state.done; }, get result() { return copy(state.result); },
    tick(dt = DT) {
      if (!Number.isFinite(dt) || dt <= 0) throw new RangeError('dt must be positive');
      let remaining = dt;
      while (remaining > EPS && !state.done && state.speed !== 'paused') {
        const step = Math.min(DT, remaining);
        state = tickState(state, step);
        remaining -= step;
      }
    },
    apply(seat, cmd) {
      if (!cmd || typeof cmd !== 'object') return { ok: false, reason: 'invalid command' };
      if (seat === 'facilitator' || seat === 'plan' && source.rules.planControlsClock
        && cmd.t === 'ctrl' && ['start', 'reset'].includes(cmd.action)) {
        if (cmd.action === 'load' && cmd.stage !== source.id) return { ok: false, reason: 'stage not loaded' };
        if (cmd.action === 'reset' || cmd.action === 'load') state = initialState(source, fleet, pure);
        else if (cmd.action === 'pause') {
          state = draftState(state);
          if (state.speed !== 'paused') state.pausedSpeed = state.speed;
          state.speed = 'paused';
        } else if (cmd.action === 'start' || cmd.action === 'resume') {
          state = draftState(state); state.speed = state.pausedSpeed;
        } else return { ok: false, reason: 'unknown control' };
        return { ok: true, appliedDay: state.t };
      }
      if (state.done) return { ok: false, reason: 'stage ended' };
      if (seat === 'plan' && cmd.action === 'commit') {
        const result = commit(state, cmd.ops);
        if (result.reason) {
          if (result.reason === 'not in the budget') {
            state = draftState(state); voice(state, 'prepRefused', ['plan']);
          }
          return { ok: false, reason: result.reason };
        }
        state = result.state;
      } else if (seat === 'site') {
        const draft = draftState(state), reason = applySite(draft, cmd);
        if (reason) return { ok: false, reason };
        arrivalVoices(draft);
        state = draft;
      } else return { ok: false, reason: 'action unavailable for seat' };
      return { ok: true, appliedDay: state.t };
    },
    view: seat => view(state, seat),
    drain() { const messages = copy(state.messages); state = { ...state, messages: [] }; return messages; },
    serialize: () => copy(state),
  };
}
