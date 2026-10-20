import test from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { once } from 'node:events';
import { mkdir, readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright';
import { build } from '../src/showcase/build.js';

const root = fileURLToPath(new URL('../../', import.meta.url));
const output = fileURLToPath(new URL('../out/e2e/showcase/', import.meta.url));
const base = 'http://localhost:7340/lob-over-showcase/';
const slice = JSON.parse(await readFile(new URL('../../Data/Scenarios/slice_texas.json', import.meta.url), 'utf8'));
const collapseTitle = slice.events.find(event => event.type === 'P1').card.title;
test('Showcase: static Pages subpath, local seats, autoplan, result and phone room', { timeout: 90_000 }, async t => {
  await build(); await mkdir(output, { recursive: true });
  const server = spawn(process.execPath, ['Monitor/src/showcase/serve.js'], { cwd: root, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  let browser, stderr = '';
  const originalCwd = process.cwd();
  server.stderr.on('data', data => { stderr += data; });
  try {
    await new Promise((resolve, reject) => {
      server.stdout.on('data', data => { if (String(data).includes('listening on 7340')) resolve(); });
      server.once('error', reject); server.once('exit', code => reject(new Error(`Static server exited ${code}: ${stderr}`)));
    });
    // Chromium's Windows WebRTC subprocess writes debug.log in its working directory.
    process.chdir(output);
    browser = await chromium.launch({ headless: true, args: ['--log-file=' + output + 'chromium.log'] });
    const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
    const errors = [], failedLocal = [];
    context.on('page', page => {
      page.on('pageerror', error => errors.push(error.message));
      page.on('response', response => { if (response.url().startsWith(base) && response.status() >= 400) failedLocal.push(response.url()); });
    });
    const landing = await context.newPage(); await landing.goto(base + 'index.html');
    assert.equal(await landing.title(), 'LOB, Over');
    assert.equal(await landing.locator('nav a').count(), 5);
    await landing.screenshot({ path: output + 'landing.png' });
    const howto = await context.newPage(); await howto.goto(base + 'howto.html');
    assert.equal(await howto.evaluate(() => document.documentElement.scrollHeight <= innerHeight), true);
    await howto.close();
    const host = await context.newPage(); await host.goto(base + 'host.html');
    await host.waitForFunction(() => !!globalThis.showcase && document.getElementById('connection').textContent === 'Connected');
    assert.equal(await host.locator('#scenario').inputValue(), 'slice_texas');
    await host.locator('#autoplan').check();
    assert.match(await host.locator('#autoplan-status').innerText(), /next at t 0.00/);
    assert.equal(await host.locator('#script option').count(), 5);
    await host.screenshot({ path: output + 'host.png' });
    await host.locator('#room-qr').screenshot({ path: output + 'room-qr.png' });
    const planOpened = context.waitForEvent('page'); await host.locator('#open-plan').click(); const plan = await planOpened;
    const siteOpened = context.waitForEvent('page'); await host.locator('#open-site').click(); const site = await siteOpened;
    for (const page of [plan, site]) await page.waitForFunction(() => typeof state !== 'undefined' && !!state);
    assert.equal(await plan.evaluate(() => state.view.trench), undefined);
    assert.equal(await site.evaluate(() => state.view.budget), undefined);
    const mirror = await context.newPage(); await mirror.goto(plan.url());
    await mirror.waitForFunction(() => document.getElementById('connection').textContent.includes('Mirror'));
    const rejection = await mirror.evaluate(() => new Promise(resolve => {
      socket.addEventListener('message', event => { const message = JSON.parse(event.data); if (message.id === 'mirror-test') resolve(message); });
      socket.send(JSON.stringify({ t: 'cmd', v: 1, id: 'mirror-test', seat: 'plan', action: 'commit', ops: [] }));
    }));
    assert.equal(rejection.reason, 'mirror'); await mirror.close();
    await host.locator('#start').click(); await host.locator('#fast').check();
    await site.getByRole('heading', { name: collapseTitle, exact: true }).waitFor();
    await host.locator('#pause').click();
    assert.equal(await plan.getByText(/collapse/i).count(), 0);
    assert.ok(await site.evaluate(() => cards.size > 0));
    const paused = await host.evaluate(() => showcase.core.state('plan').day);
    await host.waitForTimeout(300); assert.equal(await host.evaluate(() => showcase.core.state('plan').day), paused);
    await plan.screenshot({ path: output + 'plan.png' });
    await site.setViewportSize({ width: 390, height: 844 });
    assert.equal(await site.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    await site.screenshot({ path: output + 'site-phone.png' });
    await host.locator('#resume').click();
    await plan.waitForFunction(() => !!result, null, { timeout: 30_000 });
    const final = await plan.evaluate(() => result);
    assert.equal(final.lost, undefined); assert.ok(final.finishDay > 0);
    assert.equal(await plan.locator('#overlay').isVisible(), true);
    await plan.screenshot({ path: output + 'plan-result.png' });
    const transcript = await host.evaluate(() => showcase.core.transcript);
    assert.equal(transcript.result.finishDay, final.finishDay);
    assert.ok(transcript.messages.some(item => item.message.t === 'event'));
    t.diagnostic(`Local slice finished at t=${final.finishDay}; ${transcript.snapshots.length} snapshots; screenshots: ${output}`);
    // A real second browser context has neither the local channel session nor localStorage.
    await t.test('PeerJS room receives a filtered Site state in a second context', async roomTest => {
      try { await host.waitForFunction(() => document.getElementById('room-status').dataset.ready !== undefined, null, { timeout: 15_000 }); }
      catch { roomTest.skip('PeerJS public server unreachable: signaling did not open within 15 seconds'); return; }
      if (await host.locator('#room-status').getAttribute('data-ready') !== 'true') {
        const status = await host.locator('#room-status').innerText();
        assert.match(status, /server|network|socket|disconnected/i, 'Only unavailable public signaling may be skipped');
        roomTest.skip('PeerJS public server unreachable: ' + status); return;
      }
      const remote = await browser.newContext({ viewport: { width: 390, height: 844 } });
      const phone = await remote.newPage();
      await phone.goto(base + 'site.html?room=' + await host.locator('#room-code').innerText());
      await phone.waitForFunction(() => typeof state !== 'undefined' && !!state, null, { timeout: 20_000 });
      assert.equal(await phone.evaluate(() => state.stage), 'slice_texas');
      assert.equal(await phone.evaluate(() => state.view.budget), undefined);
      await phone.screenshot({ path: output + 'room-phone.png' }); await remote.close();
    });
    // Switching and reset keep existing seat connections, clear result, and select stage scripts.
    for (const stage of ['stage1_texas', 'stage2_texas', 'slice_texas']) {
      await host.locator('#scenario').selectOption(stage);
      await plan.waitForFunction(id => state.stage === id && state.day === 0 && result === null, stage);
      assert.equal(await plan.evaluate(() => state.speed), 'paused');
    }
    await host.locator('#autoplan').uncheck();
    await host.locator('#reset').click(); assert.equal(await host.locator('#autoplan-status').innerText(), 'autoplan: off');
    for (const page of [landing, plan, site]) assert.doesNotMatch(await page.locator('body').innerText(), /well done|good job|should have|right choice|correct answer|behind schedule|ahead of schedule/i);
    assert.deepEqual(errors, []); assert.deepEqual(failedLocal, []); assert.equal(stderr, '');
  } finally {
    await browser?.close();
    process.chdir(originalCwd);
    if (server.exitCode === null) { const exited = once(server, 'exit'); server.kill(); await exited; }
  }
});
