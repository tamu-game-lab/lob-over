using System.Linq;
using static LOBOver.Sim.Common;

namespace LOBOver.Sim
{
    internal static class Money
    {
        internal static bool Billing(State s, Crew c) => s.t >= c.startDay - 1 - EPS && (c.finishT == null || c.finishT > s.t + EPS)
            && !(c.demobBillingEndsT != null && s.t >= c.demobBillingEndsT - EPS);
        internal static void Accrue(State s, double dt)
        {
            var overhead = 1 + s.Rules.N("overheadRate");
            double crewDaily = 0;
            foreach (var id in CREW_IDS)
            {
                var c = s.crews[id];
                if (Billing(s, c)) { c.onsite += dt; crewDaily += s.Machine(c.machine).N("dailyUsd"); }
            }
            double protectionDaily = 0;
            foreach (var p in s.protections)
                if (CREW_IDS.Any(id => { var c = s.crews[id]; return c.mult > 0 && c.moved > EPS && c.pos >= p.N("fromFt") && c.pos <= p.N("toFt"); }))
                    protectionDaily += s.catalog.Obj("protection").Obj(p.S("kind")).N("dailyUsd");
            double hiredDaily = 0, optionDaily = 0;
            foreach (var d in s.deliveries.Where(d => d.B("hired") && d.B("arrived") && !d.B("released"))) hiredDaily += s.Machine(d.S("machine")).N("dailyUsd");
            foreach (var p in s.payments) optionDaily += p.N("perDayUsd");
            s.crewCostUsd += crewDaily * dt * overhead;
            s.spentUsd += (crewDaily + protectionDaily + hiredDaily) * dt * overhead + optionDaily * dt;
        }
        internal static void BuyMachine(State s, string machine) => s.spentUsd += s.Machine(machine).N("priceUsd") + s.Rules.N("mobilizationUsd");
        internal static void ReleaseMachine(State s, string machine)
        {
            if (machine != null) s.spentUsd -= s.Machine(machine).N("priceUsd") * s.catalog.Obj("orders").N("releaseRefundFraction");
        }
    }
}
