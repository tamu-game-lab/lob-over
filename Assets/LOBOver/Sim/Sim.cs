using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static LOBOver.Sim.Common;
using static LOBOver.Sim.Json;

namespace LOBOver.Sim
{
    public sealed class ApplyResult
    {
        [JsonProperty("ok")] public bool Ok { get; }
        [JsonProperty("appliedDay", NullValueHandling = NullValueHandling.Ignore)] public double? AppliedDay { get; }
        [JsonProperty("reason", NullValueHandling = NullValueHandling.Ignore)] public string Reason { get; }
        internal ApplyResult(double day) { Ok = true; AppliedDay = day; }
        internal ApplyResult(string reason) { Reason = reason; }
    }

    public sealed class Sim
    {
        private State state;
        private readonly JObject source, fleet;
        private readonly bool pure;
        public double T => state.t;
        public string Speed => state.speed;
        public bool Done => state.done;
        public JObject Result => state.result.Copy();

        // WO-05a: alternatives to these authored switches are intentionally unsupported.
        private static readonly JObject SUPPORTED_RULES = O("orderLagShifts", 1, "heldCrewBills", true,
            "conflictRule", "crossWithinNextDay", "conflictBehaviour", "stopAndArgue", "unpermittedBoundary", "idleAndBill",
            "pumpHalvesRework", true, "likelihoodRollsApply", false);
        private const double SUPPORTED_SHIFTS_PER_DAY = 1;
        private const int TIME_DECIMALS = 10; // sim.js quantizes tick boundary arithmetic.

        private Sim(Scenario scenario, Catalog catalog, bool pure)
        {
            source = scenario.Data.Copy(); fleet = catalog.Data.Copy(); this.pure = pure;
            foreach (var rule in SUPPORTED_RULES.Properties())
            {
                var actual = source.Obj("rules").Get(rule.Name);
                if (pure && actual == null) continue;
                var matches = rule.Value.Type == JTokenType.Integer ? source.Obj("rules").Finite(rule.Name) && source.Obj("rules").N(rule.Name) == (double)rule.Value
                    : JToken.DeepEquals(actual, rule.Value);
                if (!matches) throw new ArgumentException("Unsupported rules." + rule.Name + ": expected " + rule.Value.ToString(Formatting.None)
                    + ", got " + (actual?.ToString(Formatting.None) ?? "undefined"));
            }
            var time = source.Obj("time");
            if (!(pure && time.Get("shiftsPerDay") == null) && (!time.Finite("shiftsPerDay") || time.N("shiftsPerDay") != SUPPORTED_SHIFTS_PER_DAY))
                throw new ArgumentException("Unsupported time.shiftsPerDay: expected 1, got " + (time.Get("shiftsPerDay")?.ToString(Formatting.None) ?? "undefined"));
            state = InitialState(source, fleet, pure);
        }
        public static Sim Create(Scenario scenario, Catalog catalog, bool pure = false)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            return new Sim(scenario, catalog, pure);
        }
        private static State InitialState(JObject scenario, JObject catalog, bool pure)
        {
            var s = new State { scenario = scenario, catalog = catalog, pure = pure, rngState = new Rng(scenario.N("seed")).State,
                capFt = scenario.NullableN("capFt"), truck = O("stationFt", 0, "moving", false, "dismounted", false),
                events = scenario.Rows("events").Select(e => e.Copy()).ToList(), deviations = scenario.Rows("deviations").Select(d => d.Copy()).ToList() };
            foreach (var id in CREW_IDS)
            {
                var authored = scenario.Rows("crews").First(c => c.S("id") == id);
                var c = new Crew { id = id, name = authored.S("name"), machine = authored.S("machine"), startDay = authored.N("startDay"), locked = authored.B("locked"),
                    rate = s.Machine(authored.S("machine")).N("rateFtPerDay") };
                s.crews[id] = c;
                s.actualLines[id] = new List<JObject> { O("t", 0, "ft", 0) };
                s.reportedLines[id] = new List<JObject> { O("t", 0, "ft", 0) };
                s.plannedLines[id] = O("startDay", c.startDay, "rate", c.rate, "holds", new JArray(), "segments", new JArray(O("fromT", c.startDay - 1, "rate", c.rate)));
            }
            foreach (var z in s.Zones)
            {
                var key = State.ZoneKey(z);
                s.permits[key] = O("entered", pure || scenario.Obj("permits").S("heldBy") == "pm", "signed", false);
                var permit = scenario.Obj("permits").Rows("cards").FirstOrDefault(c => c.N("zone") == z.N("id"));
                if (permit != null) s.permits[key]["number"] = permit["number"];
                s.inspections[key] = O("cleared", pure || !scenario.Obj("inspections").B("required"), "reported", false);
                var inspection = scenario.Obj("inspections").Rows("cards").FirstOrDefault(c => c.N("zone") == z.N("id"));
                if (inspection != null) s.inspections[key]["number"] = inspection["number"];
            }
            if (!pure) { Voice(s, "stageStart", new[] { "plan", "site" }); Events.UpdateEvents(s); }
            Crews.RefreshCrews(s);
            return s;
        }
        private static void RecordActual(State s)
        {
            foreach (var id in CREW_IDS)
                if (s.actualLines[id].Last().N("t") != s.t) s.actualLines[id].Add(O("t", s.t, "ft", s.crews[id].pos));
        }
        private static void Report(State s)
        {
            var feet = new JObject();
            foreach (var id in CREW_IDS) feet[id] = Math.Floor(s.crews[id].pos + EPS);
            var notes = CREW_IDS.Select(id => s.crews[id].name + ": " + F(feet.N(id) - s.reportedLines[id].Last().N("ft")) + " ft today").ToArray();
            foreach (var id in CREW_IDS) s.reportedLines[id].Add(O("t", s.t, "ft", feet[id]));
            var item = O("day", s.t, "feetByCrew", feet, "spendUsd", s.spentUsd, "notes", notes);
            var inbox = item.Copy(); inbox["from"] = "Daily report"; inbox["subject"] = "Day " + F(s.t); s.inbox.Add(inbox);
            item["seats"] = new JArray("plan", "facilitator"); Emit(s, "report", item);
        }
        private static void Finish(State s)
        {
            var lost = s.pure ? null : s.spentUsd > s.Plan.N("budgetUsd") ? "budget" : s.t > s.Plan.N("deadlineDay") - 1 + EPS ? "deadline" : null;
            var complete = s.crews["back"].finishT != null && (s.pure || Permits.FinalInspectionEntered(s));
            if (lost == null && !complete) return;
            RecordActual(s);
            var idleDays = new JObject(); double idleCrewDays = 0;
            foreach (var id in CREW_IDS) { var idle = Math.Max(0, s.crews[id].onsite - s.crews[id].work); idleDays[id] = idle; idleCrewDays += idle; }
            s.done = true;
            var result = lost != null ? O("lost", lost) : O("finishDay", s.crews["back"].finishT);
            if (s.Plan.Has("promisedFinishDay")) result["promisedDay"] = s.Plan["promisedFinishDay"];
            if (s.Plan.Has("budgetUsd")) result["budgetUsd"] = s.Plan["budgetUsd"];
            result["spentUsd"] = s.spentUsd; result["crewCostUsd"] = s.crewCostUsd;
            result["idleDays"] = idleDays; result["idleCrewDays"] = idleCrewDays;
            result["peakOpenTrenchFt"] = s.peakOpenTrenchFt; result["finesUsd"] = s.finesUsd;
            result["deviationsCaught"] = s.deviations.Count(d => d.B("triggered") && d.B("caught"));
            result["deviationsMissed"] = s.deviations.Count(d => d.B("triggered") && !d.B("caught"));
            result["events"] = new JArray(s.events.Where(e => e.B("fired")).Select(e => O("id", e["id"], "day", e["firedT"], "option", e.Get("option"),
                "daysLost", e.S("type") == "A2" ? e.N("durationDays") * e.N("rateDrop") + e.N("reworkDays") : (e.N("resolvedT", s.t) - e.N("firedT")) * e.N("rateDrop"),
                "protection", e["protection"])));
            result["changeLog"] = Token(s.changeLog);
            result["lines"] = O("planned", Planning.PlannedPolylines(s), "actual", s.actualLines);
            s.result = result;
            if (!s.pure)
            {
                Voice(s, "stageEnd", new[] { "plan", "site" });
                var message = O("stage", s.scenario["id"]);
                foreach (var field in result.Properties()) message[field.Name] = field.Value.DeepClone();
                message["seats"] = new JArray("plan", "site", "facilitator"); Emit(s, "result", message);
            }
        }
        private static State TickState(State state, double dt)
        {
            if (state.done || state.speed == "paused") return state;
            var s = state.Draft();
            if (!s.pure) { Site.UpdateTruck(s); Orders.UpdateOrders(s); Events.UpdateEvents(s); }
            Crews.RefreshCrews(s);
            if (!s.pure) Conflict.DetectConflicts(s);
            Crews.MoveCrews(s, dt); Money.Accrue(s, dt);
            if (!s.pure) Crews.UpdateMood(s, dt);
            s.t = Math.Round(s.t + dt, TIME_DECIMALS, MidpointRounding.AwayFromZero);
            if (!s.pure)
            { Site.UpdateTruck(s); Orders.UpdateOrders(s); Permits.UpdateHoldPoints(s); Events.UpdateEvents(s); Crews.RefreshCrews(s); }
            if (Math.Floor(s.t + EPS) > Math.Floor(state.t + EPS)) { RecordActual(s); if (!s.pure) Report(s); }
            Finish(s);
            return s;
        }
        private static (State State, string Reason) Commit(State state, JArray ops)
        {
            if (ops == null) return (null, "invalid commit");
            var conflictReason = Conflict.ValidateConflictCommit(state, ops);
            if (conflictReason != null) return (null, conflictReason);
            var s = state.Draft();
            foreach (var token in ops)
            {
                var op = token as JObject; var reason = Orders.ValidateOp(s, op);
                if (reason != null) return (null, reason);
                Orders.IssueOrder(s, op);
            }
            Conflict.ResolveConflicts(s);
            foreach (var id in CREW_IDS)
                if (s.crews[id].stop != null && !ops.Any(op => (op.S("op") == "hold" || op.S("op") == "resume") && op.S("crew") == id)
                    && !s.orders.Any(o => o.Obj("op").S("op") == "resume" && o.S("crew") == id && !o.Has("appliedT")))
                    Orders.IssueOrder(s, O("op", "resume", "crew", id));
            s.pendingStops.Clear(); Orders.UpdateOrders(s); Permits.UpdateHoldPoints(s);
            if (s.crews["back"].finishT == null && s.crews["back"].pos >= s.Length - EPS && Permits.FinalInspectionEntered(s)) s.crews["back"].finishT = s.t;
            Crews.RefreshCrews(s);
            var change = O("day", s.t, "ops", ops); if (ops.Count == 0) change["note"] = "no change"; s.changeLog.Add(change);
            if (s.speed == "paused") s.pausedSpeed = "full"; else s.speed = "full";
            Finish(s);
            return (s, null);
        }
        public void Tick(double dt = DT)
        {
            if (!double.IsFinite(dt) || dt <= 0) throw new ArgumentOutOfRangeException(nameof(dt), "dt must be positive");
            var remaining = dt;
            while (remaining > EPS && !state.done && state.speed != "paused")
            {
                var step = Math.Min(DT, remaining); state = TickState(state, step); remaining -= step;
            }
        }
        public ApplyResult Apply(string seat, JObject cmd)
        {
            if (cmd == null) return new ApplyResult("invalid command");
            var action = cmd.S("action");
            if (seat == "facilitator")
            {
                if (action == "load" && cmd.S("stage") != source.S("id")) return new ApplyResult("stage not loaded");
                if (action == "reset" || action == "load") state = InitialState(source, fleet, pure);
                else if (action == "pause")
                { state = state.Draft(); if (state.speed != "paused") state.pausedSpeed = state.speed; state.speed = "paused"; }
                else if (action == "start" || action == "resume") { state = state.Draft(); state.speed = state.pausedSpeed; }
                else return new ApplyResult("unknown control");
                return new ApplyResult(state.t);
            }
            if (state.done) return new ApplyResult("stage ended");
            if (seat == "plan" && action == "commit")
            {
                var result = Commit(state, cmd["ops"] as JArray);
                if (result.Reason != null)
                {
                    if (result.Reason == "not in the budget") { state = state.Draft(); Voice(state, "prepRefused", new[] { "plan" }); }
                    return new ApplyResult(result.Reason);
                }
                state = result.State;
            }
            else if (seat == "site")
            {
                var draft = state.Draft(); var reason = Site.ApplySite(draft, cmd);
                if (reason != null) return new ApplyResult(reason);
                state = draft;
            }
            else return new ApplyResult("action unavailable for seat");
            return new ApplyResult(state.t);
        }
        public JObject View(string seat) => Views.View(state, seat);
        // presenter-only (MR diorama); the full-scale seat and the JavaScript sim do not have it
        public JObject InspectAt(string target, double stationFt) => Views.InspectAt(state, target, stationFt);
        public List<JObject> Drain()
        {
            var messages = state.messages.Select(m => m.Copy()).ToList();
            state.messages = new List<JObject>(); return messages;
        }
        public JObject Serialize() => JObject.FromObject(state);
    }
}
