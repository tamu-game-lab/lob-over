import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createSim, tickState } from '../src/sim/sim.js';
import { loadCatalog } from '../src/common/catalog.js';

const catalog = loadCatalog();
const stage = (number = 1) => JSON.parse(readFileSync(
  new URL(`../../Data/Scenarios/stage${number}_texas.json`, import.meta.url), 'utf8'));
const commit = (sim, ...ops) => sim.apply('plan', { action: 'commit', ops });
const site = (sim, action, fields = {}) => sim.apply('site', { action, ...fields });
const advance = (sim, t) => { while (sim.t < t - 1e-6 && !sim.done) sim.tick(.01); };
const close = (actual, expected) => assert.ok(Math.abs(actual - expected) < 1e-6, `${actual} vs ${expected}`);
function quiet() {
  const scenario = stage(2);
  scenario.events = []; scenario.deviations = [];
  scenario.permits.heldBy = 'pm'; scenario.inspections.required = false;
  scenario.plan.budgetUsd = 1e7; scenario.plan.deadlineDay = 200;
  scenario.crews[1].startDay = scenario.crews[2].startDay = 100;
  return scenario;
}
function ride(sim, stationFt) {
  if (sim.view('site').truck.dismounted) assert.equal(site(sim, 'mount').ok, true);
  assert.equal(site(sim, 'moveTo', { stationFt }).ok, true);
  while (sim.view('site').truck.moving) sim.tick(.01);
  assert.equal(site(sim, 'dismount').ok, true);
}

test('O10: Stage 2 idles and bills at 0 ft, then spends F2 only at the first boundary between zones', () => {
  const scenario = stage(2), sim = createSim(scenario, catalog);
  advance(sim, 1);
  const first = sim.view('facilitator');
  assert.equal(first.crews.exc.pos, 0);
  assert.equal(first.crews.exc.idleReason, 'idleNoPermit');
  assert.equal(first.finesUsd, 0);
  close(first.crewCostUsd, catalog.machines.excavator_standard.dailyUsd * (1 + scenario.rules.overheadRate));
  assert.equal(first.deviations.find(d => d.type === 'F2').state, 'armed');
  assert.equal(commit(sim, ...scenario.permits.cards.slice(0, 2)
    .map(card => ({ op: 'enterPermit', zone: card.zone, code: card.code }))).ok, true);
  advance(sim, 16);
  const boundary = sim.view('facilitator');
  close(boundary.crews.exc.pos, 3100);
  assert.equal(boundary.crews.exc.idleReason, 'idleNoPermit');
  assert.equal(boundary.finesUsd, scenario.rules.permitFineUsd);
  assert.equal(boundary.deviations.find(d => d.type === 'F2').zone, 3);
  assert.equal(boundary.deviations.find(d => d.type === 'F2').state, 'missed');
  advance(sim, 17);
  assert.equal(sim.serialize().finesUsd, scenario.rules.permitFineUsd);
});

for (const atBackfill of [false, true]) {
  test(`O11: stop-work lift lands ${atBackfill ? 'at 5.2 with Site at backfill' : 'at 6.0 with Site at 7+20'}`, () => {
    const sim = createSim(stage(), catalog);
    ride(sim, 720); advance(sim, 5.1);
    assert.equal(site(sim, 'stopWork', { zone: 1 }).ok, true);
    advance(sim, 5.15);
    const stoppedAt = sim.serialize().crews.back.pos;
    assert.ok(stoppedAt > 250 && stoppedAt < 300);
    if (atBackfill) ride(sim, stoppedAt);
    advance(sim, 5.2);
    assert.equal(commit(sim).ok, true);
    const state = sim.serialize();
    const resume = state.orders.find(o => o.op.op === 'resume' && o.crew === 'back');
    assert.equal(resume.landsT, atBackfill ? 5.2 : 6);
    assert.equal(state.orders.filter(o => o.op.op === 'resume').length, 3);
    if (atBackfill) {
      assert.equal(state.crews.back.stop, null);
      assert.equal(site(sim, 'confirm', { orderId: resume.id, outcome: 'done' }).ok, true);
    } else {
      const pipeResume = state.orders.find(o => o.op.op === 'resume' && o.crew === 'pipe');
      assert.equal(pipeResume.appliedT, 5.2);
      assert.equal(state.crews.pipe.stop, null);
      assert.equal(state.crews.pipe.mult, 0); // The collapse still blocks pipe.
      assert.equal(site(sim, 'confirm', { orderId: pipeResume.id, outcome: 'done' }).ok, true);
      advance(sim, 5.99);
      close(sim.serialize().crews.back.pos, stoppedAt);
      assert.ok(sim.serialize().crews.back.stop);
      advance(sim, 6);
      assert.equal(sim.serialize().crews.back.stop, null);
      assert.equal(sim.serialize().orders.find(o => o.id === resume.id).appliedT, 6);
      assert.equal(sim.serialize().crews.back.status, 'working');
    }
  });
}

test('O11: a hold replaces stop-work at landing without an implicit resume for that crew', () => {
  const sim = createSim(quiet(), catalog);
  ride(sim, 0); site(sim, 'stopWork', { zone: 1 }); advance(sim, .1);
  site(sim, 'mount');
  assert.equal(commit(sim, { op: 'hold', crew: 'exc' }).ok, true);
  assert.equal(sim.serialize().orders.some(o => o.op.op === 'resume' && o.crew === 'exc'), false);
  assert.ok(sim.serialize().crews.exc.stop);
  advance(sim, 1);
  assert.equal(sim.serialize().crews.exc.stop, null);
  assert.equal(sim.serialize().crews.exc.status, 'held');
});

test('O13: empty commit acknowledges no change, restores full speed and records the note', () => {
  const sim = createSim(stage(), catalog);
  advance(sim, 5.1);
  assert.equal(sim.speed, 'slow');
  assert.deepEqual(commit(sim), { ok: true, appliedDay: 5.1 });
  assert.equal(sim.speed, 'full');
  assert.deepEqual(sim.serialize().changeLog.at(-1), { day: 5.1, ops: [], note: 'no change' });
  assert.deepEqual(sim.serialize().orders, []);
  sim.apply('facilitator', { action: 'pause' });
  assert.equal(commit(sim).ok, true);
  assert.equal(sim.speed, 'paused');
  sim.apply('facilitator', { action: 'resume' });
  assert.equal(sim.speed, 'full');
});

test('(a) an argument preserves held mood; only a full working day resets it', () => {
  const scenario = quiet();
  scenario.crews[1].startDay = scenario.crews[2].startDay = 1;
  let state = createSim(scenario, catalog).serialize();
  state.t = 5;
  state.crews.exc.pos = 2000; state.crews.pipe.pos = 500; state.crews.back.pos = 450;
  Object.assign(state.crews.pipe, { hold: { fromT: 0, untilT: null }, mood: 2, waitDays: 2 });
  state = tickState(state, .01);
  assert.equal(state.crews.pipe.status, 'arguing');
  assert.equal(state.crews.pipe.mood, 2);
  for (let i = 0; i < 100; i++) state = tickState(state, .01);
  assert.equal(state.crews.pipe.mood, 2);
  state.conflicts[0].resolved = true;
  state.crews.pipe.hold = null;
  state.crews.pipe.arguing = state.crews.back.arguing = false;
  state.crews.back.hold = { fromT: 0, untilT: null };
  for (let i = 0; i < 99; i++) state = tickState(state, .01);
  assert.equal(state.crews.pipe.mood, 2);
  state = tickState(state, .01);
  assert.equal(state.crews.pipe.mood, 0);
});

test('(b) a delayed remob charges its fee at commit, once, before landing and the return delay', () => {
  const scenario = quiet(), sim = createSim(scenario, catalog);
  commit(sim, { op: 'demob', crew: 'exc' }); advance(sim, 2.2);
  const before = sim.serialize().spentUsd;
  assert.equal(commit(sim, { op: 'remob', crew: 'exc' }).ok, true);
  close(sim.serialize().spentUsd - before, scenario.rules.demobFeeUsd);
  assert.equal(sim.serialize().orders.at(-1).landsT, 3);
  advance(sim, 3);
  close(sim.serialize().spentUsd - before, scenario.rules.demobFeeUsd);
  assert.equal(sim.serialize().crews.exc.remobT, 4);
  assert.equal(commit(sim, { op: 'radio', orderId: 'o-2' }).ok, true);
  advance(sim, 4);
  close(sim.serialize().spentUsd - before, scenario.rules.demobFeeUsd);
  assert.equal(sim.serialize().crews.exc.demobilized, false);
});

test('(c) a radioed order disappears from Site while in transit and reappears on landing', () => {
  const scenario = quiet(); scenario.deviations = stage().deviations;
  const sim = createSim(scenario, catalog);
  commit(sim, { op: 'hold', crew: 'exc' }); advance(sim, 1.1);
  assert.equal(commit(sim, { op: 'radio', orderId: 'o-1' }).ok, true);
  ride(sim, 440); advance(sim, 1.7);
  assert.equal(sim.serialize().orders[0].landedT, 1);
  assert.deepEqual(sim.view('site').orders, []);
  assert.equal(site(sim, 'confirm', { orderId: 'o-1', outcome: 'done' }).reason, 'order not landed here');
  advance(sim, 2);
  assert.deepEqual(sim.view('site').orders.map(o => o.id), ['o-1']);
  assert.equal(site(sim, 'confirm', { orderId: 'o-1', outcome: 'done' }).ok, true);
});

test('(d) pending pay clear_own and lend reserve the machine across and within commits', () => {
  const pay = { op: 'pay', item: 'clear_own' };
  const lend = { op: 'lend', machine: 'excavator_standard', toCrew: 'pipe', days: 2 };
  for (const first of [pay, lend]) for (const second of [pay, lend]) {
    const scenario = quiet();
    scenario.events = [stage().events[0]]; scenario.events[0].trigger = { kind: 'day', day: 1 };
    scenario.deviations = stage().deviations;
    const sim = createSim(scenario, catalog);
    const initial = sim.serialize();
    assert.equal(commit(sim, first, second).reason, 'machine already committed');
    assert.deepEqual(sim.serialize(), initial);
    assert.equal(commit(sim, first).ok, true);
    const before = sim.serialize();
    assert.equal(commit(sim, second).reason, 'machine already committed');
    assert.deepEqual(sim.serialize(), before);
    advance(sim, 1.1); // F1's ignored order still reserves its machine.
    assert.equal(commit(sim, second).reason, 'machine already committed');
  }
});

test('(e) a crew with no machine works at the lent rate, and stops when the loan ends', () => {
  const scenario = quiet();
  scenario.crews[1].machine = null; scenario.crews[1].startDay = 1;
  const sim = createSim(scenario, catalog);
  advance(sim, 2);
  assert.equal(sim.serialize().crews.pipe.pos, 0);
  ride(sim, sim.serialize().crews.exc.pos);
  assert.equal(commit(sim, { op: 'lend', machine: 'excavator_standard', toCrew: 'pipe', days: .5 }).ok, true);
  advance(sim, 2.1);
  const loan = sim.serialize().loans[0];
  const working = sim.serialize().crews.pipe;
  assert.equal(working.rate, catalog.machines.excavator_standard.rateFtPerDay);
  assert.ok(working.pos > 0);
  sim.view('facilitator'); // Missing owned equipment must not break a seat view.
  advance(sim, loan.returnsT + .1);
  const ended = sim.serialize().crews.pipe;
  close(ended.pos, .5 * catalog.machines.excavator_standard.rateFtPerDay);
  assert.equal(ended.rate, 0);
  assert.equal(ended.mult, 0);
  assert.ok(Number.isFinite(ended.work));
  assert.ok(Number.isFinite(sim.serialize().spentUsd));
});

test('(f) an unstarted crew ignores stop-work until its start day, then idles and bills if it still stands', () => {
  for (const liftBeforeStart of [false, true]) {
    const scenario = quiet(); scenario.crews[2].startDay = 3;
    const sim = createSim(scenario, catalog);
    ride(sim, 0); site(sim, 'stopWork', { zone: 1 }); advance(sim, 1.5);
    const before = sim.serialize().crews.back;
    assert.equal(before.status, 'idle'); assert.equal(before.stop, null);
    assert.equal(before.mood, 0); assert.equal(before.onsite, 0);
    if (liftBeforeStart) assert.equal(commit(sim).ok, true);
    advance(sim, 2);
    const started = sim.serialize().crews.back;
    assert.equal(started.status, liftBeforeStart ? 'working' : 'stopped');
    assert.equal(started.mood, 0);
    if (liftBeforeStart) assert.equal(started.stop, null);
    else {
      advance(sim, 3);
      const stopped = sim.serialize().crews.back;
      assert.equal(stopped.pos, 0); close(stopped.onsite, 1);
      assert.equal(stopped.mood, 1);
    }
  }
});

test('scenario switches reject every unsupported authored alternative, including orderLagShifts', () => {
  const changes = { orderLagShifts: 2, heldCrewBills: false, conflictRule: 'other',
    conflictBehaviour: 'other', unpermittedBoundary: 'other', pumpHalvesRework: false,
    likelihoodRollsApply: true };
  for (const [key, value] of Object.entries(changes)) {
    const scenario = stage(); scenario.rules[key] = value;
    assert.throws(() => createSim(scenario, catalog), new RegExp(`Unsupported rules\\.${key}: expected`));
  }
  const scenario = stage(); scenario.time.shiftsPerDay = 2;
  assert.throws(() => createSim(scenario, catalog), /Unsupported time\.shiftsPerDay: expected 1, got 2/);
});
