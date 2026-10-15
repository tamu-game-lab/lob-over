// WO-02 crew order, precision, haul-road speed, proximity and mood conventions.
export const CREW_IDS = ['exc', 'pipe', 'back'];
export const LEADER = { pipe: 'exc', back: 'pipe' };
export const DT = 0.01;
export const EPS = 1e-6;
export const NEAR_FT = 100;
export const HAUL_FT_PER_HOUR = 1500;
export const HOURS_PER_DAY = 24;
export const MAX_MOOD = 3;
export const MOODS = ['calm', 'grumbling', 'angry', 'furious'];
// WO-02: conflicts look one day ahead; P1 durations round to half days.
export const CONFLICT_LOOKAHEAD_DAYS = 1;
export const CLEARING_DAY_STEP = 0.5;
export const travelDays = (a, b) => Math.abs(a - b) / HAUL_FT_PER_HOUR / HOURS_PER_DAY;
export const displayDay = t => Math.floor(t + EPS) + 1;
export const nextBoundary = t => Math.floor(t + EPS) + 1;
export const copy = value => JSON.parse(JSON.stringify(value));
export const zoneAt = (s, ft) => s.scenario.job.zones.find(z => ft >= z.fromFt
  && (ft < z.toFt || ft === s.scenario.job.lengthFt));
export const nearby = (s, ft) => s.truck.dismounted && !s.truck.moving
  && Math.abs(s.truck.stationFt - ft) <= NEAR_FT;
export const crewAt = s => CREW_IDS.map(id => s.crews[id])
  .filter(c => Math.abs(c.pos - s.truck.stationFt) <= NEAR_FT)
  .sort((a, b) => Math.abs(a.pos - s.truck.stationFt) - Math.abs(b.pos - s.truck.stationFt))[0];
export const protectionAt = (s, ft) => Number.isFinite(ft)
  ? s.protections.find(p => ft >= p.fromFt && ft <= p.toFt) : undefined;
export const activeCollapse = s => s.events.find(e => e.type === 'P1' && e.fired && !e.resolved);
export const emit = (s, type, fields) => s.messages.push({ t: type, v: 1, day: s.t, ...fields });

export function voice(s, trigger, seats, crewIds) {
  const lines = s.scenario.lines.filter(line => line.trigger === trigger
    && (!crewIds || crewIds.includes(line.speaker.replace('foreman_', ''))));
  if (!lines.length) return;
  emit(s, 'voice', { lineIds: lines.map(line => line.id), seats });
  s.pmLines.push(...lines.filter(line => line.speaker === 'pm').map(line => line.text));
}

// Reducers own this draft; source state and caller-owned scenario/catalog stay untouched.
// Histories are append-only, so copying their outer arrays avoids repeatedly cloning points.
export function draftState(s) {
  const { scenario, catalog, actualLines, plannedLines, inbox, changeLog, messages, ...dynamic } = s;
  return {
    ...structuredClone(dynamic), scenario, catalog,
    actualLines: Object.fromEntries(CREW_IDS.map(id => [id, [...actualLines[id]]])),
    plannedLines: copy(plannedLines), inbox: [...inbox], changeLog: [...changeLog], messages: [...messages],
  };
}
