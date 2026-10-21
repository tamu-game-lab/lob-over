#if UNITY_EDITOR || !UNITY_ANDROID
// Legacy Site.unity remains available in the Editor; WO-L2 excludes this seat from Quest.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using LOBOver.Sim;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Simulation = LOBOver.Sim.Sim;

namespace LOBOver.Runtime
{
    public sealed class Director : MonoBehaviour
    {
        public string scenarioId = "slice_texas";
        public SiteView SiteView { get; private set; }
        public PlanView PlanView { get; private set; }
        public JObject Job { get; private set; }
        public JObject MachineCatalog { get; private set; }
        public JArray Lines { get; private set; }
        public double DeadlineDay { get; private set; }
        public double TimeDays => simulation == null ? 0 : simulation.T;
        // Same day boundary as Common.DisplayDay; view snapshots use simulation time instead.
        public int DisplayDay => (int)Math.Floor(TimeDays + 1e-6) + 1;
        public string Speed => simulation == null ? "paused" : simulation.Speed;
        public bool Ready => simulation != null;
        public bool Done => simulation != null && simulation.Done;
        // WO-S4 Amendment 1: pausing the script leaves its cursor untouched.
        private bool autoplanEnabled = true;
        public bool AutoplanEnabled
        {
            get => autoplanEnabled;
            set
            {
                if (autoplanEnabled == value) return;
                autoplanEnabled = value;
                if (value && simulation != null && autoplan != null)
                { autoplan.Advance(simulation.T, Apply); Publish(); }
            }
        }
        public double ScenarioSecondsPerDay => secondsFull;
        public double SecondsPerDay
        {
            get => playbackSeconds ?? secondsFull;
            set
            {
                if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value));
                playbackSeconds = value;
            }
        }
        public string LoadError { get; private set; }
        public string TranscriptPath { get; private set; }
        public event Action Loaded;
        public event Action<SiteView, PlanView> ViewsChanged;
        public event Action<JObject> Message;
        public event Action<JObject> Event;
        public event Action<JObject> Voice;
        public event Action<JObject> Result;

        private Simulation simulation;
        private Scenario scenario;
        private Catalog catalog;
        private Autoplan autoplan;
        private Pacer pacer;
        private Coroutine loading;
        private double secondsFull, secondsSlow;
        private double? playbackSeconds;
        private JObject transcript;
        private JArray snapshots, messages, commands;
        private int lastSnapshot = -1;
        private bool savedResult;

        private void OnEnable() { if (!Ready && loading == null) Load(scenarioId); }

        // Public Start is also Unity's lifecycle method. A slow Android load is started by OnEnable.
        public void Start()
        {
            if (simulation == null || simulation.Done) return;
            Apply("facilitator", new JObject { ["action"] = "start" });
            Publish();
        }
        public void Pause()
        {
            if (simulation == null) return;
            Apply("facilitator", new JObject { ["action"] = "pause" });
            Publish(); SaveTranscript();
        }
        public void Reset()
        {
            if (scenario == null) return;
            SaveTranscript(); Initialize();
        }
        public void Load(string id)
        {
            if (id != "slice_texas" && id != "stage1_texas" && id != "stage2_texas")
                throw new ArgumentException("Unknown scenario: " + id, nameof(id));
            SaveTranscript();
            if (loading != null) StopCoroutine(loading);
            pacer?.Close(); simulation = null; SiteView = null; PlanView = null;
            scenarioId = id; LoadError = null;
            playbackSeconds = null;
            loading = StartCoroutine(LoadFiles());
        }
        private IEnumerator LoadFiles()
        {
            // Let scene subscribers attach before File-backed Editor reads can finish.
            yield return null;
            string catalogText = null, scenarioText = null, scriptText = null;
            Action<string> failed = error => { LoadError = error; Debug.LogError(error, this); };
            yield return StreamingData.ReadText("Data/catalog.json", value => catalogText = value, failed);
            if (LoadError != null) yield break;
            yield return StreamingData.ReadText("Data/Scenarios/" + scenarioId + ".json", value => scenarioText = value, failed);
            if (LoadError != null) yield break;
            var script = scenarioId == "slice_texas" ? "slice_autoplan" : scenarioId == "stage1_texas" ? "stage1_pm_play" : "stage2_pull_play";
            yield return StreamingData.ReadText("Data/scripts/" + script + ".json", value => scriptText = value, failed);
            if (LoadError != null) yield break;
            try
            {
                var source = JObject.Parse(scenarioText);
                catalog = Catalog.Load(catalogText); scenario = Scenario.Load(scenarioText, catalog);
                Job = (JObject)source["job"].DeepClone();
                MachineCatalog = (JObject)JObject.Parse(catalogText)["machines"];
                Lines = (JArray)source["lines"].DeepClone();
                DeadlineDay = (double)source["plan"]["deadlineDay"];
                secondsFull = (double)source["time"]["secondsPerDayFull"];
                secondsSlow = (double)source["time"]["secondsPerDaySlow"];
                if (secondsFull <= 0 || secondsSlow <= 0) throw new InvalidDataException("Scenario day lengths must be positive");
                // Autoplan removes Site entries; the presenter retains every Site verb.
                autoplan = new Autoplan(JArray.Parse(scriptText));
                Initialize();
            }
            catch (Exception error) { failed(error.ToString()); }
            loading = null;
        }
        private void Initialize()
        {
            pacer?.Close(); simulation = Simulation.Create(scenario, catalog); autoplan.Reset();
            snapshots = new JArray(); messages = new JArray(); commands = new JArray();
            transcript = new JObject { ["scenario"] = scenarioId, ["snapshots"] = snapshots,
                ["messages"] = messages, ["commands"] = commands, ["result"] = null };
            TranscriptPath = Path.Combine(Application.persistentDataPath, "transcripts",
                scenarioId + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json");
            lastSnapshot = -1; savedResult = false;
            pacer = new Pacer(() => simulation, AfterTick);
            Loaded?.Invoke();
            if (AutoplanEnabled) autoplan.Advance(simulation.T, Apply);
            Start();
            Drain(); CaptureDay();
        }
        private void Update()
        {
            if (simulation == null) return;
            // Pacer's 5/20-second day cadence is scaled to the scenario's full/slow seconds per day.
            var baselineMs = (Speed == "slow" ? Pacer.SLOW_MS : Pacer.FULL_MS) / Pacer.STEP_DAYS;
            var authoredMs = (Speed == "slow" ? secondsSlow : secondsFull) * 1000;
            // Preserve the authored full/slow ratio when the presenter changes playback speed.
            if (playbackSeconds.HasValue) authoredMs *= playbackSeconds.Value / secondsFull;
            pacer.Advance(UnityEngine.Time.unscaledDeltaTime * 1000 * baselineMs / authoredMs);
            Publish();
        }
        private void AfterTick()
        {
            if (AutoplanEnabled) autoplan.Advance(simulation.T, Apply);
            Drain(); CaptureDay();
            if (simulation.Done && !savedResult) { savedResult = true; SaveTranscript(); }
        }
        private void Apply(string seat, JObject command)
        {
            var reply = simulation.Apply(seat, command);
            commands.Add(new JObject { ["t"] = simulation.T, ["seat"] = seat, ["cmd"] = command.DeepClone() });
            var acknowledgement = JObject.FromObject(reply);
            acknowledgement["t"] = reply.Ok ? "ack" : "nack";
            acknowledgement["id"] = command["id"];
            messages.Add(new JObject { ["t"] = simulation.T, ["message"] = acknowledgement });
            // Only the initiating seat receives its rejection text.
            if (seat == "site") Message?.Invoke(acknowledgement);
            Drain();
        }
        public void Send(JObject command)
        {
            if (simulation == null || command == null) return;
            Apply("site", command); Publish();
        }
        // presenter-only (MR diorama); the full-scale seat and the JavaScript sim do not have it
        public JObject InspectAt(string target, double stationFt) => simulation?.InspectAt(target, stationFt);
        public static bool ForSeat(JObject message, string seat) =>
            message["seats"] is JArray seats && seats.Any(value => (string)value == seat);
        private void Drain()
        {
            foreach (var message in simulation.Drain())
            {
                messages.Add(new JObject { ["t"] = message["day"] ?? new JValue(simulation.T), ["message"] = message.DeepClone() });
                Message?.Invoke(message);
                if ((string)message["t"] == "event") Event?.Invoke(message);
                if ((string)message["t"] == "voice") Voice?.Invoke(message);
                if ((string)message["t"] == "result") Result?.Invoke(message);
            }
        }
        private void Publish()
        {
            SiteView = SiteView.From(simulation.View("site"));
            PlanView = PlanView.From(simulation.View("plan"));
            ViewsChanged?.Invoke(SiteView, PlanView);
        }
        private void CaptureDay()
        {
            var day = (int)Math.Floor(simulation.T + 1e-6);
            if (day == lastSnapshot) return;
            snapshots.Add(simulation.View("facilitator")); lastSnapshot = day;
            SaveTranscript();
        }
        public void SaveTranscript()
        {
            if (transcript == null || simulation == null) return;
            transcript["result"] = simulation.Result;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(TranscriptPath));
                File.WriteAllText(TranscriptPath, transcript.ToString(Formatting.Indented));
            }
            catch (IOException error) { Debug.LogWarning("Transcript: " + error.Message, this); }
            catch (UnauthorizedAccessException error) { Debug.LogWarning("Transcript: " + error.Message, this); }
        }
        private void OnApplicationPause(bool paused) { if (paused) SaveTranscript(); }
        private void OnDisable()
        {
            SaveTranscript();
            if (loading != null) { StopCoroutine(loading); loading = null; }
        }
        private void OnDestroy() { pacer?.Close(); }
    }
}

#endif
