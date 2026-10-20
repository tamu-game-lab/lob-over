using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using static LOBOver.Sim.Common;
using static LOBOver.Sim.Json;

namespace LOBOver.Sim
{
    internal static class Permits
    {
        internal static string ValidatePermit(State s, JObject op)
        {
            var card = s.scenario.Obj("permits").Rows("cards").FirstOrDefault(c => op.Finite("zone") && c.N("zone") == op.N("zone"));
            if (card == null || !JToken.DeepEquals(card["code"], op["code"])) return "code not on file";
            if (!s.permits[State.ZoneKey(op, "zone")].B("entered") && s.Zones.Count(z => s.permits[State.ZoneKey(z)].B("entered") && !s.permits[State.ZoneKey(z)].B("signed"))
                >= s.scenario.Obj("permits").N("openZoneLimit")) return "city limit: " + F(s.scenario.Obj("permits").N("openZoneLimit")) + " zones open";
            return null;
        }
        internal static string ValidateInspection(State s, JObject op)
        {
            var card = s.scenario.Obj("inspections").Rows("cards").FirstOrDefault(c => op.Finite("zone") && c.N("zone") == op.N("zone"));
            return card == null || !JToken.DeepEquals(card["number"], op["number"]) ? "code not on file" : null;
        }
        internal static double ExcavationLimit(State s, double desired)
        {
            var zone = s.Zones.FirstOrDefault(z => !s.permits[State.ZoneKey(z)].B("entered"));
            if (zone == null) return s.Length;
            var limit = zone.N("fromFt");
            if (desired >= limit - EPS && s.crews["exc"].pos >= limit - EPS) limit = Deviations.BoundaryAllowance(s, zone);
            return Math.Max(s.crews["exc"].pos, limit);
        }
        internal static double BackfillLimit(State s) => !s.scenario.Obj("inspections").B("required") ? s.Length
            : s.Zones.FirstOrDefault(z => !s.inspections[State.ZoneKey(z)].B("cleared")).N("toFt", s.Length);
        internal static void UpdateHoldPoints(State s)
        {
            foreach (var zone in s.Zones)
            {
                var inspection = s.inspections[State.ZoneKey(zone)];
                if (s.crews["back"].pos < zone.N("toFt") - EPS) continue;
                if (!inspection.B("reported") && s.scenario.Obj("inspections").B("required"))
                {
                    inspection["reported"] = true;
                    // WO-02: both authored Texas stages use this compaction fact.
                    s.inbox.Add(O("day", s.t, "from", "City inspector", "subject", "Zone " + F(zone.N("id")) + " compaction", "body", "passed"));
                }
                if (inspection.B("cleared") || !s.scenario.Obj("inspections").B("required")) s.permits[State.ZoneKey(zone)]["signed"] = true;
            }
        }
        internal static bool FinalInspectionEntered(State s) => !s.scenario.Obj("inspections").B("required") || s.inspections[State.ZoneKey(s.Zones.Last())].B("cleared");
    }
}
