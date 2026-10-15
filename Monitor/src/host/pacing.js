// WO-03: identical simulation ticks at full, office and test speeds.
export const STEP_DAYS = 0.01;
export const FULL_MS = 50;
export const SLOW_MS = 200;
export const FAST_MS = 2;
export const STATE_MS = 250;
// WO-03 rejection 1: bound catch-up after a stalled callback.
const MAX_CATCH_UP_STEPS = 20;

export function createPacer(getSim, afterTick, {
  now = () => performance.now(), setTimer = setTimeout, clearTimer = clearTimeout,
} = {}) {
  let timer, fast = false, closed = false;
  let currentSim, stepMs, accountedAt;
  function interval(sim) {
    if (closed || sim.done || sim.speed === 'paused') return null;
    return fast ? FAST_MS : sim.speed === 'slow' ? SLOW_MS : FULL_MS;
  }
  function schedule() {
    clearTimer(timer);
    const sim = getSim();
    const nextMs = interval(sim);
    // Commands at the same speed must not discard partial steps; pauses and new runs must.
    if (sim !== currentSim || nextMs !== stepMs) {
      currentSim = sim; stepMs = nextMs; accountedAt = now();
    }
    if (stepMs === null) return;
    timer = setTimer(() => {
      const elapsed = now() - accountedAt;
      const due = Math.floor(elapsed / stepMs);
      // Retain the fractional step, but drop excess backlog after a long stall.
      accountedAt += due * stepMs;
      for (let i = 0; i < Math.min(due, MAX_CATCH_UP_STEPS); i++) {
        sim.tick(STEP_DAYS);
        afterTick();
        // A tick can fire an event or finish the stage before the batch ends.
        if (getSim() !== sim || interval(sim) !== stepMs) break;
      }
      schedule();
    }, Math.max(0, stepMs - (now() - accountedAt)));
  }
  return {
    sync: schedule,
    setFast(value) { fast = value; schedule(); },
    close() { closed = true; clearTimer(timer); },
  };
}
