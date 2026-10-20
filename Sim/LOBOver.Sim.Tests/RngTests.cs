using System;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LOBOver.Sim.Tests
{
    public sealed class RngTests
    {
        [Fact]
        public void HundredDrawsMatchJavaScriptBitExactly()
        {
            var expected = JArray.Parse(Replay.Read("Sim/LOBOver.Sim.Tests/fixtures/rng_seed_20261020.json")); var rng = new Rng(20261020);
            Assert.Equal(100, expected.Count);
            foreach (var value in expected) Assert.Equal(BitConverter.DoubleToInt64Bits((double)value), BitConverter.DoubleToInt64Bits(rng.Next()));
            var resumed = new Rng(rng.State);
            for (var i = 0; i < 100; i++) Assert.Equal(rng.Next(), resumed.Next());
        }
        [Fact]
        public void InvalidSeedsBoundsAndEmptyPicksAreRejected()
        {
            foreach (var seed in new[] { -1d, 0.5, double.NaN, double.PositiveInfinity, 4294967296d }) Assert.Throws<ArgumentOutOfRangeException>(() => new Rng(seed));
            var rng = new Rng(0);
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.Int(2, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.Int(0, 0.5));
            Assert.Throws<ArgumentException>(() => rng.Pick(Array.Empty<int>()));
            Assert.Equal(7, rng.Int(7, 7)); Assert.Equal("only", rng.Pick(new[] { "only" }));
        }
    }
}
