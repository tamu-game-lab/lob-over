using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using static LOBOver.Sim.Common;
using static LOBOver.Sim.Json;

namespace LOBOver.Sim
{
    internal static class Views
    {
        private static JToken Clean(JToken value)
        {
            if (value is JArray array) return new JArray(array.Select(Clean));
            if (value is JObject obj) return new JObject(obj.Properties().Where(p => !p.Name.StartsWith("_", StringComparison.Ordinal)).Select(p => new JProperty(p.Name, Clean(p.Value))));
            return value.DeepClone();
        }
        internal static double StakeAt(State s, double ft) => Math.Floor(ft / s.Job.N("stationEveryFt") + 0.5) * s.Job.N("stationEveryFt");
        private static bool CollapseHere(State s, double ft)
        {
            var e = ActiveCollapse(s); return e != null && StakeAt(s, e.N("stationFt")) == StakeAt(s, ft);
        }
        private static JObject GroundAt(State s, double ft) => s.scenario.Obj("ground").Rows("segments").FirstOrDefault(x => ft >= x.N("fromFt") && ft <= x.N("toFt"));
        internal static JObject TrenchAt(State s, double stationFt)
        {
            bool Passed(string id) => s.crews[id].pos > stationFt + EPS || s.crews[id].finishT != null;
            var state = !Passed("exc") ? "undug" : !Passed("pipe") ? "open" : !Passed("back") ? "piped" : "backfilled";
            var open = state == "open" || state == "piped"; var p = ProtectionAt(s, stationFt);
            return O("stationFt", stationFt, "state", state,
                "water", CollapseHere(s, stationFt) || open && Rain.RainOrRework(s) ? "standing" : open && GroundAt(s, stationFt).B("seepage") ? "seep" : "none",
                "wall", CollapseHere(s, stationFt) ? "slumped" : "sound", "protection", p != null ? p.S("kind").Replace("trench_", "") : "none", "pipeDamage", "none");
        }
        internal static string CrewText(State s, Crew c)
        {
            if (c == null) return "No machine within sight.";
            var text = "Working.";
            var leader = Leader(c.id) == null ? null : s.crews[Leader(c.id)];
            if (c.status == "demobilized") text = "Demobilized.";
            else if (c.status == "arguing")
            {
                var conflict = s.conflicts.FirstOrDefault(x => !x.B("resolved") && (x.S("trailing") == c.id || x.S("leader") == c.id));
                var other = conflict == null ? null : s.crews[conflict.S("trailing") == c.id ? conflict.S("leader") : conflict.S("trailing")];
                text = "Arguing with " + (other?.name ?? leader?.name ?? c.name) + ".";
            }
            else if (c.status == "stopped") text = "Stopped: stop-work.";
            else if (c.status == "held") text = "Held by the office since Day " + F(DisplayDay(c.hold.N("fromT"))) + ".";
            else if (c.status == "idle") text = "Idle, waiting on " + (leader?.name ?? c.name) + ".";
            return text + " " + MOODS[(int)c.mood];
        }
        internal static string InspectionText(State s, string target)
        {
            var ft = StakeAt(s, s.truck.N("stationFt")); var c = CrewAt(s); var p = ProtectionAt(s, ft);
            var zone = ZoneAt(s, ft); var segment = GroundAt(s, ft);
            if (target == "wall")
            {
                if (CollapseHere(s, ft)) return "Wall slumped into the trench.";
                if (s.crews["exc"].pos <= ft + EPS) return "Not excavated here yet.";
                return segment.Obj("indicator").S("wall") ?? s.scenario.Obj("ground").Obj("defaultIndicator").S("wall");
            }
            if (target == "water")
            {
                if (TrenchAt(s, ft).S("water") == "standing") return "Standing water in the trench bottom.";
                return segment.Obj("indicator").S("water") ?? s.scenario.Obj("ground").Obj("defaultIndicator").S("water");
            }
            if (target == "machine")
            {
                var machine = c == null ? null : s.Machine(c.machine);
                return machine == null ? "No machine within sight." : c.name + ": " + machine.S("label") + ", plate " + machine.S("sizeClass");
            }
            if (target == "protection") return p == null ? "No protection here." : s.catalog.Obj("protection").Obj(p.S("kind")).S("label") + " in place, " + F(p.N("fromFt")) + " to " + F(p.N("toFt"));
            if (target == "board")
            {
                var permit = s.permits[State.ZoneKey(zone)]; var inspection = s.inspections[State.ZoneKey(zone)];
                return "Zone " + F(zone.N("id")) + " board: " + (permit.B("entered") ? "permit " + permit.S("number") : "no permit posted")
                    + (inspection.B("cleared") ? "; inspection " + inspection.S("number") : "");
            }
            if (target == "sky") return Rain.Sky(s) == "rain" ? "Rain." : Rain.Sky(s) == "towersSouth" ? "Towers building to the south." : "Clear.";
            if (target == "ticket")
            {
                var delivery = s.deliveries.FirstOrDefault(d => !d.B("arrived"));
                return delivery == null ? "No truck at the gate." : "Delivery ticket: " + s.Machine(delivery.S("machine")).S("label") + ", due Day " + F(DisplayDay(delivery.N("arrivesT")));
            }
            if (target == "crew") return CrewText(s, c);
            if (target == "pipe") return s.crews["back"].pos > ft + EPS ? "Backfilled." : s.crews["pipe"].pos > ft + EPS ? "Pipe laid, open trench." : "No pipe here yet.";
            return null;
        }
        internal static JObject PublicOrder(JObject order) => O("id", order["id"], "op", order["op"], "crew", order["crew"], "issuedDay", order["issuedT"], "landsDay", order["landsT"], "status", order["status"]);
        internal static JObject PlanView(State s)
        {
            var machines = new JObject();
            foreach (var entry in s.catalog.Obj("machines").Properties().Where(p => !p.Name.StartsWith("_", StringComparison.Ordinal)))
            {
                var machine = (JObject)Clean(entry.Value);
                machine["leadDays"] = O("normal", entry.Value.Obj("leadDays").N("normal") + s.City.N("leadDelayDays"));
                machines[entry.Name] = machine;
            }
            var permits = new JArray(); var inspections = new JArray();
            foreach (var z in s.Zones)
            {
                var permit = s.permits[State.ZoneKey(z)];
                var item = O("zone", z["id"], "entered", permit["entered"]);
                if (permit.B("entered") && permit.Has("number")) item["number"] = permit["number"];
                permits.Add(item); inspections.Add(O("zone", z["id"], "cleared", s.inspections[State.ZoneKey(z)]["cleared"]));
            }
            var uncertainty = new JObject();
            foreach (var id in CREW_IDS)
            {
                var report = s.reportedLines[id].Last(); var line = s.plannedLines[id];
                var estimate = report.N("ft") + Planning.PlannedRate(line, s.t) * Math.Max(0, s.t - Math.Max(report.N("t"), line.N("startDay") - 1));
                var band = s.t <= report.N("t") + EPS ? 0 : s.Rules.N("uncertaintyBandFt");
                uncertainty[id] = O("estimateFt", Math.Min(s.Length, estimate), "fromFt", Math.Max(0, estimate - band), "toFt", Math.Min(s.Length, estimate + band), "asOfT", report["t"]);
            }
            return O("plannedLines", s.plannedLines,
                "responses", new JArray(Events.AvailableResponses(s).Select(r => O("key", r.Option["key"], "label", r.Option["label"], "cost", r.Option["cost"], "eventTitle", r.Event.Obj("card")["title"]))),
                "reportedLines", s.reportedLines, "budget", O("budgetUsd", s.Plan["budgetUsd"], "spentUsd", s.spentUsd, "committedUsd", s.spentUsd,
                    "deadlineDay", s.Plan["deadlineDay"], "promisedDay", s.Plan["promisedFinishDay"]),
                "inbox", s.inbox, "radioLog", new JArray(s.orders.Select(PublicOrder)), "catalog", O("machines", machines, "protection", Clean(s.catalog["protection"])),
                "permits", permits, "inspections", inspections, "flags", s.flags, "draft", new JArray(), "uncertainty", uncertainty, "pmLines", s.pmLines);
        }
        internal static JObject SiteView(State s)
        {
            var trench = new JArray();
            for (double ft = 0; ft <= s.Length; ft += s.Job.N("stationEveryFt")) trench.Add(TrenchAt(s, ft));
            var local = s.events.FirstOrDefault(e => e.B("fired") && !e.B("resolved") && (!e.Has("zone")
                || e.N("zone") == ZoneAt(s, s.truck.N("stationFt")).N("id", double.NaN) || e.B("inspected")));
            return O("truck", O("stationFt", s.truck["stationFt"], "moving", s.truck["moving"], "dismounted", s.truck["dismounted"]),
                "crews", new JArray(CREW_IDS.Select(id =>
                {
                    var c = s.crews[id]; var size = s.Machine(c.machine).S("sizeClass");
                    return O("id", id, "stationFt", c.pos, "status", c.status, "machinePlate", string.IsNullOrEmpty(size) ? null : char.ToUpperInvariant(size[0]) + size.Substring(1), "moodLevel", c.mood);
                })), "trench", trench,
                "boards", new JArray(s.Zones.Select(z => O("zone", z["id"], "permitNumber", s.permits[State.ZoneKey(z)].B("entered") ? s.permits[State.ZoneKey(z)].Get("number") : null,
                    "inspectionNumber", s.inspections[State.ZoneKey(z)].B("cleared") ? s.inspections[State.ZoneKey(z)].Get("number") : null))),
                "sky", Rain.Sky(s), "gate", O("trucksWaiting", s.deliveries.Count(d => !d.B("arrived"))),
                "activeEvent", local == null ? null : O("id", local["id"], "card", local["shownCard"]),
                "orders", new JArray(s.orders.Where(o => o.S("crew") != null && o.Has("landedT") && o.S("status") != "sent" && o.S("status") != "confirmed"
                    && Nearby(s, s.crews[o.S("crew")].pos)).Select(PublicOrder)));
        }
        internal static JObject View(State s, string seat)
        {
            if (seat == "plan") return PlanView(s);
            if (seat == "site") return SiteView(s);
            if (seat == "facilitator")
            {
                var state = JObject.FromObject(s); state.Remove("messages");
                state["deviations"] = new JArray(s.deviations.Select(d => { var result = d.Copy(); result["state"] = !d.B("triggered") ? "armed" : d.B("caught") ? "caught" : "missed"; return result; }));
                state["plan"] = PlanView(s); state["site"] = SiteView(s); return state;
            }
            throw new ArgumentException("unknown seat");
        }
    }
}
