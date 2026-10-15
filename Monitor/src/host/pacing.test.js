import test from 'node:test';
import assert from 'node:assert/strict';
import { createPacer, STEP_DAYS } from './pacing.js';

function fakeClock(lateness = 0) {
  let time = 0, id = 0;
  const timers = new Map();
  function next() {
    return [...timers].sort((a, b) => a[1].at - b[1].at)[0];
  }
  function fire([key, timer]) {
    timers.delete(key);
    timer.callback();
  }
  return {
    now: () => time,
    setTimer(callback, delay) {
      timers.set(++id, { at: time + delay, callback });
      return id;
    },
    clearTimer: key => timers.delete(key),
    elapse(ms) { time += ms; },
    advance(ms) {
      const end = time + ms;
      let timer;
      while ((timer = next()) && timer[1].at + lateness <= end) {
        time = Math.max(time, timer[1].at + lateness);
        fire(timer);
      }
      time = Math.max(time, end);
    },
    flush() {
      const timer = next();
      assert.ok(timer && timer[1].at <= time, 'a timer is due');
      fire(timer);
    },
    get pending() { return timers.size; },
  };
}

function simulation(speed = 'full') {
  return { speed, done: false, steps: [], tick(dt) { this.steps.push(dt); } };
}

for (const [speed, expected, lateness] of [['full', 20, 12], ['slow', 5, 60]]) {
  test(`pacing: 1,000 ms at ${speed} accounts for delayed timers and callback work`, () => {
    const clock = fakeClock(lateness), sim = simulation(speed);
    let callbacks = 0;
    const pacer = createPacer(() => sim, () => { callbacks++; clock.elapse(3); }, clock);
    pacer.sync();
    clock.advance(1000);
    assert.ok(Math.abs(sim.steps.length - expected) <= 1, `${sim.steps.length} steps`);
    assert.equal(callbacks, sim.steps.length);
    assert.ok(sim.steps.every(dt => dt === STEP_DAYS));
    pacer.close();
  });
}

test('pacing: repeated sync preserves elapsed time and fractional steps', () => {
  const clock = fakeClock(), sim = simulation();
  const pacer = createPacer(() => sim, () => {}, clock);
  pacer.sync();
  for (let i = 0; i < 100; i++) {
    clock.advance(10);
    pacer.sync();
  }
  assert.equal(sim.steps.length, 20);
  assert.equal(clock.pending, 1);
  pacer.close();
});

test('pacing: pause, resume, reset, completion and close discard inactive time', () => {
  const clock = fakeClock();
  let sim = simulation('paused');
  const pacer = createPacer(() => sim, () => {}, clock);
  pacer.sync();
  clock.advance(1000);
  assert.equal(sim.steps.length, 0);
  sim.speed = 'full'; pacer.sync();
  clock.advance(75);
  assert.equal(sim.steps.length, 1);
  sim.speed = 'paused'; pacer.sync();
  clock.advance(1000);
  assert.equal(sim.steps.length, 1);
  sim.speed = 'full'; pacer.sync();
  clock.advance(49);
  assert.equal(sim.steps.length, 1);
  clock.advance(1);
  assert.equal(sim.steps.length, 2);
  clock.advance(25);
  sim = simulation(); pacer.sync();
  clock.advance(49);
  assert.equal(sim.steps.length, 0);
  clock.advance(1);
  assert.equal(sim.steps.length, 1);
  sim.done = true; pacer.sync();
  clock.advance(1000);
  assert.equal(sim.steps.length, 1);
  assert.equal(clock.pending, 0);
  sim = simulation(); pacer.sync(); pacer.close();
  clock.advance(1000);
  assert.equal(sim.steps.length, 0);
  assert.equal(clock.pending, 0);
});

test('pacing: an event changes the rate before more catch-up steps run', () => {
  const clock = fakeClock(), sim = simulation();
  const pacer = createPacer(() => sim, () => { sim.speed = 'slow'; }, clock);
  pacer.sync();
  clock.elapse(500); clock.flush();
  assert.equal(sim.steps.length, 1);
  clock.advance(199);
  assert.equal(sim.steps.length, 1);
  clock.advance(1);
  assert.equal(sim.steps.length, 2);
  sim.speed = 'full'; pacer.sync();
  clock.advance(50);
  assert.equal(sim.steps.length, 3);
  pacer.close();
});

test('pacing: a stall caps catch-up at 20 steps and carries only the remainder', () => {
  const clock = fakeClock(), sim = simulation();
  const pacer = createPacer(() => sim, () => {}, clock);
  pacer.sync();
  clock.elapse(5025); clock.flush();
  assert.equal(sim.steps.length, 20);
  clock.advance(24);
  assert.equal(sim.steps.length, 20);
  clock.advance(1);
  assert.equal(sim.steps.length, 21);
  pacer.close();
});

test('pacing: fast uses 2 ms steps and completion stops a catch-up batch', () => {
  const clock = fakeClock(), sim = simulation('slow');
  const pacer = createPacer(() => sim, () => {
    if (sim.steps.length === 10) sim.done = true;
  }, clock);
  pacer.setFast(true);
  clock.advance(10);
  assert.equal(sim.steps.length, 5);
  clock.elapse(30); clock.flush();
  assert.equal(sim.steps.length, 10);
  assert.equal(clock.pending, 0);
  pacer.close();
});
