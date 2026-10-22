using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LOBOver.Client;
using LOBOver.Play;
using LOBOver.World;
using LOBOver.XR;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LOBOver.Tests
{
    public sealed class Level1HostTests
    {
        private Process host;
        private HostConnection connection;
        private WarpController warp;
        private readonly List<JObject> sent = new List<JObject>();
        private readonly HashSet<string> received = new HashSet<string>();
        private AudioClip testClip;
        private readonly List<string> warpLogs = new List<string>();
        [UnitySetUp]
        public IEnumerator Setup()
        {
#if UNITY_EDITOR
            Application.logMessageReceived += RecordWarpLog;
            var root = Directory.GetParent(Application.dataPath).FullName;
            Assert.That(File.Exists(Path.Combine(root, "Data/Scenarios/level1_texas.json")), Is.True, "WO-L1 scenario must land before the integration test runs");
            // Local Node host only; no browser, package restore, transcript output, or discovery broadcast.
            host = Process.Start(new ProcessStartInfo("node", "Assets/LOBOver/Tests/level1-host.mjs")
            { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
                RedirectStandardError = true, RedirectStandardInput = true });
            HostConnection.TestEndpointOverride = "ws://127.0.0.1:7333/ws";
            SceneManager.sceneLoaded += SetTestEndpoint;
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Play.unity", new LoadSceneParameters(LoadSceneMode.Single));
            SceneManager.sceneLoaded -= SetTestEndpoint;
            HostConnection.TestEndpointOverride = null;
            connection = Object.FindFirstObjectByType<HostConnection>(); warp = Object.FindFirstObjectByType<WarpController>();
            Assert.That(connection, Is.Not.Null, "Run LOB Over > Build > Play Scene before the test");
            yield return Until(() => connection.Connected && connection.View != null && received.Contains("welcome") && received.Contains("state"), 10);
            Assert.That(connection.ScenarioId, Is.EqualTo("level1_texas")); Assert.That(connection.Mirror, Is.False);
#else
            Assert.Ignore("Node integration test runs in Editor PlayMode."); yield break;
#endif
        }
        private void SetTestEndpoint(Scene scene, LoadSceneMode mode)
        {
            var client = XRRig.Components<HostConnection>(scene).Single();
            client.enabled = false; client.EndpointOverride = "ws://127.0.0.1:7333/ws";
            client.Message += message => received.Add((string)message["t"]);
            client.CommandSent += sent.Add; client.enabled = true;
        }
        private IEnumerator Until(Func<bool> condition, float seconds)
        {
            var deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                if (host != null && host.HasExited) Assert.Fail("Node host exited: " + host.StandardError.ReadToEnd());
                yield return null;
            }
            Assert.That(condition(), Is.True, "Timed out after " + seconds + " seconds");
        }
        private static void Press(CrewButton button, bool poke = false)
        {
            var pose = new Pose(button.transform.position, button.transform.rotation);
            var evt = new PointerEvent(77, PointerEventType.Select, pose);
            if (poke) button.GetComponent<PokeInteractable>().PublishPointerEvent(evt);
            else button.GetComponent<RayInteractable>().PublishPointerEvent(evt);
        }
        private void RecordWarpLog(string message, string stack, LogType type)
        { if (message.StartsWith("LOBOver Warp: ", StringComparison.Ordinal)) warpLogs.Add(message); }
        private void AssertGlobals()
        {
            Assert.That(Shader.GetGlobalFloat("_WarpProgress"), Is.EqualTo(warp.Progress).Within(0.00001));
            var expected = warp.Progress >= 1 ? 1e6f : 160 * (1 - (1 - warp.Progress) * (1 - warp.Progress));
            Assert.That(Shader.GetGlobalFloat("_WarpRadius"), Is.EqualTo(expected).Within(0.001));
            Assert.That(Shader.GetGlobalFloat("_WarpBand"), Is.EqualTo(6));
            Vector3 origin = Shader.GetGlobalVector("_WarpOrigin");
            Assert.That(origin, Is.EqualTo(warp.Origin));
            Assert.That(origin.y, Is.EqualTo(warp.site.position.y).Within(0.00001));
        }
        [UnityTest]
        public IEnumerator MarcusThenBackPreservesRigAndUsesOneMoveTo()
        {
            var rig = warp.rig;
            var camera = rig.CentreEye.GetComponent<Camera>();
            Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(camera.backgroundColor.a, Is.Zero); Assert.That(camera.allowHDR, Is.False);
            var hub = Object.FindFirstObjectByType<HubPanel>();
            yield return Until(() => hub.Placed, 3);
            StringAssert.StartsWith("Connected to ", hub.status.text);
            var panelPosition = hub.panel.position; var rootPosition = rig.CameraRig.transform.position;
            var end = Time.realtimeSinceStartup + 3;
            while (Time.realtimeSinceStartup < end)
            {
                Assert.That(rig.CameraRig.transform.position.y, Is.GreaterThanOrEqualTo(-0.01f));
                Assert.That(hub.panel.position, Is.EqualTo(panelPosition)); yield return null;
            }
            var marcus = hub.panel.GetComponentsInChildren<CrewButton>().Single(b => b.crew == "pipe");
            yield return Until(() => marcus.Available, 3);
            Assert.That(warp.Progress, Is.Zero);
            var started = Time.realtimeSinceStartupAsDouble;
            Press(marcus);
            var fixedPosition = warp.site.position; var fixedRotation = warp.site.rotation;
            var dissolveDeadline = Time.realtimeSinceStartup + 3;
            while (warp.Busy && Time.realtimeSinceStartup < dissolveDeadline)
            {
                AssertGlobals();
                Assert.That(warp.rig.Passthrough.enabled, Is.True); Assert.That(warp.rig.Passthrough.hidden, Is.False);
                Assert.That(marcus.Available, Is.False); Assert.That(warp.presenter.Frozen, Is.True);
                Assert.That(warp.wrist.GetComponentInChildren<CrewButton>().Available, Is.True);
                foreach (var animator in warp.site.GetComponentsInChildren<Animator>(true)) Assert.That(animator.speed, Is.Zero);
                Assert.That(warp.site.position, Is.EqualTo(fixedPosition)); Assert.That(warp.site.rotation, Is.EqualTo(fixedRotation));
                yield return null;
            }
            Assert.That(Time.realtimeSinceStartupAsDouble - started, Is.InRange(1.4, 1.6));
            Assert.That(warp.Progress, Is.EqualTo(1)); AssertGlobals();
            Assert.That(warp.Busy, Is.False, "Warp did not finish within three seconds");
            Assert.That(warp.InSite, Is.True); Assert.That(warp.site.gameObject.activeSelf, Is.True);
            Assert.That(rig.Passthrough.enabled, Is.False); Assert.That(rig.CameraRig.transform.position, Is.EqualTo(rootPosition));
            var pipe = connection.View.crews.Single(c => c.id == "pipe");
            var beside = warp.site.TransformPoint(new Vector3((float)pipe.stationFt * WorldUnits.MetresPerFoot, 0, WorldUnits.RoadZ));
            Assert.That(Vector3.Distance(new Vector3(rig.ViewEye.position.x, 0, rig.ViewEye.position.z), beside), Is.LessThan(0.5f));
            yield return Until(() => sent.Any(m => (string)m["action"] == "dismount"), 3);
            Assert.That(sent.Count(m => (string)m["action"] == "moveTo"), Is.EqualTo(1));
            Assert.That((string)sent.First()["action"], Is.EqualTo("moveTo"));
            var back = warp.wrist.GetComponentInChildren<CrewButton>();
            yield return Until(() => back.Available, 3); Press(back, true);
            yield return Until(() => !warp.Busy, 3);
            Assert.That(warp.Progress, Is.Zero); AssertGlobals();
            CollectionAssert.AreEqual(new[] { "in start", "in done", "out start", "out done" },
                warpLogs.Select(line => line.Substring("LOBOver Warp: ".Length).Split(new[] { " progress=" }, StringSplitOptions.None)[0]).ToArray());
            Assert.That(warp.InSite, Is.False); Assert.That(warp.site.gameObject.activeSelf, Is.False);
            Assert.That(warp.hub.activeInHierarchy, Is.True); Assert.That(rig.Passthrough.enabled, Is.True);
            Assert.That(hub.panel.position, Is.EqualTo(panelPosition));
            yield return Until(() => marcus.Available, 3); Press(marcus, true);
            yield return Until(() => !warp.Busy && sent.Count(m => (string)m["action"] == "dismount") == 2, 4);
            Assert.That(sent.Count(m => (string)m["action"] == "moveTo"), Is.EqualTo(2), "One moveTo per press, including after Back");
            Assert.That(sent.Count(m => (string)m["action"] == "mount"), Is.EqualTo(1));
            Assert.That(received.Contains("nack"), Is.False);
        }
        [UnityTest]
        public IEnumerator BackHalfwayReversesWithoutAProgressJump()
        {
            Assert.That(warp.Visit("pipe"), Is.True);
            yield return Until(() => warp.Progress >= 0.5f, 1.1f);
            Assert.That(warp.Progress, Is.LessThan(0.6f));
            var progress = warp.Progress;
            var back = warp.wrist.GetComponentInChildren<CrewButton>();
            Assert.That(back.Available, Is.True); Press(back, true);
            Assert.That(warp.Progress, Is.EqualTo(progress).Within(0.001));
            while (warp.Busy)
            {
                Assert.That(warp.Progress, Is.LessThanOrEqualTo(progress)); progress = warp.Progress;
                Assert.That(warp.rig.Passthrough.enabled, Is.True); AssertGlobals(); yield return null;
            }
            Assert.That(warp.Progress, Is.Zero); Assert.That(warp.InSite, Is.False);
            Assert.That(warp.site.gameObject.activeSelf, Is.False); Assert.That(warp.hub.activeSelf, Is.True);
            Assert.That(warp.rig.Passthrough.enabled, Is.True); Assert.That(warp.rig.Passthrough.hidden, Is.False);
            Assert.That(warp.presenter.Frozen, Is.False);
            Assert.That(warpLogs.Any(line => line.Contains("in done")), Is.False);
        }
        [UnityTest]
        public IEnumerator WatchdogFinishesStoppedCoroutinesInEitherDirection()
        {
            Assert.That(warp.Visit("pipe"), Is.True);
            var started = Time.realtimeSinceStartupAsDouble;
            warp.StopAllCoroutines();
            yield return Until(() => !warp.Busy, 3.1f);
            Assert.That(Time.realtimeSinceStartupAsDouble - started, Is.InRange(2.9, 3.1));
            Assert.That(warp.InSite, Is.True); Assert.That(warp.Progress, Is.EqualTo(1));
            Assert.That(warp.rig.Passthrough.enabled, Is.False); Assert.That(warp.presenter.Frozen, Is.False);
            Assert.That(warp.Back(), Is.True); started = Time.realtimeSinceStartupAsDouble;
            warp.StopAllCoroutines();
            yield return Until(() => !warp.Busy, 3.1f);
            Assert.That(Time.realtimeSinceStartupAsDouble - started, Is.InRange(2.9, 3.1));
            Assert.That(warp.site.gameObject.activeSelf, Is.False); Assert.That(warp.Progress, Is.Zero);
            Assert.That(warp.rig.Passthrough.enabled, Is.True); Assert.That(warp.presenter.Frozen, Is.False);
            Assert.That(warpLogs.Count(line => line.Contains("watchdog")), Is.EqualTo(2));
        }
        [UnityTest]
        public IEnumerator MidWaveHasRoomOverheadAndOpaqueGroundThenFullCoverage()
        {
#if UNITY_EDITOR
            Assert.That(warp.Visit("pipe"), Is.True);
            yield return Until(() => !warp.Busy, 2);
            var preview = Object.FindFirstObjectByType<PlayPreview>();
            var wristVisible = warp.wrist.activeSelf; warp.wrist.SetActive(false);
            try
            {
                warp.PreviewProgress(0.45f);
                var frame = preview.CaptureFrame();
                try
                {
                    var pixels = frame.GetPixels(); var size = frame.width;
                    Assert.That(pixels.Skip(size * size / 2).Count(c => c.a == 0), Is.GreaterThan(0), "Room must remain overhead");
                    var bottom = Enumerable.Range(0, size / 4).SelectMany(y => Enumerable.Range(size * 2 / 5, size / 5).Select(x => pixels[y * size + x]));
                    Assert.That(bottom.Count(c => c.a == 1), Is.GreaterThan(0), "Filled ground must cover bottom centre");
                }
                finally { Object.DestroyImmediate(frame); }
                warp.PreviewProgress(1);
                frame = preview.CaptureFrame();
                try { Assert.That(frame.GetPixels().Min(c => c.a), Is.EqualTo(1), "At progress 1 no room remains"); }
                finally { Object.DestroyImmediate(frame); }
                preview.WriteWarpFrames(warp);
                foreach (var label in new[] { "0.2", "0.45", "0.7", "0.9", "1.0" })
                    Assert.That(File.Exists("Monitor/out/unity/warp-" + label + ".png"), Is.True);
            }
            finally { warp.PreviewProgress(1); warp.wrist.SetActive(wristVisible); }
#else
            yield break;
#endif
        }
        [UnityTest]
        public IEnumerator TwoTurnExchangesPlayInOrderWithoutOverlap()
        {
            var voice = Object.FindFirstObjectByType<HostVoice>();
            testClip = AudioClip.Create("Level1 test turn", 2400, 1, 24000, false);
            var paths = new List<string>(); var events = new List<(int turn, bool start, float time)>();
            var active = 0;
            voice.ClipOverride = path => { paths.Add(path); return testClip; };
            voice.TurnPlayback += (line, turn, start) =>
            {
                active += start ? 1 : -1;
                Assert.That(active, Is.InRange(0, 1)); events.Add((turn, start, Time.realtimeSinceStartup));
            };
            host.StandardInput.WriteLine("exchange"); host.StandardInput.Flush();
            yield return Until(() => events.Count == 8, 8);
            CollectionAssert.AreEqual(new[] { 1, 1, 2, 2, 1, 1, 2, 2 }, events.Select(e => e.turn).ToArray());
            for (var i = 2; i < events.Count; i += 2)
                Assert.That(events[i].time - events[i - 1].time, Is.GreaterThanOrEqualTo(HostVoice.TurnGapSeconds - 0.02f));
            Assert.That(paths[0], Is.EqualTo("Voice/level1_texas/grumble_back_pipe_1.wav"));
            Assert.That(paths[1], Is.EqualTo("Voice/level1_texas/grumble_back_pipe_2.wav"));
            Assert.That(voice.source.spatialBlend, Is.EqualTo(1));
            Assert.That(voice.source.minDistance, Is.EqualTo(40)); Assert.That(voice.source.maxDistance, Is.EqualTo(120));
        }
        [TearDown]
        public void Cleanup()
        {
            Application.logMessageReceived -= RecordWarpLog; warpLogs.Clear();
            HostConnection.TestEndpointOverride = null;
            SceneManager.sceneLoaded -= SetTestEndpoint;
            if (connection != null) connection.enabled = false;
            if (host != null) { if (!host.HasExited) host.Kill(); host.Dispose(); host = null; }
            if (testClip != null) Object.Destroy(testClip);
            sent.Clear(); received.Clear();
        }
    }
}
