using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LOBOver.Sim.Tests
{
    public sealed class GoldensTests
    {
        // Data/goldens/no_events.json _doc tolerances and goldens.js guard.
        private const double DaysTolerance = 0.1, CostTolerance = 500, FeetTolerance = 10, GuardDays = 300;
        public static IEnumerable<object[]> Cases()
        {
            foreach (var property in ((JObject)JObject.Parse(Replay.Read("Data/goldens/no_events.json"))["cases"]).Properties()) yield return new object[] { property.Name };
        }
        [Theory, MemberData(nameof(Cases))]
        public void PureCrewModelMatchesOracle(string name)
        {
            var goldens = JObject.Parse(Replay.Read("Data/goldens/no_events.json")); var entry = goldens["cases"][name]; var catalog = Replay.Catalog();
            var crews = new JArray();
            foreach (var id in Replay.CrewIds) crews.Add(new JObject { ["id"] = id, ["name"] = id, ["machine"] = entry["input"]["machines"][id], ["startDay"] = entry["input"]["startDay"][id] });
            var data = new JObject
            {
                ["id"] = name, ["seed"] = 0, ["capFt"] = entry["input"]["capFt"],
                ["job"] = new JObject { ["lengthFt"] = goldens["lengthFt"], ["stationEveryFt"] = 100,
                    ["zones"] = new JArray(new JObject { ["id"] = 1, ["fromFt"] = 0, ["toFt"] = goldens["lengthFt"] }) },
                ["crews"] = crews, ["plan"] = new JObject(), ["permits"] = new JObject { ["cards"] = new JArray() },
                ["inspections"] = new JObject { ["required"] = false, ["cards"] = new JArray() },
                ["rules"] = new JObject { ["overheadRate"] = JObject.Parse(Replay.Read("Data/catalog.json"))["money"]["overheadRate"] },
                ["events"] = new JArray(), ["deviations"] = new JArray(), ["inbox"] = new JArray(), ["lines"] = new JArray()
            };
            var sim = Sim.Create(Scenario.Load(data.ToString(), catalog), catalog, pure: true);
            while (!sim.Done && sim.T < GuardDays) sim.Tick((double)goldens["dt"]);
            Assert.True(sim.Done, name + " did not finish"); var actual = sim.Result; var expected = entry["expected"];
            Replay.Near(expected["finishDay"], actual["finishDay"], DaysTolerance, name + ".finishDay");
            foreach (var id in Replay.CrewIds) Replay.Near(expected["idleDays"][id], actual["idleDays"][id], DaysTolerance, name + ".idleDays." + id);
            Replay.Near(expected["crewCostUsd"], actual["crewCostUsd"], CostTolerance, name + ".crewCostUsd");
            Replay.Near(expected["peakOpenTrenchFt"], actual["peakOpenTrenchFt"], FeetTolerance, name + ".peakOpenTrenchFt");
        }
    }
}
