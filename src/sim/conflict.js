import { LEADER, EPS, CONFLICT_LOOKAHEAD_DAYS, emit, voice } from './common.js';

export function detectConflicts(s) {
  for (const [trailingId, leaderId] of Object.entries(LEADER)) {
    const trailing = s.crews[trailingId], leader = s.crews[leaderId];
    const key = `${trailingId}:${leaderId}`;
    // A completed leader has left the workfront; the job endpoint cannot be crossed.
    if (leader.finishT != null || s.conflicts.some(c => c.pair === key)
      || trailing.mult <= 0 || trailing.pos >= leader.pos - EPS) continue;
    if (trailing.pos + trailing.rate * trailing.mult * CONFLICT_LOOKAHEAD_DAYS
      <= leader.pos + leader.rate * leader.mult * CONFLICT_LOOKAHEAD_DAYS + EPS) continue;
    s.conflicts.push({ pair: key, trailing: trailingId, leader: leaderId, firedT: s.t, resolved: false });
    trailing.arguing = leader.arguing = true;
    trailing.mult = leader.mult = 0;
    trailing.status = leader.status = 'arguing';
    s.speed = 'slow';
    voice(s, 'conflict', ['site'], [trailingId, leaderId]);
    emit(s, 'event', { eventId: `conflict_${trailingId}_${leaderId}`, day: s.t,
      stationFt: trailing.pos, zone: null, seats: ['site'],
      card: { title: 'Crews stopped', facts: [`${trailing.name} and ${leader.name} foremen are arguing.`] } });
  }
}

export function validateConflictCommit(s, ops) {
  if (s.conflicts.some(c => !c.resolved && !ops.some(op => op?.crew === c.trailing
    && (op.op === 'hold' || op.op === 'demob')))) return 'conflict: hold or demobilize the trailing crew';
}

export function resolveConflicts(s) {
  for (const conflict of s.conflicts.filter(c => !c.resolved)) {
    conflict.resolved = true;
    conflict.resolvedT = s.t;
    // The trailing crew waits for its field order; only the leader resumes at commit.
    s.crews[conflict.leader].arguing = false;
  }
}
