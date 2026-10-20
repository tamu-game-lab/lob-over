using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LOBOver.Sim.Tests
{
    public sealed class ParityTests
    {
        public static IEnumerable<object[]> Transcripts() => Replay.Names.Select(name => new object[] { name });
        [Theory, MemberData(nameof(Transcripts))]
        public void RecordedJavaScriptTranscriptMatches(string name)
        {
            var expected = Replay.Expected(name); var actual = Replay.Run(name).Transcript;
            Assert.Null(actual["safetyStop"]);
            var snapshots = (JArray)expected["snapshots"]; Assert.Equal(snapshots.Count, ((JArray)actual["snapshots"]).Count);
            for (var day = 0; day < snapshots.Count; day++)
            {
                var e = snapshots[day]; var a = actual["snapshots"][day]; var path = name + ".day" + day;
                Replay.Near(e["t"], a["t"], 0.001, path + ".t"); Replay.Equal(e["speed"], a["speed"], path + ".speed");
                foreach (var id in Replay.CrewIds)
                {
                    Replay.Near(e["crews"][id]["pos"], a["crews"][id]["pos"], 1, path + ".crews." + id + ".pos");
                    Replay.Equal(e["crews"][id]["status"], a["crews"][id]["status"], path + ".crews." + id + ".status");
                    Replay.Near(e["crews"][id]["mood"], a["crews"][id]["mood"], 0, path + ".crews." + id + ".mood");
                }
                Replay.Compare(e["site"]["trench"], a["site"]["trench"], path + ".trench", 0);
                Replay.Compare(e["site"]["boards"], a["site"]["boards"], path + ".boards", 0);
                Replay.Compare(e["site"]["truck"], a["site"]["truck"], path + ".truck");
                Replay.Near(e["spentUsd"], a["spentUsd"], 100, path + ".spentUsd");
                // Also guard all public fields, including projections, response costs and orders.
                Replay.Compare(e["plan"], a["plan"], path + ".plan");
                Replay.Compare(e["site"], a["site"], path + ".site");
            }
            var em = ((JArray)expected["messages"]).OfType<JObject>().ToArray(); var am = ((JArray)actual["messages"]).OfType<JObject>().ToArray();
            Assert.Equal(em.Length, am.Length);
            for (var i = 0; i < em.Length; i++)
            {
                var path = name + ".messages[" + i + "]"; var e = em[i]["message"]; var a = am[i]["message"];
                Replay.Equal(e["t"], a["t"], path + ".type");
                Replay.Near(em[i]["t"], am[i]["t"], 0.02, path + ".t");
                if ((string)e["t"] == "event")
                { Replay.Equal(e["eventId"], a["eventId"], path + ".eventId"); Replay.Equal(e["card"]["title"], a["card"]["title"], path + ".title"); }
                if ((string)e["t"] == "nack")
                { Replay.Equal(e["id"], a["id"], path + ".id"); Replay.Equal(e["reason"], a["reason"], path + ".reason"); }
                Replay.Compare(e, a, path + ".message");
            }
            var er = expected["result"]; var ar = actual["result"];
            Replay.Equal(er["lost"], ar["lost"], name + ".result.lost");
            if (er["finishDay"] != null) Replay.Near(er["finishDay"], ar["finishDay"], 0.05, name + ".result.finishDay");
            Replay.Near(er["idleCrewDays"], ar["idleCrewDays"], 0.1, name + ".result.idleCrewDays");
            Replay.Near(er["peakOpenTrenchFt"], ar["peakOpenTrenchFt"], 5, name + ".result.peakOpenTrenchFt");
            foreach (var field in new[] { "finesUsd", "deviationsCaught", "deviationsMissed" }) Replay.Near(er[field], ar[field], 0, name + ".result." + field);
            Replay.Compare(er, ar, name + ".result");
        }
    }
}
