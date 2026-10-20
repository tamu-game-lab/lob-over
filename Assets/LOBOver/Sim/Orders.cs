using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using static LOBOver.Sim.Common;
using static LOBOver.Sim.Json;

namespace LOBOver.Sim
{
    internal static class Orders
    {
        private static readonly HashSet<string> OFFICE_OPS = new HashSet<string> { "setStart", "enterPermit", "enterInspection", "pay" };
        private static readonly HashSet<string> CREW_OPS = new HashSet<string> { "setStart", "hold", "resume", "demob", "remob" };
        private static readonly HashSet<string> PLAN_OPS = new HashSet<string>(CREW_OPS.Concat(OFFICE_OPS).Concat(new[] { "lend", "order", "protect", "radio" }));
        private static bool Positive(JObject op, string key) => op.Finite(key) && op.N(key) > 0;
        private static bool Owns(JObject obj, string key) => key != null && !key.StartsWith("_", StringComparison.Ordinal) && obj.Property(key) != null;
        private static string OwnerOf(State s, string machine) => CREW_IDS.FirstOrDefault(id => s.crews[id].machine == machine);
        private static bool MachineCommitted(State s, string machine) => s.orders.Any(order => order.Obj("op").S("op") == "lend"
            && order.Obj("op").S("machine") == machine && !order.Has("appliedT"));
        internal static double LandingTime(State s, string crew) => s.pure || crew != null && Nearby(s, s.crews[crew].pos) ? s.t : NextBoundary(s.t);
        internal static string ValidateOp(State s, JObject op)
        {
            if (op == null || !PLAN_OPS.Contains(op.S("op"))) return "unknown operation";
            var name = op.S("op"); var crewId = op.S("crew");
            if (CREW_OPS.Contains(name) && !CREW_IDS.Contains(crewId)) return "unknown crew";
            var c = crewId != null && s.crews.TryGetValue(crewId, out var crew) ? crew : null;
            if (name == "setStart")
            {
                if (s.Plan.B("locked") || c.locked) return "plan locked";
                if (s.t >= c.startDay - 1 - EPS) return "crew already started";
                if (!op.Integer("day") || op.N("day") < DisplayDay(s.t)) return "invalid start day";
            }
            if (name == "hold" && (op.Has("days") && !Positive(op, "days")
                || op.Has("fromDay") && (!op.Integer("fromDay") || op.N("fromDay") < DisplayDay(s.t)))) return "invalid hold";
            if (name == "lend")
            {
                if (MachineCommitted(s, op.S("machine"))) return "machine already committed";
                var owner = OwnerOf(s, op.S("machine"));
                if (owner == null || s.crews[owner].lentAway || s.loans.Any(l => l.S("fromCrew") == owner && l.S("phase") != "returned")) return "machine unavailable";
                if (!CREW_IDS.Contains(op.S("toCrew")) || op.S("toCrew") == owner || !Positive(op, "days")) return "invalid lend";
            }
            if (name == "protect" || name == "order")
            {
                if (!s.Plan.B("preparationAllowed")) return "not in the budget";
                if (name == "order")
                {
                    if (!Owns(s.catalog.Obj("machines"), op.S("machine"))) return "unknown machine";
                    if (op.B("rush")) return "rush unavailable";
                }
                else
                {
                    if (!Owns(s.catalog.Obj("protection"), op.S("kind"))) return "unknown protection";
                    var protection = s.catalog.Obj("protection").Obj(op.S("kind"));
                    if (protection.B("reactOnly")) return "no such response now";
                    if (op.S("kind") == "trench_slope" && (s.City.B("slopeRefused") || protection.Includes("refusedInCities", s.scenario.S("city")))) return "slope refused";
                    if (!op.Finite("fromFt") || !op.Finite("toFt") || op.N("fromFt") < 0 || op.N("toFt") > s.Length || op.N("fromFt") >= op.N("toFt")) return "invalid stretch";
                }
            }
            if (name == "pay")
            {
                if (op.S("item") == "clear_own" && MachineCommitted(s, s.crews["exc"].machine)) return "machine already committed";
                if (!Events.AvailableResponses(s).Any(r => r.Option.S("key") == op.S("item"))) return "no such response now";
                if (op.S("item") == "hire_excavator" && (!Owns(s.catalog.Obj("machines"), op.S("machine")) || s.Machine(op.S("machine")).S("crew") != "exc")) return "unknown machine";
                if (op.S("item") == "clear_own" && s.crews["exc"].lentAway) return "machine unavailable";
            }
            if (name == "enterPermit") return Permits.ValidatePermit(s, op);
            if (name == "enterInspection") return Permits.ValidateInspection(s, op);
            if (name == "radio" && !s.orders.Any(order => order.S("id") == op.S("orderId"))) return "order not on file";
            return null;
        }
        private static void StartLoan(State s, JObject op)
        {
            var fromCrew = OwnerOf(s, op.S("machine"));
            var e = fromCrew == "exc" && op.S("toCrew") == "pipe" ? ActiveCollapse(s) : null;
            s.loans.Add(O("machine", op["machine"], "fromCrew", fromCrew, "toCrew", op["toCrew"], "phase", "outbound",
                "arrivesT", s.t + TravelDays(s.crews[fromCrew].pos, s.crews[op.S("toCrew")].pos), "days", op["days"], "eventId", e?.Get("id")));
            s.crews[fromCrew].lentAway = true;
            if (e != null) e["option"] = "clear_own";
            Deviations.FirstLend(s);
        }
        private static void Purchase(State s, string machine, JObject e = null)
        {
            Money.BuyMachine(s, machine);
            s.deliveries.Add(O("machine", machine, "crew", s.Machine(machine)["crew"], "issuedT", s.t,
                "arrivesT", s.t + s.Machine(machine).Obj("leadDays").N("normal") + s.City.N("leadDelayDays"),
                "hired", e != null, "eventId", e?.Get("id"), "arrived", false, "released", false));
        }
        private static void Pay(State s, JObject op)
        {
            var response = Events.AvailableResponses(s).First(r => r.Option.S("key") == op.S("item"));
            var e = response.Event; var option = response.Option; var effect = option.Obj("effect");
            e["option"] = op["item"];
            if (op.S("item") == "hire_excavator") { Purchase(s, op.S("machine"), e); return; }
            s.spentUsd += option.Obj("cost").N("usd");
            s.payments.Add(O("item", op["item"], "paidT", s.t, "perDayUsd", option.Obj("cost")["perDayUsd"]));
            if (effect.Has("lend"))
            {
                var fromCrew = effect.Obj("lend").S("machineOf");
                IssueOrder(s, O("op", "lend", "machine", s.crews[fromCrew].machine, "toCrew", effect.Obj("lend")["toCrew"],
                    "days", Events.ClearingDays(s, e, s.crews[fromCrew].machine)));
            }
            if (effect.Has("resumeAfterDays")) e["endsT"] = s.t + effect.N("resumeAfterDays");
            if (effect.Has("rainRateRestore"))
            { e["pumpPaidT"] = s.t; e["restore"] = effect["rainRateRestore"].DeepClone(); e["reworkMultiplier"] = effect["reworkMultiplier"]; }
        }
        private static void ApplyLanded(State s, JObject order)
        {
            var op = order.Obj("op"); var name = op.S("op");
            var c = order.S("crew") == null ? null : s.crews[order.S("crew")];
            if (name == "hold")
            {
                var fromT = Math.Max(s.t, op.Has("fromDay") ? op.N("fromDay") - 1 : s.t);
                c.hold = O("fromT", fromT, "untilT", op.Has("days") ? (double?)(fromT + op.N("days")) : null);
                c.arguing = false; c.stop = null;
            }
            else if (name == "resume") { c.hold = null; c.stop = null; }
            else if (name == "lend") StartLoan(s, op);
            else if (name == "protect") s.protections.Add(O("kind", op["kind"], "fromFt", op["fromFt"], "toFt", op["toFt"], "placedT", s.t));
            else if (name == "demob") { c.demobilized = true; c.demobBillingEndsT = NextBoundary(s.t); c.arguing = false; }
            else if (name == "remob") c.remobT = s.t + s.City.N("remobilizationDelayDays");
            order["appliedT"] = s.t;
        }
        internal static void IssueOrder(State s, JObject op)
        {
            var name = op.S("op");
            if (name == "radio")
            {
                var previous = s.orders.First(order => order.S("id") == op.S("orderId"));
                previous["issuedT"] = s.t; previous["landsT"] = LandingTime(s, previous.S("crew"));
                previous["status"] = "sent"; previous["ignored"] = false; previous["reissued"] = true;
                previous["alreadyApplied"] = previous.Has("appliedT");
                return;
            }
            var crew = op.S("crew") ?? (name == "lend" ? OwnerOf(s, op.S("machine"))
                : name == "order" ? s.Machine(op.S("machine")).S("crew") : name == "protect" ? "exc" : null);
            var order = O("id", "o-" + s.nextOrderId++, "op", op, "crew", crew, "issuedT", s.t,
                "landsT", OFFICE_OPS.Contains(name) ? s.t : LandingTime(s, crew), "status", "sent");
            s.orders.Add(order);
            Planning.ProjectOrder(s, order);
            if (!s.pure) Deviations.MarkFirstRadio(s, order);
            if (name == "protect") s.spentUsd += s.catalog.Obj("protection").Obj(op.S("kind")).N("priceUsd");
            if (name == "order") Purchase(s, op.S("machine"));
            if (name == "remob") s.spentUsd += s.Rules.N("demobFeeUsd");
            if (name == "hold") Voice(s, "holdCommitted", new[] { "plan" });
            if (name == "demob") Voice(s, "demobCommitted", new[] { "plan" });
            if (!OFFICE_OPS.Contains(name)) return;
            if (name == "setStart")
            {
                s.crews[op.S("crew")].startDay = op.N("day");
                s.plannedLines[op.S("crew")]["startDay"] = op["day"];
                s.plannedLines[op.S("crew")]["segments"][0]["fromT"] = op.N("day") - 1;
            }
            else if (name == "enterPermit") s.permits[State.ZoneKey(op, "zone")]["entered"] = true;
            else if (name == "enterInspection") { s.inspections[State.ZoneKey(op, "zone")]["cleared"] = true; Permits.UpdateHoldPoints(s); }
            else if (name == "pay") Pay(s, op);
            order["appliedT"] = s.t; order["landedT"] = s.t; order["status"] = "landed";
        }
        internal static void UpdateOrders(State s)
        {
            foreach (var order in s.orders)
            {
                if (order.S("status") != "sent" || s.t < order.N("landsT") - EPS) continue;
                order["status"] = "landed"; order["landedT"] = s.t;
                Voice(s, "orderLanded", new[] { "plan" });
                if (!order.B("ignored") && !order.B("alreadyApplied")) ApplyLanded(s, order);
            }
            foreach (var loan in s.loans)
            {
                if (loan.S("phase") == "outbound" && s.t >= loan.N("arrivesT") - EPS)
                {
                    loan["phase"] = "working";
                    var e = s.events.FirstOrDefault(e => e.S("id") == loan.S("eventId"));
                    loan["returnsT"] = s.t + (e != null ? Events.ClearingDays(s, e, loan.S("machine")) : loan.N("days"));
                    if (e != null) { e["clearingT"] = s.t; e["endsT"] = loan["returnsT"]; }
                }
                if (loan.S("phase") == "working" && s.t >= loan.N("returnsT") - EPS)
                {
                    loan["phase"] = "returning";
                    loan["homeT"] = s.t + TravelDays(s.crews[loan.S("toCrew")].pos, s.crews[loan.S("fromCrew")].pos);
                }
                if (loan.S("phase") == "returning" && s.t >= loan.N("homeT") - EPS)
                { loan["phase"] = "returned"; s.crews[loan.S("fromCrew")].lentAway = false; }
            }
            foreach (var delivery in s.deliveries)
            {
                if (!delivery.B("arrived") && s.t >= delivery.N("arrivesT") - EPS)
                {
                    delivery["arrived"] = true;
                    if (delivery.B("hired"))
                    {
                        var e = s.events.First(e => e.S("id") == delivery.S("eventId"));
                        e["clearingT"] = s.t; e["endsT"] = s.t + Events.ClearingDays(s, e, delivery.S("machine"));
                        delivery["releaseT"] = e["endsT"];
                    }
                    else
                    {
                        var crew = s.crews[delivery.S("crew")];
                        Money.ReleaseMachine(s, crew.machine); crew.machine = delivery.S("machine");
                    }
                }
                if (delivery.B("hired") && delivery.B("arrived") && !delivery.B("released") && s.t >= delivery.N("releaseT") - EPS)
                { delivery["released"] = true; Money.ReleaseMachine(s, delivery.S("machine")); }
            }
        }
    }
}
