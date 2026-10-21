import { CREW_IDS, EPS } from './common.js';

// Projections use issued instructions and catalog rates, never live field positions.
export function plannedRate(line, t) {
  if (t < line.startDay - 1) return 0;
  if (line.holds.some(h => t >= h.fromT && (h.untilT == null || t < h.untilT))) return 0;
  const segment = [...line.segments].sort((a, b) => a.fromT - b.fromT).findLast(segment => segment.fromT <= t);
  const extra = (line.loans ?? []).filter(loan => t >= loan.fromT && t < loan.untilT)
    .reduce((sum, loan) => sum + loan.rate, 0);
  return (segment?.rate ?? line.rate) + extra;
}

export function plannedPolylines(s) {
  return Object.fromEntries(CREW_IDS.map(id => {
    const line = s.plannedLines[id];
    const breaks = [...new Set([line.startDay - 1, ...line.segments.map(segment => segment.fromT),
      ...line.holds.flatMap(h => h.untilT == null ? [h.fromT] : [h.fromT, h.untilT]),
      ...(line.loans ?? []).flatMap(loan => [loan.fromT, loan.untilT])])]
      .filter(t => t >= line.startDay - 1).sort((a, b) => a - b);
    let t = line.startDay - 1, ft = 0;
    const points = [{ t, ft }];
    for (const next of breaks.slice(1)) {
      const rate = plannedRate(line, t);
      if (rate > 0 && ft + (next - t) * rate >= s.scenario.job.lengthFt) {
        points.push({ t: t + (s.scenario.job.lengthFt - ft) / rate, ft: s.scenario.job.lengthFt });
        return [id, points];
      }
      ft += (next - t) * rate; t = next; points.push({ t, ft });
    }
    const rate = plannedRate(line, t + EPS);
    if (rate > 0) points.push({ t: t + (s.scenario.job.lengthFt - ft) / rate, ft: s.scenario.job.lengthFt });
    else if (s.t > t) points.push({ t: s.t, ft });
    return [id, points];
  }));
}

export function projectOrder(s, order) {
  // WO-L1 chart compares against the PM's original plan throughout the run.
  if (s.scenario.rules.planControlsClock) return;
  const op = order.op, line = s.plannedLines[order.crew];
  if (op.op === 'hold' || op.op === 'demob') {
    const fromT = Math.max(order.landsT, op.fromDay != null ? op.fromDay - 1 : order.landsT);
    line.holds.push({ fromT, untilT: op.days == null ? null : fromT + op.days, kind: op.op });
  }
  if (op.op === 'resume' || op.op === 'remob') {
    const untilT = order.landsT + (op.op === 'remob' ? s.catalog.cities[s.scenario.city].remobilizationDelayDays : 0);
    for (const hold of line.holds) {
      if ((op.op === 'resume' ? hold.kind === 'hold' : hold.kind === 'demob')
        && (hold.untilT == null || hold.untilT > untilT)) hold.untilT = Math.max(hold.fromT, untilT);
    }
  }
  if (op.op === 'order') {
    const machine = s.catalog.machines[op.machine];
    line.segments.push({ fromT: order.issuedT + machine.leadDays.normal + s.catalog.cities[s.scenario.city].leadDelayDays,
      rate: machine.rateFtPerDay });
  }
  if (op.op === 'lend') {
    const fromT = order.landsT, untilT = fromT + op.days;
    line.holds.push({ fromT, untilT, kind: 'lend' });
    if (!s.events.some(e => e.type === 'P1' && e.fired && !e.resolved && order.crew === 'exc' && op.toCrew === 'pipe')) {
      const recipient = s.plannedLines[op.toCrew];
      recipient.loans ??= [];
      recipient.loans.push({ fromT, untilT, rate: s.catalog.machines[op.machine].rateFtPerDay });
    }
  }
}
