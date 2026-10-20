using Newtonsoft.Json;
using Xunit;

namespace LOBOver.Sim.Tests
{
    public sealed class DeterminismTests
    {
        [Fact]
        public void TwoSlicePlayRunsSerializeIdentically()
        {
            var a = Replay.Run("slice_play"); var b = Replay.Run("slice_play");
            Assert.Equal(a.Serialized.ToString(Formatting.None), b.Serialized.ToString(Formatting.None));
            Assert.Equal(a.Transcript.ToString(Formatting.None), b.Transcript.ToString(Formatting.None));
        }
    }
}
