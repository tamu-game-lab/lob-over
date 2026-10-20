using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace LOBOver.Sim
{
    internal static class Rain
    {
        private const double REWORK_CHUNK_FT = 500; // WO-02 A2: commenced 500 ft.
        internal static double ReworkDuration(double openFt, double daysPer500Ft, double multiplier = 1)
            => Math.Ceiling(openFt / REWORK_CHUNK_FT) * daysPer500Ft * multiplier;
        internal static double RainMultiplier(State s, string crewId)
        {
            double mult = 1;
            foreach (var e in s.events.Where(e => e.S("type") == "A2" && e.B("fired") && !e.B("rainEnded")))
                mult *= e.Includes("restore", crewId) ? 1 : 1 - e.N("rateDrop");
            return mult;
        }
        internal static void FinishRain(State s, JObject e)
        {
            e["rainEnded"] = true;
            e["reworkDays"] = ReworkDuration(e.N("openFt"), e.Obj("rework").N("daysPer500Ft", s.Rules.N("rainReworkDaysPer500Ft")),
                e.Obj("rework").B("pumpHalves") && e.Has("pumpPaidT") && e.N("pumpPaidT") < e.N("endsT") ? e.N("reworkMultiplier") : 1);
            e["reworkEndsT"] = e.N("endsT") + e.N("reworkDays");
        }
        internal static bool RainOrRework(State s) => s.events.Any(e => e.S("type") == "A2" && e.B("fired") && !e.B("resolved"));
        internal static bool PipeReworking(State s) => s.events.Any(e => e.S("type") == "A2" && e.B("rainEnded") && !e.B("resolved"));
        internal static string Sky(State s)
        {
            if (s.events.Any(e => e.S("type") == "A2" && e.B("fired") && !e.B("rainEnded"))) return "rain";
            return s.events.Any(e => !e.B("fired") && e.Has("forecast") && s.t >= e.Obj("forecast").N("skyFromDay") - 1) ? "towersSouth" : "clear";
        }
    }
}
