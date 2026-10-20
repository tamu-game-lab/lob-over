import { EPS } from './common.js';
import { boundaryAllowance } from './deviations.js';

export function validatePermit(s, op) {
  const card = s.scenario.permits.cards.find(card => card.zone === op.zone);
  if (!card || card.code !== op.code) return 'code not on file';
  if (!s.permits[op.zone].entered && Object.values(s.permits).filter(p => p.entered && !p.signed).length
      >= s.scenario.permits.openZoneLimit) return `city limit: ${s.scenario.permits.openZoneLimit} zones open`;
}

export function validateInspection(s, op) {
  const card = s.scenario.inspections.cards.find(card => card.zone === op.zone);
  if (!card || card.number !== op.number) return 'code not on file';
}

export function excavationLimit(s, desired) {
  const zone = s.scenario.job.zones.find(z => !s.permits[z.id].entered);
  if (!zone) return s.scenario.job.lengthFt;
  let limit = zone.fromFt;
  if (desired >= limit - EPS && s.crews.exc.pos >= limit - EPS) limit = boundaryAllowance(s, zone);
  // An already excavated F2 stretch is never rolled back by a later gate calculation.
  return Math.max(s.crews.exc.pos, limit);
}

export function backfillLimit(s) {
  if (!s.scenario.inspections.required) return s.scenario.job.lengthFt;
  return s.scenario.job.zones.find(z => !s.inspections[z.id].cleared)?.toFt ?? s.scenario.job.lengthFt;
}

export function updateHoldPoints(s) {
  for (const zone of s.scenario.job.zones) {
    const inspection = s.inspections[zone.id];
    if (s.crews.back.pos >= zone.toFt - EPS) {
      if (!inspection.reported && s.scenario.inspections.required) {
        inspection.reported = true;
        // WO-02: both authored Texas stages return this compaction result.
        s.inbox.push({ day: s.t, from: 'City inspector', subject: `Zone ${zone.id} compaction`, body: 'passed' });
      }
      if (inspection.cleared || !s.scenario.inspections.required) s.permits[zone.id].signed = true;
    }
  }
}

export const finalInspectionEntered = s => !s.scenario.inspections.required
  || s.inspections[s.scenario.job.zones.at(-1).id].cleared;
