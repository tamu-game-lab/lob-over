import { EPS, nearby, nextBoundary, protectionAt } from './common.js';

export function markFirstRadio(s, order) {
  const d = s.deviations.find(d => d.type === 'F1' && !d.triggered
    && d.when.ops.includes(order.op.op) && order.landsT > order.issuedT);
  if (d) {
    d.triggered = true;
    d.orderId = order.id;
    order.ignored = true;
  }
}

export function confirmDeviation(s, order, outcome) {
  const d = s.deviations.find(d => d.type === 'F1' && d.orderId === order.id);
  if (d && outcome === 'notDone' && s.t < nextBoundary(order.landedT) - EPS) d.caught = true;
}

export function boundaryAllowance(s, zone) {
  // WO-05a O10: F2 requires crossing from another zone, not starting the job.
  if (!s.scenario.job.zones.some(previous => previous.toFt === zone.fromFt)) return zone.fromFt;
  const d = s.deviations.find(d => d.type === 'F2');
  if (!d) return zone.fromFt;
  if (d.triggered) return d.zone === zone.id && !d.caught ? zone.fromFt + d.overshootFt : zone.fromFt;
  d.triggered = true;
  d.zone = zone.id;
  d.caught = nearby(s, zone.fromFt) || !!s.crews.exc.hold || !!s.crews.exc.stop;
  if (!d.caught) {
    s.finesUsd += s.scenario.rules.permitFineUsd;
    s.spentUsd += s.scenario.rules.permitFineUsd;
  }
  return d.caught ? zone.fromFt : zone.fromFt + d.overshootFt;
}

export function inspectProtection(s, stationFt) {
  const p = protectionAt(s, stationFt);
  if (!p) return;
  for (const d of s.deviations.filter(d => d.type === 'F3')) {
    const e = s.events.find(e => e.id === d.when.eventId);
    if (!e?.fired && !(e.stationFt >= p.fromFt && e.stationFt <= p.toFt)) d.inspected = true;
  }
}

export function eventDeviations(s, event) {
  for (const d of s.deviations.filter(d => d.type === 'F3' && d.when.eventId === event.id)) {
    const missedStretch = s.protections.some(p => event.stationFt < p.fromFt || event.stationFt > p.toFt);
    if (missedStretch && !protectionAt(s, event.stationFt)) {
      d.triggered = true;
      d.caught = !!d.inspected;
    }
  }
}

// WO-02 reserves F5's firstLend hook but specifies no delay behaviour in these stages.
export function firstLend(s) {
  for (const d of s.deviations.filter(d => d.type === 'F5' && !d.triggered)) d.triggered = true;
}
