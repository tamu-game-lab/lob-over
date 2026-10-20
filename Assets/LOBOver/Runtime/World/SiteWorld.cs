using System.Collections.Generic;
using System.Linq;
using LOBOver.Runtime;
using LOBOver.Sim;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;

namespace LOBOver.World
{
    public sealed class SiteWorld : MonoBehaviour
    {
        public Director director;
        public WorldAssets assets;
        public TrailerBoard trailerBoard;
        public PlanPanel planPanel;
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
        public void Configure(JObject job, double deadline)
        {
            if (generated != null) WorldDrawing.Remove(generated.gameObject);
            generated = WorldDrawing.Group("GeneratedSite", transform).transform;
            cells.Clear(); stakes.Clear(); boards.Clear(); crews.Clear(); CurrentView = null;
            LengthFt = (double)job["lengthFt"]; zones = (JArray)job["zones"].DeepClone();
            jobSign.Apply((JObject)job["sign"]);
            var boundaries = zones.OfType<JObject>().ToDictionary(z => (int)z["id"], z => (double)z["fromFt"]);
            trailerBoard.Configure(assets, LengthFt, deadline, boundaries); planPanel.Configure(assets, LengthFt, deadline);
            if (sky.State != null) sky.Apply("clear"); else sky.Configure(assets);
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
            sky.Apply(view.sky); gateText.text = "Trucks waiting: " + view.gate.trucksWaiting;
            truck.localPosition = new Vector3((float)view.truck.stationFt * WorldUnits.MetresPerFoot, 0, WorldUnits.RoadZ);
            trailerBoard.Apply(view, displayDay);
        }
        private void Build(SiteView view)
        {
            foreach (var sample in view.trench)
            {
                // The endpoint is a stake, not an extra 100 ft of trench.
                if (sample.stationFt >= LengthFt) continue;
                var cell = Instantiate(assets.cellPrefab, generated); cell.Initialize(sample.stationFt, assets);
                cells[(int)sample.stationFt] = cell; stakes[(int)sample.stationFt] = cell.stake;
                var spoil = Instantiate(assets.spoil, generated); spoil.name = "Spoil_" + (int)sample.stationFt;
                spoil.transform.localPosition = new Vector3((float)sample.stationFt * WorldUnits.MetresPerFoot + WorldUnits.CellLength / 2, 0, -3);
            }
            var endpoint = WorldDrawing.Box("Stake_" + (int)LengthFt, generated, new Vector3((float)LengthFt * WorldUnits.MetresPerFoot, 0.55f, 1.2f), new Vector3(0.08f, 1.1f, 0.08f), assets.timber);
            var label = WorldDrawing.Text("Station", endpoint.transform, new Vector3(0, 0.6f, 0), new Vector2(1.5f, 0.4f), 0.3f, assets.font);
            // Text cannot inherit the thin post's scale.
            label.transform.SetParent(generated, true); label.transform.localScale = Vector3.one;
            label.transform.localPosition = endpoint.transform.localPosition + Vector3.up * 0.6f; label.transform.localRotation = Quaternion.Euler(0, 180, 0);
            label.text = WorldUnits.Station(LengthFt); stakes[(int)LengthFt] = endpoint.transform;
            foreach (var zone in zones.OfType<JObject>())
            {
                var root = WorldDrawing.Group("ZoneBoard", generated); root.transform.localPosition = new Vector3((float)zone["fromFt"] * WorldUnits.MetresPerFoot, 0, 1.8f);
                root.transform.localRotation = Quaternion.Euler(0, 180, 0);
                WorldDrawing.Box("Post", root.transform, new Vector3(0, 1, 0), new Vector3(0.1f, 2, 0.1f), assets.timber);
                var face = WorldDrawing.Box("Board", root.transform, new Vector3(0, 1.8f, 0), new Vector3(1, 1.5f, 0.06f), assets.steel);
                InspectTarget.Attach(face, "board"); var board = root.AddComponent<ZoneBoard>();
                board.label = WorldDrawing.Text("Permit", root.transform, new Vector3(0, 1.8f, -0.05f), new Vector2(0.9f, 1.3f), 0.12f, assets.font);
                board.Initialize((int)zone["id"]); boards[board.Zone] = board;
            }
            foreach (var id in WorldUnits.CrewIds)
            { var crew = Instantiate(assets.crewPrefab, generated); crew.Initialize(id, assets); crews[id] = crew; }
        }
    }
}
