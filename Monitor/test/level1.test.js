import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { loadCatalog } from '../src/common/catalog.js';
import { loadScenario } from '../src/scenario/load.js';
import { createSim } from '../src/sim/sim.js';
import { voice } from '../src/sim/common.js';
import { arrivalVoices } from '../src/sim/voices.js';
import { landingTime } from '../src/sim/orders.js';
import { runHeadless, roundedJSON } from '../src/sim/headless.js';
import { createHost } from '../src/host/core.js';
import { createPacer } from '../src/host/pacing.js';

const catalog = loadCatalog();
const scenario = loadScenario(new URL('../../Data/Scenarios/level1_texas.json', import.meta.url), { catalog });
const copy = value => JSON.parse(JSON.stringify(value));
const commit = (sim, ...ops) => sim.apply('plan', { action: 'commit', ops });
const voices = sim => sim.drain().filter(message => message.t === 'voice');
const near = (a, b) => assert.ok(Math.abs(a - b) < 1e-6, `${a} != ${b}`);
const until = (sim, t) => sim.tick(Math.max(.01, t - sim.t));
const script = name => JSON.parse(readFileSync(new URL(`./scripts/level1_${name}.json`, import.meta.url)));

test('Level 1: scenario has one timed collapse, posted permits and no other systems to manage', () => {
  assert.equal(scenario.events.length, 1);
  assert.equal(scenario.events[0].durationDays, 2);
  assert.deepEqual(scenario.events[0].trigger, { kind: 'crewReaches', crew: 'pipe', ft: 520 });
  assert.equal(scenario.permits.heldBy, 'pm');
  assert.ok(scenario.permits.cards.every(card => !card.code));
  assert.equal(scenario.inspections.required, false);
  assert.equal(scenario.deviations.length + scenario.inbox.length, 0);
  assert.ok(Object.values(createSim(scenario, catalog).serialize().permits).every(p => p.entered));
});

test('Level 1: P1 resolves after exactly two days; event voice carries ordered turns to Site only', () => {
  const sim = createSim(scenario, catalog);
  commit(sim, { op: 'hold', crew: 'back' });
  until(sim, 3.89);
  const event = sim.serialize().events[0];
  near(event.firedT, 3.89); near(event.endsT, 5.89);
  const reaction = voices(sim).find(m => m.lineIds.includes('marcus_collapse'));
  assert.equal(reaction.turns.length, 3); assert.deepEqual(reaction.seats, ['site']);
  assert.deepEqual(reaction.turns.map(t => t.clip), [1, 2, 3].map(n => `marcus_collapse_${n}.wav`));
  const pos = sim.serialize().crews.pipe.pos;
  until(sim, 5.88); near(sim.serialize().crews.pipe.pos, pos);
  until(sim, 5.89); assert.equal(sim.serialize().events[0].resolved, true);
  sim.tick(); assert.ok(sim.serialize().crews.pipe.pos > pos);
  assert.equal(voices(sim).some(m => m.lineIds.includes('marcus_collapse')), false);
});

test('Level 1: all order landing times are immediate, including when Site is absent', () => {
  const sim = createSim(scenario, catalog); until(sim, .37);
  const raw = sim.serialize();
  for (const crew of [null, 'exc', 'pipe', 'back']) near(landingTime(raw, crew), .37);
  assert.equal(commit(sim, { op: 'hold', crew: 'exc' }).ok, true);
  assert.equal(sim.serialize().crews.exc.status, 'held');
  near(sim.serialize().orders.at(-1).appliedT, .37);
  commit(sim, { op: 'resume', crew: 'exc' });
  assert.equal(sim.serialize().crews.exc.status, 'working');
});

test('Level 1: slow speed survives unrelated commits, event end and pause while an argument remains', () => {
  const sim = createSim(scenario, catalog);
  until(sim, 3.89); assert.equal(sim.speed, 'slow');
  commit(sim, { op: 'hold', crew: 'exc' }); assert.equal(sim.speed, 'slow');
  commit(sim, { op: 'resume', crew: 'exc' });
  until(sim, 4.1); assert.ok(sim.serialize().conflicts.some(c => !c.resolved));
  for (const ops of [[{ op: 'resume', crew: 'back' }], [{ op: 'hold', crew: 'pipe' }],
    [{ op: 'demob', crew: 'back' }], [{ op: 'hold', crew: 'back' }, { op: 'hold', crew: 'exc' }]]) {
    assert.equal(commit(sim, ...ops).reason, "The field won't take that order right now");
  }
  until(sim, 6); assert.equal(sim.serialize().events[0].resolved, true); assert.equal(sim.speed, 'slow');
  sim.apply('facilitator', { action: 'pause' });
  commit(sim, { op: 'hold', crew: 'back' }); assert.equal(sim.speed, 'paused');
  sim.apply('facilitator', { action: 'resume' }); assert.equal(sim.speed, 'full');
});

test('Level 1: held unstarted crew remains off site until Go, and finite holds also delay start', () => {
  for (const finite of [false, true]) {
    const sim = createSim(scenario, catalog);
    commit(sim, { op: 'hold', crew: 'back', ...(finite ? { days: 8 } : {}) });
    until(sim, 7.99);
    const crew = sim.serialize().crews.back;
    assert.equal(crew.onsite, 0); assert.equal(crew.work, 0); assert.equal(crew.waitDays, 0);
    assert.equal(sim.view('site').crews.find(c => c.id === 'back').onSite, false);
    until(sim, 8); if (!finite) commit(sim, { op: 'resume', crew: 'back' });
    near(sim.serialize().crews.back.startDay, 9);
    sim.tick(); near(sim.serialize().crews.back.onsite, .01);
    assert.equal(sim.view('site').crews.find(c => c.id === 'back').onSite, true);
    near(sim.serialize().crews.back.pos, 2.5);
    commit(sim, { op: 'hold', crew: 'back' });sim.tick(1);
    assert.ok(sim.serialize().crews.back.onsite > 1);
  }
});

test('Level 1: arrivals require dismount within 30m, and are limited per crew/day across statuses', () => {
  const sim = createSim(scenario, catalog);sim.drain();sim.tick(.2);
  const raw = sim.serialize();raw.messages=[];
  raw.truck.stationFt = raw.crews.exc.pos + 99;raw.truck.dismounted=true;
  arrivalVoices(raw); assert.equal(raw.messages.length, 0);
  raw.truck.stationFt = raw.crews.exc.pos + 30 / .3048;
  raw.truck.dismounted=false;arrivalVoices(raw);assert.equal(raw.messages.length, 0);
  raw.truck.dismounted=true;arrivalVoices(raw);arrivalVoices(raw);
  assert.deepEqual(raw.messages.map(m => m.lineIds), [['ray_working']]);
  raw.t=1.2;arrivalVoices(raw);assert.equal(raw.messages.length, 2);
  const idle=copy(raw);idle.messages=[];idle.voicePlayed=[];idle.truck.stationFt=500;
  idle.crews.pipe.pos=500;idle.crews.pipe.status='idle';
  arrivalVoices(idle);idle.crews.pipe.status='working';arrivalVoices(idle);
  assert.deepEqual(idle.messages.map(m => m.lineIds), [['marcus_idle']]);
});

test('Level 1: conflict picks pair and cause; repeat grumbles once per day without stopping', () => {
  for (const [events, expected] of [[scenario.events, 'arg_back_pipe_stopped'], [[], 'arg_back_pipe_caught']]) {
    const s=copy(scenario);s.events=events;
    const sim=createSim(s,catalog);
    while (!sim.serialize().conflicts.length) sim.tick();
    const message=voices(sim).find(m=>m.lineIds[0]===expected);
    assert.equal(message.turns.length,4);assert.deepEqual(message.seats,['site']);
  }
  const sim=createSim(scenario,catalog);until(sim,4.1);commit(sim,{op:'hold',crew:'back'});
  until(sim,6.1);commit(sim,{op:'resume',crew:'back'});sim.drain();until(sim,13);
  const grumbles=voices(sim).filter(m=>m.lineIds[0]==='grumble_back_pipe');
  assert.ok(grumbles.length>=2);
  assert.equal(new Set(grumbles.map(m=>Math.floor(m.day+1e-6))).size,grumbles.length);
  assert.equal(sim.serialize().conflicts.length,1);assert.equal(sim.serialize().crews.back.arguing,false);
  assert.equal(sim.speed,'full');
  const raw=sim.serialize();raw.messages=[];raw.voicePlayed=[];
  raw.scenario.lines=[{id:'any',trigger:'conflict',speaker:'foreman_pipe',text:'Any'},
    {id:'pair',trigger:'conflict',pair:'back-pipe',speaker:'foreman_pipe',text:'Pair'},
    {id:'specific',trigger:'conflict',pair:'back-pipe',cause:'leaderStopped',speaker:'foreman_pipe',text:'Stopped'}];
  voice(raw,'conflict',['site'],undefined,{pair:'back-pipe',cause:'leaderStopped'});
  assert.deepEqual(raw.messages.map(m=>m.lineIds),[['specific']]);
});

test('Level 1: Plan controls only start/reset, mirror cannot control, and connection kind reaches Plan', () => {
  const host=createHost({scenario,catalog});
  function connect(seat, client='web') {
    const messages=[];let receive,close;
    host.attach({send:m=>messages.push(m),onMessage:fn=>receive=fn,onClose:fn=>close=fn,close:()=>{}});
    receive({t:'hello',v:1,seat,client});
    return { messages, close, send:message=>receive({v:1,id:'x',...message}), last:()=>messages.at(-1) };
  }
  try {
    const plan=connect('plan');assert.equal(plan.messages.some(m=>m.t==='voice'),false);
    const mirror=connect('plan');const site=connect('site','quest');
    assert.equal(host.state('plan').view.siteClient,'quest');
    mirror.send({t:'ctrl',action:'start'});assert.equal(mirror.last().reason,'mirror');
    plan.send({t:'ctrl',action:'pause'});assert.equal(plan.last().reason,'action unavailable for seat');
    site.send({t:'ctrl',action:'start'});assert.equal(site.last().t,'nack');
    plan.send({t:'ctrl',action:'start'});assert.equal(host.state('plan').speed,'full');
    assert.equal(plan.messages.filter(m=>m.t==='voice').length,1);
    assert.equal(site.messages.some(m=>m.t==='voice'),false);
    plan.send({t:'ctrl',action:'reset'});assert.equal(host.state('plan').speed,'paused');
    site.close();assert.equal(host.state('plan').view.siteClient,null);
    connect('site');assert.equal(host.state('plan').view.siteClient,'web');
    assert.doesNotMatch(JSON.stringify(host.state('plan').view), /"(?:budget|spentUsd|actualLines|activeEvent|radioLog|catalog|inbox)":/);
    assert.doesNotMatch(JSON.stringify(host.state('site').view), /"(?:budget|spentUsd|plannedLines|reportedLines)":/);
  } finally {host.close();}
});

test('Level 1: real-time pacing uses 40ms full ticks and 150ms slow ticks', () => {
  const sim=createSim(scenario,catalog);let delay;
  const pacer=createPacer(()=>sim,()=>{}, {now:()=>0,setTimer:(fn,ms)=>{delay=ms;return 1;},clearTimer:()=>{}});
  pacer.sync();assert.equal(delay,40);
  until(sim,3.89);pacer.sync();assert.equal(delay,150);pacer.close();
});

test('Level 1 measured scripts: pull has no argument and fewer idle crew-days; deterministic reruns', () => {
  const push=runHeadless(scenario,catalog,script('push')),pull=runHeadless(scenario,catalog,script('pull'));
  for (const [name,run] of [['push',push],['pull',pull]]) {
    assert.equal(run.result.lost,undefined);
    assert.equal(run.messages.some(m=>m.message.t==='nack'),false);
    assert.equal(roundedJSON(run),roundedJSON(runHeadless(scenario,catalog,script(name))));
    console.log(`${name}: t=${run.result.finishDay}, display day=${Math.floor(run.result.finishDay)+1}, idle=${JSON.stringify(run.result.idleDays)}, argument=${run.messages.some(m=>m.message.eventId?.startsWith('conflict_'))}`);
  }
  assert.ok(push.messages.some(m=>m.message.eventId==='conflict_back_pipe'));
  assert.equal(pull.messages.some(m=>m.message.eventId?.startsWith('conflict_')),false);
  assert.ok(pull.result.idleCrewDays<push.result.idleCrewDays);
  near(push.result.finishDay,19.67);near(pull.result.finishDay,20);
  near(pull.result.lines.actual.exc.at(-1).t,13.64);
  near(pull.result.lines.actual.pipe.at(-1).t,19.67);
});
