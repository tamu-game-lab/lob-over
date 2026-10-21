import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdir, readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright';
import { startHost } from '../src/host/server.js';

const output = fileURLToPath(new URL('../out/guided/', import.meta.url));
const guidePath = fileURLToPath(new URL('../../Data/Guides/slice_guided.json', import.meta.url));
const guide = JSON.parse(await readFile(guidePath, 'utf8'));
const script = JSON.parse(await readFile(new URL('../test/scripts/slice_play.json', import.meta.url), 'utf8'));
const beatFor = time => time <= 3.8 ? 0 : time <= 4 ? 1 : time < 5 ? 2 : time < 7 ? 3 : time < 8 ? 4 : time < 9 ? 5 : time < 20 ? 6 : 7;

async function clickAck(page, locator) {
  await locator.waitFor({ state: 'visible' });
  const reply = page.evaluate(() => new Promise(resolve => {
    const listener = event => {
      const message = JSON.parse(event.data);
      if (['ack', 'nack'].includes(message.t)) { socket.removeEventListener('message', listener); resolve(message); }
    };
    socket.addEventListener('message', listener);
  }));
  try { await locator.click(); } catch (error) { reply.catch(() => {}); throw error; }
  const message = await reply;
  assert.equal(message.t, 'ack', message.reason); return message;
}
async function perform(pages, { seat, cmd: source }) {
  const cmd = structuredClone(source);
  const page = pages[seat];
  if (seat === 'plan') {
    for (const op of cmd.ops) {
      if (op.op === 'enterPermit' || op.op === 'enterInspection') {
        const permit = op.op === 'enterPermit';
        await page.locator('#key-zone').selectOption(String(op.zone));
        await page.locator(permit ? '#permit-code' : '#inspection-number').fill(permit ? op.code : op.number);
        await clickAck(page, page.locator(permit ? '#enter-permit' : '#enter-inspection')); return;
      }
      if (op.op === 'lend') {
        await page.locator('#lend-machine').selectOption(op.machine);
        await page.locator('#lend-crew').selectOption(op.toCrew);
        await page.locator('#lend-days').fill(String(op.days)); await page.locator('#add-lend').click();
      } else if (op.op === 'hold') {
        await page.locator('#hold-crew').selectOption(op.crew);
        await page.locator('#hold-days').fill(String(op.days)); await page.locator('#add-hold').click();
      } else if (op.op === 'radio') {
        await clickAck(page, page.getByRole('button', { name: 'Radio again', exact: true })); return;
      } else throw new Error('Unhandled plan op: ' + op.op);
    }
    await clickAck(page, page.locator('#commit')); return;
  }
  const controls = { mount: '#mount', dismount: '#mount', listen: '#listen', stopWork: '#stop-work' };
  if (cmd.action === 'moveTo') {
    // The transcript's 9+00 visit means the excavation crew. Paused decisions can
    // land a shift earlier, so follow the beat prompt to that crew's live station.
    if (cmd.id === 'c-11') cmd.stationFt = await page.evaluate(() => state.view.crews.find(crew => crew.id === 'exc').stationFt);
    const before = await page.evaluate(() => ({ day: state.day, ft: state.view.truck.stationFt }));
    await page.locator('#destination').fill(String(cmd.stationFt)); await clickAck(page, page.locator('#move'));
    await page.waitForFunction(ft => !state.view.truck.moving && state.view.truck.stationFt === ft, cmd.stationFt);
    const after = await page.evaluate(() => ({ day: state.day, speed: state.speed }));
    assert.ok(Math.abs(after.day - before.day - Math.abs(cmd.stationFt - before.ft) / 1500 / 24) < 1e-8);
    assert.equal(after.speed, 'paused');
  } else if (cmd.action === 'inspect') await clickAck(page, page.locator(`[data-inspect="${cmd.target}"]`));
  else if (cmd.action === 'confirm') await clickAck(page, page.locator(`button[data-order="${cmd.orderId}"][data-outcome="${cmd.outcome}"]`));
  else if (controls[cmd.action]) await clickAck(page, page.locator(controls[cmd.action]));
  else throw new Error('Unhandled site action: ' + cmd.action);
}

test('Guided slice: three filtered prompts, script actions, Ready, all beat screenshots and result', { timeout: 180_000 }, async t => {
  await mkdir(output, { recursive: true });
  const host = await startHost({ scenarioPath: fileURLToPath(new URL('../../Data/Scenarios/slice_texas.json', import.meta.url)),
    guidePath, port: 0, testMode: true });
  let browser;
  try {
    browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({ viewport: { width: 1400, height: 1000 } });
    context.setDefaultTimeout(10_000);
    const pages = {}, errors = [];
    for (const seat of ['plan', 'site', 'rules', 'facilitator']) {
      const page = pages[seat] = await context.newPage();
      page.on('pageerror', error => errors.push(seat + ': ' + error.message));
      await page.goto(`http://127.0.0.1:${host.port}/${seat}.html`);
      await page.waitForFunction(() => typeof state !== 'undefined' && !!state);
    }
    await pages.rules.waitForFunction(() => document.querySelectorAll('#manual-content section').length === 8);
    assert.equal(await pages.rules.locator('#cards .card').count(), 4);
    assert.equal(await pages.rules.locator('#day-boxes input').count(), 8);
    await pages.rules.getByLabel('Permit day zone 1', { exact: true }).fill('1');
    await clickAck(pages.facilitator, pages.facilitator.locator('#start'));
    host.core.setFast(true);
    for (const [index, beat] of guide.beats.entries()) {
      for (const seat of ['plan', 'site', 'rules']) {
        const page = pages[seat];
        await page.waitForFunction(id => LobGuide.current?.beat?.id === id, beat.id, { timeout: 30_000 });
        assert.equal(await page.locator('#guide-prompt').innerText(), beat.prompts[seat]);
        const message = await page.evaluate(() => LobGuide.current);
        assert.equal(message.narration, undefined); assert.equal(message.prompts, undefined);
        for (const other of ['plan', 'site', 'rules'].filter(other => other !== seat)) assert.equal(await page.getByText(beat.prompts[other], { exact: true }).count(), 0);
        assert.equal(await page.getByText(beat.narration, { exact: true }).count(), 0);
        await page.evaluate(() => window.scrollTo(0, 0));
        await page.screenshot({ path: output + `${index}-${beat.id}-${seat}.png` });
      }
      assert.equal(await pages.facilitator.locator('#guide-narration').innerText(), beat.narration);
      assert.equal(await pages.rules.locator(`#sections [data-section="${beat.rulesSection}"]`).getAttribute('data-highlight'), 'true');
      assert.equal(await pages.plan.evaluate(() => state.view.trench), undefined);
      assert.equal(await pages.site.evaluate(() => state.view.budget), undefined);
      assert.deepEqual(await pages.rules.evaluate(() => Object.keys(state.view).sort()), ['displayDay', 'speed', 'stage']);
      for (const entry of script.filter(entry => beatFor(entry.t) === index)) await perform(pages, entry);
      for (const seat of ['plan', 'site', 'rules']) {
        await clickAck(pages[seat], pages[seat].locator('#guide-ready'));
        assert.equal(await pages[seat].locator('#guide-ready').innerText(), 'Waiting for the others');
      }
      t.diagnostic(`${beat.id}: prompts, actions and Ready checked`);
    }
    await pages.facilitator.waitForFunction(() => LobGuide.current.beats.every(beat => beat.status === 'done'));
    const final = await pages.plan.evaluate(() => result);
    assert.ok(final.finishDay > 0); assert.equal(final.lost, undefined);
    assert.equal(await pages.plan.evaluate(() => state.view.radioLog.find(order => order.op.op === 'lend').status), 'confirmed');
    assert.equal(await pages.site.evaluate(() => result.spentUsd), undefined);
    assert.equal(await pages.plan.locator('#overlay').isVisible(), true);
    assert.equal(await pages.rules.getByLabel('Permit day zone 1', { exact: true }).inputValue(), '1');
    await pages.rules.setViewportSize({ width: 390, height: 844 });
    assert.equal(await pages.rules.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    assert.deepEqual(errors, []);
    t.diagnostic(`Guided result t=${final.finishDay}; 27 screenshots in ${output}`);
  } finally { await browser?.close(); await host.close(); }
});
