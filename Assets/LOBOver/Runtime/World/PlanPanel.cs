using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LOBOver.Sim;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;

namespace LOBOver.World
{
    // Showcase only. During the study, the Plan seat uses the web page, away from the Site seat.
    public sealed class PlanPanel : MonoBehaviour
    {
        private Chart chart;
        private Transform drawing;
        private readonly Dictionary<string, LineRenderer> planned = new Dictionary<string, LineRenderer>();
        private readonly Dictionary<string, LineRenderer> reported = new Dictionary<string, LineRenderer>();
        private TextMeshPro budget, inbox, radio, responses, keypad, result;
        private LineRenderer promise, deadlineRule;
        private double length, deadline;
        private static readonly CultureInfo Format = CultureInfo.InvariantCulture;
        public void Configure(WorldAssets assets, double lengthFt, double deadlineDay)
        {
            if (drawing != null) WorldDrawing.Remove(drawing.gameObject);
            drawing = WorldDrawing.Group("PlanPlot", transform).transform;
            planned.Clear(); reported.Clear(); length = lengthFt; deadline = deadlineDay;
            chart = new Chart(drawing, assets, length, deadline, new Rect(-1.32f, -0.45f, 1.55f, 0.9f));
            chart.Label("Title", "PLAN   exc / pipe / back   ·   thin: planned   thick: reported", new Vector2(0, 0.66f), new Vector2(2.8f, 0.13f), 0.06f);
            budget = chart.Label("Budget", "", new Vector2(0, 0.53f), new Vector2(2.8f, 0.13f), 0.06f);
            inbox = chart.Label("Inbox", "", new Vector2(0.88f, 0.32f), new Vector2(1.05f, 0.32f), 0.045f);
            radio = chart.Label("Radio", "", new Vector2(0.88f, -0.02f), new Vector2(1.05f, 0.28f), 0.042f);
            responses = chart.Label("Respond", "", new Vector2(0.88f, -0.3f), new Vector2(1.05f, 0.25f), 0.042f);
            keypad = chart.Label("Keypad", "", new Vector2(0, -0.61f), new Vector2(2.8f, 0.14f), 0.045f);
            result = chart.Label("Result", "", new Vector2(-0.55f, -0.2f), new Vector2(1.4f, 0.4f), 0.055f);
            foreach (var text in new[] { inbox, radio, responses, keypad })
            { text.enableAutoSizing = true; text.fontSizeMin = 0.2f; text.fontSizeMax = text.fontSize; text.overflowMode = TextOverflowModes.Truncate; }
            promise = chart.Rule("Promise", 1, true, Color.gray); deadlineRule = chart.Rule("Deadline", deadline, true, Color.white);
            for (var i = 0; i < WorldUnits.CrewIds.Length; i++)
            {
                var id = WorldUnits.CrewIds[i];
                planned[id] = WorldDrawing.Line("Planned_" + id, drawing, assets.ink, WorldUnits.CrewColours[i], 0.004f);
                reported[id] = WorldDrawing.Line("Reported_" + id, drawing, assets.ink, WorldUnits.CrewColours[i], 0.01f);
            }
        }
        public void Apply(PlanView view, double time)
        {
            if (chart == null) return;
            var b = view.budget;
            budget.text = string.Format(Format, "${0:N0} spent   ${1:N0} committed   ${2:N0} budget   |   promise {3}   deadline {4}", b.spentUsd, b.committedUsd, b.budgetUsd, b.promisedDay, b.deadlineDay);
            promise.SetPositions(new[] { chart.Point(0, b.promisedDay), chart.Point(length, b.promisedDay) });
            deadlineRule.SetPositions(new[] { chart.Point(0, b.deadlineDay), chart.Point(length, b.deadlineDay) });
            foreach (var id in WorldUnits.CrewIds)
            {
                var projection = Project(view.plannedLines[id], length, Math.Max(time, deadline - 1));
                planned[id].positionCount = projection.Count;
                planned[id].SetPositions(projection.Select(p => chart.Point(p.ft, Math.Min(deadline, p.t + 1))).ToArray());
                var steps = new List<Vector3>(); PointView previous = null;
                foreach (var point in view.reportedLines[id])
                {
                    if (previous != null) steps.Add(chart.Point(previous.ft, point.t + 1));
                    steps.Add(chart.Point(point.ft, point.t + 1)); previous = point;
                }
                reported[id].positionCount = steps.Count; reported[id].SetPositions(steps.ToArray());
            }
            inbox.text = "INBOX\n" + string.Join("\n", view.inbox.Skip(Math.Max(0, view.inbox.Count - 3)).Select(item =>
                "Day " + item.day.ToString("0.##", Format) + " " + item.subject + ": " + (item.body ?? string.Join("; ", item.notes ?? new List<string>()))));
            radio.text = "RADIO\n" + string.Join("\n", view.radioLog.Select(item => item.id + " " + item.crew + " " + (string)item.op?["op"] + " · " + item.status));
            responses.text = "RESPOND\n" + string.Join("\n", view.responses.Select(item => item.label + " " + item.cost?.usd + " " + item.cost?.perDayUsd));
            keypad.text = "KEYPAD   " + string.Join("   ", view.permits.Select(item => "ZONE " + item.zone + " permit: " + (item.entered ? item.number : "—")))
                + "\n" + string.Join("   ", view.inspections.Select(item => "ZONE " + item.zone + " inspection: " + (item.cleared ? "entered" : "—")));
        }
        public void ShowResult(JObject value)
        {
            // Numerical outcome facts only; the Site board never receives the result or budget.
            result.text = "Finish day: " + (value["finishDay"]?.ToString() ?? "—") + "\nSpent: $" + ((double?)value["spentUsd"] ?? 0).ToString("N0", Format)
                + "\nIdle crew-days: " + ((double?)value["idleCrewDays"] ?? 0).ToString("0.0", Format)
                + "\nPeak open trench: " + ((double?)value["peakOpenTrenchFt"] ?? 0).ToString("0", Format) + " ft";
        }

        // Mirrors Planning.PlannedPolylines using PlanView only, including holds and loans.
        public static List<PointView> Project(PlannedLineView line, double length, double horizon)
        {
            var holds = line.holds ?? new List<HoldView>(); var loans = line.loans ?? new List<LoanView>();
            var segments = line.segments ?? new List<SegmentView>(); var start = line.startDay - 1;
            var breaks = new List<double> { start, horizon };
            breaks.AddRange(segments.Select(s => s.fromT));
            foreach (var hold in holds) { breaks.Add(hold.fromT); if (hold.untilT.HasValue) breaks.Add(hold.untilT.Value); }
            foreach (var loan in loans) { breaks.Add(loan.fromT); breaks.Add(loan.untilT); }
            double time = start, feet = 0;
            var points = new List<PointView> { new PointView { t = start, ft = 0 } };
            foreach (var next in breaks.Distinct().Where(t => t > start && t <= horizon).OrderBy(t => t))
            {
                var segment = segments.OrderBy(s => s.fromT).LastOrDefault(s => s.fromT <= time);
                var rate = holds.Any(h => time >= h.fromT && (!h.untilT.HasValue || time < h.untilT)) ? 0
                    : (segment == null ? line.rate : segment.rate) + loans.Where(l => time >= l.fromT && time < l.untilT).Sum(l => l.rate);
                if (rate > 0 && feet + (next - time) * rate >= length)
                { points.Add(new PointView { t = time + (length - feet) / rate, ft = length }); break; }
                feet += (next - time) * rate; time = next; points.Add(new PointView { t = time, ft = feet });
            }
            return points;
        }
    }
}
