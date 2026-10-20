using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LOBOver.Sim
{
    // JSON helpers preserve the authored field names and missing/null distinction.
    internal static class Json
    {
        internal static JToken Get(this JToken value, string key) => value is JObject obj && key != null ? obj[key] : null;
        internal static bool Has(this JToken value, string key) => value.Get(key) != null && value.Get(key).Type != JTokenType.Null;
        internal static double N(this JToken value, string key, double fallback = 0) => value.Has(key) ? (double)value.Get(key) : fallback;
        internal static double? NullableN(this JToken value, string key) => value.Has(key) ? (double?)value.Get(key) : null;
        internal static string S(this JToken value, string key) => (string)value.Get(key);
        internal static bool B(this JToken value, string key) => value.Get(key)?.Type == JTokenType.Boolean && (bool)value.Get(key);
        internal static JObject Obj(this JToken value, string key) => value.Get(key) as JObject;
        internal static IEnumerable<JObject> Rows(this JToken value, string key) => (value.Get(key) as JArray ?? new JArray()).OfType<JObject>();
        internal static bool Includes(this JToken value, string key, string text) => (value.Get(key) as JArray)?.Any(x => x.Type == JTokenType.String && (string)x == text) == true;
        internal static bool Finite(this JToken value, string key) => value.Get(key) is JValue v
            && (v.Type == JTokenType.Integer || v.Type == JTokenType.Float) && double.IsFinite((double)v);
        internal static bool Integer(this JToken value, string key) => value.Finite(key) && value.N(key) == Math.Floor(value.N(key));
        internal static JObject Copy(this JObject value) => value == null ? null : (JObject)value.DeepClone();
        internal static JToken Token(object value) => value == null ? JValue.CreateNull() : value is JToken token ? token.DeepClone() : JToken.FromObject(value);
        internal static JObject O(params object[] pairs)
        {
            var result = new JObject();
            for (var i = 0; i < pairs.Length; i += 2) result[(string)pairs[i]] = Token(pairs[i + 1]);
            return result;
        }
        internal static string F(double number) => number.ToString("G", CultureInfo.InvariantCulture);
    }

    internal static class Common
    {
        // Monitor/src/sim/common.js, WO-02: crew order, precision, travel and mood.
        internal static readonly string[] CREW_IDS = { "exc", "pipe", "back" };
        internal static readonly string[] MOODS = { "calm", "grumbling", "angry", "furious" };
        internal const double DT = 0.01, EPS = 1e-6, NEAR_FT = 100, HAUL_FT_PER_HOUR = 1500, HOURS_PER_DAY = 24;
        internal const double MAX_MOOD = 3, CONFLICT_LOOKAHEAD_DAYS = 1, CLEARING_DAY_STEP = 0.5;
        internal static string Leader(string id) => id == "pipe" ? "exc" : id == "back" ? "pipe" : null;
        internal static double TravelDays(double a, double b) => Math.Abs(a - b) / HAUL_FT_PER_HOUR / HOURS_PER_DAY;
        internal static double DisplayDay(double t) => Math.Floor(t + EPS) + 1;
        internal static double NextBoundary(double t) => Math.Floor(t + EPS) + 1;
        internal static JObject ZoneAt(State s, double ft) => s.Zones.FirstOrDefault(z => ft >= z.N("fromFt") && (ft < z.N("toFt") || ft == s.Length));
        internal static bool Nearby(State s, double ft) => s.truck.B("dismounted") && !s.truck.B("moving") && Math.Abs(s.truck.N("stationFt") - ft) <= NEAR_FT;
        internal static Crew CrewAt(State s) => CREW_IDS.Select(id => s.crews[id]).Where(c => Math.Abs(c.pos - s.truck.N("stationFt")) <= NEAR_FT)
            .OrderBy(c => Math.Abs(c.pos - s.truck.N("stationFt"))).FirstOrDefault();
        internal static JObject ProtectionAt(State s, double? ft) => ft.HasValue && double.IsFinite(ft.Value)
            ? s.protections.FirstOrDefault(p => ft >= p.N("fromFt") && ft <= p.N("toFt")) : null;
        internal static JObject ActiveCollapse(State s) => s.events.FirstOrDefault(e => e.S("type") == "P1" && e.B("fired") && !e.B("resolved"));
        internal static void Emit(State s, string type, JObject fields)
        {
            var message = Json.O("t", type, "v", 1, "day", s.t);
            foreach (var field in fields.Properties()) message[field.Name] = field.Value.DeepClone();
            s.messages.Add(message);
        }
        internal static void Voice(State s, string trigger, string[] seats, string[] crewIds = null)
        {
            var lines = s.scenario.Rows("lines").Where(line => line.S("trigger") == trigger
                && (crewIds == null || crewIds.Contains(line.S("speaker").Replace("foreman_", "")))).ToList();
            if (lines.Count == 0) return;
            Emit(s, "voice", Json.O("lineIds", lines.Select(line => line.S("id")), "seats", seats));
            s.pmLines.AddRange(lines.Where(line => line.S("speaker") == "pm").Select(line => line.S("text")));
        }
    }

    internal sealed class Crew
    {
        public string id, name, machine, status = "idle", idleReason;
        public double startDay, pos, work, onsite, rate, mult, mood, waitDays, workingDays, moved;
        public double? finishT, remobT, demobBillingEndsT;
        public bool locked, arguing, demobilized, lentAway;
        public JObject hold, stop;
        internal Crew Copy()
        {
            var c = (Crew)MemberwiseClone();
            c.hold = hold.Copy(); c.stop = stop.Copy();
            return c;
        }
    }

    internal sealed class State
    {
        public JObject scenario, catalog, result, truck;
        public bool pure, done;
        public uint rngState;
        public double t, spentUsd, crewCostUsd, finesUsd, peakOpenTrenchFt;
        public double? capFt;
        public string speed = "full", pausedSpeed = "full";
        public int nextOrderId = 1;
        public Dictionary<string, Crew> crews = new Dictionary<string, Crew>();
        public Dictionary<string, JObject> pendingStops = new Dictionary<string, JObject>();
        public Dictionary<string, JObject> permits = new Dictionary<string, JObject>(), inspections = new Dictionary<string, JObject>();
        public Dictionary<string, JObject> plannedLines = new Dictionary<string, JObject>();
        public Dictionary<string, List<JObject>> actualLines = new Dictionary<string, List<JObject>>(), reportedLines = new Dictionary<string, List<JObject>>();
        public List<JObject> orders = new List<JObject>(), loans = new List<JObject>(), deliveries = new List<JObject>(), payments = new List<JObject>();
        public List<JObject> protections = new List<JObject>(), events = new List<JObject>(), deviations = new List<JObject>(), conflicts = new List<JObject>();
        public List<JObject> flags = new List<JObject>(), inbox = new List<JObject>(), messages = new List<JObject>(), changeLog = new List<JObject>();
        public List<int> inboxDelivered = new List<int>();
        public List<string> pmLines = new List<string>();
        [JsonIgnore] internal JObject Job => scenario.Obj("job");
        [JsonIgnore] internal JObject Rules => scenario.Obj("rules");
        [JsonIgnore] internal JObject Plan => scenario.Obj("plan");
        [JsonIgnore] internal JObject City => catalog.Obj("cities").Obj(scenario.S("city"));
        [JsonIgnore] internal IEnumerable<JObject> Zones => Job.Rows("zones");
        [JsonIgnore] internal double Length => Job.N("lengthFt");
        internal JObject Machine(string key) => key == null ? null : catalog.Obj("machines").Obj(key);
        internal static string ZoneKey(JToken obj, string key = "id") => Json.F(obj.N(key));

        // Reducers own a draft. Authored data and append-only history entries are shared.
        internal State Draft()
        {
            var s = (State)MemberwiseClone();
            s.crews = Common.CREW_IDS.ToDictionary(id => id, id => crews[id].Copy());
            s.truck = truck.Copy(); s.result = result.Copy();
            s.pendingStops = CloneMap(pendingStops); s.permits = CloneMap(permits); s.inspections = CloneMap(inspections);
            s.plannedLines = CloneMap(plannedLines);
            s.orders = CloneList(orders); s.loans = CloneList(loans); s.deliveries = CloneList(deliveries); s.payments = CloneList(payments);
            s.protections = CloneList(protections); s.events = CloneList(events); s.deviations = CloneList(deviations); s.conflicts = CloneList(conflicts);
            s.flags = CloneList(flags); s.inboxDelivered = new List<int>(inboxDelivered); s.pmLines = new List<string>(pmLines);
            s.actualLines = Common.CREW_IDS.ToDictionary(id => id, id => new List<JObject>(actualLines[id]));
            s.reportedLines = Common.CREW_IDS.ToDictionary(id => id, id => new List<JObject>(reportedLines[id]));
            s.inbox = new List<JObject>(inbox); s.messages = new List<JObject>(messages); s.changeLog = new List<JObject>(changeLog);
            return s;
        }
        private static Dictionary<string, JObject> CloneMap(Dictionary<string, JObject> map) => map.ToDictionary(p => p.Key, p => p.Value.Copy());
        private static List<JObject> CloneList(List<JObject> list) => list.Select(x => x.Copy()).ToList();
    }
}
