using System.Collections.Generic;
using LOBOver.Sim;
using UnityEngine;

namespace LOBOver.World
{
    public sealed class TrailerBoard : MonoBehaviour
    {
        private readonly Dictionary<string, List<Vector3>> points = new Dictionary<string, List<Vector3>>();
        private readonly Dictionary<string, LineRenderer> lines = new Dictionary<string, LineRenderer>();
        private readonly Dictionary<int, LineRenderer> permits = new Dictionary<int, LineRenderer>();
        private int lastDay = -1;
        private Chart chart;
        private Transform drawing;
        public int LineCount => lines.Count;
        public int PointCount(string crew) => points[crew].Count;

        public void Configure(WorldAssets assets, double lengthFt, double deadline, Dictionary<int, double> boundaries)
        {
            if (drawing != null) WorldDrawing.Remove(drawing.gameObject);
            drawing = WorldDrawing.Group("SitePlot", transform).transform;
            points.Clear(); lines.Clear(); permits.Clear(); lastDay = -1;
            chart = new Chart(drawing, assets, lengthFt, deadline, new Rect(-0.82f, -0.36f, 1.65f, 0.65f));
            chart.Label("Title", "SITE  ·  actual feet / day", new Vector2(0, 0.42f), new Vector2(1.8f, 0.15f), 0.08f);
            for (var i = 0; i < WorldUnits.CrewIds.Length; i++)
            {
                var id = WorldUnits.CrewIds[i]; points[id] = new List<Vector3>();
                lines[id] = WorldDrawing.Line("Actual_" + id, drawing, assets.ink, WorldUnits.CrewColours[i], 0.009f);
            }
            foreach (var item in boundaries)
                permits[item.Key] = chart.Rule("UnpostedZone_" + item.Key, item.Value, false, Color.gray);
        }
        public void Apply(SiteView view, int displayDay)
        {
            foreach (var board in view.boards) if (permits.TryGetValue((int)board.zone, out var rule)) rule.gameObject.SetActive(board.permitNumber == null);
            if (displayDay == lastDay) return;
            foreach (var crew in view.crews)
            {
                points[crew.id].Add(chart.Point(crew.stationFt, displayDay));
                lines[crew.id].positionCount = points[crew.id].Count;
                lines[crew.id].SetPositions(points[crew.id].ToArray());
            }
            lastDay = displayDay;
        }
    }

    internal sealed class Chart
    {
        private readonly Transform root;
        private readonly WorldAssets assets;
        private readonly double length, deadline;
        private readonly Rect area;
        public Chart(Transform root, WorldAssets assets, double length, double deadline, Rect area)
        {
            this.root = root; this.assets = assets; this.length = length; this.deadline = deadline; this.area = area;
            var axes = WorldDrawing.Line("Axes", root, assets.ink, Color.white, 0.005f);
            axes.positionCount = 3; axes.SetPositions(new[] { Point(0, deadline), Point(0, 1), Point(length, 1) });
            Label("StationAxis", "0 ft                       " + length + " ft", new Vector2(area.center.x, area.yMin - 0.045f), new Vector2(area.width, 0.1f), 0.045f);
            Label("DayAxis", "Day " + deadline, new Vector2(area.xMin + 0.1f, area.yMax + 0.045f), new Vector2(0.3f, 0.1f), 0.045f);
        }
        public Vector3 Point(double feet, double day) => new Vector3(
            area.xMin + (float)(feet / length) * area.width,
            area.yMin + (float)((day - 1) / (deadline - 1)) * area.height, -0.065f);
        public TMPro.TextMeshPro Label(string name, string value, Vector2 position, Vector2 size, float height)
        {
            var text = WorldDrawing.Text(name, root, new Vector3(position.x, position.y, -0.07f), size, height, assets.font);
            text.text = value; return text;
        }
        public LineRenderer Rule(string name, double value, bool day, Color colour)
        {
            var line = WorldDrawing.Line(name, root, assets.ink, colour, 0.004f); line.positionCount = 2;
            line.SetPositions(day ? new[] { Point(0, value), Point(length, value) } : new[] { Point(value, 1), Point(value, deadline) });
            return line;
        }
    }
}
