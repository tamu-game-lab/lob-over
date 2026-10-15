import { CREW_IDS, EPS, HOURS_PER_DAY, activeCollapse, crewAt, emit, nearby, travelDays, voice, zoneAt } from './common.js';
import { confirmDeviation, inspectProtection } from './deviations.js';
import { inspectionText, stakeAt } from './views.js';

const TARGETS = new Set(['wall', 'water', 'pipe', 'machine', 'protection', 'board', 'sky', 'ticket', 'crew']);
export function applySite(s, cmd) {
  const truck = s.truck;
  if (cmd.action === 'moveTo') {
    if (truck.moving || truck.dismounted) return 'mount before moving';
    if (!Number.isFinite(cmd.stationFt) || cmd.stationFt < 0 || cmd.stationFt > s.scenario.job.lengthFt) return 'invalid station';
    truck.fromFt = truck.stationFt;
    truck.toFt = cmd.stationFt;
    truck.departT = s.t;
    truck.arrivesT = s.t + travelDays(truck.stationFt, cmd.stationFt);
    truck.moving = truck.arrivesT > s.t;
    return;
  }
  if (cmd.action === 'mount' || cmd.action === 'dismount') {
    if (truck.moving) return 'truck moving';
    truck.dismounted = cmd.action === 'dismount';
    return;
  }
  if (!truck.dismounted || truck.moving) return 'dismount first';
  if (cmd.action === 'inspect') {
    if (!TARGETS.has(cmd.target)) return 'unknown inspection target';
    const ft = stakeAt(s, truck.stationFt);
    if (cmd.target === 'protection') inspectProtection(s, ft);
    const event = activeCollapse(s);
    if (event && stakeAt(s, event.stationFt) === ft) event.inspected = true;
    emit(s, 'inspection', { id: cmd.id, target: cmd.target, text: inspectionText(s, cmd.target), seats: ['site'] });
    return;
  }
  if (cmd.action === 'flag') {
    // Protocol caps authored notes at 40 characters.
    const MAX_NOTE_LENGTH = 40;
    if (!Number.isFinite(cmd.stationFt) || cmd.stationFt < 0 || cmd.stationFt > s.scenario.job.lengthFt
      || cmd.note != null && (typeof cmd.note !== 'string' || cmd.note.length > MAX_NOTE_LENGTH)) return 'invalid flag';
    s.flags.push({ stationFt: cmd.stationFt, day: s.t, ...(cmd.note ? { note: cmd.note } : {}) });
    return;
  }
  if (cmd.action === 'stopWork') {
    if (zoneAt(s, truck.stationFt)?.id !== cmd.zone) return 'outside zone';
    for (const id of CREW_IDS) {
      const c = s.crews[id];
      if (zoneAt(s, c.pos)?.id === cmd.zone) c.stop = { zone: cmd.zone,
        fromT: s.t + s.scenario.rules.stopWorkReachHours / HOURS_PER_DAY };
    }
    return;
  }
  if (cmd.action === 'confirm') {
    const order = s.orders.find(order => order.id === cmd.orderId);
    if (!order?.crew || order.landedT == null || order.status === 'sent'
      || !nearby(s, s.crews[order.crew].pos)) return 'order not landed here';
    if (!['done', 'notDone'].includes(cmd.outcome)) return 'invalid outcome';
    order.status = cmd.outcome === 'done' ? 'confirmed' : 'notCarriedOut';
    confirmDeviation(s, order, cmd.outcome);
    if (cmd.outcome === 'notDone') voice(s, 'orderNotDone', ['plan']);
    return;
  }
  if (cmd.action === 'listen') {
    const c = crewAt(s);
    if (c?.arguing) {
      const conflict = s.conflicts.find(conflict => conflict.trailing === c.id || conflict.leader === c.id);
      voice(s, 'conflict', ['site'], [conflict.trailing, conflict.leader]);
    } else if (c && (c.status === 'held' || c.status === 'stopped') && c.mood > 0) {
      voice(s, `heldDay${c.mood}`, ['site'], [c.id]);
    } else emit(s, 'inspection', { id: cmd.id, target: 'crew', text: 'Nothing to hear.', seats: ['site'] });
    return;
  }
  return 'unknown action';
}

export function updateTruck(s) {
  const truck = s.truck;
  if (!truck.moving) return;
  if (s.t >= truck.arrivesT - EPS) {
    truck.stationFt = truck.toFt;
    truck.moving = false;
  } else truck.stationFt = truck.fromFt + (truck.toFt - truck.fromFt)
    * (s.t - truck.departT) / (truck.arrivesT - truck.departT);
}
