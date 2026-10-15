import test from 'node:test';
import assert from 'node:assert/strict';
import { once } from 'node:events';
import { fileURLToPath } from 'node:url';
import { WebSocket } from 'ws';
import { startHost } from './server.js';

const scenarioPath = fileURLToPath(new URL('../../../Data/Scenarios/stage1_texas.json', import.meta.url));
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
function inbox(socket) {
  const messages = [], waiters = new Set();
  socket.on('message', data => {
    const message = JSON.parse(data);messages.push(message);
    for (const waiter of waiters) if (waiter.predicate(message)) {
      clearTimeout(waiter.timer);waiters.delete(waiter);waiter.resolve(message);
    }
  });
  return { messages, next(predicate) {
    return new Promise((resolve, reject) => {
      const waiter = { predicate, resolve };
      waiter.timer = setTimeout(() => { waiters.delete(waiter);reject(new Error('Protocol reply timed out')); }, 3000);
      waiters.add(waiter);
    });
  } };
}
test('host: static files, seat ownership, request routing, pacing and stage controls', { timeout: 15_000 }, async () => {
  const host = await startHost({ scenarioPath, port: 0, testMode: false });
  const clients = [];
  let id = 0;
  async function connect(seat) {
    const socket = new WebSocket(`ws://127.0.0.1:${host.port}/ws`), queue = inbox(socket);
    clients.push(socket);await once(socket, 'open');
    const welcome = queue.next(m => m.t === 'welcome');
    socket.send(JSON.stringify({ t: 'hello', v: 1, seat, client: 'web' }));
    return { socket, queue, welcome: await welcome, seat };
  }
  async function command(client, action, fields = {}, type = client.seat === 'facilitator' ? 'ctrl' : 'cmd') {
    const commandId = `test-${++id}`;
    const next = client.queue.next(m => m.id === commandId && (m.t === 'ack' || m.t === 'nack'));
    client.socket.send(JSON.stringify({ t: type, v: 1, id: commandId, seat: client.seat, action, ...fields }));
    return next;
  }
  try {
    for (const page of ['plan', 'site', 'facilitator']) {
      const response = await fetch(`http://127.0.0.1:${host.port}/${page}.html`);
      assert.equal(response.status, 200);assert.match(response.headers.get('content-type'), /text\/html/);
    }
    assert.equal((await fetch(`http://127.0.0.1:${host.port}/Voice/stage1_texas/pm_open_01.wav`, { method: 'HEAD' })).status, 200);
    assert.equal((await fetch(`http://127.0.0.1:${host.port}/%2e%2e%5cData/catalog.json`)).status, 404);
    const plan = await connect('plan'), mirror = await connect('plan'), site = await connect('site'), siteMirror = await connect('site'), facilitator = await connect('facilitator');
    assert.equal(plan.welcome.mirror, false);assert.equal(mirror.welcome.mirror, true);
    assert.equal(plan.welcome.state.speed, 'paused');
    assert.equal(plan.welcome.state.view.actualLines, undefined);
    assert.equal(plan.welcome.state.view.trench, undefined);
    assert.equal(site.welcome.state.view.budget, undefined);
    assert.equal(site.welcome.state.view.plannedLines, undefined);
    assert.equal(site.welcome.state.view.trench.find(c => c.stationFt === 2000).water, undefined);
    assert.equal((await command(mirror, 'commit', { ops: [] })).reason, 'mirror');
    assert.equal((await command(plan, 'start', {}, 'ctrl')).reason, 'action unavailable for seat');
    assert.equal((await command(plan, 'moveTo', { seat: 'site', stationFt: 720 })).reason, 'action unavailable for seat');
    assert.equal((await command(facilitator, 'fast')).reason, 'unknown control');
    assert.equal((await command(site, 'dismount')).t, 'ack');
    const inspection = site.queue.next(m => m.t === 'inspection');
    assert.equal((await command(site, 'inspect', { target: 'board' })).t, 'ack');
    assert.match((await inspection).text, /TX-0141/);
    await sleep(300);
    assert.equal(siteMirror.queue.messages.some(m => m.t === 'inspection'), false);
    const posted = await command(plan, 'commit', { ops: [{ op: 'enterPermit', zone: 1, code: '4821-5' }] });
    assert.deepEqual(posted.permits, [{ zone: 1, number: 'TX-0141' }]);
    assert.equal((await command(plan, 'commit', { ops: [{ op: 'setStart', crew: 'back', day: 12 }] })).reason, 'plan locked');
    assert.equal((await command(facilitator, 'start')).t, 'ack');
    await sleep(320);
    const running = facilitator.queue.next(m => m.t === 'state');
    await command(facilitator, 'pause');
    const stopped = await running;
    assert.equal(stopped.speed, 'paused');assert.ok(stopped.day >= .04 && stopped.day <= .1);
    await sleep(300);
    assert.equal(facilitator.queue.messages.filter(m => m.t === 'state').at(-1).day, stopped.day);
    const loaded = facilitator.queue.next(m => m.t === 'state' && m.stage === 'stage2_texas');
    assert.equal((await command(facilitator, 'load', { stage: 'stage2_texas' })).t, 'ack');
    const stage2 = await loaded;
    assert.equal(stage2.day, 0);assert.equal(stage2.speed, 'paused');
    assert.equal((await command(plan, 'commit', { ops: [{ op: 'setStart', crew: 'back', day: 12 }] })).t, 'ack');
    const reset = facilitator.queue.next(m => m.t === 'state' && m.runId > stage2.runId);
    await command(facilitator, 'reset');assert.equal((await reset).day, 0);
    assert.equal((await command(facilitator, 'load', { stage: 'missing' })).reason, 'stage not loaded');
    for (const client of [plan, mirror, site, facilitator]) {
      const replies = client.queue.messages.filter(m => m.t === 'ack' || m.t === 'nack');
      assert.equal(new Set(replies.map(m => m.id)).size, replies.length);
    }
  } finally {
    for (const socket of clients) socket.terminate();
    await host.close();
  }
});
