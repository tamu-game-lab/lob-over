#if UNITY_EDITOR || !UNITY_ANDROID
// Legacy Site.unity remains available in the Editor; WO-L2 excludes this seat from Quest.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LOBOver.Runtime;
using LOBOver.XR;
using LOBOver.Sim;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;

namespace LOBOver.World
{
    public sealed class DesktopPreview : MonoBehaviour
    {
        public Director director;
        public XRRig xr;
        public SiteWorld world;
        public Camera previewCamera;
        public string LastInspection { get; private set; } = "";
        public string LastCard { get; private set; } = "";
        private string voiceText = "";
        private float voiceUntil;
        private double lastTruckStation;
        private bool lastDismounted, hasTruck;
        private readonly Queue<string> voiceQueue = new Queue<string>();
        private Coroutine voiceRoutine;
        private AudioSource voiceSource = null;
        private GUIStyle hud;
        // Preview-only camera controls, independent of the simulation clock.
        private const float FlyMetresPerSecond = 12, FastMultiplier = 4, LookDegreesPerPixel = 0.12f;

        private void OnEnable()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            gameObject.SetActive(false);
            return;
#else
            if (director == null) return;
            if (xr != null) xr.Resolve();
            director.Message += OnMessage; director.Voice += OnVoice; director.Loaded += Clear;
            if (voiceSource == null) voiceSource = WorldDrawing.Sound(WorldDrawing.Group("VoiceAudio", transform), false);
#endif
        }
        private void OnDisable()
        {
            if (director != null) { director.Message -= OnMessage; director.Voice -= OnVoice; director.Loaded -= Clear; }
            Clear(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        private void Clear()
        {
            LastInspection = ""; LastCard = ""; voiceText = ""; hasTruck = false; voiceQueue.Clear();
            if (voiceRoutine != null) StopCoroutine(voiceRoutine);
            voiceRoutine = null;
            if (voiceSource != null) { voiceSource.Stop(); if (voiceSource.clip != null) Destroy(voiceSource.clip); voiceSource.clip = null; }
        }
        private void Update()
        {
            var keyboard = Keyboard.current; var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;
            var flying = mouse.rightButton.isPressed;
            Cursor.lockState = flying ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !flying;
            if (flying)
            {
                var delta = mouse.delta.ReadValue() * LookDegreesPerPixel;
                var angles = previewCamera.transform.eulerAngles;
                var pitch = angles.x > 180 ? angles.x - 360 : angles.x;
                previewCamera.transform.rotation = Quaternion.Euler(Mathf.Clamp(pitch - delta.y, -85, 85), angles.y + delta.x, 0);
                var move = new Vector3((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0), 0,
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                previewCamera.transform.Translate(move * FlyMetresPerSecond * (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed ? FastMultiplier : 1) * Time.unscaledDeltaTime);
            }
            if (!director.Ready) return;
            if (keyboard.spaceKey.wasPressedThisFrame) { if (director.Speed == "paused") director.Start(); else director.Pause(); }
            if (keyboard.rKey.wasPressedThisFrame) director.Reset();
            var view = director.SiteView;
            if (view == null) return;
            FollowTruck(view.truck);
            // RMB scopes WASD to flight so D/S cannot also issue Site commands.
            if (!flying)
            {
                if (keyboard.dKey.wasPressedThisFrame) Send(view.truck.dismounted ? "mount" : "dismount");
                if (keyboard.sKey.wasPressedThisFrame) director.Send(new JObject { ["action"] = "stopWork", ["zone"] = world.ZoneAt(view.truck.stationFt) });
            }
            var digits = new[] { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
            for (var i = 0; i < digits.Length; i++) if (keyboard[digits[i]].wasPressedThisFrame) Ride((i + 1) * WorldUnits.StationFeet);
            if (keyboard.leftBracketKey.wasPressedThisFrame) Ride((Mathf.Round((float)view.truck.stationFt / WorldUnits.StationFeet) - 1) * WorldUnits.StationFeet);
            if (keyboard.rightBracketKey.wasPressedThisFrame) Ride((Mathf.Round((float)view.truck.stationFt / WorldUnits.StationFeet) + 1) * WorldUnits.StationFeet);
            if (keyboard.iKey.wasPressedThisFrame) Inspect();
            if (keyboard.fKey.wasPressedThisFrame) director.Send(new JObject { ["action"] = "flag", ["stationFt"] = view.truck.stationFt, ["note"] = "" });
            if (keyboard.cKey.wasPressedThisFrame) Confirm("done");
            if (keyboard.nKey.wasPressedThisFrame) Confirm("notDone");
            if (keyboard.lKey.wasPressedThisFrame) Send("listen");
        }
        private void FollowTruck(TruckView truck)
        {
            if (truck.moving) return;
            if (hasTruck && (truck.stationFt != lastTruckStation || truck.dismounted != lastDismounted))
            {
                previewCamera.transform.position = world.transform.TransformPoint(new Vector3((float)truck.stationFt * WorldUnits.MetresPerFoot, 1.7f,
                    truck.dismounted ? WorldUnits.TrenchWidth / 2 + 1.5f : WorldUnits.RoadZ));
                previewCamera.transform.rotation = world.transform.rotation * Quaternion.Euler(12, 180, 0);
            }
            lastTruckStation = truck.stationFt; lastDismounted = truck.dismounted; hasTruck = true;
        }
        private void Ride(float station) => director.Send(new JObject { ["action"] = "moveTo", ["stationFt"] = Mathf.Clamp(station, 0, (float)world.LengthFt) });
        private void Send(string action) => director.Send(new JObject { ["action"] = action });
        private void Confirm(string outcome)
        {
            var order = director.SiteView.orders.FirstOrDefault(item => item.status == "landed");
            if (order == null) { LastInspection = "no order landed here"; return; }
            director.Send(new JObject { ["action"] = "confirm", ["orderId"] = order.id, ["outcome"] = outcome });
        }
        private void Inspect()
        {
            if (!Physics.Raycast(previewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0)), out var hit, 6000, ~0, QueryTriggerInteraction.Collide)) return;
            var target = hit.collider.GetComponentInParent<InspectTarget>();
            if (target != null) director.Send(new JObject { ["action"] = "inspect", ["target"] = target.target });
        }
        private void OnMessage(JObject message)
        {
            var type = (string)message["t"];
            if (type == "nack") LastInspection = (string)message["reason"];
            if (!Director.ForSeat(message, "site")) return;
            if (type == "inspection") LastInspection = (string)message["text"];
            if (type == "event") LastCard = (string)message["card"]?["title"] + "\n" + string.Join("\n", message["card"]?["facts"]?.Values<string>() ?? Enumerable.Empty<string>());
        }
        private void OnVoice(JObject message)
        {
            if (!Director.ForSeat(message, "site")) return;
            foreach (var id in message["lineIds"].Values<string>()) voiceQueue.Enqueue(id);
            if (voiceRoutine == null) voiceRoutine = StartCoroutine(PlayVoice());
        }
        private IEnumerator PlayVoice()
        {
            yield return null;
            while (voiceQueue.Count > 0)
            {
                var id = voiceQueue.Dequeue(); var line = director.Lines.OfType<JObject>().FirstOrDefault(item => (string)item["id"] == id);
                if (line == null) continue;
                var speaker = (string)line["speaker"];
                var crew = speaker.StartsWith("foreman_") ? world.Crew(speaker.Substring("foreman_".Length)) : null;
                voiceSource.transform.position = crew == null ? previewCamera.transform.position : crew.transform.position;
                voiceSource.spatialBlend = crew == null ? 0 : 1;
                voiceText = (string)line["text"]; voiceUntil = Time.unscaledTime + 8;
                var relative = "Voice/" + director.scenarioId + "/" + id + ".wav";
#if !UNITY_ANDROID || UNITY_EDITOR
                if (!File.Exists(Path.Combine(Application.streamingAssetsPath, relative))) continue;
#endif
                using (var request = UnityWebRequestMultimedia.GetAudioClip(StreamingData.Url(relative), AudioType.WAV))
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success) continue;
                    var clip = DownloadHandlerAudioClip.GetContent(request); voiceSource.clip = clip; voiceSource.Play();
                    voiceUntil = Time.unscaledTime + Mathf.Max(8, clip.length);
                    while (voiceSource.isPlaying) yield return null;
                    voiceSource.clip = null; Destroy(clip);
                }
            }
            voiceRoutine = null;
        }
        private void OnGUI()
        {
            if (hud == null) hud = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 16, wordWrap = true, padding = new RectOffset(12, 12, 10, 10) };
            var truck = director.SiteView?.truck;
            var text = director.LoadError ?? (director.Ready ? "Day " + director.DisplayDay + "  ·  " + director.Speed + "\nTruck " + WorldUnits.Station(truck.stationFt)
                + (truck.moving ? " driving" : truck.dismounted ? " dismounted" : " mounted") : "Loading site");
            text += "\n" + LastInspection + "\n" + LastCard;
            if (Time.unscaledTime < voiceUntil) text += "\n" + voiceText;
            text += "\nRMB + WASD fly · Shift fast · 1–9 / [ ] ride\nD mount/dismount · I inspect · F flag · S stop\nC confirm · N not carried out · L listen · Space pause · R reset";
            var width = Mathf.Min(520, Screen.width - 20);
            GUI.Box(new Rect(10, 10, width, hud.CalcHeight(new GUIContent(text), width)), text, hud);
            GUI.Label(new Rect(Screen.width / 2f - 6, Screen.height / 2f - 10, 20, 20), "+");
        }
    }
}

#endif
