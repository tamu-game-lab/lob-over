import test from 'node:test';
import assert from 'node:assert/strict';
import { createAutoplan } from '../src/host/autoplan.js';
import { createHost } from '../src/host/core.js';
import { loadCatalog } from '../src/common/catalog.js';
import { loadScenario } from '../src/scenario/load.js';
import { readFileSync } from 'node:fs';
const entry = (t, id, seat = 'plan') => ({ t, seat, cmd: { action: 'commit', id, ops: [] } });

test('autoplan filters Site, orders equal times stably and consumes each entry once', () => {
  const source = [entry(4, 'later'), entry(0, 'site', 'site'), entry(0, 'first'), entry(0, 'second')];
  const player = createAutoplan(source), calls = [];
  source[2].cmd.id = 'mutated';
  const apply = (seat, cmd) => calls.push([seat, cmd.id]);
  player.advance(0, apply); player.advance(0, apply); player.advance(3.99, apply);
  assert.deepEqual(calls, [['plan', 'first'], ['plan', 'second']]);
  assert.deepEqual(player.status, { enabled: true, nextT: 4 });
  player.advance(4, apply); player.advance(9, apply);
  assert.equal(calls.length, 3); assert.equal(player.status.nextT, null);
  player.reset(); player.setEnabled(false); player.advance(10, apply);
  assert.equal(calls.length, 3); player.setEnabled(true); player.advance(0, apply);
  assert.equal(calls.length, 5);
});
test('autoplan retains non-Site facilitator entries and rejects malformed scripts', () => {
  const player = createAutoplan([{ t: 1, seat: 'facilitator', cmd: { action: 'pause' } }]);
  const calls = []; player.advance(1, (...args) => calls.push(args));
  assert.equal(calls[0][0], 'facilitator');
  for (const script of [{}, [entry(-1, 'x')], [entry(NaN, 'x')], [entry(0, 'x', 'unknown')], [{ t: 0, seat: 'plan' }]]) {
    assert.throws(() => createAutoplan(script), /Autoplan|autoplan/);
  }
});
test('paced host runs authored slice autoplan at authored times, excludes Site, and rewinds on reset', () => {
  const catalog = loadCatalog();
  const scenario = loadScenario(new URL('../../Data/Scenarios/slice_texas.json', import.meta.url), { catalog });
  const script = JSON.parse(readFileSync(new URL('./scripts/slice_autoplan.json', import.meta.url)));
  let now = 0, timer, sequence = 0;
  const core = createHost({ scenario, catalog, autoplan: script, allowFast: true,
    pacing: { now: () => now, setTimer(callback, delay) { timer = { callback, at: now + delay, id: ++sequence }; return sequence; },
      clearTimer(id) { if (timer?.id === id) timer = null; } },
  });
  try {
    core.start(); core.setFast(true);
    for (let i = 0; i < 8000 && !core.transcript.result; i++) {
      assert.ok(timer, 'a running simulation has a pending tick');
      const pending = timer; timer = null; now = pending.at; pending.callback();
    }
    assert.ok(core.transcript.result); assert.equal(core.transcript.result.lost, undefined);
    const replies = core.transcript.messages.filter(item => item.message.id);
    for (const command of script.filter(item => item.seat !== 'site')) {
      const reply = replies.find(item => item.message.id === command.cmd.id);
      assert.ok(reply, command.cmd.id); assert.ok(Math.abs(reply.t - command.t) < 0.011);
    }
    assert.equal(core.state('site').view.truck.stationFt, 0);
    core.reset(); assert.equal(core.state('plan').day, 0);
    assert.equal(core.state('facilitator').view.autoplan.nextT, script.filter(item => item.seat !== 'site')[0].t);
  } finally { core.close(); }
});
