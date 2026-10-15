import { CREW_IDS, EPS, LEADER, MOODS, NEAR_FT, activeCollapse, copy, crewAt,
  displayDay, nearby, protectionAt, zoneAt } from './common.js';
import { availableResponses } from './events.js';
import { rainOrRework, sky } from './rain.js';
import { plannedRate } from './planning.js';

const clean = value => Array.isArray(value) ? value.map(clean) : value && typeof value === 'object'
  ? Object.fromEntries(Object.entries(value).filter(([key]) => !key.startsWith('_')).map(([key, v]) => [key, clean(v)])) : value;
export const stakeAt = (s, ft) => Math.round(ft / s.scenario.job.stationEveryFt) * s.scenario.job.stationEveryFt;
const collapseHere = (s, ft) => {
  const event = activeCollapse(s);
  return event && stakeAt(s, event.stationFt) === stakeAt(s, ft);
};
const groundAt = (s, ft) => s.scenario.ground.segments.find(segment => ft >= segment.fromFt && ft <= segment.toFt);

export function trenchAt(s, stationFt) {
  const passed = id => s.crews[id].pos > stationFt + EPS || s.crews[id].finishT != null;
  const state = !passed('exc') ? 'undug' : !passed('pipe') ? 'open' : !passed('back') ? 'piped' : 'backfilled';
  const open = state === 'open' || state === 'piped';
  const p = protectionAt(s, stationFt);
  return { stationFt, state,
    water: collapseHere(s, stationFt) || open && rainOrRework(s) ? 'standing'
      : open && groundAt(s, stationFt)?.seepage ? 'seep' : 'none',
    wall: collapseHere(s, stationFt) ? 'slumped' : 'sound',
    protection: p ? p.kind.replace('trench_', '') : 'none', pipeDamage: 'none' };
}

export function crewText(s, c) {
  if (!c) return 'No machine within sight.';
  let text = 'Working.';
  if (c.status === 'demobilized') text = 'Demobilized.';
  else if (c.status === 'arguing') {
    const conflict = s.conflicts.find(conflict => !conflict.resolved
      && (conflict.trailing === c.id || conflict.leader === c.id));
    const other = conflict && s.crews[conflict.trailing === c.id ? conflict.leader : conflict.trailing];
    text = `Arguing with ${other?.name ?? s.crews[LEADER[c.id]]?.name ?? c.name}.`;
  } else if (c.status === 'stopped') text = 'Stopped: stop-work.';
  else if (c.status === 'held') text = `Held by the office since Day ${displayDay(c.hold.fromT)}.`;
  else if (c.status === 'idle') text = `Idle, waiting on ${s.crews[LEADER[c.id]]?.name ?? c.name}.`;
  return `${text} ${MOODS[c.mood]}`;
}

export function inspectionText(s, target) {
  const ft = stakeAt(s, s.truck.stationFt), c = crewAt(s), p = protectionAt(s, ft);
  const zone = zoneAt(s, ft);
  const segment = groundAt(s, ft);
  if (target === 'wall') {
    if (collapseHere(s, ft)) return 'Wall slumped into the trench.';
    if (s.crews.exc.pos <= ft + EPS) return 'Not excavated here yet.';
    return segment?.indicator.wall ?? s.scenario.ground.defaultIndicator.wall;
  }
  if (target === 'water') {
    if (trenchAt(s, ft).water === 'standing') return 'Standing water in the trench bottom.';
    return segment?.indicator.water ?? s.scenario.ground.defaultIndicator.water;
  }
  if (target === 'machine') {
    if (!c) return 'No machine within sight.';
    const machine = s.catalog.machines[c.machine];
    return `${c.name}: ${machine.label}, plate ${machine.sizeClass}`;
  }
  if (target === 'protection') return p
    ? `${s.catalog.protection[p.kind].label} in place, ${p.fromFt} to ${p.toFt}` : 'No protection here.';
  if (target === 'board') {
    const permit = s.permits[zone.id], inspection = s.inspections[zone.id];
    return `Zone ${zone.id} board: ${permit.entered ? `permit ${permit.number}` : 'no permit posted'}`
      + (inspection.cleared ? `; inspection ${inspection.number}` : '');
  }
  if (target === 'sky') return { clear: 'Clear.', towersSouth: 'Towers building to the south.', rain: 'Rain.' }[sky(s)];
  if (target === 'ticket') {
    const delivery = s.deliveries.find(d => !d.arrived);
    return delivery ? `Delivery ticket: ${s.catalog.machines[delivery.machine].label}, due Day ${displayDay(delivery.arrivesT)}`
      : 'No truck at the gate.';
  }
  if (target === 'crew') return crewText(s, c);
  if (target === 'pipe') return s.crews.back.pos > ft + EPS ? 'Backfilled.'
    : s.crews.pipe.pos > ft + EPS ? 'Pipe laid, open trench.' : 'No pipe here yet.';
}

export function publicOrder(order) {
  // The radio log carries the issued instruction, not whether the crew obeyed it.
  return { id: order.id, op: order.op, crew: order.crew, issuedDay: order.issuedT,
    landsDay: order.landsT, status: order.status };
}

export function planView(s) {
  return {
    plannedLines: s.plannedLines,
    responses: availableResponses(s).map(({ event, option }) => ({ key: option.key, label: option.label,
      cost: option.cost, eventTitle: event.card.title })),
    reportedLines: s.reportedLines,
    budget: { budgetUsd: s.scenario.plan.budgetUsd, spentUsd: s.spentUsd, committedUsd: s.spentUsd,
      deadlineDay: s.scenario.plan.deadlineDay, promisedDay: s.scenario.plan.promisedFinishDay },
    inbox: s.inbox, radioLog: s.orders.map(publicOrder),
    catalog: { machines: Object.fromEntries(Object.entries(s.catalog.machines).filter(([key]) => !key.startsWith('_'))
      .map(([key, machine]) => [key, { ...clean(machine), leadDays: { normal: machine.leadDays.normal
        + s.catalog.cities[s.scenario.city].leadDelayDays } }])), protection: clean(s.catalog.protection) },
    permits: s.scenario.job.zones.map(z => ({ zone: z.id, entered: s.permits[z.id].entered,
      ...(s.permits[z.id].entered ? { number: s.permits[z.id].number } : {}) })),
    inspections: s.scenario.job.zones.map(z => ({ zone: z.id, cleared: s.inspections[z.id].cleared })),
    flags: s.flags, draft: [],
    uncertainty: Object.fromEntries(CREW_IDS.map(id => {
      const report = s.reportedLines[id].at(-1);
      const estimate = report.ft + plannedRate(s.plannedLines[id], s.t)
        * Math.max(0, s.t - Math.max(report.t, s.plannedLines[id].startDay - 1));
      const band = s.t <= report.t + EPS ? 0 : s.scenario.rules.uncertaintyBandFt;
      return [id, { estimateFt: Math.min(s.scenario.job.lengthFt, estimate),
        fromFt: Math.max(0, estimate - band), toFt: Math.min(s.scenario.job.lengthFt, estimate + band), asOfT: report.t }];
    })), pmLines: s.pmLines,
  };
}

export function siteView(s) {
  const trench = [];
  for (let ft = 0; ft <= s.scenario.job.lengthFt; ft += s.scenario.job.stationEveryFt) trench.push(trenchAt(s, ft));
  const localEvent = s.events.find(e => e.fired && !e.resolved
    && (e.zone == null || e.zone === zoneAt(s, s.truck.stationFt)?.id || e.inspected));
  return {
    truck: { stationFt: s.truck.stationFt, moving: s.truck.moving, dismounted: s.truck.dismounted },
    crews: CREW_IDS.map(id => { const c = s.crews[id], size = s.catalog.machines[c.machine].sizeClass;
      return { id, stationFt: c.pos, status: c.status, machinePlate: size[0].toUpperCase() + size.slice(1), moodLevel: c.mood }; }),
    trench, boards: s.scenario.job.zones.map(z => ({ zone: z.id,
      permitNumber: s.permits[z.id].entered ? s.permits[z.id].number : null,
      inspectionNumber: s.inspections[z.id].cleared ? s.inspections[z.id].number : null })),
    sky: sky(s), gate: { trucksWaiting: s.deliveries.filter(d => !d.arrived).length },
    activeEvent: localEvent ? { id: localEvent.id, card: localEvent.shownCard } : null,
    orders: s.orders.filter(order => order.crew && order.landedT != null && order.status !== 'confirmed'
      && nearby(s, s.crews[order.crew].pos)).map(publicOrder),
  };
}

export function view(s, seat) {
  if (seat === 'plan') return copy(planView(s));
  if (seat === 'site') return copy(siteView(s));
  if (seat === 'facilitator') {
    const { messages, ...state } = s;
    return copy({ ...state, plan: planView(s), site: siteView(s) });
  }
  throw new Error('unknown seat');
}
