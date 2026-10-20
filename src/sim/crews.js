import { CREW_IDS, LEADER, EPS, MAX_MOOD, voice } from './common.js';
import { excavationLimit, backfillLimit, finalInspectionEntered } from './permits.js';
import { rainMultiplier, pipeReworking } from './rain.js';

const MOOD_RECOVERY_DAYS = 1; // WO-05a (a): mood falls only after a full day of work.

export function refreshCrews(s) {
  for (const id of CREW_IDS) {
    const c = s.crews[id];
    // WO-05a (f): the stop reaches an unstarted crew only on its scheduled day.
    if (s.pendingStops[id] && s.t >= c.startDay - 1 - EPS) {
      c.stop = s.pendingStops[id];
      delete s.pendingStops[id];
    }
    if (c.hold?.untilT != null && s.t >= c.hold.untilT - EPS) c.hold = null;
    if (c.remobT != null && s.t >= c.remobT - EPS) {
      c.demobilized = false;
      c.demobBillingEndsT = null;
      c.remobT = null;
    }
    c.rate = s.catalog.machines[c.machine]?.rateFtPerDay ?? 0;
    for (const loan of s.loans.filter(l => l.toCrew === id && l.phase === 'working' && !l.eventId)) {
      c.rate += s.catalog.machines[loan.machine].rateFtPerDay;
    }
    const held = c.hold && s.t >= c.hold.fromT - EPS;
    const stopped = c.stop && s.t >= c.stop.fromT - EPS;
    c.status = c.demobilized ? 'demobilized' : c.arguing ? 'arguing'
      : stopped ? 'stopped' : held ? 'held' : 'working';
    c.mult = 1;
    if (s.t < c.startDay - 1 - EPS || c.finishT != null) { c.mult = 0; c.status = 'idle'; }
    if (c.demobilized || c.arguing || stopped || held || c.lentAway || c.rate === 0) c.mult = 0;
    if (!s.pure) {
      c.mult *= rainMultiplier(s, id);
      for (const event of s.events.filter(e => e.fired && !e.resolved && e.type !== 'A2'
        && (e.hits === id || e.hits === 'all'))) c.mult *= 1 - event.rateDrop;
      if (id === 'pipe' && pipeReworking(s)) c.mult = 0;
    }
    if (!c.mult && c.status === 'working') c.status = 'idle';
    if (c.status === 'working' && c.idleReason && c.moved < EPS) c.status = 'idle';
  }
}

export function moveCrews(s, dt) {
  for (const id of CREW_IDS) {
    const c = s.crews[id];
    if (s.t < c.startDay - 1 - EPS || c.finishT != null) continue;
    const leader = LEADER[id] && s.crews[LEADER[id]];
    let limit = Math.min(s.scenario.job.lengthFt, leader?.pos ?? s.scenario.job.lengthFt);
    let desired = c.pos + c.rate * c.mult * dt;
    c.idleReason = null;
    if (s.pure && id === 'exc' && s.capFt != null) limit = Math.min(limit, s.crews.back.pos + s.capFt);
    if (!s.pure && id === 'exc') {
      const permitLimit = excavationLimit(s, c.mult > 0 ? desired : c.pos);
      limit = Math.min(limit, permitLimit);
      if (desired > permitLimit || c.pos >= permitLimit - EPS && permitLimit < s.scenario.job.lengthFt) c.idleReason = 'idleNoPermit';
    }
    if (!s.pure && id === 'back') {
      const holdPoint = backfillLimit(s);
      limit = Math.min(limit, holdPoint);
      if (desired > holdPoint || c.pos >= holdPoint - EPS && !finalInspectionEntered(s)) c.idleReason = 'idleHoldPoint';
    }
    desired = Math.max(c.pos, Math.min(desired, limit));
    const moved = desired - c.pos;
    if (moved > 0) c.work += moved / c.rate;
    c.pos = desired;
    c.moved = moved;
    if (leader && moved < c.rate * c.mult * dt - EPS && !c.idleReason) c.idleReason = 'leader';
    if (c.status === 'working' && moved < EPS) c.status = 'idle';
    if (c.pos >= s.scenario.job.lengthFt - EPS
      && (id !== 'back' || s.pure || finalInspectionEntered(s))) c.finishT = s.t + dt;
  }
  s.peakOpenTrenchFt = Math.max(s.peakOpenTrenchFt, s.crews.exc.pos - s.crews.back.pos);
}

export function updateMood(s, dt) {
  for (const id of CREW_IDS) {
    const c = s.crews[id];
    if (s.t < c.startDay - 1 - EPS) continue;
    const held = c.status === 'held' || c.status === 'stopped';
    const waiting = held || c.idleReason === 'leader';
    c.waitDays = waiting ? c.waitDays + dt : 0;
    const level = Math.min(MAX_MOOD, Math.floor((c.waitDays + EPS) / s.scenario.rules.moodStepDays));
    if (held && level > c.mood) {
      for (let mood = c.mood + 1; mood <= level; mood++) voice(s, `heldDay${mood}`, ['site'], [id]);
    }
    c.workingDays = c.status === 'working' && c.moved > EPS ? c.workingDays + dt : 0;
    c.mood = c.workingDays >= MOOD_RECOVERY_DAYS - EPS ? 0 : Math.max(c.mood, level);
  }
}
