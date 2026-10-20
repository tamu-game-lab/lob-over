using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace LOBOver.Sim
{
    public sealed class Autoplan
    {
        private readonly List<JObject> commands;
        private int index;
        public bool Enabled { get; private set; }
        public double? NextT => index < commands.Count ? commands[index].N("t") : (double?)null;
        public JObject Status => Json.O("enabled", Enabled, "nextT", NextT);
        public Autoplan(JArray script = null, bool? enabled = null)
        {
            script = script == null ? new JArray() : (JArray)script.DeepClone();
            Enabled = enabled ?? script.Count > 0;
            for (var i = 0; i < script.Count; i++)
            {
                var entry = script[i];
                if (!entry.Finite("t") || entry.N("t") < 0 || !new[] { "plan", "site", "facilitator" }.Contains(entry.S("seat"))
                    || entry.Obj("cmd").Get("action")?.Type != JTokenType.String) throw new ArgumentException("Invalid autoplan entry " + (i + 1));
            }
            // LINQ's stable sort preserves script order at equal times, like autoplan.js.
            commands = script.OfType<JObject>().Where(e => e.S("seat") != "site").OrderBy(e => e.N("t")).ToList();
        }
        public void SetEnabled(bool value) => Enabled = value;
        public void Reset() => index = 0;
        public void Advance(double time, Action<string, JObject> apply)
        {
            if (apply == null) throw new ArgumentNullException(nameof(apply));
            while (Enabled && index < commands.Count && commands[index].N("t") <= time + Common.EPS)
            { var entry = commands[index++]; apply(entry.S("seat"), entry.Obj("cmd").Copy()); }
        }
    }
}
