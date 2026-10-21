import test from 'node:test';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { mkdir, readFile } from 'node:fs/promises';
import { chromium } from 'playwright';
import { startHost } from '../src/host/server.js';

const scenarioPath = fileURLToPath(new URL('../../Data/Scenarios/level1_texas.json', import.meta.url));
const output = fileURLToPath(new URL('../out/play/', import.meta.url));
const forbidden = /\b(money|budget|inbox|catalog|codes|radio log|collapse|conflict|arguing|rain|trench wall|correct|wrong|score|mistake|well done|good job|should have)\b/i;
async function clickReply(page, selector) {
  const response = page.evaluate(() => new Promise(resolve => {
    const listener = event => {
      const message = JSON.parse(event.data);
      if (['ack', 'nack'].includes(message.t)) { socket.removeEventListener('message', listener); resolve(message); }
    };
    socket.addEventListener('message', listener);
  }));
  await page.locator(selector).click(); return response;
}
async function fits(page) {
  assert.deepEqual(await page.evaluate(() => ({
    horizontal: document.documentElement.scrollWidth > innerWidth,
    vertical: document.documentElement.scrollHeight > innerHeight,
    clipped: [...document.querySelectorAll('.crew-card,.pm,.reports,.board')].some(element => {
      const box = element.getBoundingClientRect();
      return box.left < 0 || box.top < 0 || box.right > innerWidth || box.bottom > innerHeight;
    }),
    reportClipped: [...document.querySelectorAll('.report')].some(item => item.scrollHeight > item.clientHeight || item.scrollWidth > item.clientWidth),
  })), { horizontal: false, vertical: false, clipped: false, reportClipped: false });
  assert.doesNotMatch(await page.locator('body').innerText(), forbidden);
}
async function screenshots(page, name) {
  for (const [width, height] of [[1366, 768], [1920, 1080]]) {
    await page.setViewportSize({ width, height }); await fits(page);
    await page.waitForFunction(() => !document.getAnimations().some(animation => animation.playState === 'running'));
    await page.screenshot({ path: `${output}${name}-${width}.png` });
  }
  await page.setViewportSize({ width: 1366, height: 768 });
}

test('Level 1 office: reports, audio, switches, slow clock, PM, result and replay at both desktop sizes', { timeout: 90_000 }, async () => {
  // An ephemeral port keeps Bryan's live host and primary Plan seat untouched.
  const host = await startHost({ scenarioPath, port: 0, testMode: true }); let browser;
  try {
    assert.notEqual(host.port, 7331);
    await mkdir(output, { recursive: true });
    browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({ viewport: { width: 1366, height: 768 } });
    const plan = await context.newPage(), site = await context.newPage(), errors = [], failedLocal = [];
    for (const [name, page] of [['plan', plan], ['site', site]]) {
      page.on('pageerror', error => errors.push(`${name}: ${error.message}`));
      page.on('response', response => { if (response.status() >= 400) failedLocal.push(response.url()); });
    }
    // Observe the real browser audio graph and actual WAV playback without replacing them.
    await plan.addInitScript(() => {
      window.voiceAudio = []; window.uiContexts = []; window.uiGains = []; window.soundStarts = 0;
      window.Audio = new Proxy(window.Audio, { construct(target, args) {
        const audio = Reflect.construct(target, args); voiceAudio.push(audio); return audio;
      } });
      window.AudioContext = new Proxy(window.AudioContext, { construct(target, args) {
        const context = Reflect.construct(target, args); uiContexts.push(context);
        for (const method of ['createOscillator', 'createBufferSource']) {
          const create = context[method].bind(context);
          context[method] = (...args) => {
            const source = create(...args), start = source.start.bind(source);
            source.start = (...args) => { soundStarts++; return start(...args); }; return source;
          };
        }
        const createGain = context.createGain.bind(context);
        context.createGain = () => { const gain = createGain(); uiGains.push(gain); return gain; };
        return context;
      } });
    });
    await plan.goto(`http://127.0.0.1:${host.port}/play.html`);
    await plan.waitForFunction(() => typeof state !== 'undefined' && !!state);
    assert.deepEqual(await plan.evaluate(() => [uiContexts.length, voiceAudio.length, soundStarts]), [0, 0, 0]);
    assert.equal(await plan.locator('#board-title').innerText(), "The PM's plan: promised Day 18");
    assert.equal(await plan.locator('[data-kind="plan"]').count(), 3);
    assert.equal(await plan.locator('#reported-lines polyline').count(), 0);
    assert.equal(await plan.locator('#clock').innerText(), 'Paused');
    assert.equal(await plan.locator('#site-status').innerText(), 'Waiting for the headset');
    assert.equal(await plan.locator('#pm-portrait').evaluate(img => img.complete && img.naturalWidth > 0), true);
    await screenshots(plan, 'before-start');
    await plan.evaluate(() => {
      window.received = [];
      socket.addEventListener('message', event => received.push(JSON.parse(event.data)));
    });
    await site.goto(`http://127.0.0.1:${host.port}/site.html`); await site.waitForFunction(() => !!state);
    await plan.waitForFunction(() => document.querySelector('#site-status').textContent === 'Site connected in a browser');
    assert.equal((await clickReply(plan, '#start')).t, 'ack');
    await plan.waitForFunction(() => document.querySelector('#pm-bubble').textContent.startsWith('Morning.'));
    assert.equal(await plan.locator('#pm-bubble').innerText(), await plan.evaluate(() => received.find(m => m.t === 'voice').turns[0].text));
    assert.match(await plan.locator('#pm-portrait').getAttribute('src'), /explaining_Final/);
    assert.equal(await plan.locator('#clock').innerText(), 'Running');
    assert.equal(await plan.locator('#ruler').isVisible(), true);
    await plan.waitForFunction(() => uiContexts[0]?.state === 'running' && soundStarts > 0);
    await fits(plan);
    await plan.locator('#mute').click();
    assert.equal(await plan.evaluate(() => uiGains[0].gain.value), 0);
    assert.equal(await plan.evaluate(() => voiceAudio.at(-1).muted), true);
    const soundsBeforeHold = await plan.evaluate(() => soundStarts);
    assert.equal((await clickReply(plan, '[data-crew="back"]')).t, 'ack');
    await plan.waitForFunction(() => state.view.held.back && document.querySelector('[data-crew="back"]').getAttribute('aria-checked') === 'true');
    assert.equal(await plan.evaluate(() => soundStarts), soundsBeforeHold);
    assert.equal(await plan.locator('#pm-bubble').isVisible(), true);
    assert.equal(await plan.locator('#order-back').innerText(), 'Holding since Day 1');
    await plan.locator('#mute').click();
    assert.ok(await plan.evaluate(() => uiGains[0].gain.value > 0));
    assert.equal(await plan.evaluate(() => voiceAudio.at(-1).muted), false);
    host.core.setFast(true);
    await plan.waitForFunction(() => state.speed === 'slow');
    host.core.setFast(false);
    assert.equal(await plan.locator('#clock').innerText(), 'Field is slow today');
    assert.match(await plan.locator('#pm-portrait').getAttribute('src'), /worriedSurprise/);
    assert.doesNotMatch(await plan.locator('body').innerText(), forbidden);
    await plan.waitForFunction(() => !document.querySelector('#pm-portrait').src.endsWith('worriedSurprise.png'), null, { timeout: 5000 });
    assert.equal(await plan.locator('#clock').innerText(), 'Field is slow today');
    host.core.setFast(true);
    await plan.waitForFunction(() => state.day >= 8); host.core.pause();
    await plan.waitForFunction(() => state.speed === 'paused' && typing.size === 0);
    assert.equal(await plan.locator('#reports .report').count(), 5);
    const reports = await plan.evaluate(() => received.filter(m => m.t === 'report'));
    const last = reports.at(-1), prior = reports.at(-2);
    const format = value => Math.round(value).toLocaleString('en-US');
    assert.equal(await plan.locator('#reports .report').last().textContent(), `Day ${last.day} · ` + ['exc', 'pipe', 'back'].map((id, i) =>
      `${['Excavation', 'Pipe', 'Backfill'][i]} +${format(last.feetByCrew[id] - prior.feetByCrew[id])} ft (${format(last.feetByCrew[id])})`).join(' · '));
    assert.ok(await plan.locator('.ink-segment').count() > 0);
    assert.equal(await plan.locator('.ink-segment').first().evaluate(line => getComputedStyle(line).animationDuration), '0.4s');
    assert.equal(await plan.locator('#ruler').evaluate(line => getComputedStyle(line).transitionDuration), '0.4s');
    await screenshots(plan, 'mid-run');
    const beforeGo = await plan.evaluate(() => soundStarts);
    assert.equal((await clickReply(plan, '[data-crew="back"]')).t, 'ack');
    await plan.waitForFunction(() => document.querySelector('[data-crew="back"]').getAttribute('aria-checked') === 'false');
    assert.ok(await plan.evaluate(() => soundStarts) >= beforeGo + 4);
    assert.equal(await plan.locator('#order-back').innerText(), 'Go');
    host.core.resume(); await plan.waitForFunction(() => !!result, null, { timeout: 20_000 });
    assert.match(await plan.locator('#finish').innerText(), /Finished Day \d+ · Promised Day 18/);
    assert.equal(await plan.locator('#result').evaluate(stamp => getComputedStyle(stamp).animationDuration), '0.3s');
    await plan.waitForFunction(() => ['exc', 'pipe', 'back'].every(id => Number(document.getElementById('idle-' + id).textContent) === Number(result.idleDays[id].toFixed(2))));
    assert.equal(await plan.locator('#start').innerText(), 'Run again');
    const first = await plan.evaluate(() => result);
    assert.ok(first.lines.actual.back.length > 2);
    assert.equal(await plan.locator('[data-kind="actual"]').count(), 3);
    assert.doesNotMatch(JSON.stringify(first), /"(?:budgetUsd|spentUsd|changeLog|events)":/);
    await site.waitForFunction(() => !!result);
    assert.deepEqual(await site.evaluate(() => Object.keys(result).sort()), ['stage', 't', 'v']);
    assert.equal(await site.locator('#listen-lines').innerText(), '');
    const pdf = await context.request.get(`http://127.0.0.1:${host.port}/level1/rules.pdf`);
    assert.equal(pdf.status(), 200); assert.match(pdf.headers()['content-type'], /application\/pdf/);
    assert.equal((await context.request.get(`http://127.0.0.1:${host.port}/level1/rules.html`)).status(), 200);
    await screenshots(plan, 'result');
    assert.equal((await clickReply(plan, '#start')).t, 'ack');
    await plan.waitForFunction(() => !result && state.day < 1 && state.speed !== 'paused');
    assert.equal(await plan.locator('#day').innerText(), '1');
    assert.equal(await plan.locator('[data-kind="previous"]').count(), 3);
    assert.deepEqual(await plan.locator('[data-kind="previous"]').evaluateAll(lines => lines.map(line => line.getAttribute('opacity'))), ['0.25', '0.25', '0.25']);
    assert.deepEqual(await plan.evaluate(() => state.view.previousRun), first.lines.actual);
    await screenshots(plan, 'run-again');
    host.core.setFast(true);
    await site.waitForFunction(() => state.view.crews.some(c => c.status === 'arguing'));
    host.core.setFast(false);
    await plan.waitForFunction(() => state.speed === 'slow');
    assert.equal(await plan.locator('#clock').innerText(), 'Field is slow today');
    const refusal = await clickReply(plan, '[data-crew="exc"]');
    assert.equal(refusal.reason, "The field won't take that order right now");
    assert.equal(await plan.locator('#refusal-exc').innerText(), refusal.reason);
    assert.equal(await plan.locator('[data-crew="exc"]').getAttribute('aria-checked'), 'false');
    assert.equal(await plan.locator('#order-exc').innerText(), 'Go');
    assert.equal(await plan.locator('#refusal-exc').evaluate(tape => getComputedStyle(tape).backgroundColor), 'rgb(173, 64, 54)');
    await screenshots(plan, 'refused-switch');
    await plan.waitForFunction(() => document.querySelector('#refusal-exc').hidden, null, { timeout: 4000 });
    assert.equal((await clickReply(plan, '[data-crew="back"]')).t, 'ack');
    assert.deepEqual(errors, []); assert.deepEqual(failedLocal, []);
    const source = await readFile(new URL('../web/play.html', import.meta.url), 'utf8');
    assert.doesNotMatch(source, forbidden);
    console.log('Start -> Hold -> Go -> result -> Run again; slow clock, 3s refusal rollback, PM text/audio/mute, 400ms ink, five reports, 25% previous lines, PDF and filtered result verified');
    console.log('1366x768 and 1920x1080: no scrollbars; screenshots saved to Monitor/out/play/');
  } finally { await browser?.close(); await host.close(); }
});
