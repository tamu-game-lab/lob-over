#if UNITY_EDITOR || !UNITY_ANDROID
// Legacy Site.unity remains available in the Editor; WO-L2 excludes this seat from Quest.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LOBOver.Runtime;
using LOBOver.World;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace LOBOver.VR
{
    public sealed class SeatAudio : MonoBehaviour
    {
        public Director director;
        public SiteWorld world;
        public Transform listener, dashboard;
        public bool presenter;
        public event Action<string> MissingVoice;
        private readonly Queue<string> queue = new Queue<string>();
        private readonly List<string> waiting = new List<string>();
        private AudioSource source;
        private Coroutine playing;
        private const float ListenMetres = 10, MissingSeconds = 8; // WO-09 sections 5-6.
        private void OnEnable()
        {
            if (director == null) return;
            director.Voice += OnVoice; director.Loaded += Clear;
            if (source == null) source = WorldDrawing.Sound(WorldDrawing.Group("ForemanVoice", transform), false);
        }
        private void OnDisable()
        {
            if (director != null) { director.Voice -= OnVoice; director.Loaded -= Clear; }
            Clear();
        }
        private void Clear()
        {
            queue.Clear(); waiting.Clear();
            if (playing != null) StopCoroutine(playing);
            playing = null;
            if (source != null) { source.Stop(); if (source.clip != null) Destroy(source.clip); source.clip = null; }
        }
        private JObject Line(string id) => director.Lines?.OfType<JObject>().FirstOrDefault(line => (string)line["id"] == id);
        private CrewRig Speaker(JObject line)
        {
            var speaker = (string)line?["speaker"];
            return speaker != null && speaker.StartsWith("foreman_", StringComparison.Ordinal) ? world.Crew(speaker.Substring(8)) : null;
        }
        private bool Audible(string id)
        {
            var crew = Speaker(Line(id));
            return presenter || crew == null || listener != null && Vector3.Distance(listener.position, crew.transform.position) <= ListenMetres;
        }
        private void OnVoice(JObject message)
        {
            if (!Director.ForSeat(message, "site")) return;
            foreach (var id in message["lineIds"]?.Values<string>() ?? Enumerable.Empty<string>())
                if (Audible(id)) queue.Enqueue(id); else if (!waiting.Contains(id)) waiting.Add(id);
        }
        private void Update()
        {
            for (var i = 0; i < waiting.Count;)
                if (Audible(waiting[i])) { queue.Enqueue(waiting[i]); waiting.RemoveAt(i); } else i++;
            if (playing == null && queue.Count > 0) playing = StartCoroutine(Play());
        }
        private IEnumerator Play()
        {
            yield return null;
            while (queue.Count > 0)
            {
                var id = queue.Dequeue(); var line = Line(id);
                if (line == null) continue;
                var crew = Speaker(line);
                var relative = "Voice/" + director.scenarioId + "/" + id + ".wav";
#if !UNITY_ANDROID || UNITY_EDITOR
                if (!File.Exists(Path.Combine(Application.streamingAssetsPath, relative)))
                { MissingVoice?.Invoke((string)line["text"]); yield return new WaitForSecondsRealtime(MissingSeconds); continue; }
#endif
                using (var request = UnityWebRequestMultimedia.GetAudioClip(StreamingData.Url(relative), AudioType.WAV))
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    { MissingVoice?.Invoke((string)line["text"]); yield return new WaitForSecondsRealtime(MissingSeconds); continue; }
                    source.clip = DownloadHandlerAudioClip.GetContent(request);
                    Position(crew); source.Play();
                    while (source.isPlaying) { Position(crew); yield return null; }
                    Destroy(source.clip); source.clip = null;
                }
            }
            playing = null;
        }
        private void Position(CrewRig crew)
        {
            source.transform.position = crew != null ? crew.transform.position : dashboard.position;
            source.spatialBlend = 1;
            // A logarithmic tail keeps the miniature audible beyond its scaled field radius.
            source.rolloffMode = presenter ? AudioRolloffMode.Logarithmic : AudioRolloffMode.Linear;
            var scale = presenter ? world.transform.lossyScale.x : 1;
            source.minDistance = 2 * scale; source.maxDistance = 50 * scale;
        }
    }
}

#endif
