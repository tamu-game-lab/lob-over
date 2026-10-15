import { CREW_IDS, EPS } from './common.js';

export const billing = (s, c) => s.t >= c.startDay - 1 - EPS && (c.finishT == null || c.finishT > s.t + EPS)
  && !(c.demobBillingEndsT != null && s.t >= c.demobBillingEndsT - EPS);

export function accrue(s, dt) {
  const overhead = 1 + s.scenario.rules.overheadRate;
  let crewDaily = 0;
  for (const id of CREW_IDS) {
    const c = s.crews[id];
    if (billing(s, c)) {
      c.onsite += dt;
      crewDaily += s.catalog.machines[c.machine].dailyUsd;
    }
  }
  const protectionDaily = s.protections.reduce((sum, p) => sum
    + (CREW_IDS.some(id => {
      const c = s.crews[id];
      return c.mult > 0 && c.moved > EPS && c.pos >= p.fromFt && c.pos <= p.toFt;
    }) ? s.catalog.protection[p.kind].dailyUsd : 0), 0);
  const hiredDaily = s.deliveries.filter(d => d.hired && d.arrived && !d.released)
    .reduce((sum, d) => sum + s.catalog.machines[d.machine].dailyUsd, 0);
  const optionDaily = s.payments.reduce((sum, p) => sum + p.perDayUsd, 0);
  s.crewCostUsd += crewDaily * dt * overhead;
  s.spentUsd += (crewDaily + protectionDaily + hiredDaily) * dt * overhead + optionDaily * dt;
}

export function buyMachine(s, machine) {
  s.spentUsd += s.catalog.machines[machine].priceUsd + s.scenario.rules.mobilizationUsd;
}

export function releaseMachine(s, machine) {
  s.spentUsd -= s.catalog.machines[machine].priceUsd * s.catalog.orders.releaseRefundFraction;
}
