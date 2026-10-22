import test from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { once } from 'node:events';
import { readFile } from 'node:fs/promises';
import { resolve, extname, relative } from 'node:path';
import { chromium } from 'playwright';
import { build } from '../../Showcase/build.mjs';
import { dist } from '../src/showcase/build.js';
import { startRelay, connect, poll } from '../../Relay/test/helpers.js';
import { localRooms } from './local-rooms.js';

async function staticServer(t) {
  const types = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json' };
  const server = createServer(async (request, response) => {
    try {
      const url = new URL(request.url, 'http://localhost');
      const path = resolve(dist, '.' + decodeURIComponent(url.pathname).replace(/^\/lob-over/, ''));
      if (relative(dist, path).startsWith('..')) throw new Error('Outside showcase');
      const body = await readFile(path);
      response.writeHead(200, { 'Content-Type': types[extname(path)] || 'application/octet-stream' });
      response.end(body);
    } catch {
      t.diagnostic('Showcase request returned 404: ' + request.url);
      response.writeHead(404).end();
    }
  });
  server.listen(0, '127.0.0.1'); await once(server, 'listening');
  return { base: `http://127.0.0.1:${server.address().port}/lob-over/`,
    close: () => new Promise(resolve => { server.close(resolve); server.closeAllConnections(); }),
  };
}

test('Level 1 browser relay: Quest welcome/state/cmd, PeerJS coexistence and relay loss', { timeout: 60_000 }, async t => {
  await build();
  const relay = await startRelay();
  let server, rooms, browser;
  try {
    server = await staticServer(t); rooms = await localRooms();
    browser = await chromium.launch({ headless: true, args: ['--log-file=' + resolve(dist, 'chromium-relay.log')] });
    const context = await browser.newContext({ viewport: { width: 1366, height: 768 } });
    const errors = [];
    context.on('page', page => page.on('pageerror', error => errors.push(error.message)));
    await rooms.configure(context);
    const shipped = await (await context.request.get(server.base + 'Data/relay.json')).json();
    assert.deepEqual(shipped, JSON.parse(await readFile(new URL('../../Data/relay.json', import.meta.url))));
    await context.route('**/Data/relay.json', route => route.fulfill({ json: { relay: relay.base } }));
    const plan = await context.newPage();
    await plan.goto(server.base + 'play.html?host=browser');
    await plan.waitForFunction(() => !!state && document.querySelector('#relay-status').textContent === 'Relay: connected');
    assert.equal(await plan.locator('#start').isEnabled(), true);
    const code = await plan.locator('#room-code').innerText();
    assert.match(code, /^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{6}$/);
    const list = () => fetch(relay.base + '/rooms').then(response => response.json());
    assert.equal((await list())[0].code, code);
    const quest = await connect(relay.ws + '/join/' + code + '?seat=site&client=quest', t);
    quest.send({ t: 'hello', v: 1, seat: 'site', client: 'quest' });
    const welcome = await quest.next(message => message.t === 'welcome');
    assert.equal(welcome.seat, 'site'); assert.equal(welcome.mirror, false);
    assert.equal(welcome.stage, 'level1_texas');
    const state = await quest.next(message => message.t === 'state');
    assert.ok(state.view.truck);
    assert.equal(state.view.plan, undefined); assert.equal(state.view.budget, undefined);
    await plan.waitForFunction(() => document.querySelector('#site-status').textContent === 'Headset connected');
    assert.equal(await plan.evaluate(() => state.view.trench), undefined);
    quest.send({ t: 'cmd', v: 1, id: 'quest-dismount', seat: 'site', action: 'dismount' });
    assert.equal((await quest.next(message => message.id === 'quest-dismount')).t, 'ack');
    await quest.next(message => message.t === 'state' && message.view.truck.dismounted);
    // Real PeerJS/WebRTC joins the same core while Quest uses the relay.
    const phoneContext = await browser.newContext(); await rooms.configure(phoneContext);
    const phone = await phoneContext.newPage();
    await phone.goto(await plan.locator('#room-link').getAttribute('href'));
    await phone.waitForFunction(() => !!state && document.querySelector('#connection').textContent === 'Mirror · read only');
    assert.equal(await phone.evaluate(() => state.view.truck.dismounted), true);
    assert.equal(await phone.evaluate(() => state.view.plan), undefined);
    assert.equal(await plan.locator('#site-status').innerText(), 'Headset connected');
    assert.ok(rooms.forwarded.includes('OFFER') && rooms.forwarded.includes('ANSWER'));
    await phoneContext.close();
    quest.socket.close(); await quest.closed();
    await plan.waitForFunction(() => state.view.siteClient === null);
    const rejoined = await connect(relay.ws + '/join/' + code + '?seat=site&client=quest', t);
    rejoined.send({ t: 'hello', v: 1, seat: 'site', client: 'quest' });
    assert.equal((await rejoined.next(message => message.t === 'welcome')).mirror, false);
    await plan.waitForFunction(() => document.querySelector('#site-status').textContent === 'Headset connected');
    await plan.close();
    assert.equal((await rejoined.closed()).reason, 'host left');
    await poll(async () => (await list()).length === 0);

    // An unreachable relay socket is reported while Start/Hold still work.
    // This port answers HTTP but refuses upgrades; no external network is used.
    await context.route('**/Data/relay.json', route => route.fulfill({ json: { relay: server.base } }));
    const offline = await context.newPage();
    await offline.goto(server.base + 'play.html?host=browser');
    await offline.waitForFunction(() => !!state && document.querySelector('#relay-status').textContent === 'Relay: unavailable');
    await offline.locator('#start').click();
    await offline.waitForFunction(() => state.speed !== 'paused');
    await offline.locator('[data-crew="back"]').click();
    await offline.waitForFunction(() => state.view.held.back);
    assert.deepEqual(errors, []);
    t.diagnostic('Quest welcome/state/cmd and status, filtered seats, simultaneous PeerJS, reconnect, host-left and unavailable-relay play passed');
  } finally {
    await browser?.close(); await rooms?.close(); await server?.close(); await relay.close();
  }
});
