import test from 'node:test';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { mkdir } from 'node:fs/promises';
import { chromium } from 'playwright';
import { startHost } from '../src/host/server.js';

const scenarioPath=fileURLToPath(new URL('../../Data/Scenarios/level1_texas.json',import.meta.url));
const output=fileURLToPath(new URL('../out/level1/',import.meta.url));
async function clickReply(page, selector) {
  const response=page.evaluate(()=>new Promise(resolve=>{
    const listener=event=>{const m=JSON.parse(event.data);if(['ack','nack'].includes(m.t)){socket.removeEventListener('message',listener);resolve(m);}};
    socket.addEventListener('message',listener);
  }));
  await page.locator(selector).click();return response;
}
test('Level 1 page: Start, Hold, Go, result, Run again, browser Site status and argument rejection', {timeout:60000}, async () => {
  const host=await startHost({scenarioPath,port:0,testMode:true});let browser;
  try {
    browser=await chromium.launch({headless:true});
    const context=await browser.newContext({viewport:{width:1280,height:1000}});
    const plan=await context.newPage(),site=await context.newPage(),errors=[];
    for(const [name,page]of [['plan',plan],['site',site]]) page.on('pageerror',e=>errors.push(`${name}: ${e.message}`));
    await plan.goto(`http://127.0.0.1:${host.port}/play.html`);await plan.waitForFunction(()=>!!state);
    assert.equal(await plan.locator('#site-status').innerText(),'Waiting for the headset');
    await site.goto(`http://127.0.0.1:${host.port}/site.html`);await site.waitForFunction(()=>!!state);
    await plan.waitForFunction(()=>document.querySelector('#site-status').textContent==='Site connected in a browser');
    assert.equal((await clickReply(plan,'#start')).t,'ack');
    assert.equal((await clickReply(plan,'[data-crew="back"]')).t,'ack');
    await plan.waitForFunction(()=>document.querySelector('[data-crew="back"]').textContent==='Go');
    host.core.setFast(true);
    await plan.waitForFunction(()=>state.day>=8);host.core.pause();
    assert.equal((await clickReply(plan,'[data-crew="back"]')).t,'ack');
    await plan.waitForFunction(()=>document.querySelector('[data-crew="back"]').textContent==='Hold');
    host.core.resume();await plan.waitForFunction(()=>!!result,null,{timeout:20000});
    assert.match(await plan.locator('#finish').innerText(),/Finished Day \d+ · Promised Day 18/);
    assert.match(await plan.locator('#idle').innerText(),/Idle crew-days: Excavation/);
    assert.equal(await plan.locator('#start').innerText(),'Run again');
    const first=await plan.evaluate(()=>result);
    assert.ok(first.lines.actual.back.length>2);
    assert.doesNotMatch(JSON.stringify(first),/"(?:budgetUsd|spentUsd|changeLog|events)":/);
    assert.deepEqual(await site.evaluate(()=>Object.keys(result).sort()),['stage','t','v']);
    assert.equal(await site.locator('#listen-lines').innerText(),'');
    assert.doesNotMatch(await plan.locator('body').innerText(),/\b(money|budget|inbox|catalog|codes|JSON|radio log)\b/i);
    const pdf=await context.request.get(`http://127.0.0.1:${host.port}/level1/rules.pdf`);
    assert.equal(pdf.status(),200);assert.match(pdf.headers()['content-type'],/application\/pdf/);
    assert.equal((await context.request.get(`http://127.0.0.1:${host.port}/level1/rules.html`)).status(),200);
    await mkdir(output,{recursive:true});await plan.screenshot({path:output+'result.png',fullPage:true});
    assert.equal((await clickReply(plan,'#start')).t,'ack');
    await plan.waitForFunction(()=>!result&&state.day<1&&state.speed!=='paused');
    assert.equal(await plan.locator('[data-kind="previous"]').count(),3);
    assert.deepEqual(await plan.evaluate(()=>state.view.previousRun),first.lines.actual);
    host.core.setFast(true);
    await site.waitForFunction(()=>state.view.crews.some(c=>c.status==='arguing'));
    const refusal=await clickReply(plan,'[data-crew="exc"]');
    assert.equal(refusal.reason,"The field won't take that order right now");
    assert.equal(await plan.locator('#feedback').innerText(),refusal.reason);
    assert.equal((await clickReply(plan,'[data-crew="back"]')).t,'ack');
    assert.deepEqual(errors,[]);
    console.log('Start -> Hold -> Go -> result -> Run again; Site status, previous lines, PDF, filtered result and argument refusal verified');
  } finally {await browser?.close();await host.close();}
});
