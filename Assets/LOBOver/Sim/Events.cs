using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using static LOBOver.Sim.Common;
using static LOBOver.Sim.Json;

namespace LOBOver.Sim
{
    internal static class Events
    {
        internal static double ClearingDays(State s, JObject e, string machine)
        {
            var size = s.Machine(machine).S("sizeClass");
            var severity = e.Has("protection") ? e.Obj("severityByProtection").N(e.S("protection"), 1) : 1;
            // JavaScript Math.round chooses the upper integer at a positive half.
            return Math.Floor(e.Obj("durationDaysByExcavator").N(size) * severity / CLEARING_DAY_STEP + 0.5) * CLEARING_DAY_STEP;
        }
        internal static IEnumerable<(JObject Event, JObject Option)> AvailableResponses(State s) => s.events
            .Where(e => e.B("fired") && !e.B("resolved") || e.Has("forecast") && s.t >= e.Obj("forecast").N("inboxDay") - 1 && !e.B("resolved"))
            .SelectMany(e => e.Rows("options").Select(option => (e, option)));
        internal static void UpdateEvents(State s)
        {
            foreach (var e in s.events)
            {
                var forecast = e.Obj("forecast");
                if (forecast != null && !e.B("forecastSent") && s.t >= forecast.N("inboxDay") - 1 - EPS)
                {
                    e["forecastSent"] = true;
                    s.inbox.Add(O("day", forecast.N("inboxDay") - 1, "from", "Forecast", "subject", "Weather", "body", forecast["text"]));
                }
                if (!e.B("fired"))
                {
                    var trigger = e.Obj("trigger");
                    var ready = trigger.S("kind") == "day" ? s.t >= trigger.N("day") - 1 - EPS
                        : trigger.S("kind") == "crewReaches" ? s.crews[trigger.S("crew")].pos >= trigger.N("ft") - EPS
                        : trigger.S("kind") == "zoneSigned" && s.permits[State.ZoneKey(trigger, "zone")].B("signed");
                    if (!ready) continue;
                    e["fired"] = true; e["firedT"] = s.t;
                    e["protection"] = ProtectionAt(s, e.NullableN("stationFt"))?.Get("kind") ?? JValue.CreateNull();
                    e["shownCard"] = (e.Has("protection") && e.Has("cardWhenProtected") ? e["cardWhenProtected"] : e["card"]).DeepClone();
                    e["endsT"] = e.S("type") == "P1" ? JValue.CreateNull() : new JValue(s.t + e.N("durationDays"));
                    if (e.S("type") == "A2") e["openFt"] = Math.Max(0, s.crews["exc"].pos - s.crews["back"].pos);
                    s.speed = "slow";
                    Emit(s, "event", O("eventId", e["id"], "day", s.t, "zone", e["zone"], "stationFt", e["stationFt"], "card", e["shownCard"], "seats", e["visibleTo"]));
                    Deviations.EventDeviations(s, e);
                }
                if (e.B("resolved") || !e.Has("endsT") || s.t < e.N("endsT") - EPS) continue;
                if (e.S("type") == "A2")
                {
                    if (!e.B("rainEnded")) Rain.FinishRain(s, e);
                    if (s.t < e.N("reworkEndsT") - EPS) continue;
                }
                e["resolved"] = true; e["resolvedT"] = s.t;
            }
            var index = 0;
            foreach (var item in s.scenario.Rows("inbox"))
            {
                if (!s.inboxDelivered.Contains(index) && s.t >= item.N("day") - 1 - EPS)
                {
                    s.inboxDelivered.Add(index);
                    var delivered = item.Copy(); delivered["day"] = item.N("day") - 1; s.inbox.Add(delivered);
                }
                index++;
            }
        }
    }
}
