using System;
using System.Collections.Generic;
using System.Linq;
using LOBOver.Runtime;
using LOBOver.Sim;
using LOBOver.VR;
using LOBOver.World;
using LOBOver.XR;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;

namespace LOBOver.MR
{
    public sealed class DioramaSeat : MonoBehaviour
    {
        public Director director;
        public SiteWorld world;
        public Transform model, wrist;
        public XRRig xr;
        private Transform eye => xr == null ? null : xr.CentreEye;
        public IInspectorInput Input { get; set; } = new OvrInput();
        public SeatAudio voice;
        public WorldAssets assets;
        public AudioClip clickClip;
        private TextMeshPro label, card, controlsText;
        private Transform labelTarget;
        private float labelUntil, cardUntil;
        private bool dragging;
        private Vector3 previousHand;
        private int speedIndex, configuredCells;
        private string machines;
        private readonly HashSet<int> scaledAudio = new HashSet<int>();
        private static readonly double[] DayMultipliers = { 1, 0.5, 2 }; // WO-S4 amendment 1.
        // WO-S4 sections 3 and 6: slice/table sizes, placement and overlays.
        public const float SliceMetres = 2, TableLimitMetres = 4, PlaceAhead = 0.7f, TableHeight = 0.85f;
        private const float LabelSeconds = 6, CardSeconds = 10, PlanHeight = 0.4f, PlanWidth = 0.6f;
        public const double SliceLengthFeet = 3000; // Data/Scenarios/slice_texas.json: job.lengthFt.
        public static float ScaleFor(double lengthFt) => (float)((lengthFt <= SliceLengthFeet ? SliceMetres : TableLimitMetres) / (lengthFt * WorldUnits.MetresPerFoot));

        private void OnEnable()
        {
            if (director == null) return;
            if (label == null) BuildControls();
            wrist.gameObject.SetActive(true);
            director.Loaded += Configure; director.ViewsChanged += OnViews; director.Event += OnEvent;
            InspectTarget.Selected += Point; voice.MissingVoice += MissingVoice;
            if (director.Ready) Configure();
            Place();
        }
        private void OnDisable()
        {
            if (director == null) return;
            director.Loaded -= Configure; director.ViewsChanged -= OnViews; director.Event -= OnEvent;
            InspectTarget.Selected -= Point; voice.MissingVoice -= MissingVoice;
            wrist.gameObject.SetActive(false); dragging = false;
        }
        private void BuildControls()
        {
            label = WorldDrawing.Text("PointedInspection", transform, Vector3.zero, new Vector2(0.6f, 0.2f), 0.022f, assets.font);
            card = WorldDrawing.Text("EventCard", transform, Vector3.zero, new Vector2(0.65f, 0.3f), 0.025f, assets.font);
            controlsText = WorldDrawing.Text("Playback", wrist, new Vector3(0, 0.045f, -0.012f), new Vector2(0.25f, 0.06f), 0.012f, assets.font);
            RayButton.Create(wrist, "Play / Pause", new Vector3(-0.065f, 0, -0.015f), new Vector2(0.12f, 0.035f), assets, clickClip,
                () => { if (director.Speed == "paused") director.Start(); else director.Pause(); });
            RayButton.Create(wrist, "Speed", new Vector3(0.065f, 0, -0.015f), new Vector2(0.12f, 0.035f), assets, clickClip,
                () => { speedIndex = (speedIndex + 1) % DayMultipliers.Length; director.SecondsPerDay = director.ScenarioSecondsPerDay * DayMultipliers[speedIndex]; });
            RayButton.Create(wrist, "Reset", new Vector3(-0.065f, -0.045f, -0.015f), new Vector2(0.12f, 0.035f), assets, clickClip, director.Reset);
            RayButton.Create(wrist, "Scenario", new Vector3(0.065f, -0.045f, -0.015f), new Vector2(0.12f, 0.035f), assets, clickClip,
                () => {
                    var next = director.scenarioId == "slice_texas" ? "stage1_texas" : director.scenarioId == "stage1_texas" ? "stage2_texas" : "slice_texas";
                    PlayerPrefs.SetString("LOBOver.Scenario", next); PlayerPrefs.Save(); director.Load(next);
                });
        }
        private void Configure()
        {
            var length = (double)director.Job["lengthFt"];
            model.localScale = Vector3.one * ScaleFor(length);
            var span = (float)length * WorldUnits.MetresPerFoot;
            // Presenter-only: this model may show Plan. The full-scale inspector never sees this panel.
            FitBoard(world.planPanel.transform, new Vector3(span, PlanHeight / model.localScale.x, 0), PlanWidth / 3);
            FitBoard(world.trailerBoard.transform, new Vector3(0, 0.15f / model.localScale.x, 0), 0.3f);
            labelUntil = cardUntil = 0; configuredCells = 0; machines = null;
            speedIndex = Array.FindIndex(DayMultipliers, n => Math.Abs(director.SecondsPerDay / director.ScenarioSecondsPerDay - n) < 0.001);
            if (speedIndex < 0) speedIndex = 0;
            // SiteSky's desktop observer offset is in metres. Keep rain local to the miniature instead.
            world.sky.enabled = false;
            world.sky.rain.transform.localPosition = new Vector3(span / 2, 12, 0);
            foreach (var bank in model.GetComponentsInChildren<Transform>(true).Where(t => t.name == "FarBank" || t.name == "NearBank" || t.name == "Subsoil" || t.name == "Road"))
            {
                var position = bank.localPosition; position.x = span / 2; bank.localPosition = position;
                var size = bank.localScale; size.x = span; bank.localScale = size;
            }
        }
        private void FitBoard(Transform board, Vector3 position, float scale)
        {
            board.SetParent(model, false); board.localPosition = position;
            board.localRotation = Quaternion.Euler(0, 90, 0); board.localScale = Vector3.one * (scale / model.localScale.x);
        }
        public void Place()
        {
            if (eye == null || model == null) return;
            var forward = Vector3.ProjectOnPlane(eye.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
            model.position = new Vector3(eye.position.x, TableHeight, eye.position.z) + forward * PlaceAhead;
            model.rotation = Quaternion.LookRotation(forward) * Quaternion.Euler(0, -90, 0);
        }
        private void OnViews(SiteView site, PlanView ignored)
        {
            var key = string.Join("/", site.crews.Select(c => c.machinePlate));
            if (configuredCells == world.CellCount && machines == key) return;
            configuredCells = world.CellCount; machines = key;
            foreach (var audio in model.GetComponentsInChildren<AudioSource>(true))
                if (scaledAudio.Add(audio.GetInstanceID()))
                { audio.minDistance *= model.localScale.x; audio.maxDistance *= model.localScale.x; audio.spatialBlend = 1; audio.rolloffMode = AudioRolloffMode.Logarithmic; }
            foreach (var particle in model.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particle.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
            }
            // Cell surfaces are pointable even while undug; crew proxies cover tiny animated meshes.
            foreach (var cell in world.GetComponentsInChildren<StationCell>()) InspectTarget.Attach(cell.gameObject, "wall");
            foreach (var crew in world.GetComponentsInChildren<CrewRig>())
                if (crew.GetComponent<BoxCollider>() == null)
                {
                    var hit = crew.gameObject.AddComponent<BoxCollider>(); hit.center = new Vector3(0, 2, 0); hit.size = new Vector3(6, 4, 6);
                    InspectTarget.Attach(crew.gameObject, "crew");
                }
        }
        private void Point(InspectTarget target, Vector3 point)
        {
            if (!director.Ready || target == null || !target.transform.IsChildOf(model)) return;
            var crew = target.GetComponentInParent<CrewRig>(); var cell = target.GetComponentInParent<StationCell>();
            var board = target.GetComponentInParent<ZoneBoard>();
            var ft = crew != null ? crew.StationFt : cell != null ? cell.StationFt : board != null
                ? (double)director.Job["zones"].OfType<JObject>().First(z => (int)z["id"] == board.Zone)["fromFt"]
                : Mathf.Clamp(world.transform.InverseTransformPoint(point).x / WorldUnits.MetresPerFoot, 0, (float)world.LengthFt);
            var reply = director.InspectAt(target.target, ft);
            label.text = (string)reply?["text"]; labelTarget = target.transform; labelUntil = Time.unscaledTime + LabelSeconds;
        }
        private void OnEvent(JObject message)
        {
            if (!Director.ForSeat(message, "site")) return;
            card.text = (string)message["card"]?["title"] + "\n" + string.Join("\n", message["card"]?["facts"]?.Values<string>() ?? Enumerable.Empty<string>());
            cardUntil = Time.unscaledTime + CardSeconds;
        }
        private void MissingVoice(string text)
        { label.text = text; labelTarget = null; labelUntil = Time.unscaledTime + 8; } // WO-09 missing-clip fallback.
        private void Update()
        {
            if (Input.GoDown) Place();
            var hand = xr.RightController.position;
            if (Input.GripHeld)
            { if (dragging) model.position += hand - previousHand; dragging = true; }
            else dragging = false;
            previousHand = hand;
            if (controlsText != null) controlsText.text = director.scenarioId + "\nDay " + director.DisplayDay + " / " + director.SecondsPerDay.ToString("0.##") + " s per day";
        }
        private void LateUpdate()
        {
            // SiteSky is shared with the desktop world; its fog must not cover the real room.
            RenderSettings.fog = false;
            if (label == null) return;
            label.gameObject.SetActive(Time.unscaledTime < labelUntil);
            label.transform.position = (labelTarget == null ? model.position : labelTarget.position) + Vector3.up * 0.2f;
            label.transform.rotation = Quaternion.LookRotation(label.transform.position - eye.position);
            card.gameObject.SetActive(Time.unscaledTime < cardUntil);
            card.transform.position = model.position + model.right * (world.LengthFt > 0 ? (float)world.LengthFt * WorldUnits.MetresPerFoot * model.localScale.x / 2 : 1) + Vector3.up * 0.5f;
            card.transform.rotation = Quaternion.LookRotation(card.transform.position - eye.position);
        }
    }
}
