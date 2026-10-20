using Newtonsoft.Json.Linq;
using Xunit;

namespace LOBOver.Sim.Tests
{
    public sealed class PacerTests
    {
        private static Sim Full() => Replay.Create(edit: data => { data["events"] = new JArray(); data["deviations"] = new JArray(); });
        private static Sim Slow() => Replay.Create(edit: data => data["events"][0]["trigger"] = new JObject { ["kind"] = "day", ["day"] = 1 });
        [Theory]
        [InlineData(false, 0.2)]
        [InlineData(true, 0.05)]
        public void ElapsedTimeProducesFixedSteps(bool slow, double expected)
        {
            var sim = slow ? Slow() : Full(); var callbacks = 0;
            var pacer = new Pacer(() => sim, () => callbacks++);
            for (var i = 0; i < 100; i++) { pacer.Advance(10); pacer.Sync(); }
            Assert.Equal(expected, sim.T, 10); Assert.Equal((int)(expected / Pacer.STEP_DAYS), callbacks);
        }
        [Fact]
        public void StallCapsBacklogAndPreservesOnlyRemainder()
        {
            var sim = Full(); var pacer = new Pacer(() => sim);
            Assert.Equal(20, pacer.Advance(5025)); Assert.Equal(0.2, sim.T, 10);
            Assert.Equal(0, pacer.Advance(24)); Assert.Equal(1, pacer.Advance(1)); Assert.Equal(0.21, sim.T, 10);
        }
        [Fact]
        public void PauseResumeReplacementAndCloseDiscardInactiveTime()
        {
            var sim = Full(); var pacer = new Pacer(() => sim);
            pacer.Advance(75); Assert.Equal(0.01, sim.T);
            sim.Apply("facilitator", new JObject { ["action"] = "pause" }); pacer.Sync(); Assert.Equal(0, pacer.Advance(1000));
            sim.Apply("facilitator", new JObject { ["action"] = "resume" }); pacer.Sync();
            Assert.Equal(0, pacer.Advance(49)); Assert.Equal(1, pacer.Advance(1));
            pacer.Advance(25); sim = Full(); pacer.Sync();
            Assert.Equal(0, pacer.Advance(49)); Assert.Equal(1, pacer.Advance(1));
            pacer.Close(); Assert.Equal(0, pacer.Advance(1000)); Assert.Equal(0.01, sim.T);
        }
        [Fact]
        public void EventStopsCatchupBeforeNewSpeedTakesEffect()
        {
            var sim = Replay.Create(edit: data => data["events"][0]["trigger"] = new JObject { ["kind"] = "day", ["day"] = 1.01 });
            var pacer = new Pacer(() => sim);
            Assert.Equal(1, pacer.Advance(500)); Assert.Equal("slow", sim.Speed);
            Assert.Equal(0, pacer.Advance(199)); Assert.Equal(1, pacer.Advance(1));
        }
        [Fact]
        public void FastCompletionAndReplacementStopCurrentBatch()
        {
            var sim = Full(); var callbacks = 0;
            var pacer = new Pacer(() => sim, () => { if (++callbacks == 10) sim = Full(); });
            pacer.SetFast(true); Assert.Equal(5, pacer.Advance(10)); Assert.Equal(5, pacer.Advance(30)); Assert.Equal(0, sim.T);
            Assert.Equal(1, pacer.Advance(2));
            var finishing = Replay.Create(edit: data => { data["plan"]["budgetUsd"] = 1; data["events"] = new JArray(); });
            var end = new Pacer(() => finishing); end.SetFast(true);
            Assert.Equal(1, end.Advance(100)); Assert.True(finishing.Done); Assert.Equal(0, end.Advance(1000));
        }
    }
}
