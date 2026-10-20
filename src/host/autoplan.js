import { EPS } from '../sim/common.js';

/** Headless script order is stable at equal times; Site remains under human control. */
export function createAutoplan(script = [], { enabled = script.length > 0 } = {}) {
  if (!Array.isArray(script)) throw new Error('Autoplan script must be an array');
  const commands = structuredClone(script).map((entry, index) => {
    if (!Number.isFinite(entry.t) || entry.t < 0 || !['plan', 'site', 'facilitator'].includes(entry.seat)
      || !entry.cmd || typeof entry.cmd.action !== 'string') throw new Error(`Invalid autoplan entry ${index + 1}`);
    return { ...entry, index };
  }).filter(entry => entry.seat !== 'site').sort((a, b) => a.t - b.t || a.index - b.index);
  let index = 0;
  return {
    get status() { return { enabled, nextT: commands[index]?.t ?? null }; },
    setEnabled(value) { enabled = Boolean(value); },
    reset() { index = 0; },
    advance(time, apply) {
      while (enabled && index < commands.length && commands[index].t <= time + EPS) {
        const { seat, cmd } = commands[index++];
        apply(seat, structuredClone(cmd));
      }
    },
  };
}
