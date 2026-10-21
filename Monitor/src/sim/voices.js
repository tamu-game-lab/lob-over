import { CREW_IDS, EPS, voice } from './common.js';

// Docs/VOICE.md: dismounted within 30 m; one arrival per crew per display day.
const ARRIVE_FT = 30 / 0.3048;
export function arrivalVoices(s) {
  if (!s.scenario.lines.some(line => line.trigger === 'arrive') || !s.truck.dismounted || s.truck.moving) return;
  for (const id of CREW_IDS) {
    const crew = s.crews[id];
    if (crew.startDelayed || s.t < crew.startDay - 1 - EPS || crew.finishT != null) continue;
    if (Math.abs(s.truck.stationFt - crew.pos) <= ARRIVE_FT + EPS) {
      voice(s, 'arrive', ['site'], undefined, { crew: id, status: crew.status });
    }
  }
}
