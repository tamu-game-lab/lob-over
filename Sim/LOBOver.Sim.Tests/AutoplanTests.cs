using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LOBOver.Sim.Tests
{
    public sealed class AutoplanTests
    {
        [Fact]
        public void StableTimingSkipsSiteAndCopiesCommands()
        {
            var script = JArray.Parse(@"[
                { 't': 2, 'seat': 'plan', 'cmd': { 'action': 'commit', 'id': 'later', 'ops': [] } },
                { 't': 1, 'seat': 'plan', 'cmd': { 'action': 'commit', 'id': 'first', 'ops': [] } },
                { 't': 1, 'seat': 'site', 'cmd': { 'action': 'dismount', 'id': 'human' } },
                { 't': 1, 'seat': 'facilitator', 'cmd': { 'action': 'pause', 'id': 'second' } }]");
            var autoplan = new Autoplan(script); script[1]["cmd"]["id"] = "mutated";
            var calls = new List<string>();
            void Apply(string seat, JObject cmd) { calls.Add(seat + ":" + (string)cmd["id"]); cmd["id"] = "mutated in callback"; }
            autoplan.Advance(0.9, Apply); Assert.Empty(calls);
            autoplan.Advance(1 - 0.0000005, Apply);
            Assert.Equal(new[] { "plan:first", "facilitator:second" }, calls);
            Assert.Equal(2, autoplan.NextT);
            autoplan.SetEnabled(false); autoplan.Advance(3, Apply); Assert.Equal(2, calls.Count);
            autoplan.SetEnabled(true); autoplan.Advance(3, Apply); Assert.Equal("plan:later", calls[2]); Assert.Null(autoplan.NextT);
            autoplan.Reset(); calls.Clear(); autoplan.Advance(1, Apply);
            Assert.Equal(new[] { "plan:first", "facilitator:second" }, calls);
        }
        [Fact]
        public void ValidationAlsoCoversSkippedSiteEntries()
        {
            Assert.Throws<ArgumentException>(() => new Autoplan(JArray.Parse("[{'t':-1,'seat':'site','cmd':{'action':'mount'}}]")));
            Assert.Throws<ArgumentException>(() => new Autoplan(JArray.Parse("[{'t':0,'seat':'rules','cmd':{'action':'mount'}}]")));
            Assert.False(new Autoplan().Enabled);
        }
    }
}
