import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { loadCatalog } from '../src/common/catalog.js';
import { createSim } from '../src/sim/sim.js';
import { runHeadless, roundedJSON } from '../src/sim/headless.js';
import { runGoldens } from '../src/sim/goldens.js';

const catalog = loadCatalog();
const read = path => JSON.parse(readFileSync(new URL(path, import.meta.url), 'utf8'));
const s1 = read('../../Data/Scenarios/stage1_texas.json');
const s2 = read('../../Data/Scenarios/stage2_texas.json');
const script1 = read('./scripts/stage1_pm_play.json');
const script2 = read('./scripts/stage2_pull_play.json');
const slice = read('../../Data/Scenarios/slice_texas.json');
const slicePlay = read('./scripts/slice_play.json');
const slicePull = read('./scripts/slice_pull_play.json');
const sliceAutoplan = read('./scripts/slice_autoplan.json');
const forbidden = /\b(correct|wrong|score|well done|should have)\b/i;
const events = run => run.messages.filter(x => x.message.t === 'event');
const nacks = run => run.messages.filter(x => x.message.t === 'nack');
const collapse = run => events(run).find(x => x.message.eventId === 'ev_p1');

test('all five independent oracle cases pass every tolerance', () => {
  const results = runGoldens(read('../../Data/goldens/no_events.json'), catalog);
  assert.equal(results.length, 5);
  assert.ok(results.every(r => r.pass), JSON.stringify(results));
});

test('Stage 1 and 2 scripts complete with the specified comparative facts and zero rejected commands', () => {
  const first = runHeadless(s1, catalog, script1);
  const second = runHeadless(s2, catalog, script2);
  assert.deepEqual(nacks(first), []); assert.deepEqual(nacks(second), []);
  assert.equal(first.result.lost, undefined); assert.equal(second.result.lost, undefined);
  assert.ok(first.result.finishDay > s1.plan.promisedFinishDay);
  assert.ok(first.result.finishDay < s1.plan.deadlineDay - 1);
  assert.ok(second.result.finishDay < s2.plan.deadlineDay - 1);
  assert.ok(second.result.idleCrewDays < first.result.idleCrewDays);
  assert.ok(second.result.peakOpenTrenchFt <= 3000);
  assert.equal(second.result.finesUsd, 0);
  assert.ok(Math.abs(collapse(first).t - 5) <= .02);
  assert.deepEqual(collapse(second).message.card, s2.events[0].cardWhenProtected);
  assert.equal(first.result.deviationsCaught, 1);
  assert.ok(second.result.events.some(e => e.option === 'drainage_pump'));
  for (const run of [first, second]) {
    for (const snapshot of run.snapshots) {
      assert.equal(snapshot.t % 1, 0);
      assert.doesNotMatch(JSON.stringify(snapshot.plan), forbidden);
      assert.doesNotMatch(JSON.stringify(snapshot.site), forbidden);
      assert.equal(Object.hasOwn(snapshot.plan, 'crews'), false);
      assert.equal(Object.hasOwn(snapshot.site, 'budget'), false);
      assert.equal(Object.hasOwn(snapshot.site, 'plan'), false);
    }
    for (const message of run.messages) assert.doesNotMatch(JSON.stringify(message), forbidden);
    assert.equal(run.result.changeLog.length, (run === first ? script1 : script2).filter(x => x.cmd.action === 'commit').length);
    for (const points of Object.values(run.result.lines.actual)) assert.ok(points.length > 2);
  }
});

test('slice push plays collapse, argument, listen, hold and rain with F1 caught and no rejected commands', () => {
  const run = runHeadless(slice, catalog, slicePlay);
  assert.deepEqual(nacks(run), []);
  assert.equal(run.result.lost, undefined);
  assert.ok(run.result.finishDay >= 22.5 && run.result.finishDay <= 25.5);
  const cards = events(run);
  assert.deepEqual(cards.map(x => x.message.eventId), ['ev_p1', 'conflict_back_pipe', 'ev_a2']);
  assert.equal(cards[0].t, 3.89);
  assert.deepEqual(cards[0].message.card, slice.events[0].card);
  const argument = cards[1];
  assert.equal(argument.message.card.title, 'Crews stopped');
  assert.ok(argument.t >= 4 && argument.t < 5);
  assert.equal(cards[2].t, 8);
  const listen = slicePlay.find(x => x.cmd.action === 'listen');
  const hold = slicePlay.find(x => x.cmd.ops?.some(op => op.op === 'hold' && op.crew === 'back'));
  assert.ok(listen.t > argument.t && hold.t > listen.t);
  const conflictLines = slice.lines.filter(line => line.trigger === 'conflict').map(line => line.id);
  assert.ok(run.messages.some(x => x.t === listen.t && x.message.t === 'voice'
    && x.message.lineIds.some(id => conflictLines.includes(id))), 'Site hears the argument at the listen stop');
  assert.equal(run.result.deviationsCaught, 1);
  assert.equal(run.result.deviationsMissed, 0);
});

test('slice pull finishes before push with less idle time, timely holds and no rejected commands', () => {
  const push = runHeadless(slice, catalog, slicePlay);
  const pull = runHeadless(slice, catalog, slicePull);
  assert.deepEqual(nacks(pull), []);
  assert.equal(pull.result.lost, undefined);
  assert.ok(pull.result.finishDay < push.result.finishDay);
  assert.ok(pull.result.idleCrewDays < push.result.idleCrewDays);
  assert.equal(collapse(pull).t, 3.89);
  assert.deepEqual(collapse(pull).message.card, slice.events[0].cardWhenProtected);
  assert.equal(events(pull).find(x => x.message.eventId === 'ev_a2').t, 8);
  for (const argument of events(pull).filter(x => x.message.card.title === 'Crews stopped')) {
    const hold = slicePull.find(x => x.t > argument.t
      && x.cmd.ops?.some(op => op.op === 'hold' && op.crew === 'back'));
    assert.ok(hold && hold.t - argument.t <= .5, 'Plan holds backfill within half a day of the argument');
  }
  assert.equal(pull.result.deviationsCaught, 1);
  assert.equal(pull.result.deviationsMissed, 0);
  assert.ok(pull.result.events.some(e => e.option === 'drainage_pump'));
});

test('slice autoplan retains its event chain, finish and idle time with F1 missed and no rejected commands', () => {
  const run = runHeadless(slice, catalog, sliceAutoplan);
  assert.deepEqual(nacks(run), []);
  assert.equal(run.result.lost, undefined);
  // WO-05a Amendment 1: the no-Site baseline remains unchanged.
  assert.ok(Math.abs(run.result.finishDay - 24.8) <= .05);
  assert.ok(Math.abs(run.result.idleCrewDays - 19.967) <= .001);
  assert.deepEqual(events(run).map(x => [x.message.eventId, x.t]), [
    ['ev_p1', 3.89], ['conflict_back_pipe', 4.09], ['ev_a2', 8],
  ]);
  assert.equal(run.result.deviationsCaught, 0);
  assert.equal(run.result.deviationsMissed, 1);
});

test('two headless CLI runs produce byte-for-byte identical rounded transcripts', () => {
  const root = fileURLToPath(new URL('../../', import.meta.url));
  const args = ['Monitor/src/sim/headless.js', 'Data/Scenarios/stage1_texas.json',
    '--script', 'Monitor/test/scripts/stage1_pm_play.json'];
  const run = () => spawnSync(process.execPath, args, { cwd: root, encoding: 'utf8', maxBuffer: 20 * 1024 * 1024 });
  const a = run(), b = run();
  assert.ifError(a.error); assert.ifError(b.error);
  assert.equal(a.status, 0, a.stderr); assert.equal(b.status, 0, b.stderr);
  assert.equal(a.stdout, b.stdout);
  const parsed = JSON.parse(a.stdout);
  assert.equal(parsed.scenario, s1.id); assert.equal(parsed.seed, s1.seed);
  assert.equal(a.stdout, roundedJSON(parsed));
});

test('empty Stage 1 script ends on deadline, without a finish day', () => {
  const run = runHeadless(s1, catalog, read('./scripts/stage1_noaction.json'));
  assert.equal(run.result.lost, 'deadline'); assert.equal(run.result.finishDay, undefined);
  assert.equal(run.safetyStop, undefined);
});

test('Plan sees only last-report feet and elapsed-rate estimates; Site sees live positions without money', () => {
  const sim = createSim(s1, catalog); sim.tick(.5);
  const plan = sim.view('plan'), site = sim.view('site');
  assert.equal(plan.reportedLines.exc.at(-1).ft, 0);
  assert.ok(site.crews.find(c => c.id === 'exc').stationFt > 100);
  assert.equal(plan.uncertainty.exc.estimateFt, 110);
  assert.equal(plan.uncertainty.exc.toFt, 260);
  assert.doesNotMatch(JSON.stringify(plan), /"(?:pos|stationFt|actualLines|activeEvent)":/);
  assert.doesNotMatch(JSON.stringify(site), /"(?:budget|budgetUsd|spentUsd|plannedLines|reportedLines|forecast)":/);
  sim.tick(.5);
  const report = sim.view('plan');
  assert.equal(report.reportedLines.exc.at(-1).ft, 220);
  assert.equal(report.uncertainty.exc.fromFt, report.uncertainty.exc.toFt);
});

test('headless safety stop is explicit when facilitator leaves the sim paused', () => {
  const run = runHeadless(s1, catalog, [{ t: 0, seat: 'facilitator', cmd: { t: 'ctrl', action: 'pause' } }]);
  assert.equal(run.result, null);
  assert.equal(run.safetyStop.reason, 'paused without a same-time resume');
});
