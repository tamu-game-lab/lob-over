import test from 'node:test';
import assert from 'node:assert/strict';
import { createHost } from '../../../Monitor/src/host/core.js';
import { loadCatalog } from '../../../Monitor/src/common/catalog.js';
import { loadScenario } from '../../../Monitor/src/scenario/load.js';

// No sockets or network: verify the real WO-L1 host contract consumed by the Unity client.
test('Level 1 host accepts site/quest and exposes the presenter geometry and exchange shape', () => {
  const catalog = loadCatalog();
  const scenario = loadScenario('Data/Scenarios/level1_texas.json', { catalog });
  const host = createHost({ scenario, catalog });
  const messages = []; let receive;
  const socket = { send: value => messages.push(value), onMessage: callback => { receive = callback; }, onClose() {}, close() {} };
  try {
    host.attach(socket);
    receive({ t: 'hello', v: 1, seat: 'site', client: 'quest' });
    const welcome = messages.find(message => message.t === 'welcome');
    assert.equal(welcome.seat, 'site'); assert.equal(welcome.stage, 'level1_texas');
    const view = welcome.state.view;
    assert.equal(view.project.job.lengthFt, 3000); assert.ok(Array.isArray(view.project.job.zones));
    assert.deepEqual(view.crews.map(crew => crew.id), ['exc', 'pipe', 'back']);
    assert.equal(view.budget, undefined); assert.equal(view.plannedLines, undefined);
    for (const [i, action] of ['moveTo', 'dismount', 'mount', 'moveTo', 'dismount'].entries()) {
      receive({ t: 'cmd', v: 1, id: `quest-${i}`, seat: 'site', action, ...(action === 'moveTo' ? { stationFt: 0 } : {}) });
      assert.equal(messages.find(message => message.id === `quest-${i}`).t, 'ack', action);
    }
    const exchange = scenario.lines.find(line => line.id === 'grumble_back_pipe');
    assert.equal(exchange.turns.length, 2);
    assert.deepEqual(exchange.turns.map(turn => turn.speaker), ['foreman_back', 'foreman_pipe']);
    assert.ok(messages.some(message => message.t === 'state'));
  } finally { host.close(); }
});
