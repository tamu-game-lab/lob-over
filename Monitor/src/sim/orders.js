import { CREW_IDS, EPS, activeCollapse, displayDay, nearby, nextBoundary, travelDays, voice } from './common.js';
import { markFirstRadio, firstLend } from './deviations.js';
import { availableResponses, clearingDays } from './events.js';
import { buyMachine, releaseMachine } from './money.js';
import { validateInspection, validatePermit, updateHoldPoints } from './permits.js';
import { projectOrder } from './planning.js';

const OFFICE_OPS = new Set(['setStart', 'enterPermit', 'enterInspection', 'pay']);
const CREW_OPS = new Set(['setStart', 'hold', 'resume', 'demob', 'remob']);
const PLAN_OPS = new Set([...CREW_OPS, ...OFFICE_OPS, 'lend', 'order', 'protect', 'radio']);
const positive = n => Number.isFinite(n) && n > 0;
const owns = (object, key) => typeof key === 'string' && Object.hasOwn(object, key) && !key.startsWith('_');
const ownerOf = (s, machine) => CREW_IDS.find(id => s.crews[id].machine === machine);
const machineCommitted = (s, machine) => s.orders.some(order => order.op.op === 'lend'
  && order.op.machine === machine && order.appliedT == null);
export const landingTime = (s, crew) => s.pure || s.scenario.rules.orderLag === 'none'
  || crew && nearby(s, s.crews[crew].pos) ? s.t : nextBoundary(s.t);

export function validateOp(s, op) {
  if (!op || !PLAN_OPS.has(op.op)) return 'unknown operation';
  if (CREW_OPS.has(op.op) && !CREW_IDS.includes(op.crew)) return 'unknown crew';
  const c = s.crews[op.crew];
  if (op.op === 'setStart') {
    if (s.scenario.plan.locked || c.locked) return 'plan locked';
    if (s.t >= c.startDay - 1 - EPS) return 'crew already started';
    if (!Number.isInteger(op.day) || op.day < displayDay(s.t)) return 'invalid start day';
  }
  if (op.op === 'hold' && (op.days != null && !positive(op.days)
    || op.fromDay != null && (!Number.isInteger(op.fromDay) || op.fromDay < displayDay(s.t)))) return 'invalid hold';
  if (op.op === 'lend') {
    if (machineCommitted(s, op.machine)) return 'machine already committed';
    const owner = ownerOf(s, op.machine);
    if (!owner || s.crews[owner].lentAway || s.loans.some(l => l.fromCrew === owner && l.phase !== 'returned')) return 'machine unavailable';
    if (!CREW_IDS.includes(op.toCrew) || op.toCrew === owner || !positive(op.days)) return 'invalid lend';
  }
  if (op.op === 'protect' || op.op === 'order') {
    if (!s.scenario.plan.preparationAllowed) return 'not in the budget';
    if (op.op === 'order') {
      if (!owns(s.catalog.machines, op.machine)) return 'unknown machine';
      if (op.rush) return 'rush unavailable';
    } else {
      if (!owns(s.catalog.protection, op.kind)) return 'unknown protection';
      if (s.catalog.protection[op.kind].reactOnly) return 'no such response now';
      if (op.kind === 'trench_slope' && (s.catalog.cities[s.scenario.city].slopeRefused
        || s.catalog.protection[op.kind].refusedInCities?.includes(s.scenario.city))) return 'slope refused';
      if (!Number.isFinite(op.fromFt) || !Number.isFinite(op.toFt) || op.fromFt < 0
        || op.toFt > s.scenario.job.lengthFt || op.fromFt >= op.toFt) return 'invalid stretch';
    }
  }
  if (op.op === 'pay') {
    if (op.item === 'clear_own' && machineCommitted(s, s.crews.exc.machine)) return 'machine already committed';
    const response = availableResponses(s).find(r => r.option.key === op.item);
    if (!response) return 'no such response now';
    if (op.item === 'hire_excavator' && (!owns(s.catalog.machines, op.machine)
      || s.catalog.machines[op.machine].crew !== 'exc')) return 'unknown machine';
    if (op.item === 'clear_own' && s.crews.exc.lentAway) return 'machine unavailable';
  }
  if (op.op === 'enterPermit') return validatePermit(s, op);
  if (op.op === 'enterInspection') return validateInspection(s, op);
  if (op.op === 'radio' && !s.orders.some(order => order.id === op.orderId)) return 'order not on file';
}

function startLoan(s, op) {
  const fromCrew = ownerOf(s, op.machine);
  const event = fromCrew === 'exc' && op.toCrew === 'pipe' ? activeCollapse(s) : null;
  const loan = { machine: op.machine, fromCrew, toCrew: op.toCrew, phase: 'outbound',
    arrivesT: s.t + travelDays(s.crews[fromCrew].pos, s.crews[op.toCrew].pos), days: op.days,
    eventId: event?.id ?? null };
  s.crews[fromCrew].lentAway = true;
  s.loans.push(loan);
  if (event) event.option = 'clear_own';
  firstLend(s);
}

function purchase(s, machine, event) {
  buyMachine(s, machine);
  s.deliveries.push({ machine, crew: s.catalog.machines[machine].crew, issuedT: s.t,
    arrivesT: s.t + s.catalog.machines[machine].leadDays.normal + s.catalog.cities[s.scenario.city].leadDelayDays,
    hired: !!event, eventId: event?.id ?? null, arrived: false, released: false });
}

function pay(s, op) {
  const { event, option } = availableResponses(s).find(r => r.option.key === op.item);
  event.option = op.item;
  if (op.item === 'hire_excavator') {
    purchase(s, op.machine, event);
    return;
  }
  s.spentUsd += option.cost.usd;
  s.payments.push({ item: op.item, paidT: s.t, perDayUsd: option.cost.perDayUsd });
  if (option.effect.lend) {
    const fromCrew = option.effect.lend.machineOf;
    issueOrder(s, { op: 'lend', machine: s.crews[fromCrew].machine,
      toCrew: option.effect.lend.toCrew, days: clearingDays(s, event, s.crews[fromCrew].machine) });
  }
  if (option.effect.resumeAfterDays != null) event.endsT = s.t + option.effect.resumeAfterDays;
  if (option.effect.rainRateRestore) {
    event.pumpPaidT = s.t;
    event.restore = option.effect.rainRateRestore;
    event.reworkMultiplier = option.effect.reworkMultiplier;
  }
}

function applyLanded(s, order) {
  const op = order.op, c = s.crews[order.crew];
  if (op.op === 'hold') {
    const fromT = Math.max(s.t, op.fromDay != null ? op.fromDay - 1 : s.t);
    c.hold = { fromT, untilT: op.days != null ? fromT + op.days : null };
    if (s.scenario.rules.holdBeforeStartDelaysStart && !c.onsite && !c.work && fromT <= c.startDay - 1 + EPS) {
      c.startDelayed = true;
    }
    c.arguing = false;
    c.stop = null;
  } else if (op.op === 'resume') {
    if (c.startDelayed) { c.startDay = s.t + 1; c.startDelayed = false; }
    c.hold = c.stop = null;
  } else if (op.op === 'lend') startLoan(s, op);
  else if (op.op === 'protect') s.protections.push({ kind: op.kind, fromFt: op.fromFt, toFt: op.toFt, placedT: s.t });
  else if (op.op === 'demob') {
    c.demobilized = true;
    c.demobBillingEndsT = nextBoundary(s.t);
    c.arguing = false;
  } else if (op.op === 'remob') {
    c.remobT = s.t + s.catalog.cities[s.scenario.city].remobilizationDelayDays;
  }
  order.appliedT = s.t;
}

export function issueOrder(s, op) {
  if (op.op === 'radio') {
    const order = s.orders.find(order => order.id === op.orderId);
    order.issuedT = s.t;
    order.landsT = landingTime(s, order.crew);
    order.status = 'sent';
    order.ignored = false;
    order.reissued = true;
    // Acknowledging an already-applied order must not buy or lend twice.
    order.alreadyApplied = order.appliedT != null;
    return;
  }
  const crew = op.crew ?? (op.op === 'lend' ? ownerOf(s, op.machine)
    : op.op === 'order' ? s.catalog.machines[op.machine].crew : op.op === 'protect' ? 'exc' : null);
  const order = { id: `o-${s.nextOrderId++}`, op: { ...op }, crew, issuedT: s.t,
    landsT: OFFICE_OPS.has(op.op) ? s.t : landingTime(s, crew), status: 'sent' };
  s.orders.push(order);
  projectOrder(s, order);
  if (!s.pure) markFirstRadio(s, order);
  if (op.op === 'protect') s.spentUsd += s.catalog.protection[op.kind].priceUsd;
  if (op.op === 'order') purchase(s, op.machine);
  if (op.op === 'remob') s.spentUsd += s.scenario.rules.demobFeeUsd;
  if (op.op === 'hold') voice(s, 'holdCommitted', ['plan']);
  if (op.op === 'demob') voice(s, 'demobCommitted', ['plan']);
  if (!OFFICE_OPS.has(op.op)) return;
  if (op.op === 'setStart') {
    s.crews[op.crew].startDay = op.day;
    s.plannedLines[op.crew].startDay = op.day;
    s.plannedLines[op.crew].segments[0].fromT = op.day - 1;
  } else if (op.op === 'enterPermit') s.permits[op.zone].entered = true;
  else if (op.op === 'enterInspection') {
    s.inspections[op.zone].cleared = true;
    updateHoldPoints(s);
  } else if (op.op === 'pay') pay(s, op);
  order.appliedT = order.landedT = s.t;
  order.status = 'landed';
}

export function updateOrders(s) {
  for (const order of s.orders) {
    if (order.status !== 'sent' || s.t < order.landsT - EPS) continue;
    order.status = 'landed';
    order.landedT = s.t;
    voice(s, 'orderLanded', ['plan']);
    if (!order.ignored && !order.alreadyApplied) applyLanded(s, order);
  }
  for (const loan of s.loans) {
    if (loan.phase === 'outbound' && s.t >= loan.arrivesT - EPS) {
      loan.phase = 'working';
      const event = s.events.find(e => e.id === loan.eventId);
      loan.returnsT = s.t + (event ? clearingDays(s, event, loan.machine) : loan.days);
      if (event) { event.clearingT = s.t; event.endsT = loan.returnsT; }
    }
    if (loan.phase === 'working' && s.t >= loan.returnsT - EPS) {
      loan.phase = 'returning';
      loan.homeT = s.t + travelDays(s.crews[loan.toCrew].pos, s.crews[loan.fromCrew].pos);
    }
    if (loan.phase === 'returning' && s.t >= loan.homeT - EPS) {
      loan.phase = 'returned';
      s.crews[loan.fromCrew].lentAway = false;
    }
  }
  for (const delivery of s.deliveries) {
    if (!delivery.arrived && s.t >= delivery.arrivesT - EPS) {
      delivery.arrived = true;
      if (delivery.hired) {
        const event = s.events.find(e => e.id === delivery.eventId);
        event.clearingT = s.t;
        event.endsT = s.t + clearingDays(s, event, delivery.machine);
        delivery.releaseT = event.endsT;
      } else {
        const crew = s.crews[delivery.crew];
        releaseMachine(s, crew.machine);
        crew.machine = delivery.machine;
      }
    }
    if (delivery.hired && delivery.arrived && !delivery.released && s.t >= delivery.releaseT - EPS) {
      delivery.released = true;
      releaseMachine(s, delivery.machine);
    }
  }
}
