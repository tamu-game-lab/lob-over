import { EPS } from '../sim/common.js';

export const PLAYER_SEATS = ['site', 'plan', 'rules'];
// WO-S7: backfill within ten feet of the zone end triggers the hold-point beat.
const HOLD_POINT_FT = 10;

/** Host-only sequence and wall-clock readiness; never changes simulation rules. */
export function createGuide(definition, { pause, resume, changed = () => {},
  connected = () => [], setTimer = setTimeout, clearTimer = clearTimeout } = {}) {
  const source = structuredClone(definition);
  if (source.schemaVersion !== 1 || !Array.isArray(source.beats)
    || !Number.isFinite(source.resumeDelaySeconds) || source.resumeDelaySeconds < 0) {
    throw new Error('Invalid guide');
  }
  const ids = new Set();
  for (const beat of source.beats) {
    const trigger = beat.trigger;
    if (!beat.id || ids.has(beat.id) || typeof beat.title !== 'string'
      || !PLAYER_SEATS.every(seat => typeof beat.prompts?.[seat] === 'string')
      || typeof beat.narration !== 'string' || typeof beat.rulesSection !== 'string'
      || !['start', 'event', 'conflict', 't', 'holdPoint', 'result'].includes(trigger?.kind)
      || trigger.kind === 't' && !Number.isFinite(trigger.atLeast)
      || trigger.kind === 'event' && typeof trigger.id !== 'string'
      || trigger.kind === 'conflict' && typeof trigger.pair !== 'string'
      || trigger.kind === 'holdPoint' && !Number.isInteger(trigger.zone)) throw new Error('Invalid guide beat');
    ids.add(beat.id);
  }
  let statuses, fired, ready, active, cursor, started, timer;
  const cancel = () => { clearTimer(timer); timer = undefined; };
  function reset() {
    cancel(); statuses = source.beats.map(() => 'upcoming'); fired = new Set();
    ready = Object.fromEntries(PLAYER_SEATS.map(seat => [seat, false]));
    active = null; cursor = -1; started = false;
  }
  reset();
  function openNext() {
    if (active !== null) return false;
    const next = source.beats.findIndex((_, index) => index > cursor && fired.has(index));
    if (next < 0) return false;
    for (let i = cursor + 1; i < next; i++) statuses[i] = 'skipped';
    active = next; cursor = next; statuses[next] = 'current';
    ready = Object.fromEntries(PLAYER_SEATS.map(seat => [seat, false]));
    cancel(); pause(); changed();
    return true;
  }
  function matches(trigger, state) {
    switch (trigger.kind) {
      case 'start': return started;
      case 'event': return state.events.some(event => event.id === trigger.id && event.fired);
      case 'conflict': return state.conflicts.some(conflict => `${conflict.trailing}-${conflict.leader}` === trigger.pair);
      case 't': return state.t + EPS >= trigger.atLeast;
      case 'holdPoint': {
        const zone = state.scenario.job.zones.find(zone => zone.id === trigger.zone);
        return zone && state.crews.back.pos >= zone.toFt - HOLD_POINT_FT - EPS;
      }
      case 'result': return state.done;
    }
  }
  function observe(state, { start = false, defer = false } = {}) {
    started ||= start;
    if (!started) return;
    source.beats.forEach((beat, index) => {
      if (index > cursor && matches(beat.trigger, state)) fired.add(index);
    });
    if (!defer) openNext();
  }
  function proceed() {
    if (active === null) return false;
    cancel(); statuses[active] = 'done'; active = null;
    if (!openNext()) { resume(); changed(); }
    return true;
  }
  function connectionsChanged() {
    const seats = connected().filter(seat => PLAYER_SEATS.includes(seat));
    const allReady = active !== null && seats.length > 0 && seats.every(seat => ready[seat]);
    if (!allReady || !source.autoResumeWhenAllReady) cancel();
    else if (timer === undefined) timer = setTimer(() => { timer = undefined; proceed(); }, source.resumeDelaySeconds * 1000);
  }
  return {
    observe, reset, proceed, connectionsChanged,
    get paused() { return active !== null; },
    markReady(seat) {
      if (active === null || !PLAYER_SEATS.includes(seat)) return false;
      ready[seat] = true; changed(); connectionsChanged(); return true;
    },
    message(seat) {
      const beat = source.beats[active];
      return { t: 'guide', v: 1,
        beat: beat ? { id: beat.id, index: active, count: source.beats.length, title: beat.title } : null,
        prompt: beat?.prompts[seat] ?? '', rulesSection: beat?.rulesSection ?? null, ready: { ...ready },
        ...(seat === 'facilitator' ? { narration: beat?.narration ?? '', prompts: beat?.prompts ?? {},
          beats: source.beats.map((item, index) => ({ id: item.id, title: item.title, status: statuses[index] })) } : {}),
      };
    },
    close: cancel,
  };
}
