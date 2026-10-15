import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createSim, tickState } from '../src/sim/sim.js';
import { loadCatalog } from '../src/common/catalog.js';
import { reworkDuration, rainMultiplier } from '../src/sim/rain.js';
import { clearingDays } from '../src/sim/events.js';
import { refreshCrews } from '../src/sim/crews.js';
import { detectConflicts } from '../src/sim/conflict.js';
import { inspectionText, trenchAt } from '../src/sim/views.js';
import { plannedRate, plannedPolylines } from '../src/sim/planning.js';

const catalog = loadCatalog();
const read = path => JSON.parse(readFileSync(new URL(path, import.meta.url), 'utf8'));
const stage = (number = 1) => read(`../../Data/Scenarios/stage${number}_texas.json`);
function quiet() {
  const scenario = stage(2);
  scenario.events = []; scenario.deviations = [];
  scenario.permits.heldBy = 'pm'; scenario.permits.openZoneLimit = 4;
  scenario.inspections.required = false;
  scenario.plan.budgetUsd = 1e7; scenario.plan.deadlineDay = 200;
  scenario.crews[1].startDay = 100; scenario.crews[2].startDay = 100;
  return scenario;
}
const commit = (sim, ...ops) => sim.apply('plan', { action: 'commit', ops });
const site = (sim, action, fields = {}) => sim.apply('site', { action, ...fields });
const advance = (sim, t) => { while (sim.t < t - 1e-6 && !sim.done) sim.tick(.01); };
const close = (actual, expected, tolerance = 1e-6) => assert.ok(Math.abs(actual - expected) < tolerance, `${actual} vs ${expected}`);
function ride(sim, ft) {
  if (sim.view('site').truck.dismounted) assert.equal(site(sim, 'mount').ok, true);
  assert.equal(site(sim, 'moveTo', { stationFt: ft }).ok, true);
  while (sim.view('site').truck.moving) sim.tick(.01);
  assert.equal(site(sim, 'dismount').ok, true);
}

test('orders land at next shift, or immediately with Site dismounted within 100 ft', () => {
  const absent = createSim(quiet(), catalog);
  advance(absent, .2);
  assert.equal(commit(absent, { op: 'hold', crew: 'exc', days: 2 }).ok, true);
  assert.equal(absent.serialize().orders[0].landsT, 1);
  advance(absent, .99); assert.ok(absent.serialize().crews.exc.pos > 200);
  advance(absent, 1.1); close(absent.serialize().crews.exc.pos, 220);
  const present = createSim(quiet(), catalog);
  site(present, 'dismount');
  commit(present, { op: 'hold', crew: 'exc', days: 2 });
  assert.equal(present.serialize().orders[0].landsT, 0);
  advance(present, .5); assert.equal(present.serialize().crews.exc.pos, 0);
  assert.ok(present.serialize().spentUsd > 0);
});

test('setStart checks plan lock, crew lock, started crews and whole display days', () => {
  const locked = createSim(stage(), catalog);
  assert.equal(commit(locked, { op: 'setStart', crew: 'back', day: 12 }).reason, 'plan locked');
  const scenario = quiet(); scenario.crews[1].locked = true;
  const sim = createSim(scenario, catalog);
  assert.equal(commit(sim, { op: 'setStart', crew: 'pipe', day: 12 }).reason, 'plan locked');
  assert.equal(commit(sim, { op: 'setStart', crew: 'exc', day: 12 }).reason, 'crew already started');
  assert.equal(commit(sim, { op: 'setStart', crew: 'back', day: 1.5 }).ok, false);
  assert.equal(commit(sim, { op: 'setStart', crew: 'back', day: 12 }).ok, true);
  assert.equal(sim.serialize().crews.back.startDay, 12);
  assert.equal(sim.view('plan').plannedLines.back.segments[0].fromT, 11);
});

test('a rejected commit rolls back earlier purchases, permits, messages and order IDs', () => {
  const sim = createSim(quiet(), catalog); sim.drain();
  const before = sim.serialize();
  const reply = commit(sim, { op: 'protect', kind: 'trench_box', fromFt: 600, toFt: 900 },
    { op: 'enterPermit', zone: 1, code: 'absent' });
  assert.equal(reply.reason, 'code not on file');
  assert.deepEqual(sim.serialize(), before);
});

test('finite holds expire, future holds start later, indefinite holds require resume', () => {
  const sim = createSim(quiet(), catalog); site(sim, 'dismount');
  commit(sim, { op: 'hold', crew: 'exc', days: 1 });
  advance(sim, 1); close(sim.serialize().crews.exc.pos, 0);
  advance(sim, 1.1); close(sim.serialize().crews.exc.pos, 22);
  commit(sim, { op: 'hold', crew: 'exc', fromDay: 3 });
  advance(sim, 2); const pos = sim.serialize().crews.exc.pos;
  advance(sim, 4); close(sim.serialize().crews.exc.pos, pos);
  ride(sim, pos); commit(sim, { op: 'resume', crew: 'exc' });
  assert.ok(sim.view('plan').plannedLines.exc.holds.every(hold => hold.untilT != null));
  sim.tick(.01); assert.ok(sim.serialize().crews.exc.pos > pos);
});

test('F1 selects the first matching delayed op, ignores its landing, and can be caught and radioed', () => {
  const scenario = quiet(); scenario.deviations = stage().deviations;
  const sim = createSim(scenario, catalog);
  commit(sim, { op: 'hold', crew: 'exc', days: 2 }, { op: 'hold', crew: 'pipe', days: 2 });
  advance(sim, 1.1);
  const s = sim.serialize();
  assert.equal(s.orders[0].status, 'landed'); assert.equal(s.crews.exc.hold, null);
  assert.ok(s.crews.pipe.hold); assert.ok(s.crews.exc.pos > 220);
  ride(sim, s.crews.exc.pos);
  assert.equal(site(sim, 'confirm', { orderId: 'o-1', outcome: 'notDone' }).ok, true);
  assert.equal(sim.serialize().deviations[0].caught, true);
  commit(sim, { op: 'radio', orderId: 'o-1' });
  const pos = sim.serialize().crews.exc.pos;
  sim.tick(.1); close(sim.serialize().crews.exc.pos, pos);
  assert.equal(site(sim, 'confirm', { orderId: 'o-1', outcome: 'done' }).ok, true);
});

test('F1 ignores present Site orders, and confirmation after the next report is not caught', () => {
  const scenario = quiet(); scenario.deviations = stage().deviations;
  const sim = createSim(scenario, catalog); site(sim, 'dismount');
  commit(sim, { op: 'hold', crew: 'exc', days: .1 });
  assert.equal(sim.serialize().deviations[0].triggered, undefined);
  advance(sim, 1); site(sim, 'mount'); commit(sim, { op: 'hold', crew: 'exc' });
  advance(sim, 3); ride(sim, sim.serialize().crews.exc.pos);
  site(sim, 'confirm', { orderId: 'o-2', outcome: 'notDone' });
  assert.equal(sim.serialize().deviations[0].caught, undefined);
});

test('permits gate excavation and a multi-op commit cannot exceed the open-zone limit', () => {
  const scenario = quiet(); scenario.permits = stage(2).permits;
  const sim = createSim(scenario, catalog);
  sim.tick(.1); assert.equal(sim.serialize().crews.exc.pos, 0);
  assert.equal(sim.serialize().crews.exc.idleReason, 'idleNoPermit');
  const permits = scenario.permits.cards.map(c => ({ op: 'enterPermit', zone: c.zone, code: c.code }));
  assert.equal(commit(sim, ...permits.slice(0, 3)).reason, 'city limit: 2 zones open');
  assert.equal(sim.view('plan').permits.some(p => p.entered), false);
  assert.equal(commit(sim, ...permits.slice(0, 2)).ok, true);
  advance(sim, 15); close(sim.serialize().crews.exc.pos, 3000);
});

test('hold points post a compaction result, release an open-zone slot only when closed, and gate finishing', () => {
  const scenario = quiet(); scenario.inspections.required = true;
  const sim = createSim(scenario, catalog), state = sim.serialize();
  state.t = 10;
  state.crews.exc.pos = state.crews.pipe.pos = 2000;
  state.crews.back.pos = 1499; state.crews.back.startDay = 1;
  let next = tickState(state, .01);
  close(next.crews.back.pos, 1500);
  assert.ok(next.inbox.some(item => item.subject === 'Zone 1 compaction' && item.body === 'passed'));
  assert.equal(next.permits[1].signed, false);
  next = tickState(next, .01); close(next.crews.back.pos, 1500);
  assert.equal(next.crews.back.idleReason, 'idleHoldPoint');
  next.inspections[1].cleared = true;
  next = tickState(next, .01); assert.ok(next.crews.back.pos > 1500); assert.equal(next.permits[1].signed, true);
  next.crews.exc.pos = next.crews.pipe.pos = next.crews.back.pos = 6000;
  next.crews.exc.finishT = next.crews.pipe.finishT = next.t;
  for (const id of [1, 2, 3]) next.inspections[id].cleared = true;
  next = tickState(next, .01); assert.equal(next.done, false);
  next.inspections[4].cleared = true;
  next = tickState(next, .01); assert.equal(next.done, true);
});

test('F2 overshoots only once and charges one fine; Site at the boundary prevents it', () => {
  for (const present of [false, true]) {
    const scenario = quiet(); scenario.permits = stage(2).permits; scenario.deviations = stage(2).deviations;
    const sim = createSim(scenario, catalog), s = sim.serialize();
    s.t = 10; s.crews.exc.pos = 3000; s.permits[1].entered = s.permits[2].entered = true;
    s.truck = { stationFt: 3000, moving: false, dismounted: present };
    let next = s;
    for (let i = 0; i < 100; i++) next = tickState(next, .01);
    close(next.crews.exc.pos, present ? 3000 : 3100);
    assert.equal(next.finesUsd, present ? 0 : scenario.rules.permitFineUsd);
    assert.equal(next.deviations.find(d => d.type === 'F2').caught, present);
  }
});

test('stop-work reaches crews after one hour, bills, raises mood, and a commit releases it', () => {
  const sim = createSim(quiet(), catalog); site(sim, 'dismount');
  assert.equal(site(sim, 'stopWork', { zone: 2 }).reason, 'outside zone');
  assert.equal(site(sim, 'stopWork', { zone: 1 }).ok, true);
  sim.tick(.04); assert.ok(sim.serialize().crews.exc.pos > 0);
  advance(sim, .06); const pos = sim.serialize().crews.exc.pos;
  advance(sim, 1.06); close(sim.serialize().crews.exc.pos, pos);
  assert.equal(sim.serialize().crews.exc.mood, 1);
  advance(sim, 3.1); assert.equal(sim.serialize().crews.exc.mood, 3);
  assert.ok(sim.serialize().spentUsd > 0);
  commit(sim); sim.tick(.01); assert.ok(sim.serialize().crews.exc.pos > pos);
});

test('P1 clearing times use machine size and half-day protection rounding', () => {
  const s = createSim(stage(2), catalog).serialize(), event = s.events[0];
  for (const [machine, plain, boxed] of [['excavator_small', 3, 1.5], ['excavator_standard', 2, 1], ['excavator_large', 1, .5]]) {
    assert.equal(clearingDays(s, { ...event, protection: null }, machine), plain);
    assert.equal(clearingDays(s, { ...event, protection: 'trench_box' }, machine), boxed);
  }
});

test('own-machine clearing waits for haul travel, stops lender, resumes pipe, then returns', () => {
  const scenario = quiet(); scenario.events = [stage().events[0]];
  scenario.events[0].trigger = { kind: 'day', day: 1 };
  const sim = createSim(scenario, catalog); site(sim, 'dismount');
  commit(sim, { op: 'lend', machine: 'excavator_standard', toCrew: 'pipe', days: 20 });
  assert.equal(sim.serialize().crews.exc.lentAway, true);
  advance(sim, 1.99); assert.equal(sim.serialize().events[0].resolved, undefined);
  advance(sim, 2.01); assert.equal(sim.serialize().events[0].resolved, true);
  assert.equal(sim.serialize().crews.exc.lentAway, false);
});

test('ordinary lending adds rate for the chosen days, with same-day transport in congested cities', () => {
  const scenario = quiet(); scenario.city = 'los_angeles';
  const sim = createSim(scenario, catalog); site(sim, 'dismount');
  commit(sim, { op: 'lend', machine: 'excavator_standard', toCrew: 'pipe', days: 1 });
  sim.tick(.01); assert.equal(sim.serialize().crews.pipe.rate, 400);
  advance(sim, 1.02); assert.equal(sim.serialize().crews.pipe.rate, 180);
  assert.equal(sim.serialize().crews.exc.lentAway, false);
});

test('machine purchases charge at commit, arrive after city lead time, and release the old machine', () => {
  const scenario = quiet(); scenario.city = 'los_angeles';
  const sim = createSim(scenario, catalog);
  commit(sim, { op: 'order', machine: 'excavator_small' });
  assert.equal(sim.serialize().spentUsd, 43000);
  advance(sim, 4.99); assert.equal(sim.serialize().crews.exc.machine, 'excavator_standard');
  const before = sim.serialize().spentUsd;
  advance(sim, 5); assert.equal(sim.serialize().crews.exc.machine, 'excavator_small');
  close(sim.serialize().spentUsd - before, 1200 * .01 * 1.3 - 15000);
});

test('purchases are refused throughout Stage 1 with prep voice; active event payments remain allowed', () => {
  const scenario = stage(); scenario.events[0].trigger = { kind: 'day', day: 1 };
  const sim = createSim(scenario, catalog); sim.drain();
  assert.equal(commit(sim, { op: 'order', machine: 'excavator_small' }).reason, 'not in the budget');
  assert.ok(sim.drain().some(m => m.lineIds?.includes('pm_prep_refused_01')));
  assert.equal(commit(sim, { op: 'pay', item: 'drainage_pump' }).reason, 'no such response now');
  assert.equal(commit(sim, { op: 'pay', item: 'emergency_shoring' }).ok, true);
  close(sim.serialize().spentUsd, 18000);
  advance(sim, 1); assert.equal(sim.serialize().events[0].resolved, true);
});

test('hired clearing uses lead plus size duration and refunds the released machine', () => {
  const scenario = quiet(); scenario.events = [stage().events[0]];
  scenario.events[0].trigger = { kind: 'day', day: 1 };
  const sim = createSim(scenario, catalog);
  commit(sim, { op: 'pay', item: 'hire_excavator', machine: 'excavator_small' });
  close(sim.serialize().spentUsd, 43000);
  advance(sim, 3); close(sim.serialize().events[0].endsT, 6);
  advance(sim, 6); assert.equal(sim.serialize().events[0].resolved, true);
  assert.equal(sim.serialize().deliveries[0].released, true);
  assert.equal(sim.serialize().crews.exc.machine, 'excavator_standard');
});

test('demobilization ends billing next boundary; remobilization bills after the city delay and fee', () => {
  const sim = createSim(quiet(), catalog); site(sim, 'dismount');
  sim.tick(.1); commit(sim, { op: 'demob', crew: 'exc' });
  advance(sim, 1); const spent = sim.serialize().spentUsd;
  advance(sim, 2); close(sim.serialize().spentUsd, spent);
  commit(sim, { op: 'remob', crew: 'exc' });
  close(sim.serialize().spentUsd - spent, 5000);
  advance(sim, 2.99); assert.equal(sim.serialize().crews.exc.demobilized, true);
  advance(sim, 3.01); assert.equal(sim.serialize().crews.exc.demobilized, false);
  assert.ok(sim.serialize().spentUsd > spent + 5000);
});

test('rain rework uses commenced 500-ft chunks; a timely pump halves it and restores only listed crews', () => {
  assert.equal(reworkDuration(1300, 1), 3);
  assert.equal(reworkDuration(1300, 1, .5), 1.5);
  for (const pump of [false, true]) {
    const scenario = quiet(); scenario.events = [stage().events[1]];
    scenario.events[0].trigger = { kind: 'day', day: 2 };
    scenario.events[0].forecast = { inboxDay: 1, skyFromDay: 1, text: 'Forecast' };
    const sim = createSim(scenario, catalog);
    if (pump) assert.equal(commit(sim, { op: 'pay', item: 'drainage_pump' }).ok, true);
    let s = sim.serialize(); s.t = 1; s.crews.exc.pos = 1300;
    s = tickState(s, .01);
    assert.equal(rainMultiplier(s, 'exc'), pump ? 1 : .5);
    assert.equal(rainMultiplier(s, 'back'), pump ? 1 : .5);
    assert.equal(rainMultiplier(s, 'pipe'), .5);
    while (s.t < 3) s = tickState(s, .01);
    assert.equal(s.events[0].openFt, 1300);
    assert.equal(s.events[0].reworkDays, pump ? 1.5 : 3);
  }
});

test('conflicts stop both crews, require a hold or demob, release the leader immediately, and fire once', () => {
  const scenario = quiet(); scenario.crews[1].startDay = 1; scenario.crews[2].startDay = 1;
  let s = createSim(scenario, catalog).serialize();
  s.t = 2; s.crews.exc.pos = 1000; s.crews.pipe.pos = 500; s.crews.back.pos = 450;
  refreshCrews(s); detectConflicts(s);
  assert.equal(s.conflicts.length, 1); assert.equal(s.crews.pipe.mult, 0); assert.equal(s.crews.back.mult, 0);
  const actual = createSim(stage(), catalog); advance(actual, 5.91);
  assert.equal(commit(actual).reason, 'conflict: hold or demobilize the trailing crew');
  assert.equal(commit(actual, { op: 'hold', crew: 'back', days: 2 }).ok, true);
  assert.equal(actual.serialize().crews.pipe.arguing, false);
  s.conflicts[0].resolved = true; s.crews.pipe.arguing = s.crews.back.arguing = false;
  refreshCrews(s); detectConflicts(s); assert.equal(s.conflicts.length, 1);
  s.crews.back.pos = s.crews.pipe.pos;
  s = tickState(s, .01); assert.ok(s.crews.back.pos <= s.crews.pipe.pos);
});

test('a finished leader cannot cause an argument at the job endpoint', () => {
  const s = createSim(quiet(), catalog).serialize();
  s.t = 110; s.crews.exc.pos = 6000; s.crews.exc.finishT = 30; s.crews.pipe.pos = 5999;
  refreshCrews(s); detectConflicts(s); assert.equal(s.conflicts.length, 0);
});

test('inspection and trench state use authored seepage indicators and the collapse station stake', () => {
  const s = createSim(stage(), catalog).serialize();
  s.crews.exc.pos = 900; s.truck = { stationFt: 720, dismounted: true, moving: false };
  assert.equal(inspectionText(s, 'wall'), stage().ground.segments[0].indicator.wall);
  assert.equal(inspectionText(s, 'water'), stage().ground.segments[0].indicator.water);
  assert.equal(trenchAt(s, 700).water, 'seep');
  s.events[0].fired = true;
  assert.equal(inspectionText(s, 'wall'), 'Wall slumped into the trench.');
  assert.equal(inspectionText(s, 'water'), 'Standing water in the trench bottom.');
  assert.equal(trenchAt(s, 700).wall, 'slumped');
  s.events[0].resolved = true; s.crews.exc.pos = 600;
  assert.equal(inspectionText(s, 'wall'), 'Not excavated here yet.');
});

test('all Site inspection targets and local confirmations obey position and mount state', () => {
  const sim = createSim(quiet(), catalog);
  assert.equal(site(sim, 'inspect', { target: 'wall' }).reason, 'dismount first');
  ride(sim, 0); sim.drain();
  for (const target of ['wall', 'water', 'pipe', 'machine', 'protection', 'board', 'sky', 'ticket', 'crew']) {
    assert.equal(site(sim, 'inspect', { target }).ok, true);
    assert.equal(typeof sim.drain().at(-1).text, 'string');
  }
  assert.equal(site(sim, 'flag', { stationFt: 0, note: 'Seepage' }).ok, true);
  assert.equal(sim.view('plan').flags[0].note, 'Seepage');
  assert.equal(site(sim, 'confirm', { orderId: 'absent', outcome: 'done' }).reason, 'order not landed here');
  site(sim, 'mount'); site(sim, 'moveTo', { stationFt: 1500 });
  assert.equal(site(sim, 'dismount').reason, 'truck moving');
  advance(sim, .05); assert.equal(sim.view('site').truck.moving, false);
});

test('F3 records a misplaced protection inspection before its event, and F5 has an inert lend hook', () => {
  const scenario = quiet(); scenario.events = [stage(2).events[0]]; scenario.deviations = stage(2).deviations;
  const sim = createSim(scenario, catalog); site(sim, 'dismount');
  commit(sim, { op: 'protect', kind: 'trench_box', fromFt: 0, toFt: 100 });
  site(sim, 'inspect', { target: 'protection' });
  let s = sim.serialize(); s.crews.pipe.pos = 720;
  s = tickState(s, .01);
  assert.equal(s.deviations.find(d => d.type === 'F3').caught, true);
  assert.equal(s.events[0].protection, null);
  const hook = quiet(); hook.deviations = [{ id: 'reserved', type: 'F5', when: { kind: 'firstLend' }, extraDays: 100 }];
  const h = createSim(hook, catalog); site(h, 'dismount');
  commit(h, { op: 'lend', machine: 'excavator_standard', toCrew: 'pipe', days: 1 });
  assert.equal(h.serialize().loans[0].arrivesT, 0);
  assert.equal(h.serialize().deviations[0].triggered, true);
});

test('strict budget and deadline loss omit finishDay', () => {
  for (const lost of ['budget', 'deadline']) {
    const scenario = quiet();
    if (lost === 'budget') scenario.plan.budgetUsd = 1; else scenario.plan.deadlineDay = 1;
    const sim = createSim(scenario, catalog); sim.tick(.01);
    assert.equal(sim.done, true); assert.equal(sim.result.lost, lost);
    assert.equal(Object.hasOwn(sim.result, 'finishDay'), false);
  }
});

test('facilitator pause/resume/reset and same-stage load are deterministic', () => {
  const scenario = quiet(), sim = createSim(scenario, catalog), initial = sim.serialize();
  sim.apply('facilitator', { t: 'ctrl', action: 'pause' }); sim.tick(1); assert.equal(sim.t, 0);
  sim.apply('facilitator', { t: 'ctrl', action: 'resume' }); sim.tick(.1); close(sim.t, .1);
  sim.apply('facilitator', { t: 'ctrl', action: 'reset' }); assert.deepEqual(sim.serialize(), initial);
  assert.equal(sim.apply('facilitator', { action: 'load', stage: scenario.id }).ok, true);
  assert.equal(sim.apply('site', { action: 'reset' }).ok, false);
});

test('reducers and public views do not mutate source state or caller-owned configuration', () => {
  const scenario = quiet(), before = JSON.stringify(scenario), sim = createSim(scenario, catalog);
  const s = sim.serialize(), serialized = JSON.stringify(s); tickState(s, .01);
  assert.equal(JSON.stringify(s), serialized);
  const view = sim.view('facilitator'); view.crews.exc.pos = 9999;
  assert.equal(sim.serialize().crews.exc.pos, 0); assert.equal(JSON.stringify(scenario), before);
});

test('issued machine, hold, resume and lend plans project rates without field truth', () => {
  const sim = createSim(quiet(), catalog); site(sim, 'dismount');
  commit(sim, { op: 'order', machine: 'excavator_small' });
  let line = sim.view('plan').plannedLines.exc;
  assert.equal(plannedRate(line, 2), 220); assert.equal(plannedRate(line, 3), 165);
  commit(sim, { op: 'hold', crew: 'exc', days: 2 });
  line = sim.view('plan').plannedLines.exc;
  assert.equal(plannedRate(line, 1), 0); assert.equal(plannedRate(line, 2), 220);
  sim.tick(.1); commit(sim, { op: 'resume', crew: 'exc' });
  assert.equal(plannedRate(sim.view('plan').plannedLines.exc, .1), 220);
  const s = sim.serialize();
  const before = plannedPolylines(s); s.crews.exc.pos = 5999;
  assert.deepEqual(plannedPolylines(s), before);
  const lending = createSim(quiet(), catalog); site(lending, 'dismount');
  commit(lending, { op: 'lend', machine: 'excavator_standard', toCrew: 'pipe', days: 1 });
  assert.equal(plannedRate(lending.view('plan').plannedLines.exc, .5), 0);
});

test('slope refusal comes from the city and finished crews do not keep protection billing', () => {
  const scenario = quiet(); scenario.city = 'los_angeles';
  const sim = createSim(scenario, catalog);
  assert.equal(commit(sim, { op: 'protect', kind: 'trench_slope', fromFt: 0, toFt: 100 }).reason, 'slope refused');
  const s = sim.serialize(); s.t = 30;
  s.protections.push({ kind: 'trench_box', fromFt: 5900, toFt: 6000 });
  s.crews.exc.pos = 6000; s.crews.exc.finishT = 29; s.crews.exc.moved = 2.2;
  assert.equal(tickState(s, .01).spentUsd, s.spentUsd);
});
