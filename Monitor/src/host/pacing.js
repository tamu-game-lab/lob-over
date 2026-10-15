// WO-03: identical simulation ticks at full, office and test speeds.
export const STEP_DAYS = 0.01;
export const FULL_MS = 50;
export const SLOW_MS = 200;
export const FAST_MS = 2;
export const STATE_MS = 250;

export function createPacer(getSim, afterTick) {
  let timer, fast = false, closed = false;
  function schedule() {
    clearTimeout(timer);
    const sim = getSim();
    if (closed || sim.done || sim.speed === 'paused') return;
    timer = setTimeout(() => {
      getSim().tick(STEP_DAYS);
      afterTick();
      schedule();
    }, fast ? FAST_MS : sim.speed === 'slow' ? SLOW_MS : FULL_MS);
  }
  return {
    sync: schedule,
    setFast(value) { fast = value; schedule(); },
    close() { closed = true; clearTimeout(timer); },
  };
}
