using System.Linq;
using Newtonsoft.Json.Linq;
using static LOBOver.Sim.Common;
using static LOBOver.Sim.Json;

namespace LOBOver.Sim
{
    internal static class Conflict
    {
        internal static void DetectConflicts(State s)
        {
            foreach (var trailingId in CREW_IDS.Skip(1))
            {
                var leaderId = Leader(trailingId);
                var trailing = s.crews[trailingId]; var leader = s.crews[leaderId];
                var key = trailingId + ":" + leaderId;
                if (leader.finishT != null || s.conflicts.Any(c => c.S("pair") == key) || trailing.mult <= 0 || trailing.pos >= leader.pos - EPS) continue;
                if (trailing.pos + trailing.rate * trailing.mult * CONFLICT_LOOKAHEAD_DAYS
                    <= leader.pos + leader.rate * leader.mult * CONFLICT_LOOKAHEAD_DAYS + EPS) continue;
                s.conflicts.Add(O("pair", key, "trailing", trailingId, "leader", leaderId, "firedT", s.t, "resolved", false));
                trailing.arguing = leader.arguing = true;
                trailing.mult = leader.mult = 0; trailing.status = leader.status = "arguing";
                s.speed = "slow";
                Voice(s, "conflict", new[] { "site" }, new[] { trailingId, leaderId });
                Emit(s, "event", O("eventId", "conflict_" + trailingId + "_" + leaderId, "day", s.t,
                    "stationFt", trailing.pos, "zone", null, "seats", new[] { "site" },
                    "card", O("title", "Crews stopped", "facts", new[] { trailing.name + " and " + leader.name + " foremen are arguing." })));
            }
        }
        internal static string ValidateConflictCommit(State s, JArray ops) => s.conflicts.Any(c => !c.B("resolved")
            && !ops.Any(op => op.S("crew") == c.S("trailing") && (op.S("op") == "hold" || op.S("op") == "demob")))
            ? "conflict: hold or demobilize the trailing crew" : null;
        internal static void ResolveConflicts(State s)
        {
            foreach (var c in s.conflicts.Where(c => !c.B("resolved")))
            {
                c["resolved"] = true; c["resolvedT"] = s.t;
                s.crews[c.S("leader")].arguing = false;
            }
        }
    }
}
