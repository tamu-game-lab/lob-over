using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LOBOver.Sim.Tests
{
    public sealed class ViewFilterTests
    {
        private static readonly Regex Forbidden = new Regex(@"correct|wrong|score|well done|good job|should have|right choice", RegexOptions.IgnoreCase);
        public static IEnumerable<object[]> Transcripts() => Replay.Names.Select(name => new object[] { name });
        [Theory, MemberData(nameof(Transcripts))]
        public void EveryDayFiltersSeatsAndEveryMessageAvoidsVerdicts(string name)
        {
            var run = Replay.Run(name, sim =>
            {
                var plan = sim.View("plan"); var site = sim.View("site");
                var planKeys = plan.DescendantsAndSelf().OfType<JProperty>().Select(p => p.Name).ToArray();
                foreach (var key in new[] { "pos", "crews", "truck", "trench", "activeEvent", "hiddenVariant" }) Assert.DoesNotContain(key, planKeys);
                var siteKeys = site.DescendantsAndSelf().OfType<JProperty>().Select(p => p.Name).ToArray();
                foreach (var key in new[] { "budget", "budgetUsd", "spentUsd", "committedUsd", "plannedLines", "uncertainty", "cost" }) Assert.DoesNotContain(key, siteKeys);
                Assert.False(Forbidden.IsMatch(plan.ToString(Formatting.None)), name + ": Plan contains verdict text");
                Assert.False(Forbidden.IsMatch(site.ToString(Formatting.None)), name + ": Site contains verdict text");
                var typedPlan = PlanView.From(plan); var typedSite = SiteView.From(site);
                Assert.Equal(3, typedSite.crews.Count);
                Assert.Equal((double)site["truck"]["stationFt"], typedSite.truck.stationFt);
                Assert.Equal((double)plan["budget"]["spentUsd"], typedPlan.budget.spentUsd);
                Assert.Equal(((JArray)site["trench"]).Count, typedSite.trench.Count);
                Assert.Equal(((JArray)plan["radioLog"]).Count, typedPlan.radioLog.Count);
                var active = site["activeEvent"] as JObject;
                Assert.Equal((string)active?["card"]?["title"], typedSite.activeEvent?.card.title);
            });
            foreach (var envelope in run.Transcript["messages"])
                Assert.False(Forbidden.IsMatch(envelope["message"].ToString(Formatting.None)), name + ": message contains verdict text");
        }
        [Fact]
        public void PlanReportsStayHistoricalWhileSiteMoves()
        {
            var sim = Replay.Create("stage1_texas");
            sim.Tick(1);
            var report = sim.View("plan")["reportedLines"].DeepClone();
            var before = (double)sim.View("site")["crews"][0]["stationFt"];
            sim.Tick(0.25);
            Assert.True((double)sim.View("site")["crews"][0]["stationFt"] > before);
            Assert.True(JToken.DeepEquals(report, sim.View("plan")["reportedLines"]));
            sim.Tick(0.75);
            Assert.False(JToken.DeepEquals(report, sim.View("plan")["reportedLines"]));
        }
    }
}
