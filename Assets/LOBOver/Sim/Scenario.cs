using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace LOBOver.Sim
{
    public sealed class Catalog
    {
        internal JObject Data { get; }
        private Catalog(JObject data) { Data = data; }
        public static Catalog Load(string json) => new Catalog(JObject.Parse(json));
    }

    public sealed class Scenario
    {
        internal JObject Data { get; }
        private Scenario(JObject data) { Data = data; }
        public static Scenario Load(string json, Catalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var data = JObject.Parse(json);
            var job = data.Obj("job") ?? throw new ArgumentException("Scenario requires job");
            // WO-01 loader-derived JSON fields; functions are not part of serialization.
            const double StationEveryFt = 100;
            var stations = new JArray();
            if (!job.Finite("lengthFt") || job.N("lengthFt") <= 0) throw new ArgumentException("Invalid job.lengthFt");
            for (double ft = 0; ft <= job.N("lengthFt"); ft += StationEveryFt) stations.Add(ft);
            data["stations"] = stations;
            double dailyFleetUsd = 0;
            foreach (var id in Common.CREW_IDS)
            {
                var crew = data.Rows("crews").FirstOrDefault(c => c.S("id") == id) ?? throw new ArgumentException("Missing crew " + id);
                var machine = catalog.Data.Obj("machines").Obj(crew.S("machine"));
                // The full model also supports a crew whose own machine is absent.
                if (machine != null) dailyFleetUsd += machine.N("dailyUsd");
            }
            data["dailyFleetUsd"] = dailyFleetUsd;
            return new Scenario(data);
        }
    }
}
