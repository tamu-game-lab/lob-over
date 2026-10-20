using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using static LOBOver.Sim.Common;
using static LOBOver.Sim.Json;

namespace LOBOver.Sim
{
    internal static class Planning
    {
        // Issued instructions and catalog rates, never live crew positions.
        internal static double PlannedRate(JObject line, double t)
        {
            if (t < line.N("startDay") - 1) return 0;
            if (line.Rows("holds").Any(h => t >= h.N("fromT") && (!h.Has("untilT") || t < h.N("untilT")))) return 0;
            var segment = line.Rows("segments").OrderBy(x => x.N("fromT")).LastOrDefault(x => x.N("fromT") <= t);
            double extra = 0;
            foreach (var loan in line.Rows("loans").Where(l => t >= l.N("fromT") && t < l.N("untilT"))) extra += loan.N("rate");
            return segment.N("rate", line.N("rate")) + extra;
        }
        internal static JObject PlannedPolylines(State s)
        {
            var result = new JObject();
            foreach (var id in CREW_IDS)
            {
                var line = s.plannedLines[id];
                var breaks = new List<double> { line.N("startDay") - 1 };
                breaks.AddRange(line.Rows("segments").Select(x => x.N("fromT")));
                foreach (var h in line.Rows("holds")) { breaks.Add(h.N("fromT")); if (h.Has("untilT")) breaks.Add(h.N("untilT")); }
                foreach (var loan in line.Rows("loans")) { breaks.Add(loan.N("fromT")); breaks.Add(loan.N("untilT")); }
                breaks = breaks.Distinct().Where(t => t >= line.N("startDay") - 1).OrderBy(t => t).ToList();
                var time = line.N("startDay") - 1; double ft = 0;
                var points = new JArray(O("t", time, "ft", ft));
                var complete = false;
                foreach (var next in breaks.Skip(1))
                {
                    var rate = PlannedRate(line, time);
                    if (rate > 0 && ft + (next - time) * rate >= s.Length)
                    {
                        points.Add(O("t", time + (s.Length - ft) / rate, "ft", s.Length));
                        complete = true; break;
                    }
                    ft += (next - time) * rate; time = next; points.Add(O("t", time, "ft", ft));
                }
                if (!complete)
                {
                    var rate = PlannedRate(line, time + EPS);
                    if (rate > 0) points.Add(O("t", time + (s.Length - ft) / rate, "ft", s.Length));
                    else if (s.t > time) points.Add(O("t", s.t, "ft", ft));
                }
                result[id] = points;
            }
            return result;
        }
        internal static void ProjectOrder(State s, JObject order)
        {
            var op = order.Obj("op");
            var line = order.S("crew") == null ? null : s.plannedLines[order.S("crew")];
            var name = op.S("op");
            if (name == "hold" || name == "demob")
            {
                var fromT = Math.Max(order.N("landsT"), op.Has("fromDay") ? op.N("fromDay") - 1 : order.N("landsT"));
                ((JArray)line["holds"]).Add(O("fromT", fromT, "untilT", op.Has("days") ? (double?)(fromT + op.N("days")) : null, "kind", name));
            }
            if (name == "resume" || name == "remob")
            {
                var untilT = order.N("landsT") + (name == "remob" ? s.City.N("remobilizationDelayDays") : 0);
                foreach (var hold in line.Rows("holds"))
                    if ((name == "resume" ? hold.S("kind") == "hold" : hold.S("kind") == "demob") && (!hold.Has("untilT") || hold.N("untilT") > untilT))
                        hold["untilT"] = Math.Max(hold.N("fromT"), untilT);
            }
            if (name == "order")
            {
                var machine = s.Machine(op.S("machine"));
                ((JArray)line["segments"]).Add(O("fromT", order.N("issuedT") + machine.Obj("leadDays").N("normal") + s.City.N("leadDelayDays"), "rate", machine.N("rateFtPerDay")));
            }
            if (name == "lend")
            {
                var fromT = order.N("landsT"); var untilT = fromT + op.N("days");
                ((JArray)line["holds"]).Add(O("fromT", fromT, "untilT", untilT, "kind", "lend"));
                if (!s.events.Any(e => e.S("type") == "P1" && e.B("fired") && !e.B("resolved") && order.S("crew") == "exc" && op.S("toCrew") == "pipe"))
                {
                    var recipient = s.plannedLines[op.S("toCrew")];
                    if (!recipient.Has("loans")) recipient["loans"] = new JArray();
                    ((JArray)recipient["loans"]).Add(O("fromT", fromT, "untilT", untilT, "rate", s.Machine(op.S("machine")).N("rateFtPerDay")));
                }
            }
        }
    }
}
