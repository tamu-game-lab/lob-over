using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LOBOver.Sim.Tests
{
    internal static class Replay
    {
        // headless.js: fixed tick, script epsilon and safety allowance.
        internal const double Dt = 0.01, Epsilon = 1e-6, SafetyDays = 5;
        internal static readonly string[] CrewIds = { "exc", "pipe", "back" };
        internal static readonly string[] Names = { "slice_play", "slice_pull_play", "slice_autoplan", "stage1_pm_play", "stage2_pull_play", "stage1_noaction" };
        internal static string Root { get; } = FindRoot();
        private static string FindRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Data", "catalog.json"))) directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("Run from the LOB-OVER checkout");
        }
        internal static string Read(string path) => File.ReadAllText(Path.Combine(Root, path));
        internal static Catalog Catalog() => LOBOver.Sim.Catalog.Load(Read("Data/catalog.json"));
        internal static Sim Create(string stage = "slice_texas", Action<JObject> edit = null)
        {
            var catalog = Catalog(); var data = JObject.Parse(Read("Data/Scenarios/" + stage + ".json")); edit?.Invoke(data);
            return Sim.Create(Scenario.Load(data.ToString(), catalog), catalog);
        }
        internal static JObject Expected(string name) => JObject.Parse(Read("Data/transcripts/" + name + ".json"));
        internal static (JObject Transcript, JObject Serialized) Run(string name, Action<Sim> inspect = null)
        {
            var stage = name.StartsWith("slice", StringComparison.Ordinal) ? "slice_texas" : name.StartsWith("stage1", StringComparison.Ordinal) ? "stage1_texas" : "stage2_texas";
            var script = name == "stage1_noaction" ? new JArray() : JArray.Parse(Read("Monitor/test/scripts/" + name + ".json"));
            var commands = script.OfType<JObject>().OrderBy(c => (double)c["t"]).ToArray();
            var sim = Create(stage); var source = JObject.Parse(Read("Data/Scenarios/" + stage + ".json"));
            var snapshots = new JArray(); var messages = new JArray();
            var transcript = new JObject { ["scenario"] = stage, ["seed"] = source["seed"], ["snapshots"] = snapshots, ["messages"] = messages, ["result"] = null };
            var index = 0; double snapshotDay = -1;
            void Drain()
            {
                foreach (var message in sim.Drain()) messages.Add(new JObject { ["t"] = message["day"] ?? new JValue(sim.T), ["message"] = message });
            }
            while (!sim.Done && sim.T <= (double)source["plan"]["deadlineDay"] + SafetyDays)
            {
                while (index < commands.Length && (double)commands[index]["t"] <= sim.T + Epsilon)
                {
                    var entry = commands[index++]; var cmd = (JObject)entry["cmd"];
                    var reply = sim.Apply((string)entry["seat"], cmd);
                    var message = new JObject { ["t"] = reply.Ok ? "ack" : "nack", ["v"] = 1 };
                    if (cmd["id"] != null) message["id"] = cmd["id"];
                    foreach (var field in JObject.FromObject(reply).Properties()) message[field.Name] = field.Value.DeepClone();
                    messages.Add(new JObject { ["t"] = sim.T, ["message"] = message });
                    Drain();
                }
                var day = Math.Floor(sim.T + Epsilon);
                if (day > snapshotDay) { snapshots.Add(sim.View("facilitator")); snapshotDay = day; inspect?.Invoke(sim); }
                Drain();
                if (sim.Speed == "paused") { transcript["safetyStop"] = new JObject { ["t"] = sim.T, ["reason"] = "paused without a same-time resume" }; break; }
                sim.Tick(Dt); Drain();
            }
            transcript["result"] = sim.Result;
            if (!sim.Done && transcript["safetyStop"] == null) transcript["safetyStop"] = new JObject { ["t"] = sim.T, ["reason"] = "time limit" };
            return (transcript, sim.Serialize());
        }
        internal static void Near(JToken expected, JToken actual, double tolerance, string path)
        {
            Assert.True(expected != null && actual != null, path + ": missing value");
            var e = (double)expected; var a = (double)actual;
            Assert.True(double.IsFinite(a) && Math.Abs(e - a) <= tolerance, $"{path}: expected {e:R}, actual {a:R}, tolerance {tolerance}");
        }
        internal static void Equal(JToken expected, JToken actual, string path)
        { Assert.True(JToken.DeepEquals(expected, actual), $"{path}: expected {expected?.ToString(Formatting.None) ?? "missing"}, actual {actual?.ToString(Formatting.None) ?? "missing"}"); }
        internal static void Compare(JToken expected, JToken actual, string path, double numericTolerance = 0.001)
        {
            if (expected is JObject obj)
            {
                Assert.True(actual is JObject, path + ": expected object");
                Assert.True(obj.Properties().Select(p => p.Name).OrderBy(x => x).SequenceEqual(((JObject)actual).Properties().Select(p => p.Name).OrderBy(x => x)),
                    path + ": object keys differ");
                foreach (var p in obj.Properties()) Compare(p.Value, actual[p.Name], path + "." + p.Name, numericTolerance);
            }
            else if (expected is JArray array)
            {
                Assert.True(actual is JArray, path + ": expected array");
                Assert.True(array.Count == ((JArray)actual).Count, path + ": array lengths differ");
                for (var i = 0; i < array.Count; i++) Compare(array[i], actual[i], path + "[" + i + "]", numericTolerance);
            }
            else if (expected?.Type == JTokenType.Float || expected?.Type == JTokenType.Integer) Near(expected, actual, numericTolerance, path);
            else Equal(expected, actual, path);
        }
    }
}
