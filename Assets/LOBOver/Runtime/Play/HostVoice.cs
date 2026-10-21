using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LOBOver.Client;
using LOBOver.World;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace LOBOver.Play
{
    public sealed class HostVoice : MonoBehaviour
    {
        public HostConnection connection;
        public SiteWorld world;
        public WarpController warp;
        public AudioSource source;
        // Docs/LEVEL1 Voices and WO-L2 spatial sound distances.
        public const float TurnGapSeconds = 0.3f, ClearMetres = 40, SilentMetres = 120;
        public event Action<string, int, bool> TurnPlayback;
        public bool Playing => source != null && source.isPlaying;
        public Func<string, AudioClip> ClipOverride { private get; set; }
        private readonly Queue<Turn> turns = new Queue<Turn>();
        private Coroutine playback;
        private AudioClip ownedClip;
        private string run, scenario;
        private sealed class Turn { public string Stage, Line, Speaker; public int Number; }
        private void OnEnable() { connection.Message += Receive; connection.Changed += ConnectionChanged; }
        private void OnDisable() { connection.Message -= Receive; connection.Changed -= ConnectionChanged; Clear(); }
        private void ConnectionChanged()
        {
            if (!connection.Connected || run != connection.RunId || scenario != connection.ScenarioId) Clear();
            run = connection.RunId; scenario = connection.ScenarioId;
        }
        private void Clear()
        {
            if (playback != null) StopCoroutine(playback);
            playback = null; turns.Clear();
            if (source != null) { source.Stop(); source.clip = null; }
            if (ownedClip != null) { Destroy(ownedClip); ownedClip = null; }
        }
        private void Receive(JObject message)
        {
            if ((string)message["t"] != "voice" || !VoiceTurn.SafeId(connection.ScenarioId)) return;
            if (message["seats"] is JArray seats && !seats.Values<string>().Contains("site")) return;
            foreach (var turn in VoiceTurn.Read(message))
            {
                if (CrewId(turn.Speaker) == null) continue;
                turns.Enqueue(new Turn { Stage = connection.ScenarioId, Line = turn.LineId, Speaker = turn.Speaker, Number = turn.Number });
            }
            if (playback == null && turns.Count > 0) playback = StartCoroutine(Play());
        }
        private static string CrewId(string speaker)
        {
            switch (speaker)
            {
                case "foreman_exc": case "ray": case "Ray": return "exc";
                case "foreman_pipe": case "marcus": case "Marcus": return "pipe";
                case "foreman_back": case "dana": case "Dana": return "back";
                default: return null;
            }
        }
        private IEnumerator Play()
        {
            yield return null;
            while (turns.Count > 0)
            {
                var turn = turns.Dequeue();
                var relative = "Voice/" + turn.Stage + "/" + turn.Line + "_" + turn.Number + ".wav";
                AudioClip clip = ClipOverride?.Invoke(relative);
                var owned = clip == null;
                if (clip == null)
                {
                    var path = Application.streamingAssetsPath.TrimEnd('/') + "/" + relative;
                    var url = path.Contains("://") ? path : new Uri(path).AbsoluteUri;
                    using var request = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV);
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    { Debug.LogWarning("LOB Over voice clip: " + relative + ": " + request.error); continue; }
                    clip = DownloadHandlerAudioClip.GetContent(request);
                }
                if (owned) ownedClip = clip;
                source.clip = clip;
                Position(turn.Speaker); source.Play(); TurnPlayback?.Invoke(turn.Line, turn.Number, true);
                while (source.isPlaying) { Position(turn.Speaker); yield return null; }
                TurnPlayback?.Invoke(turn.Line, turn.Number, false); source.clip = null;
                if (owned) { Destroy(clip); ownedClip = null; }
                yield return new WaitForSecondsRealtime(TurnGapSeconds);
            }
            playback = null;
        }
        private void Position(string speaker)
        {
            var crew = world.Crew(CrewId(speaker));
            if (crew != null) source.transform.position = crew.Foreman.position;
            source.mute = crew == null || !warp.site.gameObject.activeInHierarchy;
        }
    }
}
