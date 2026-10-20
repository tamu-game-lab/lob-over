using System.Linq;
using Newtonsoft.Json.Linq;
using static LOBOver.Sim.Common;

namespace LOBOver.Sim
{
    internal static class Deviations
    {
        internal static void MarkFirstRadio(State s, JObject order)
        {
            var d = s.deviations.FirstOrDefault(d => d.S("type") == "F1" && !d.B("triggered")
                && d.Obj("when").Includes("ops", order.Obj("op").S("op")) && order.N("landsT") > order.N("issuedT"));
            if (d == null) return;
            d["triggered"] = true; d["orderId"] = order["id"]; order["ignored"] = true;
        }
        internal static void ConfirmDeviation(State s, JObject order, string outcome)
        {
            var d = s.deviations.FirstOrDefault(d => d.S("type") == "F1" && d.S("orderId") == order.S("id"));
            if (d != null && outcome == "notDone" && s.t < NextBoundary(order.N("landedT")) - EPS) d["caught"] = true;
        }
        internal static double BoundaryAllowance(State s, JObject zone)
        {
            // WO-05a O10: only a boundary between zones can trigger F2.
            if (!s.Zones.Any(previous => previous.N("toFt") == zone.N("fromFt"))) return zone.N("fromFt");
            var d = s.deviations.FirstOrDefault(d => d.S("type") == "F2");
            if (d == null) return zone.N("fromFt");
            if (d.B("triggered")) return d.N("zone") == zone.N("id") && !d.B("caught") ? zone.N("fromFt") + d.N("overshootFt") : zone.N("fromFt");
            d["triggered"] = true; d["zone"] = zone["id"];
            d["caught"] = Nearby(s, zone.N("fromFt")) || s.crews["exc"].hold != null || s.crews["exc"].stop != null;
            if (!d.B("caught")) { s.finesUsd += s.Rules.N("permitFineUsd"); s.spentUsd += s.Rules.N("permitFineUsd"); }
            return d.B("caught") ? zone.N("fromFt") : zone.N("fromFt") + d.N("overshootFt");
        }
        internal static void InspectProtection(State s, double stationFt)
        {
            var p = ProtectionAt(s, stationFt);
            if (p == null) return;
            foreach (var d in s.deviations.Where(d => d.S("type") == "F3"))
            {
                var e = s.events.First(e => e.S("id") == d.Obj("when").S("eventId"));
                if (!e.B("fired") && !(e.N("stationFt") >= p.N("fromFt") && e.N("stationFt") <= p.N("toFt"))) d["inspected"] = true;
            }
        }
        internal static void EventDeviations(State s, JObject e)
        {
            foreach (var d in s.deviations.Where(d => d.S("type") == "F3" && d.Obj("when").S("eventId") == e.S("id")))
            {
                var missedStretch = s.protections.Any(p => e.N("stationFt") < p.N("fromFt") || e.N("stationFt") > p.N("toFt"));
                if (missedStretch && ProtectionAt(s, e.NullableN("stationFt")) == null)
                { d["triggered"] = true; d["caught"] = d.B("inspected"); }
            }
        }
        // WO-02 reserves F5 without specifying any delay in the authored stages.
        internal static void FirstLend(State s)
        {
            foreach (var d in s.deviations.Where(d => d.S("type") == "F5" && !d.B("triggered"))) d["triggered"] = true;
        }
    }
}
