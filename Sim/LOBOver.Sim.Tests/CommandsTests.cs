using System;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LOBOver.Sim.Tests
{
    public sealed class CommandsTests
    {
        [Fact]
        public void InvalidOfficeActionRollsBackWholeCommit()
        {
            var sim = Replay.Create("stage2_texas");
            var before = sim.Serialize();
            var reply = sim.Apply("plan", JObject.Parse(@"{'action':'commit','ops':[
                {'op':'setStart','crew':'back','day':20},
                {'op':'enterPermit','zone':1,'code':'not on file'}]}"));
            Assert.False(reply.Ok); Assert.Equal("code not on file", reply.Reason);
            Assert.True(JToken.DeepEquals(before, sim.Serialize()));
        }
        [Fact]
        public void JsonNumberZoneIdsDoNotDependOnIntegerTokenEncoding()
        {
            var sim = Replay.Create("stage2_texas");
            var source = JObject.Parse(Replay.Read("Data/Scenarios/stage2_texas.json"));
            var code = source["permits"]["cards"][0]["code"];
            var number = source["inspections"]["cards"][0]["number"];
            var reply = sim.Apply("plan", new JObject { ["action"] = "commit", ["ops"] = new JArray(
                new JObject { ["op"] = "enterPermit", ["zone"] = 1.0, ["code"] = code },
                new JObject { ["op"] = "enterInspection", ["zone"] = 1.0, ["number"] = number }) });
            Assert.True(reply.Ok, reply.Reason);
            Assert.True((bool)sim.View("plan")["permits"][0]["entered"]);
            Assert.True((bool)sim.View("plan")["inspections"][0]["cleared"]);
        }
        [Fact]
        public void CrewWithoutOwnMachineUsesLentRate()
        {
            var sim = Replay.Create("stage1_texas", data =>
            {
                data["crews"][1]["machine"] = null;
                data["crews"][1]["startDay"] = 1;
                data["events"] = new JArray(); data["deviations"] = new JArray();
            });
            Assert.True(sim.Apply("site", JObject.Parse("{'action':'dismount'}")).Ok);
            var reply = sim.Apply("plan", JObject.Parse(@"{'action':'commit','ops':[
                {'op':'lend','machine':'excavator_standard','toCrew':'pipe','days':1}]}"));
            Assert.True(reply.Ok, reply.Reason); sim.Tick(0.01);
            var expectedRate = (double)JObject.Parse(Replay.Read("Data/catalog.json"))["machines"]["excavator_standard"]["rateFtPerDay"];
            Assert.Equal(expectedRate, (double)sim.Serialize()["crews"]["pipe"]["rate"]);
        }
        [Fact]
        public void UnsupportedRuleSwitchThrowsBeforePlay()
        {
            var error = Assert.Throws<ArgumentException>(() => Replay.Create(edit: data => data["rules"]["orderLagShifts"] = 2));
            Assert.Contains("Unsupported rules.orderLagShifts", error.Message);
        }
    }
}
