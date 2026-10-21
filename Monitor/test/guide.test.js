import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createGuide } from '../src/host/guide.js';
import { createHost } from '../src/host/core.js';
import { createSim } from '../src/sim/sim.js';
import { loadCatalog } from '../src/common/catalog.js';
import { loadScenario } from '../src/scenario/load.js';
import { travelDays } from '../src/sim/common.js';

const catalog = loadCatalog();
const scenario = loadScenario(new URL('../../Data/Scenarios/slice_texas.json', import.meta.url), { catalog });
const definition = JSON.parse(readFileSync(new URL('../../Data/Guides/slice_guided.json', import.meta.url)));
const script = JSON.parse(readFileSync(new URL('./scripts/slice_play.json', import.meta.url)));
function clock() {
  let now = 0, id = 0;
  const timers = new Map();
  return {
    now: () => now,
    setTimer(callback, delay) { timers.set(++id, { callback, at: now + delay }); return id; },
    clearTimer(key) { timers.delete(key); },
    advance(ms) {
      const end = now + ms;
      for (;;) {
        const next = [...timers].sort((a, b) => a[1].at - b[1].at)[0];
        if (!next || next[1].at > end) break;
        now = next[1].at; timers.delete(next[0]); next[1].callback();
      }
      now = end;
    },
  };
}
function connect(core, seat) {
  const messages = [];
  let receive, close, sequence = 0;
  core.attach({ send: message => messages.push(message), onMessage: fn => { receive = fn; }, onClose: fn => { close = fn; }, close: () => close() });
  receive({ t: 'hello', v: 1, seat });
  return { messages, close: () => close(), command(action, fields = {}) {
    const id = seat + ++sequence;
    receive({ t: seat === 'facilitator' ? 'ctrl' : 'cmd', v: 1, id, seat, action, ...fields });
    return messages.find(message => message.id === id);
  }, get guide() { return messages.filter(message => message.t === 'guide').at(-1); } };
}

test('guide: slice_play triggers all nine beats in order at the authored events, times, hold points and result', t => {
  const sim = createSim(scenario, catalog), fired = [];
  const engine = createGuide(definition, { pause: () => sim.apply('facilitator', { action: 'pause' }),
    resume: () => sim.apply('facilitator', { action: 'resume' }) });
  function observe(start = false) {
    const raw = sim.serialize(); engine.observe(raw, { start });
    if (!engine.paused) return;
    const message = engine.message('facilitator'), trigger = definition.beats[message.beat.index].trigger;
    // Conflict events are stamped at the start of their simulation tick.
    const eventTime = trigger.kind === 'conflict' ? raw.conflicts.find(c => `${c.trailing}-${c.leader}` === trigger.pair).firedT : sim.t;
    fired.push({ id: message.beat.id, time: eventTime, pausedAt: sim.t, back: raw.crews.back.pos });
    assert.equal(sim.speed, 'paused'); engine.proceed();
  }
  observe(true);
  let index = 0;
  while (!sim.done && sim.t < 31) {
    while (index < script.length && script[index].t <= sim.t + 1e-6) {
      const { seat, cmd } = script[index++];
      const reply = sim.apply(seat, cmd); assert.equal(reply.ok, true, `${cmd.id}: ${reply.reason}`);
      sim.drain(); observe();
    }
    if (sim.done) break;
    sim.tick(.01); sim.drain(); observe();
  }
  assert.deepEqual(fired.map(beat => beat.id), definition.beats.map(beat => beat.id));
  assert.deepEqual(fired.slice(0, 6).map(beat => beat.time), [0, 3.89, 4.14, 5, 7, 8]);
  for (const beat of fired) assert.ok(beat.pausedAt - beat.time <= .010001);
  for (const [index, end] of [[6, 1500], [7, 3000]]) assert.ok(fired[index].back >= end - 10 && fired[index].back <= end);
  assert.equal(sim.result.lost, undefined);
  assert.equal(engine.message('facilitator').beats.every(beat => beat.status === 'done'), true);
  engine.close(); t.diagnostic('Trigger times: ' + fired.map(beat => beat.time).join(', '));
});

test('guide: skips unfired earlier beats, retains triggers during a pause, and filters prompts', () => {
  const sim = createSim(scenario, catalog), engine = createGuide(definition, { pause() {}, resume() {} });
  engine.observe(sim.serialize(), { start: true }); engine.proceed();
  const state = sim.serialize(); state.t = 7;
  engine.observe(state);
  let message = engine.message('facilitator');
  assert.equal(message.beat.id, 'b3_orders');
  assert.deepEqual(message.beats.slice(0, 4).map(beat => beat.status), ['done', 'skipped', 'skipped', 'current']);
  for (const seat of ['site', 'plan', 'rules']) {
    message = engine.message(seat);
    assert.equal(message.prompt, definition.beats[3].prompts[seat]);
    assert.equal(message.narration, undefined); assert.equal(message.prompts, undefined); assert.equal(message.beats, undefined);
  }
  engine.proceed(); assert.equal(engine.message('plan').beat.id, 'b4_forecast');
  engine.close();
});

test('guide host: connected readiness, two second delay, mirrors, continue, reset and Rules filtering', () => {
  const time = clock(), core = createHost({ scenario, catalog, guide: definition, pacing: time });
  try {
    const plan = connect(core, 'plan'), site = connect(core, 'site'), rules = connect(core, 'rules');
    const mirror = connect(core, 'rules'), facilitator = connect(core, 'facilitator');
    core.start();
    assert.deepEqual(core.state('rules').view, { stage: scenario.id, displayDay: 1, speed: 'paused' });
    const kit = rules.messages.find(message => message.t === 'kit');
    assert.deepEqual(kit.job.sign, scenario.job.sign); assert.deepEqual(kit.permits.cards, scenario.permits.cards);
    assert.equal(kit.events, undefined); assert.equal(kit.plan, undefined); assert.equal(kit.ground, undefined);
    assert.equal(rules.command('commit', { ops: [] }).t, 'nack');
    assert.equal(mirror.command('ready').reason, 'mirror');
    assert.equal(plan.command('ready').t, 'ack'); assert.equal(site.command('ready').t, 'ack');
    time.advance(2500); assert.equal(core.state('plan').day, 0);
    rules.command('ready'); time.advance(1999); assert.equal(core.state('plan').speed, 'paused');
    time.advance(1); assert.equal(core.state('plan').speed, 'full');
    core.reset(); core.start(); assert.equal(plan.guide.ready.plan, false);
    plan.command('ready'); site.command('ready'); rules.command('ready');
    assert.equal(facilitator.command('continue').t, 'ack'); assert.equal(core.state('plan').speed, 'full');
    core.reset(); core.start();
    plan.command('ready'); site.command('ready'); rules.close();
    time.advance(2000); assert.equal(core.state('plan').speed, 'full', 'read-only mirror does not block readiness');
    core.reset(); time.advance(3000); assert.equal(core.state('plan').day, 0);
    assert.equal(core.state('plan').speed, 'paused');
  } finally { core.close(); }
});

test('guide host: a paused move advances exactly its travel time and remains paused; commands remain available', () => {
  const time = clock(), core = createHost({ scenario, catalog, guide: definition, pacing: time });
  try {
    const site = connect(core, 'site'), plan = connect(core, 'plan'); core.start();
    assert.equal(plan.command('commit', { ops: script[0].cmd.ops }).t, 'ack');
    assert.equal(site.command('moveTo', { stationFt: 520 }).t, 'ack');
    const state = core.state('site');
    assert.ok(Math.abs(state.day - travelDays(0, 520)) < 1e-9);
    assert.deepEqual(state.view.truck, { stationFt: 520, moving: false, dismounted: false });
    assert.equal(state.speed, 'paused'); time.advance(5000); assert.equal(core.state('site').day, state.day);
    assert.equal(site.command('dismount').t, 'ack'); assert.equal(site.command('inspect', { target: 'water' }).t, 'ack');
    assert.equal(site.command('moveTo', { stationFt: 900 }).t, 'nack'); assert.equal(core.state('site').day, state.day);
    core.resume(); assert.equal(core.state('site').speed, 'paused', 'Resume cannot bypass an active beat');
    core.continue(); assert.equal(core.state('site').speed, 'full');
    core.setGuide(null); core.start(); core.pause();
    const before = core.state('site').day;
    site.command('moveTo', { stationFt: 520 }); assert.equal(core.state('site').day, before, 'unguided paused movement unchanged');
  } finally { core.close(); }
});
