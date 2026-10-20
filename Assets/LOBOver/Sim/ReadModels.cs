using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace LOBOver.Sim
{
    // Names deliberately match views.js so Unity consumes the same seat contract.
    public sealed class SiteView
    {
        public TruckView truck;
        public List<CrewView> crews;
        public List<TrenchView> trench;
        public List<BoardView> boards;
        public string sky;
        public GateView gate;
        public ActiveEventView activeEvent;
        public List<OrderView> orders;
        public static SiteView From(JObject value) => (value ?? throw new ArgumentNullException(nameof(value))).ToObject<SiteView>();
    }
    public sealed class TruckView { public double stationFt; public bool moving, dismounted; }
    public sealed class CrewView { public string id, status, machinePlate; public double stationFt, moodLevel; }
    public sealed class TrenchView { public double stationFt; public string state, water, wall, protection, pipeDamage; }
    public sealed class BoardView { public double zone; public string permitNumber, inspectionNumber; }
    public sealed class GateView { public double trucksWaiting; }
    public sealed class CardView { public string title; public List<string> facts; }
    public sealed class ActiveEventView { public string id; public CardView card; }
    public sealed class OrderView { public string id, crew, status; public JObject op; public double issuedDay, landsDay; }

    public sealed class PlanView
    {
        public Dictionary<string, PlannedLineView> plannedLines;
        public List<ResponseView> responses;
        public Dictionary<string, List<PointView>> reportedLines;
        public BudgetView budget;
        public List<InboxView> inbox;
        public List<OrderView> radioLog;
        public CatalogView catalog;
        public List<PermitView> permits;
        public List<InspectionView> inspections;
        public List<FlagView> flags;
        public List<JObject> draft;
        public Dictionary<string, UncertaintyView> uncertainty;
        public List<string> pmLines;
        public static PlanView From(JObject value) => (value ?? throw new ArgumentNullException(nameof(value))).ToObject<PlanView>();
    }
    public sealed class PointView { public double t, ft; }
    public sealed class PlannedLineView { public double startDay, rate; public List<HoldView> holds; public List<SegmentView> segments; public List<LoanView> loans; }
    public sealed class HoldView { public double fromT; public double? untilT; public string kind; }
    public sealed class SegmentView { public double fromT, rate; }
    public sealed class LoanView { public double fromT, untilT, rate; }
    public sealed class ResponseView { public string key, label, eventTitle; public CostView cost; }
    // Authored hire costs are catalog descriptions; other options use numbers.
    public sealed class CostView { public JToken usd, perDayUsd; }
    public sealed class BudgetView { public double budgetUsd, spentUsd, committedUsd, deadlineDay, promisedDay; }
    public sealed class InboxView { public double day; public string from, subject, body; public Dictionary<string, double> feetByCrew; public double? spendUsd; public List<string> notes; }
    public sealed class CatalogView { public Dictionary<string, MachineView> machines; public Dictionary<string, ProtectionView> protection; }
    public sealed class MachineView { public string crew, label, sizeClass; public double rateFtPerDay, dailyUsd, priceUsd; public LeadDaysView leadDays; }
    public sealed class LeadDaysView { public double normal; }
    public sealed class ProtectionView
    {
        public string label;
        public double priceUsd, dailyUsd;
        public double? likelihood, severity, resumeAfterDays;
        public bool reactOnly, benefitHalvedInRunningSand, halvesRainRework;
        public List<string> refusedInCities;
    }
    public sealed class PermitView { public double zone; public bool entered; public string number; }
    public sealed class InspectionView { public double zone; public bool cleared; }
    public sealed class FlagView { public double stationFt, day; public string note; }
    public sealed class UncertaintyView { public double estimateFt, fromFt, toFt, asOfT; }
}
