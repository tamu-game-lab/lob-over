using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace LOBOver.Client
{
    public sealed class VoiceTurn
    {
        public string LineId, Speaker;
        public int Number;
        public string Clip => LineId + "_" + Number + ".wav";
        public static bool SafeId(string id) => !string.IsNullOrEmpty(id) && id.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-');
        public static List<VoiceTurn> Read(JObject message)
        {
            var result = new List<VoiceTurn>();
            if (message["turns"] is JArray turns)
            {
                // WO-L1's wire turns are already ordered, including single-line arrivals.
                foreach (var turn in turns.OfType<JObject>())
                {
                    var clip = (string)turn["clip"];
                    if (clip == null || !clip.EndsWith(".wav", StringComparison.Ordinal)) continue;
                    var stem = clip.Substring(0, clip.Length - 4); var split = stem.LastIndexOf('_');
                    if (split <= 0 || !SafeId(stem) || !int.TryParse(stem.Substring(split + 1), out var number) || number < 1) continue;
                    result.Add(new VoiceTurn { LineId = stem.Substring(0, split), Number = number, Speaker = (string)turn["speaker"] });
                }
                return result;
            }
            // The host also sends authored lines; retain compatibility with that documented envelope.
            foreach (var line in (message["lines"] as JArray ?? new JArray()).OfType<JObject>())
            {
                var id = (string)line["id"];
                if (!SafeId(id)) continue;
                var exchange = line["turns"] as JArray ?? new JArray(new JObject { ["speaker"] = line["speaker"] });
                var number = 0;
                foreach (var turn in exchange.OfType<JObject>())
                    result.Add(new VoiceTurn { LineId = id, Number = ++number, Speaker = (string)turn["speaker"] });
            }
            return result;
        }
    }
}
