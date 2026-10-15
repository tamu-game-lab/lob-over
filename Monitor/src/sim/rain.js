// WO-02 A2 rework rule: one authored duration per commenced 500 ft of open trench.
export const REWORK_CHUNK_FT = 500;

export function reworkDuration(openFt, daysPer500Ft, multiplier = 1) {
  return Math.ceil(openFt / REWORK_CHUNK_FT) * daysPer500Ft * multiplier;
}

export function rainMultiplier(s, crewId) {
  return s.events.filter(e => e.type === 'A2' && e.fired && !e.rainEnded)
    .reduce((mult, e) => mult * (e.restore?.includes(crewId) ? 1 : 1 - e.rateDrop), 1);
}

export function finishRain(s, event) {
  event.rainEnded = true;
  event.reworkDays = reworkDuration(event.openFt, event.rework?.daysPer500Ft
    ?? s.scenario.rules.rainReworkDaysPer500Ft,
  event.rework?.pumpHalves && event.pumpPaidT != null && event.pumpPaidT < event.endsT
    ? event.reworkMultiplier : 1);
  event.reworkEndsT = event.endsT + event.reworkDays;
}

export const rainOrRework = s => s.events.some(e => e.type === 'A2' && e.fired && !e.resolved);
export const pipeReworking = s => s.events.some(e => e.type === 'A2' && e.rainEnded && !e.resolved);
export function sky(s) {
  if (s.events.some(e => e.type === 'A2' && e.fired && !e.rainEnded)) return 'rain';
  return s.events.some(e => !e.fired && e.forecast && s.t >= e.forecast.skyFromDay - 1)
    ? 'towersSouth' : 'clear';
}
