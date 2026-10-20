using System;
using System.Linq;
using static LOBOver.Sim.Common;

namespace LOBOver.Sim
{
    internal static class Crews
    {
        private const double MOOD_RECOVERY_DAYS = 1; // WO-05a (a): a full working day.
        internal static void RefreshCrews(State s)
        {
            foreach (var id in CREW_IDS)
            {
                var c = s.crews[id];
                if (s.pendingStops.TryGetValue(id, out var stop) && s.t >= c.startDay - 1 - EPS)
                { c.stop = stop; s.pendingStops.Remove(id); }
                if (c.hold.Has("untilT") && s.t >= c.hold.N("untilT") - EPS) c.hold = null;
                if (c.remobT != null && s.t >= c.remobT - EPS)
                { c.demobilized = false; c.demobBillingEndsT = null; c.remobT = null; }
                c.rate = s.Machine(c.machine).N("rateFtPerDay");
                foreach (var loan in s.loans.Where(l => l.S("toCrew") == id && l.S("phase") == "working" && !l.Has("eventId")))
                    c.rate += s.Machine(loan.S("machine")).N("rateFtPerDay");
                var held = c.hold != null && s.t >= c.hold.N("fromT") - EPS;
                var stopped = c.stop != null && s.t >= c.stop.N("fromT") - EPS;
                c.status = c.demobilized ? "demobilized" : c.arguing ? "arguing" : stopped ? "stopped" : held ? "held" : "working";
                c.mult = 1;
                if (s.t < c.startDay - 1 - EPS || c.finishT != null) { c.mult = 0; c.status = "idle"; }
                if (c.demobilized || c.arguing || stopped || held || c.lentAway || c.rate == 0) c.mult = 0;
                if (!s.pure)
                {
                    c.mult *= Rain.RainMultiplier(s, id);
                    foreach (var e in s.events.Where(e => e.B("fired") && !e.B("resolved") && e.S("type") != "A2"
                        && (e.S("hits") == id || e.S("hits") == "all"))) c.mult *= 1 - e.N("rateDrop");
                    if (id == "pipe" && Rain.PipeReworking(s)) c.mult = 0;
                }
                if (c.mult == 0 && c.status == "working") c.status = "idle";
                if (c.status == "working" && c.idleReason != null && c.moved < EPS) c.status = "idle";
            }
        }

        internal static void MoveCrews(State s, double dt)
        {
            foreach (var id in CREW_IDS)
            {
                var c = s.crews[id];
                if (s.t < c.startDay - 1 - EPS || c.finishT != null) continue;
                var leader = Leader(id) == null ? null : s.crews[Leader(id)];
                var limit = Math.Min(s.Length, leader?.pos ?? s.Length);
                var desired = c.pos + c.rate * c.mult * dt;
                c.idleReason = null;
                if (s.pure && id == "exc" && s.capFt != null) limit = Math.Min(limit, s.crews["back"].pos + s.capFt.Value);
                if (!s.pure && id == "exc")
                {
                    var permitLimit = Permits.ExcavationLimit(s, c.mult > 0 ? desired : c.pos);
                    limit = Math.Min(limit, permitLimit);
                    if (desired > permitLimit || c.pos >= permitLimit - EPS && permitLimit < s.Length) c.idleReason = "idleNoPermit";
                }
                if (!s.pure && id == "back")
                {
                    var holdPoint = Permits.BackfillLimit(s);
                    limit = Math.Min(limit, holdPoint);
                    if (desired > holdPoint || c.pos >= holdPoint - EPS && !Permits.FinalInspectionEntered(s)) c.idleReason = "idleHoldPoint";
                }
                desired = Math.Max(c.pos, Math.Min(desired, limit));
                var moved = desired - c.pos;
                if (moved > 0) c.work += moved / c.rate;
                c.pos = desired; c.moved = moved;
                if (leader != null && moved < c.rate * c.mult * dt - EPS && c.idleReason == null) c.idleReason = "leader";
                if (c.status == "working" && moved < EPS) c.status = "idle";
                if (c.pos >= s.Length - EPS && (id != "back" || s.pure || Permits.FinalInspectionEntered(s))) c.finishT = s.t + dt;
            }
            s.peakOpenTrenchFt = Math.Max(s.peakOpenTrenchFt, s.crews["exc"].pos - s.crews["back"].pos);
        }

        internal static void UpdateMood(State s, double dt)
        {
            foreach (var id in CREW_IDS)
            {
                var c = s.crews[id];
                if (s.t < c.startDay - 1 - EPS) continue;
                var held = c.status == "held" || c.status == "stopped";
                var waiting = held || c.idleReason == "leader";
                c.waitDays = waiting ? c.waitDays + dt : 0;
                var level = Math.Min(MAX_MOOD, Math.Floor((c.waitDays + EPS) / s.Rules.N("moodStepDays")));
                if (held && level > c.mood)
                    for (var mood = c.mood + 1; mood <= level; mood++) Voice(s, "heldDay" + Json.F(mood), new[] { "site" }, new[] { id });
                c.workingDays = c.status == "working" && c.moved > EPS ? c.workingDays + dt : 0;
                c.mood = c.workingDays >= MOOD_RECOVERY_DAYS - EPS ? 0 : Math.Max(c.mood, level);
            }
        }
    }
}
