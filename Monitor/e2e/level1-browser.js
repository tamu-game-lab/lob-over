import assert from 'node:assert/strict';

export async function level1Browser(browser, base, output, rooms, t) {
  const planContext = await browser.newContext({ viewport: { width: 1366, height: 768 } });
  const siteContext = await browser.newContext({ viewport: { width: 390, height: 844 } });
  const errors = [], failed = [], voices = [];
  try {
    for (const context of [planContext, siteContext]) {
      await rooms.configure(context);
      context.on('page', page => {
        page.on('pageerror', error => errors.push(error.message));
        page.on('response', response => {
          if (response.url().startsWith(base) && response.status() >= 400) failed.push(response.url());
          if (response.url().includes('/Voice/level1_texas/') && response.status() === 200) voices.push(response.url());
        });
      });
    }
    for (const path of ['play.html', 'Data/Scenarios/level1_texas.json', 'img/clipboard_Final.png',
      'level1/rules.pdf', 'Voice/level1_texas/pm_open_level1_1.wav']) {
      assert.equal((await planContext.request.get(base + path)).status(), 200, path);
    }
    const plan = await planContext.newPage(), site = await siteContext.newPage();
    await plan.clock.install();
    await plan.goto(base + 'play.html?host=browser');
    await plan.waitForFunction(() => !!state && document.querySelector('#room-status').dataset.ready === 'true');
    assert.equal(await plan.evaluate(() => state.stage), 'level1_texas');
    assert.equal(await plan.locator('#clock').innerText(), 'Paused');
    assert.equal(await plan.locator('#browser-banner').innerText(), 'The headset joins only a game started with Play LOB Over on a PC or Mac');
    assert.equal(await plan.locator('#site-status').innerText(), 'Waiting for the Site browser');
    assert.equal(await plan.locator('#pm-portrait').evaluate(img => img.complete && img.naturalWidth > 0), true);
    const room = await plan.locator('#room-code').innerText();
    assert.match(room, /^[A-HJ-NP-Z2-9]{6}$/);
    const url = await plan.locator('#room-link').getAttribute('href');
    assert.equal(url, base + 'site.html?room=' + room);
    await plan.locator('#browser-room summary').click();
    assert.equal(await plan.locator('#room-qr').isVisible(), true);
    assert.equal(await plan.locator('#room-qr').evaluate(canvas => canvas.width > 0 && canvas.getContext('2d').getImageData(0, 0, canvas.width, canvas.height).data.some(value => value !== 0)), true);
    await plan.screenshot({ path: output + 'level1-room.png' });
    await plan.locator('#browser-room summary').click();
    await site.goto(url);
    await site.waitForFunction(() => !!state);
    assert.equal(await site.evaluate(() => localStorage.getItem('lob-over-run')), null);
    assert.equal(await site.evaluate(() => state.stage), 'level1_texas');
    await plan.waitForFunction(() => state.view.siteClient === 'web');
    assert.equal(await plan.locator('#site-status').innerText(), 'Site connected in a browser');
    assert.equal(await plan.evaluate(() => state.view.trench), undefined);
    assert.equal(await site.evaluate(() => state.view.budget), undefined);
    assert.equal(await site.evaluate(() => state.view.plan), undefined);
    assert.equal(await site.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    // Unlock actual Site audio before the host sends its first voice exchange.
    await site.locator('#mount').click();
    await plan.locator('#start').click();
    await plan.waitForFunction(() => state.speed !== 'paused');
    await site.waitForFunction(() => state.speed !== 'paused');
    await plan.waitForFunction(() => document.querySelector('#pm-bubble').textContent.startsWith('Morning.'));
    await plan.locator('[data-crew="back"]').click();
    await plan.waitForFunction(() => state.view.held.back);
    assert.equal(await plan.locator('[data-crew="back"]').getAttribute('aria-checked'), 'true');
    // Advance the normal host clock; production exposes no test pacing controls.
    for (let i = 0; i < 12 && await plan.evaluate(() => state.day < 8); i++) await plan.clock.runFor(10_000);
    assert.ok(await plan.evaluate(() => state.day >= 8));
    await plan.locator('[data-crew="back"]').click();
    await plan.waitForFunction(() => !state.view.held.back);
    assert.equal(await plan.locator('[data-crew="back"]').getAttribute('aria-checked'), 'false');
    for (let i = 0; i < 30 && !(await plan.evaluate(() => !!result)); i++) await plan.clock.runFor(10_000);
    assert.equal(await plan.locator('#result').isVisible(), true);
    assert.match(await plan.locator('#finish').innerText(), /Finished Day \d+ · Promised Day 18/);
    await site.waitForFunction(() => !!result);
    assert.deepEqual(await site.evaluate(() => Object.keys(result).sort()), ['stage', 't', 'v']);
    assert.equal(await plan.locator('#start').innerText(), 'Run again');
    assert.ok(rooms.forwarded.includes('OFFER') && rooms.forwarded.includes('ANSWER'));
    assert.ok(voices.some(url => url.endsWith('/pm_open_level1_1.wav')));
    assert.ok(voices.some(url => !url.split('/').at(-1).startsWith('pm_')), 'Site voice served from the Pages subpath');
    for (const [width, height] of [[1366, 768], [1920, 1080]]) {
      await plan.setViewportSize({ width, height });
      assert.equal(await plan.evaluate(() => document.documentElement.scrollWidth <= innerWidth && document.documentElement.scrollHeight <= innerHeight), true);
      await plan.screenshot({ path: output + `level1-browser-${width}.png` });
    }
    await plan.locator('#start').click();
    await plan.waitForFunction(() => !result && state.day < 1 && state.speed !== 'paused');
    await site.waitForFunction(() => !result && state.day < 1);
    assert.equal(await plan.locator('#room-code').innerText(), room);
    assert.deepEqual(errors, []); assert.deepEqual(failed, []);
    t.diagnostic('Level 1 browser host: Start -> Hold -> Go -> result -> Run again; real PeerJS/WebRTC Site in a second context, QR, filtered seats, voices, Pages assets and both desktop sizes passed');
  } finally {
    await siteContext.close(); await planContext.close();
  }
}
