using System.Collections;
using System.IO;
using System.Linq;
using LOBOver.Runtime;
using LOBOver.Sim;
using LOBOver.Start;
using LOBOver.World;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Simulation = LOBOver.Sim.Sim;

namespace LOBOver.Tests
{
    [PrebuildSetup("LOBOver.Tests.Editor.BuildSiteFixture")]
    public sealed class SiteWorldTests
    {
        private SiteWorld world;
        private JObject scenario, transcript;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            SceneManager.sceneLoaded += DisableAutoplay;
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/LOBOver/Scenes/Site.unity", new LoadSceneParameters(LoadSceneMode.Single));
            SceneManager.sceneLoaded -= DisableAutoplay;
            world = Object.FindFirstObjectByType<SiteWorld>();
            scenario = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Data/Scenarios/slice_texas.json")));
            transcript = JObject.Parse(File.ReadAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Data/transcripts/slice_play.json")));
            world.Configure((JObject)scenario["job"], (double)scenario["plan"]["deadlineDay"]);
#else
            Assert.Ignore("The recorded fixture and builder run in Editor PlayMode; no headset required.");
            yield break;
#endif
        }
        private static void DisableAutoplay(Scene scene, LoadSceneMode mode)
        {
            Object.FindFirstObjectByType<Director>().gameObject.SetActive(false);
            var start = Object.FindFirstObjectByType<StartPanel>();
            start.enabled = false; start.fullScaleRoot.SetActive(true);
            start.rig.gameObject.SetActive(false); start.pointer.gameObject.SetActive(false);
            Object.FindFirstObjectByType<DesktopPreview>(FindObjectsInactive.Include).gameObject.SetActive(false);
        }
        [TearDown]
        public void TearDown() { SceneManager.sceneLoaded -= DisableAutoplay; }
        private void Feed(int day) => world.Apply(SiteView.From((JObject)transcript["snapshots"][day]["site"]), day + 1);
        private void Crew(string id, double station, string status, int mood = -1)
        {
            // Amendment 1 rounds the pipe position to 520; the recorded value is 520.2 ft.
            // Retain that exact value in the world; WO-S2's position comparison allows one foot.
            var crew = world.Crew(id); Assert.That(crew.StationFt, Is.EqualTo(station).Within(1), id + " station");
            Assert.That(crew.Status, Is.EqualTo(status), id + " status");
            if (mood >= 0) Assert.That(crew.MoodLevel, Is.EqualTo(mood), id + " mood");
        }
        private void Cell(string state, string wall, string water)
        {
            var cell = world.CellAt(520); Assert.That(cell.StationFt, Is.EqualTo(500));
            Assert.That(cell.State, Is.EqualTo(state)); Assert.That(cell.Wall, Is.EqualTo(wall)); Assert.That(cell.Water, Is.EqualTo(water));
            Assert.That(cell.slump.activeSelf, Is.EqualTo(wall == "slumped")); Assert.That(cell.water.activeSelf, Is.EqualTo(water == "standing"));
        }
        [UnityTest]
        public IEnumerator RecordedDaysDriveGeometryCrewsSkyAndBoards()
        {
            Feed(1); yield return null;
            Crew("exc", 220, "working"); Crew("pipe", 0, "working"); Crew("back", 0, "idle");
            Cell("undug", "sound", "none"); Assert.That(world.sky.State, Is.EqualTo("clear"));
            StringAssert.Contains("TX-0311", world.Board(1).Text); StringAssert.Contains("no permit posted", world.Board(2).Text);
            Feed(4); yield return null;
            Cell("piped", "slumped", "standing"); Crew("exc", 869, "stopped"); Crew("pipe", 520, "stopped"); Crew("back", 237.5, "working");
            Assert.That(world.CurrentView.activeEvent.id, Is.EqualTo("ev_p1"));
            Assert.That(world.CurrentView.truck.stationFt, Is.EqualTo(270)); Assert.That(world.CurrentView.truck.dismounted, Is.True);
            Assert.That(world.Crew("pipe").WorkingParticles, Is.False);
            Feed(5); yield return null;
            Crew("back", 272.5, "held"); Crew("exc", 869, "working", 1); Crew("pipe", 520, "idle"); Cell("piped", "slumped", "standing");
            Feed(9); yield return null;
            Cell("backfilled", "sound", "none"); Assert.That(world.sky.State, Is.EqualTo("rain")); Assert.That(world.sky.Raining, Is.True);
            Assert.That(world.sky.rain.isPlaying, Is.True); Assert.That(world.CurrentView.activeEvent.id, Is.EqualTo("ev_a2"));
            StringAssert.Contains("TX-0312", world.Board(2).Text);
            foreach (var id in WorldUnits.CrewIds) Assert.That(world.Crew(id).Status, Is.EqualTo("working"));
            Feed(16); yield return null;
            StringAssert.Contains("8403-5", world.Board(1).Text); Assert.That(world.sky.State, Is.EqualTo("clear")); Assert.That(world.sky.Raining, Is.False);
            Assert.That(world.trailerBoard.LineCount, Is.EqualTo(3));
            foreach (var id in WorldUnits.CrewIds) Assert.That(world.trailerBoard.PointCount(id), Is.EqualTo(5));
        }
        [UnityTest]
        public IEnumerator ReplayBothSeatsAt420ShowsFacingForemen()
        {
            var catalog = Catalog.Load(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Data/catalog.json")));
            var sim = Simulation.Create(Scenario.Load(scenario.ToString(), catalog), catalog);
            var script = JArray.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Data/scripts/slice_play.json")));
            var commands = script.OfType<JObject>().OrderBy(entry => (double)entry["t"]).ToArray();
            // Same fixed tick and stable, both-seat ordering as headless.js / Replay.cs (WO-S3 Amendment 1).
            const double dt = 0.01, epsilon = 1e-6, until = 4.20;
            var index = 0;
            while (sim.T <= until + epsilon)
            {
                while (index < commands.Length && (double)commands[index]["t"] <= sim.T + epsilon)
                { var entry = commands[index++]; sim.Apply((string)entry["seat"], (JObject)entry["cmd"]); sim.Drain(); }
                if (sim.T >= until - epsilon) break;
                Assert.That(sim.Speed, Is.Not.EqualTo("paused"), "Replay paused before the fixture time");
                sim.Tick(dt); sim.Drain();
            }
            world.Apply(SiteView.From(sim.View("site")), 5);
            yield return new WaitForSeconds(WorldUnits.PoseSeconds + 0.2f);
            Crew("pipe", 520, "arguing"); Crew("back", 272.5, "arguing"); Crew("exc", 869, "stopped");
            Assert.That(world.CurrentView.activeEvent.id, Is.EqualTo("ev_p1"));
            var pipe = world.Crew("pipe"); var back = world.Crew("back");
            Assert.That(pipe.Partner, Is.EqualTo(back.transform)); Assert.That(back.Partner, Is.EqualTo(pipe.transform));
            var towardBack = (back.Foreman.position - pipe.Foreman.position).normalized;
            Assert.That(Vector3.Dot(pipe.Foreman.forward, towardBack), Is.GreaterThan(0.99f));
            Assert.That(Vector3.Dot(back.Foreman.forward, -towardBack), Is.GreaterThan(0.99f));
            Assert.That((pipe.Foreman.position.x + back.Foreman.position.x) / 2, Is.EqualTo((pipe.StationFt + back.StationFt) / 2 * WorldUnits.MetresPerFoot).Within(0.05));
            CollectionAssert.AreEquivalent(new[] { "ConflictTalk", "TalkAlternate" }, new[] { pipe.ForemanClip, back.ForemanClip });
        }
    }
}
