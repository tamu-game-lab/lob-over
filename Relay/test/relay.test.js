import test from 'node:test';
import assert from 'node:assert/strict';
import { startRelay, connect, rejected, poll } from './helpers.js';

test('wrangler relay: list, two seats, routing, conflict, leave and host-left close', { timeout: 60_000 }, async t => {
  const relay = await startRelay();
  try {
    const list = () => fetch(relay.base + '/rooms').then(response => response.json());
    assert.deepEqual(await list(), []);
    assert.equal((await fetch(relay.base + '/rooms')).headers.get('access-control-allow-origin'), '*');
    await rejected(relay.ws + '/join/K7MX2Q?seat=site&client=quest', 404);
    await rejected(relay.ws + '/host/INVALID', 404);
    const before = Date.now();
    const host = await connect(relay.ws + '/host/K7MX2Q?name=Test%20Mac', t);
    const rooms = await list();
    assert.equal(rooms.length, 1);
    assert.equal(rooms[0].code, 'K7MX2Q'); assert.equal(rooms[0].host, 'Test Mac');
    assert.ok(rooms[0].since >= before && rooms[0].since <= Date.now());
    await rejected(relay.ws + '/host/K7MX2Q', 409);
    await rejected(relay.ws + '/join/K7MX2Q?seat=other&client=quest', 400);
    await rejected(relay.ws + '/join/K7MX2Q?seat=site&client=other', 400);
    const site = await connect(relay.ws + '/join/K7MX2Q?seat=site&client=quest', t);
    const first = await host.next(message => message.event === 'join');
    assert.equal(first.seat, 'site'); assert.equal(first.client, 'quest');
    const plan = await connect(relay.ws + '/join/K7MX2Q?seat=plan&client=web', t);
    const second = await host.next(message => message.event === 'join');
    assert.notEqual(first.from, second.from);
    const hello = { t: 'hello', v: 1, seat: 'site', client: 'quest' };
    site.send(hello);
    assert.deepEqual(await host.next(message => message.data?.t === 'hello'),
      { t: 'relay', from: first.from, seat: 'site', client: 'quest', data: hello });
    const command = { t: 'cmd', v: 1, id: 'p1', seat: 'plan', action: 'commit', ops: [] };
    plan.send(command);
    assert.deepEqual(await host.next(message => message.data?.id === 'p1'),
      { t: 'relay', from: second.from, seat: 'plan', client: 'web', data: command });
    host.send({ t: 'relay', to: first.from, data: { t: 'welcome', v: 1, seat: 'site' } });
    assert.deepEqual(await site.next(message => message.t === 'welcome'), { t: 'welcome', v: 1, seat: 'site' });
    host.send({ t: 'relay', to: '*', data: { t: 'state', v: 1, day: 0 } });
    for (const seat of [site, plan]) assert.deepEqual(await seat.next(message => message.t === 'state'), { t: 'state', v: 1, day: 0 });
    assert.deepEqual(plan.messages, [], 'targeted welcome did not leak to the second seat');
    // Idle long enough for local workerd hibernation; the directory and seat
    // attachments must still identify the same live room and connections.
    await new Promise(resolve => setTimeout(resolve, 12_000));
    assert.deepEqual(await list(), rooms);
    site.send({ t: 'cmd', v: 1, id: 'after-idle' });
    assert.equal((await host.next(message => message.data?.id === 'after-idle')).from, first.from);
    plan.socket.close();
    assert.deepEqual(await host.next(message => message.event === 'leave'),
      { t: 'relay', event: 'leave', from: second.from, seat: 'plan', client: 'web' });
    const rules = await connect(relay.ws + '/join/K7MX2Q?seat=rules&client=web', t);
    await host.next(message => message.event === 'join');
    host.socket.close();
    // WO-L9 rejection 2: workerd may finish a post-hibernation seat's close late.
    for (const seat of [site, rules]) assert.deepEqual(await seat.closed(15_000), { code: 1000, reason: 'host left' });
    await poll(async () => (await list()).length === 0);
    const replacement = await connect(relay.ws + '/host/K7MX2Q', t);
    assert.equal((await list())[0].host, 'Browser');
    replacement.socket.close();
    await poll(async () => (await list()).length === 0);
    t.diagnostic('Forwarding both ways, broadcast, joins/leaves, hibernation, CORS, 409 and host-left passed');
  } finally { await relay.close(); }
});
