#if UNITY_EDITOR || !UNITY_ANDROID
// Legacy Site.unity remains available in the Editor; WO-L2 excludes this seat from Quest.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LOBOver.Runtime;
using LOBOver.Sim;
using LOBOver.World;
using LOBOver.XR;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;

namespace LOBOver.VR
{
    public sealed class InspectorSeat : MonoBehaviour
    {
        public Director director;
        public SiteWorld world;
        public XRRig xr;
        public Transform siteSpace;
        public IInspectorInput Input { get; set; } = new OvrInput();
        public Transform cab, dashboard, wrist;
        public WorldAssets assets;
        public AudioClip clickClip;
        public ComfortFade fade;
        public SeatAudio voice;
        public InspectionSpeech speech;
        public GameObject flagPrefab, stopFlagPrefab;
        public InspectorInputLayer InputLayer { get; private set; }
        private TextMeshPro clipboard, dashText, cardText, wristCard, selectedText;
        private GameObject confirmation;
        private Transform zoneChoices;
        private RayButton destinationButton;
        private readonly List<RayButton> verbs = new List<RayButton>();
        private readonly List<double> destinations = new List<double>();
        private readonly List<GameObject> flags = new List<GameObject>();
        private readonly HashSet<string> listened = new HashSet<string>();
        private LineRenderer reticle;
        private AudioSource engine;
        private Coroutine ride;
        private bool positioned, dismounted, turnLatched, navLatched;
        private double station;
        private float fadeSeconds = ShortFade, cardUntil, voiceUntil;
        private string inspection = "", missingVoice = "";
        private int selected;
        private JObject pendingFact;
        // WO-09 sections 1-7: comfort, geometry and card timings.
        private const float ShortFade = 0.3f, LongFade = 0.6f, EngineSeconds = 1.5f, SeatEye = 1.15f;
        private const float StandingEye = 1.7f, EdgeOffset = 1.5f, SnapDegrees = 30, CardSeconds = 20, VoiceSeconds = 8;

        private void OnEnable()
        {
            if (director == null) return;
            if (InputLayer == null) BuildControls();
            wrist.gameObject.SetActive(true);
            director.ViewsChanged += OnViews; director.Message += OnMessage; director.Loaded += ResetSeat;
            InspectTarget.Selected += Point; voice.MissingVoice += MissingVoice;
            ResetSeat();
            if (director.SiteView != null) OnViews(director.SiteView, null);
        }
        private void OnDisable()
        {
            if (director == null) return;
            director.ViewsChanged -= OnViews; director.Message -= OnMessage; director.Loaded -= ResetSeat;
            InspectTarget.Selected -= Point; voice.MissingVoice -= MissingVoice;
            if (ride != null) StopCoroutine(ride);
            ride = null; if (engine != null) engine.Stop(); if (fade != null) fade.SetExplicitFade(0);
            wrist.gameObject.SetActive(false);
        }
        private void ResetSeat()
        {
            if (ride != null) StopCoroutine(ride);
            ride = null; positioned = false; listened.Clear(); destinations.Clear(); selected = 0;
            inspection = ""; missingVoice = ""; cardUntil = voiceUntil = 0; pendingFact = null;
            confirmation.SetActive(false);
            foreach (var flag in flags) if (flag != null) Destroy(flag);
            flags.Clear();
            if (director.Job == null) return;
            var length = (double)director.Job["lengthFt"];
            for (double ft = 0; ft <= length; ft += WorldUnits.StationFeet) destinations.Add(ft);
            foreach (var zone in director.Job["zones"].OfType<JObject>()) destinations.Add((double)zone["fromFt"]);
            if (zoneChoices != null) WorldDrawing.Remove(zoneChoices.gameObject);
            zoneChoices = WorldDrawing.Group("ZoneDestinations", dashboard).transform;
            var zones = director.Job["zones"].OfType<JObject>().ToArray();
            for (var i = 0; i < zones.Length; i++)
            {
                var ft = (double)zones[i]["fromFt"];
                RayButton.Create(zoneChoices, "Zone " + zones[i]["id"] + "\n" + WorldUnits.Station(ft),
                    new Vector3((i - (zones.Length - 1) / 2f) * 0.19f, -0.30f, -0.04f), new Vector2(0.18f, 0.07f), assets, clickClip, () => InputLayer.Ride(ft));
            }
            // The slice's collapse stake is authored at 5+20, between the regular survey stakes.
            // Offer crew stations as well (updated when cycling), without rounding the selected destination.
            RefreshDestinations();
        }
        private void BuildControls()
        {
            InputLayer = new InspectorInputLayer(); InputLayer.Command += Send;
            InputLayer.Text += text => inspection = text;
            InputLayer.ConfirmRequested += () => confirmation.SetActive(true);
            clipboard = WorldDrawing.Text("Inspection", wrist, new Vector3(0, 0.012f, -0.012f), new Vector2(0.20f, 0.10f), 0.012f, assets.font);
            clipboard.enableAutoSizing = true; clipboard.fontSizeMin = 0.65f; clipboard.fontSizeMax = 1.2f;
            wristCard = WorldDrawing.Text("Event", wrist, new Vector3(0, 0.15f, -0.012f), new Vector2(0.22f, 0.13f), 0.012f, assets.font);
            var labels = new[] { "Flag", "Stop work", "Confirm" };
            for (var i = 0; i < labels.Length; i++)
            {
                var button = (InspectorButton)(i + 1);
                verbs.Add(RayButton.Create(wrist, labels[i], new Vector3((i - 1) * 0.065f, -0.06f, -0.015f), new Vector2(0.03f, 0.02f), assets, clickClip, () => Press(button)));
            }
            confirmation = WorldDrawing.Group("ConfirmOutcome", wrist);
            RayButton.Create(confirmation.transform, "Done", new Vector3(-0.055f, -0.105f, 0), new Vector2(0.09f, 0.035f), assets, clickClip, () => Press(InspectorButton.Done));
            RayButton.Create(confirmation.transform, "Not carried out", new Vector3(0.055f, -0.105f, 0), new Vector2(0.09f, 0.035f), assets, clickClip, () => Press(InspectorButton.NotCarriedOut));
            dashText = WorldDrawing.Text("TruckState", dashboard, new Vector3(0, 0.09f, -0.04f), new Vector2(0.75f, 0.08f), 0.03f, assets.font);
            selectedText = WorldDrawing.Text("Destination", dashboard, new Vector3(0, 0.02f, -0.04f), new Vector2(0.75f, 0.08f), 0.027f, assets.font);
            RayButton.Create(dashboard, "Previous", new Vector3(-0.26f, -0.08f, -0.04f), new Vector2(0.22f, 0.06f), assets, clickClip, () => Select(-1));
            destinationButton = RayButton.Create(dashboard, "Ride", new Vector3(0, -0.08f, -0.04f), new Vector2(0.22f, 0.06f), assets, clickClip, RideSelected);
            RayButton.Create(dashboard, "Next", new Vector3(0.26f, -0.08f, -0.04f), new Vector2(0.22f, 0.06f), assets, clickClip, () => Select(1));
            RayButton.Create(dashboard, "Settings: fade 0.3 s / 0.6 s", new Vector3(0, -0.20f, -0.04f), new Vector2(0.5f, 0.06f), assets, clickClip,
                () => { fadeSeconds = fadeSeconds == ShortFade ? LongFade : ShortFade; });
            cardText = WorldDrawing.Text("EventCard", dashboard, new Vector3(0, 0.42f, -0.04f), new Vector2(0.8f, 0.45f), 0.035f, assets.font);
            engine = WorldDrawing.Sound(cab.gameObject, true); engine.clip = assets.dozer;
            reticle = WorldDrawing.Line("DwellReticle", transform, assets.ink, Color.white, 0.0005f);
            reticle.gameObject.AddComponent<RigAnchorFollower>().rig = xr;
            reticle.positionCount = 33;
        }
        private void RefreshDestinations()
        {
            if (director.SiteView != null)
                foreach (var crew in director.SiteView.crews)
                    if (!destinations.Contains(crew.stationFt)) destinations.Add(crew.stationFt);
            destinations.Sort();
        }
        private void Select(int direction)
        {
            RefreshDestinations();
            if (destinations.Count == 0) return;
            selected = (selected + direction + destinations.Count) % destinations.Count;
        }
        private void RideSelected() { if (destinations.Count > 0) InputLayer.Ride(destinations[selected]); }
        private string FirstOrder => director.SiteView?.orders.FirstOrDefault()?.id;
        private int Zone => director.SiteView == null ? 0 : world.ZoneAt(director.SiteView.truck.stationFt);
        private void Press(InspectorButton button)
        {
            DismissCard(); InputLayer.Press(button, Zone, FirstOrder);
            if (button == InspectorButton.Done || button == InspectorButton.NotCarriedOut) confirmation.SetActive(false);
        }
        private void Send(JObject command)
        {
            var action = (string)command["action"];
            pendingFact = action == "flag" || action == "stopWork" ? (JObject)command.DeepClone() : null;
            director.Send(command);
            if (action == "moveTo" && director.SiteView?.truck.moving == true && ride == null) ride = StartCoroutine(Ride());
        }
        private void OnViews(SiteView site, PlanView ignored)
        {
            InputLayer.Observe(site.truck);
            if (!positioned) { Position(site.truck); positioned = true; }
            else if (ride == null && !site.truck.moving && (station != site.truck.stationFt || dismounted != site.truck.dismounted))
                ride = StartCoroutine(Ride(false));
            if (site.truck.moving && ride == null) ride = StartCoroutine(Ride());
        }
        private void Position(TruckView truck)
        {
            station = truck.stationFt; dismounted = truck.dismounted;
            cab.localPosition = new Vector3((float)station * WorldUnits.MetresPerFoot, 0, WorldUnits.RoadZ);
            cab.localRotation = Quaternion.Euler(0, 90, 0);
            var eye = xr.CentreEye;
            // Preserve the existing discrete ride while keeping the tracked rig fixed: move only our site.
            var desired = siteSpace.TransformPoint(new Vector3((float)station * WorldUnits.MetresPerFoot,
                dismounted ? StandingEye : SeatEye, dismounted ? WorldUnits.TrenchWidth / 2 + EdgeOffset : WorldUnits.RoadZ));
            var yaw = eye.eulerAngles.y - ((dismounted ? 180 : 90) + siteSpace.eulerAngles.y);
            siteSpace.RotateAround(desired, Vector3.up, yaw);
            siteSpace.position += eye.position - desired;
            world.sky.observer = eye;
        }
        private void Point(InspectTarget target, Vector3 position)
        {
            if (director.SiteView == null || !target.transform.IsChildOf(world.transform.parent)) return;
            DismissCard();
            if (director.SiteView.truck.dismounted && !director.SiteView.truck.moving)
                director.Send(new JObject { ["action"] = "inspect", ["target"] = target.target });
        }
        private IEnumerator Ride(bool engineOn = true)
        {
            yield return Fade(0, 1);
            if (engineOn)
            { engine.Play(); yield return new WaitForSecondsRealtime(EngineSeconds); engine.Stop(); }
            while (director.SiteView == null || director.SiteView.truck.moving) yield return null;
            Position(director.SiteView.truck);
            yield return Fade(1, 0); ride = null;
        }
        private IEnumerator Fade(float from, float to)
        {
            for (float elapsed = 0; elapsed < fadeSeconds; elapsed += Time.unscaledDeltaTime)
            { fade.SetExplicitFade(Mathf.Lerp(from, to, elapsed / fadeSeconds)); yield return null; }
            fade.SetExplicitFade(to);
        }
        private void Update()
        {
            if (InputLayer == null || director.SiteView == null) return;
            var input = Input;
            if (input.MountDown) DismissCard();
            var eye = xr.CentreEye;
            InspectTarget target = null;
            if (ride == null && Physics.Raycast(eye.position, eye.forward, out var hit, 6000, ~0, QueryTriggerInteraction.Collide))
                target = hit.collider.GetComponentInParent<InspectTarget>();
            InputLayer.Tick(input, Time.unscaledDeltaTime, Time.unscaledTime, target == null ? null : target.GetInstanceID().ToString(), target?.target, Zone, FirstOrder);
            if (input.GoDown) { DismissCard(); RideSelected(); }
            if (Mathf.Abs(input.Turn) < 0.3f) turnLatched = false;
            if (!turnLatched && Mathf.Abs(input.Turn) > 0.7f && ride == null)
            { siteSpace.RotateAround(eye.position, Vector3.up, -Mathf.Sign(input.Turn) * SnapDegrees); turnLatched = true; }
            if (Mathf.Abs(input.Navigate) < 0.3f) navLatched = false;
            if (!navLatched && Mathf.Abs(input.Navigate) > 0.7f) { Select(input.Navigate > 0 ? 1 : -1); navLatched = true; }
            for (var i = 0; i < 33; i++)
            {
                var angle = i / 32f * Mathf.PI * 2;
                var radius = Mathf.Lerp(0.002f, 0.007f, InputLayer.DwellFraction);
                reticle.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0.5f));
            }
            reticle.gameObject.SetActive(ride == null && target != null);
            var truck = director.SiteView.truck;
            dashText.text = truck.moving || ride != null ? "driving" : WorldUnits.Station(truck.stationFt) + (truck.dismounted ? " dismounted" : " mounted");
            if (destinations.Count > 0)
            {
                selectedText.text = "Previous " + WorldUnits.Station(destinations[(selected + destinations.Count - 1) % destinations.Count]) + "   > " + WorldUnits.Station(destinations[selected]) + "   Next " + WorldUnits.Station(destinations[(selected + 1) % destinations.Count]);
                destinationButton.label.text = "Ride " + WorldUnits.Station(destinations[selected]);
            }
            clipboard.text = "Day " + director.DisplayDay + " / " + director.Speed + "\n" + (Time.unscaledTime < voiceUntil ? missingVoice : inspection);
            cardText.gameObject.SetActive(Time.unscaledTime < cardUntil); wristCard.gameObject.SetActive(Time.unscaledTime < cardUntil);
            foreach (var button in verbs) button.Available = truck.dismounted && !truck.moving;
            if (truck.dismounted && !truck.moving)
                foreach (var crew in director.SiteView.crews.Where(c => c.status == "arguing" || c.status == "held"))
                {
                    var key = crew.id + ":" + crew.status + ":" + crew.moodLevel;
                    if (Vector3.Distance(eye.position, world.Crew(crew.id).transform.position) <= 10 && listened.Add(key))
                        director.Send(new JObject { ["action"] = "listen" });
                }
        }
        private void DismissCard() { cardUntil = 0; }
        private void MissingVoice(string value) { missingVoice = value; voiceUntil = Time.unscaledTime + VoiceSeconds; }
        private void OnMessage(JObject message)
        {
            var type = (string)message["t"];
            if (type == "ack" && pendingFact != null)
            {
                var isStop = (string)pendingFact["action"] == "stopWork";
                var obj = Instantiate(isStop ? stopFlagPrefab : flagPrefab, world.transform);
                var position = isStop ? world.Board((int)pendingFact["zone"]).transform.localPosition : new Vector3((float)pendingFact["stationFt"] * WorldUnits.MetresPerFoot, 0, 1.2f);
                obj.transform.localPosition = position; flags.Add(obj); pendingFact = null;
            }
            if (type == "nack") { inspection = (string)message["reason"]; pendingFact = null; }
            if (!Director.ForSeat(message, "site")) return;
            if (type == "inspection") { inspection = (string)message["text"]; speech.Speak(inspection); }
            if (type == "event")
            {
                cardText.text = (string)message["card"]?["title"] + "\n" + string.Join("\n", message["card"]?["facts"]?.Values<string>() ?? Enumerable.Empty<string>());
                wristCard.text = cardText.text; cardUntil = Time.unscaledTime + CardSeconds;
            }
        }
    }
}

#endif
