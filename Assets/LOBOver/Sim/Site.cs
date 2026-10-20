using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using static LOBOver.Sim.Common;
using static LOBOver.Sim.Json;

namespace LOBOver.Sim
{
    internal static class Site
    {
        private static readonly HashSet<string> TARGETS = new HashSet<string> { "wall", "water", "pipe", "machine", "protection", "board", "sky", "ticket", "crew" };
        private const int MAX_NOTE_LENGTH = 40; // PROTOCOL.md: Site flag notes.
        internal static string ApplySite(State s, JObject cmd)
        {
            var truck = s.truck; var action = cmd.S("action");
            if (action == "moveTo")
            {
                if (truck.B("moving") || truck.B("dismounted")) return "mount before moving";
                if (!cmd.Finite("stationFt") || cmd.N("stationFt") < 0 || cmd.N("stationFt") > s.Length) return "invalid station";
                truck["fromFt"] = truck["stationFt"]; truck["toFt"] = cmd["stationFt"]; truck["departT"] = s.t;
                truck["arrivesT"] = s.t + TravelDays(truck.N("stationFt"), cmd.N("stationFt"));
                truck["moving"] = truck.N("arrivesT") > s.t;
                return null;
            }
            if (action == "mount" || action == "dismount")
            {
                if (truck.B("moving")) return "truck moving";
                truck["dismounted"] = action == "dismount"; return null;
            }
            if (!truck.B("dismounted") || truck.B("moving")) return "dismount first";
            if (action == "inspect")
            {
                if (!TARGETS.Contains(cmd.S("target"))) return "unknown inspection target";
                var ft = Views.StakeAt(s, truck.N("stationFt"));
                if (cmd.S("target") == "protection") Deviations.InspectProtection(s, ft);
                var e = ActiveCollapse(s);
                if (e != null && Views.StakeAt(s, e.N("stationFt")) == ft) e["inspected"] = true;
                var reply = O("target", cmd["target"], "text", Views.InspectionText(s, cmd.S("target")), "seats", new[] { "site" });
                if (cmd.Property("id") != null) reply["id"] = cmd["id"];
                Emit(s, "inspection", reply); return null;
            }
            if (action == "flag")
            {
                if (!cmd.Finite("stationFt") || cmd.N("stationFt") < 0 || cmd.N("stationFt") > s.Length
                    || cmd.Has("note") && (cmd["note"].Type != JTokenType.String || cmd.S("note").Length > MAX_NOTE_LENGTH)) return "invalid flag";
                var flag = O("stationFt", cmd["stationFt"], "day", s.t);
                if (!string.IsNullOrEmpty(cmd.S("note"))) flag["note"] = cmd["note"];
                s.flags.Add(flag); return null;
            }
            if (action == "stopWork")
            {
                if (!cmd.Finite("zone") || ZoneAt(s, truck.N("stationFt")).N("id", double.NaN) != cmd.N("zone")) return "outside zone";
                var stop = O("zone", cmd["zone"], "fromT", s.t + s.Rules.N("stopWorkReachHours") / HOURS_PER_DAY);
                foreach (var id in CREW_IDS)
                {
                    var c = s.crews[id];
                    if (ZoneAt(s, c.pos).N("id", double.NaN) != cmd.N("zone")) continue;
                    if (s.t >= c.startDay - 1 - EPS) c.stop = stop.Copy(); else s.pendingStops[id] = stop.Copy();
                }
                return null;
            }
            if (action == "confirm")
            {
                var order = s.orders.FirstOrDefault(o => o.S("id") == cmd.S("orderId"));
                if (order.S("crew") == null || !order.Has("landedT") || order.S("status") == "sent" || !Nearby(s, s.crews[order.S("crew")].pos)) return "order not landed here";
                if (cmd.S("outcome") != "done" && cmd.S("outcome") != "notDone") return "invalid outcome";
                order["status"] = cmd.S("outcome") == "done" ? "confirmed" : "notCarriedOut";
                Deviations.ConfirmDeviation(s, order, cmd.S("outcome"));
                if (cmd.S("outcome") == "notDone") Voice(s, "orderNotDone", new[] { "plan" });
                return null;
            }
            if (action == "listen")
            {
                var c = CrewAt(s);
                if (c?.arguing == true)
                {
                    var conflict = s.conflicts.First(x => x.S("trailing") == c.id || x.S("leader") == c.id);
                    Voice(s, "conflict", new[] { "site" }, new[] { conflict.S("trailing"), conflict.S("leader") });
                }
                else if (c != null && (c.status == "held" || c.status == "stopped") && c.mood > 0) Voice(s, "heldDay" + F(c.mood), new[] { "site" }, new[] { c.id });
                else
                {
                    var reply = O("target", "crew", "text", "Nothing to hear.", "seats", new[] { "site" });
                    if (cmd.Property("id") != null) reply["id"] = cmd["id"];
                    Emit(s, "inspection", reply);
                }
                return null;
            }
            return "unknown action";
        }
        internal static void UpdateTruck(State s)
        {
            var truck = s.truck;
            if (!truck.B("moving")) return;
            if (s.t >= truck.N("arrivesT") - EPS) { truck["stationFt"] = truck["toFt"]; truck["moving"] = false; }
            else truck["stationFt"] = truck.N("fromFt") + (truck.N("toFt") - truck.N("fromFt")) * (s.t - truck.N("departT")) / (truck.N("arrivesT") - truck.N("departT"));
        }
    }
}
