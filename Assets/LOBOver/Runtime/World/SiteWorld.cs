using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR || !UNITY_ANDROID
using LOBOver.Runtime;
#endif
using LOBOver.Sim;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;

namespace LOBOver.World
{
    public sealed class SiteWorld : MonoBehaviour
    {
#if UNITY_EDITOR || !UNITY_ANDROID
        public Director director;
        public PlanPanel planPanel;
#endif
        public WorldAssets assets;
        public TrailerBoard trailerBoard;
        public JobSign jobSign;
        public SiteSky sky;
        public TextMeshPro gateText;
        public Transform truck;
        public SiteView CurrentView { get; private set; }
        public int CellCount => cells.Count;
        public int BoardCount => boards.Count;
        public int CrewCount => crews.Count;
        public double LengthFt { get; private set; }
        private readonly Dictionary<int, StationCell> cells = new Dictionary<int, StationCell>();
        private readonly Dictionary<int, Transform> stakes = new Dictionary<int, Transform>();
        private readonly Dictionary<int, ZoneBoard> boards = new Dictionary<int, ZoneBoard>();
        private readonly Dictionary<string, CrewRig> crews = new Dictionary<string, CrewRig>();
        private JArray zones;
        private Transform generated;
        public StationCell CellAt(int feet) => cells.TryGetValue(Mathf.FloorToInt(feet / WorldUnits.StationFeet) * (int)WorldUnits.StationFeet, out var cell) ? cell : null;
        public CrewRig Crew(string id) => crews.TryGetValue(id, out var crew) ? crew : null;
        public Transform StakeAt(int feet) => stakes.TryGetValue(feet, out var stake) ? stake : null;
        public ZoneBoard Board(int zone) => boards.TryGetValue(zone, out var board) ? board : null;
        public int ZoneAt(double feet) => (int)zones.OfType<JObject>().First(z => feet >= (double)z["fromFt"] && (feet < (double)z["toFt"] || feet == LengthFt))["id"];

#if UNITY_EDITOR || !UNITY_ANDROID
        private void OnEnable()
        {
            if (director == null) return;
            director.Loaded += OnLoaded; director.ViewsChanged += OnViews; director.Result += OnResult;
            if (director.Ready) { OnLoaded(); if (director.SiteView != null) OnViews(director.SiteView, director.PlanView); }
        }
        private void OnDisable()
        {
            if (director == null) return;
            director.Loaded -= OnLoaded; director.ViewsChanged -= OnViews; director.Result -= OnResult;
        }
        private void OnLoaded() => Configure(director.Job, director.DeadlineDay);
        private void OnViews(SiteView site, PlanView plan)
        {
            Apply(site, director.DisplayDay); planPanel.Apply(plan, director.TimeDays);
        }
        private void OnResult(JObject value) => planPanel.ShowResult(value);
#endif
        public void Configure(JObject job, double deadline)
        {
            if (generated != null) WorldDrawing.Remove(generated.gameObject);
            generated = WorldDrawing.Group("GeneratedSite", transform).transform;
            cells.Clear(); stakes.Clear(); boards.Clear(); crews.Clear(); CurrentView = null;
            LengthFt = (double)job["lengthFt"]; zones = (JArray)job["zones"].DeepClone();
            if (jobSign != null && job["sign"] is JObject sign) jobSign.Apply(sign);
            var boundaries = zones.OfType<JObject>().ToDictionary(z => (int)z["id"], z => (double)z["fromFt"]);
            if (trailerBoard != null) trailerBoard.Configure(assets, LengthFt, deadline, boundaries);
#if UNITY_EDITOR || !UNITY_ANDROID
            if (planPanel != null) planPanel.Configure(assets, LengthFt, deadline);
#endif
            if (sky != null) { if (sky.State != null) sky.Apply("clear"); else sky.Configure(assets); }
        }
        public void Apply(SiteView view, int displayDay)
        {
            if (generated == null) throw new System.InvalidOperationException("Configure SiteWorld before applying a view");
            if (cells.Count == 0) Build(view);
            CurrentView = view;
            foreach (var cell in view.trench) if (cells.TryGetValue((int)cell.stationFt, out var target)) target.Apply(cell);
            foreach (var crew in view.crews) crews[crew.id].Apply(crew);
            var arguing = WorldUnits.CrewIds.Select(id => crews[id]).Where(crew => crew.Status == "arguing").ToArray();
            foreach (var crew in crews.Values) crew.FaceArgument(arguing.FirstOrDefault(other => other != crew && crew.Status == "arguing")?.transform);
            foreach (var board in view.boards) boards[(int)board.zone].Apply(board);
            if (sky != null) sky.Apply(view.sky);
            if (gateText != null) gateText.text = "Trucks waiting: " + view.gate.trucksWaiting;
            if (truck != null) truck.localPosition = new Vector3((float)view.truck.stationFt * WorldUnits.MetresPerFoot, 0, WorldUnits.RoadZ);
            if (trailerBoard != null) trailerBoard.Apply(view, displayDay);
        }
        private void Build(SiteView view)
        {
            foreach (var sample in view.trench)
            {
                // The endpoint is a stake, not an extra 100 ft of trench.
                if (sample.stationFt >= LengthFt) continue;
                var cell = Instantiate(assets.cellPrefab, generated); cell.Initialize(sample.stationFt, assets);
                cells[(int)sample.stationFt] = cell; stakes[(int)sample.stationFt] = cell.stake;
                if (!assets.detailedSite)
                {
                    var spoil = Instantiate(assets.spoil, generated); spoil.name = "Spoil_" + (int)sample.stationFt;
                    spoil.transform.localPosition = new Vector3((float)sample.stationFt * WorldUnits.MetresPerFoot + WorldUnits.CellLength / 2, 0, -3);
                }
            }
            if (assets.detailedSite)
            {
                var endpointStake = SiteDetailDrawing.Stake(generated, assets, out var endpointText);
                endpointStake.localPosition += Vector3.right * (float)LengthFt * WorldUnits.MetresPerFoot;
                endpointText.text = WorldUnits.Station(LengthFt); stakes[(int)LengthFt] = endpointStake;
            }
            else
            {
            var endpoint = WorldDrawing.Box("Stake_" + (int)LengthFt, generated, new Vector3((float)LengthFt * WorldUnits.MetresPerFoot, 0.55f, 1.2f), new Vector3(0.08f, 1.1f, 0.08f), assets.timber);
            var label = WorldDrawing.Text("Station", endpoint.transform, new Vector3(0, 0.6f, 0), new Vector2(1.5f, 0.4f), 0.3f, assets.font);
            // Text cannot inherit the thin post's scale.
            label.transform.SetParent(generated, true); label.transform.localScale = Vector3.one;
            label.transform.localPosition = endpoint.transform.localPosition + Vector3.up * 0.6f; label.transform.localRotation = Quaternion.Euler(0, 180, 0);
            label.text = WorldUnits.Station(LengthFt); stakes[(int)LengthFt] = endpoint.transform;
            }
            foreach (var zone in zones.OfType<JObject>())
            {
                var root = WorldDrawing.Group("ZoneBoard", generated); root.transform.localPosition = new Vector3((float)zone["fromFt"] * WorldUnits.MetresPerFoot, 0, 1.8f);
                root.transform.localRotation = Quaternion.Euler(0, 180, 0);
                BoardBox("Post", root.transform, new Vector3(0, 1, 0), new Vector3(0.1f, 2, 0.1f), assets.timber);
                var face = BoardBox("Board", root.transform, new Vector3(0, 1.8f, 0), new Vector3(1, 1.5f, 0.06f), assets.steel);
                InspectTarget.Attach(face, "board"); var board = root.AddComponent<ZoneBoard>();
                board.label = WorldDrawing.Text("Permit", root.transform, new Vector3(0, 1.8f, -0.05f), new Vector2(0.9f, 1.3f), 0.12f, assets.font);
                if (assets.detailedSite) board.label.fontSharedMaterial = assets.siteText;
                board.Initialize((int)zone["id"]); boards[board.Zone] = board;
            }
            foreach (var id in WorldUnits.CrewIds)
            { var crew = Instantiate(assets.crewPrefab, generated); crew.Initialize(id, assets); crews[id] = crew; }
        }
        private GameObject BoardBox(string name, Transform parent, Vector3 position, Vector3 size, Material material)
            => assets.detailedSite ? SiteDetailDrawing.Block(name, parent, position, size, material)
                : WorldDrawing.Box(name, parent, position, size, material);
    }
}
