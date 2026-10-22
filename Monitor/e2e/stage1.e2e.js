import test from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { once } from 'node:events';
import { mkdir, readFile, readdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright';
import { assertChart } from './chart-assertions.js';

const root = fileURLToPath(new URL('../../', import.meta.url));
const output = fileURLToPath(new URL('../out/e2e/', import.meta.url));
const scenario = JSON.parse(await readFile(new URL('../../Data/Scenarios/stage1_texas.json', import.meta.url), 'utf8'));

async function clickAck(page, locator) {
  const reply = page.evaluate(() => new Promise(resolve => {
    const listener = event => {
      const message = JSON.parse(event.data);
      if (message.t === 'ack' || message.t === 'nack') { socket.removeEventListener('message', listener); resolve(message); }
    };
    socket.addEventListener('message', listener);
  }));
  await locator.click();
  const message = await reply;
  assert.equal(message.t, 'ack', message.reason);
  return message;
}
async function hold(plan, days) {
  await plan.locator('#hold-crew').selectOption('back');
  await plan.locator('#hold-days').fill(String(days));
  await plan.locator('#add-hold').click();
}
async function ride(site, ft) {
  if (await site.evaluate(() => state.view.truck.dismounted)) await clickAck(site, site.locator('#mount'));
  await site.locator('#destination').fill(`${Math.floor(ft / 100)}+${String(Math.round(ft % 100)).padStart(2, '0')}`);
  await clickAck(site, site.locator('#move'));
  await site.waitForFunction(() => !state.view.truck.moving);
  await clickAck(site, site.locator('#mount'));
  assert.equal(await site.evaluate(() => state.view.truck.dismounted), true);
}
async function signInspection(plan, card) {
  await plan.waitForFunction(zone => state.view.inbox.some(item => item.subject === `Zone ${zone} compaction`), card.zone, { timeout: 40_000 });
  await plan.locator('#key-zone').selectOption(String(card.zone));
  await plan.locator('#inspection-number').fill(card.number);
  await clickAck(plan, plan.locator('#enter-inspection'));
  console.log(`Inspection posted: zone ${card.zone}`);
}

test('Stage 1: three-seat field loop, daily report, argument and final overlay', { timeout: 180_000 }, async t => {
  await mkdir(output, { recursive: true });
  const before = new Set(await readdir(new URL('../out/', import.meta.url)));
  const host = spawn(process.execPath, ['Monitor/src/host/server.js', 'Data/Scenarios/stage1_texas.json', '--port', '0'], {
    cwd: root, env: { ...process.env, INIT_CWD: root, NODE_ENV: 'test' }, windowsHide: true,
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  let stderr = '', browser;
  host.stderr.on('data', data => { stderr += data; });
  try {
    const port = await new Promise((resolve, reject) => {
      let stdout = '';
      host.stdout.on('data', data => { stdout += data; const match = stdout.match(/listening on (\d+)/); if (match) resolve(Number(match[1])); });
      host.once('error', reject);host.once('exit', code => reject(new Error(`Host exited ${code}: ${stderr}`)));
    });
    browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({ viewport: { width: 1280, height: 800 } });
    const pages = {}, errors = [];
    for (const name of ['plan', 'site', 'facilitator']) {
      const page = await context.newPage();pages[name] = page;
      page.on('pageerror', error => errors.push(`${name}: ${error.message}`));
      await page.goto(`http://127.0.0.1:${port}/${name}.html`);
      await page.waitForFunction(() => !!state);
    }
    const { plan, site, facilitator } = pages;
    await assertChart(plan);
    // WO-L7: dragging start days follows the horizontal time axis and still snaps to whole days.
    const handle = plan.locator('.start-handle[data-crew="back"]');
    const target = await handle.evaluate(element => {
      const chart = document.getElementById('chart'), matrix = chart.getScreenCTM();
      const start = new DOMPoint(Number(element.getAttribute('cx')), Number(element.getAttribute('cy'))).matrixTransform(matrix);
      const end = new DOMPoint(58 + 6 / (state.view.budget.deadlineDay - 1) * 660, Number(element.getAttribute('cy'))).matrixTransform(matrix);
      return { start: { x: start.x, y: start.y }, end: { x: end.x, y: end.y } };
    });
    await plan.mouse.move(target.start.x, target.start.y); await plan.mouse.down();
    await plan.mouse.move(target.end.x, target.end.y); await plan.mouse.up();
    assert.deepEqual(await plan.evaluate(() => draft.at(-1)), { op: 'setStart', crew: 'back', day: 7 });
    assert.equal(await plan.evaluate(() => getSelection().toString()), '');
    await plan.locator('#clear-draft').click();
    assert.equal(await facilitator.locator('#speed').innerText(), 'paused');
    await clickAck(facilitator, facilitator.locator('#start'));
    await ride(site, 720);
    assert.ok(await site.evaluate(() => state.displayDay < 6));
    await site.getByRole('heading', { name: 'Trench wall collapse at 7+20', exact: true }).waitFor({ timeout: 35_000 });
    assert.equal(await plan.getByText(/collapse/i).count(), 0);
    assert.equal(await plan.locator('#inbox .item').first().getAttribute('data-kind'), 'report');
    await plan.screenshot({ path: `${output}/plan-report.png` });
    await assertChart(plan);
    await site.screenshot({ path: `${output}/site-event.png` });
    await clickAck(site, site.locator('#stop-work'));
    await plan.locator('#lend-machine').selectOption('excavator_standard');
    await plan.locator('#lend-crew').selectOption('pipe');
    await plan.locator('#lend-days').fill('2');
    await plan.locator('#add-lend').click();
    await hold(plan, 2);
    await clickAck(plan, plan.locator('#commit'));
    await plan.waitForFunction(() => document.getElementById('speed').textContent === '5 s per day');
    await plan.waitForFunction(() => state.view.radioLog.find(o => o.op.op === 'lend')?.status === 'landed', null, { timeout: 10_000 });
    const lendId = await plan.evaluate(() => state.view.radioLog.find(o => o.op.op === 'lend').id);
    await ride(site, await site.evaluate(() => state.view.crews.find(c => c.id === 'exc').stationFt));
    await clickAck(site, site.locator(`[data-order="${lendId}"][data-outcome="notDone"]`));
    await clickAck(plan, plan.getByRole('button', { name: 'Radio again', exact: true }));
    await clickAck(site, site.locator(`[data-order="${lendId}"][data-outcome="done"]`));
    await ride(site, await site.evaluate(() => state.view.crews.find(c => c.id === 'back').stationFt));
    const holdId = await plan.evaluate(() => state.view.radioLog.find(o => o.op.op === 'hold').id);
    await clickAck(site, site.locator(`[data-order="${holdId}"][data-outcome="done"]`));
    const reportDay = await plan.evaluate(() => lastReport?.day ?? 0);
    await plan.waitForFunction(day => lastReport?.day > day, reportDay, { timeout: 10_000 });
    assert.equal(await plan.evaluate(() => lastReport.feetByCrew.pipe), 720);
    assert.equal(await plan.evaluate(() => state.view.reportedLines.pipe.at(-1).ft), 720);
    t.diagnostic(`Next daily report: pipe 720 ft, day ${await plan.evaluate(() => lastReport.day)}`);
    console.log('Field loop complete; next report pipe 720 ft');

    // WO-03 includes hold points: the paper seat supplies each authored card when its report lands.
    await signInspection(plan, scenario.inspections.cards[0]);
    await site.waitForFunction(() => state.view.crews.filter(c => c.status === 'arguing').length === 2, null, { timeout: 15_000 });
    await hold(plan, 3);
    await clickAck(plan, plan.locator('#commit'));
    console.log('Argument addressed with hold back 3; starting fast pacing');
    await facilitator.evaluate(() => control('fast'));
    for (const card of scenario.inspections.cards.slice(1)) await signInspection(plan, card);
    await plan.waitForFunction(() => !!result, null, { timeout: 30_000 });
    const final = await plan.evaluate(() => result);
    assert.ok(final.finishDay > 35 && final.finishDay < 46, JSON.stringify(final));
    assert.equal(final.lost, undefined);
    assert.equal(await plan.locator('#overlay').isVisible(), true);
    assert.ok(await plan.locator('#chart polyline').count() >= 6);
    await assertChart(plan);
    assert.equal(await plan.locator('#changes .item').count(), final.changeLog.length);
    for (const [name, page] of Object.entries(pages)) {
      await page.evaluate(() => window.scrollTo(0, 0));
      await page.screenshot({ path: `${output}/${name}.png` });
      assert.doesNotMatch(await page.locator('body').innerText(), /correct|wrong|well done|good job|score|mistake|should have/i, name);
    }
    await site.setViewportSize({ width: 390, height: 844 });
    assert.equal(await site.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    await site.screenshot({ path: `${output}/site-phone.png` });
    const transcript = await (await context.request.get(`http://127.0.0.1:${port}/transcript.json`)).json();
    assert.equal(transcript.scenario, scenario.id);
    assert.equal(transcript.result.finishDay, Number(final.finishDay.toFixed(3)));
    assert.ok(transcript.snapshots.length > 35);
    assert.ok(transcript.messages.some(m => m.message.t === 'result'));
    const written = (await readdir(new URL('../out/', import.meta.url))).filter(name => !before.has(name) && name.startsWith('stage1_texas.') && name.endsWith('.json'));
    assert.equal(written.length, 1);
    assert.deepEqual(errors, []);
    assert.equal(stderr, '');
    t.diagnostic(`finishDay=${final.finishDay}; lost=absent; ${transcript.snapshots.length} snapshots; desktop and phone screenshots saved`);
  } finally {
    await browser?.close();
    if (host.exitCode === null) { const exited = once(host, 'exit');host.kill();await exited; }
  }
});
