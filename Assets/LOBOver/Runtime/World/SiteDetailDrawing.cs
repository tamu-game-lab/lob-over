using TMPro;
using UnityEngine;

namespace LOBOver.World
{
    public static class SiteDetailDrawing
    {
        // WO-L4 item 5: lath, lettering, pipe and water dimensions in metres.
        public const float StakeHeight = 1.2f, StakeWidth = 0.05f, LetterHeight = 0.04f, WaterHeight = 0.3f, BeddingDepth = 0.1f;
        public static GameObject Mesh(string name, Transform parent, Mesh mesh, Material material, Vector3 position = default)
        {
            var root = WorldDrawing.Group(name, parent); root.transform.localPosition = position;
            root.AddComponent<MeshFilter>().sharedMesh = mesh; root.AddComponent<MeshRenderer>().sharedMaterial = material; return root;
        }
        public static GameObject Block(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var obj = WorldDrawing.Box(name, parent, position, size, material);
            var collider = obj.GetComponent<Collider>(); collider.enabled = false;
            if (Application.isPlaying) Object.Destroy(collider); else Object.DestroyImmediate(collider);
            return obj;
        }
        public static Transform Stake(Transform parent, WorldAssets assets, out TextMeshPro text)
        {
            var root = WorldDrawing.Group("StationStake", parent).transform; root.localPosition = new Vector3(0, 0, 1.2f);
            Block("WoodenLath", root, Vector3.up * StakeHeight / 2, new Vector3(StakeWidth, StakeHeight, 0.025f), assets.timber);
            var flag = Block("PinkFlagging", root, new Vector3(0.07f, 1.12f, 0), new Vector3(0.18f, 0.05f, 0.003f), assets.flagging);
            flag.transform.localRotation = Quaternion.Euler(0, 20, -12);
            Block("StationSign", root, new Vector3(0, 0.9f, 0), new Vector3(0.34f, 0.12f, 0.018f), assets.steel);
            text = WorldDrawing.Text("Station", root, new Vector3(0, 0.9f, 0.012f), new Vector2(0.31f, 0.1f), LetterHeight, assets.font);
            text.transform.localRotation = Quaternion.Euler(0, 180, 0); text.alignment = TextAlignmentOptions.Center;
            text.fontSize = LetterHeight * 10 * assets.font.faceInfo.pointSize / Mathf.Max(1, assets.font.faceInfo.capLine);
            text.fontSharedMaterial = assets.siteText; return root;
        }
        public static void Cell(StationCell cell, WorldAssets a)
        {
            var root = cell.transform; var depth = WorldUnits.TrenchDepth;
            // The floor remains cut dirt; the separate raised GravelBedding covers it when piped.
            cell.surfaceMaterial = a.soil; cell.openMaterial = a.trench; cell.pipeMaterial = a.trench; cell.backfillMaterial = a.backfill;
            cell.surface = Mesh("Surface", root, a.stripMesh, a.soil);
            cell.floor = Mesh("Floor", root, a.stripMesh, a.trench, Vector3.down * depth);
            cell.walls = WorldDrawing.Group("Walls", root);
            cell.nearWall = Mesh("NearWall", cell.walls.transform, a.wallNearMesh, a.trench).transform;
            cell.farWall = Mesh("FarWall", cell.walls.transform, a.wallFarMesh, a.trench).transform;
            cell.pipes = WorldDrawing.Group("Pipes", root);
            Mesh("GravelBedding", cell.pipes.transform, a.stripMesh, a.bedding, Vector3.down * (depth - BeddingDepth));
            // WO-L4 rejection 1: three 24-sided builder sections, 288 pipe triangles per cell.
            for (var i = 0; i < 3; i++)
            {
                var pipe = Object.Instantiate(a.pipe, cell.pipes.transform); pipe.name = "LaidPipe_" + i;
                pipe.transform.localPosition = new Vector3(WorldUnits.CellLength * (i + 0.5f) / 3, -depth + BeddingDepth, 0);
            }
            cell.wetBand = WorldDrawing.Group("SeepBand", root);
            foreach (var side in new[] { -1, 1 })
                Block("WetLowerWall", cell.wetBand.transform, new Vector3(WorldUnits.CellLength / 2, -depth + 0.25f, side * 0.54f),
                    new Vector3(WorldUnits.CellLength, 0.5f, 0.005f), a.wetSoil);
            cell.water = Mesh("StandingWater", root, a.stripMesh, a.waterSurface, Vector3.down * (depth - WaterHeight));
            cell.slump = Mesh("SlumpedWall", root, a.slumpMesh, a.trench);
            Mesh("LooseCollapseMaterial", cell.slump.transform, a.windrowMesh, a.spoilSurface,
                new Vector3(0, -depth, 0)).transform.localScale = new Vector3(1, 0.2f, 0.16f);
            cell.spoilWindrow = Mesh("SpoilWindrow", root, a.windrowMesh, a.spoilSurface);
            cell.collapseProps = WorldDrawing.Group("CollapseProps", root);
            Prop(a.barrier, cell.collapseProps.transform, new Vector3(WorldUnits.CellLength / 2, 0, 2.1f));
            foreach (var x in new[] { 1f, WorldUnits.CellLength - 1 }) Prop(a.cone, cell.collapseProps.transform, new Vector3(x, 0, 2.2f));
            cell.box = Protection(root, "TrenchBox", a.steel); cell.shoring = Protection(root, "Shoring", a.timber);
            cell.stake = Stake(root, a, out cell.stationText);
            foreach (var obj in new[] { cell.floor, cell.walls, cell.pipes, cell.wetBand, cell.water, cell.slump, cell.box, cell.shoring, cell.spoilWindrow, cell.collapseProps }) obj.SetActive(false);
        }
        private static GameObject Protection(Transform parent, string name, Material material)
        {
            var root = WorldDrawing.Group(name, parent);
            foreach (var side in new[] { -1, 1 }) Block("Panel", root.transform, new Vector3(WorldUnits.CellLength / 2, -0.9f, side * 0.5f),
                new Vector3(WorldUnits.CellLength, 1.7f, 0.06f), material);
            for (var i = 0; i < 3; i++) Block("Strut", root.transform, new Vector3((i + 0.5f) * WorldUnits.CellLength / 3, -0.3f, 0), new Vector3(0.1f, 0.1f, 1), material);
            return root;
        }
        public static GameObject Prop(GameObject prefab, Transform parent, Vector3 position)
        {
            if (prefab == null) return null;
            var prop = Object.Instantiate(prefab, parent); prop.transform.localPosition = position; return prop;
        }
        public static void CrewSurroundings(Transform crew, WorldAssets assets)
        {
            var props = WorldDrawing.Group("CrewSurroundings", crew).transform;
            Prop(assets.spoil, props, new Vector3(-5, 0, -5));
            // WO-L4: fence every 10 m, 60 m each side, outside the four metre road.
            for (var x = -60; x <= 60; x += 10) Prop(assets.fence, props, new Vector3(x, 0, WorldUnits.RoadZ + 3));
        }
        public static GameObject MachinePerimeter(GameObject machine, Transform crew, WorldAssets assets)
        {
            var props = WorldDrawing.Group("MachinePerimeter", crew);
            var renderers = machine.GetComponentsInChildren<Renderer>(); if (renderers.Length == 0) return props;
            var bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            foreach (var x in new[] { bounds.min.x - 0.6f, bounds.max.x + 0.6f }) foreach (var z in new[] { bounds.min.z - 0.6f, bounds.max.z + 0.6f })
            {
                var position = crew.InverseTransformPoint(new Vector3(x, bounds.min.y, z)); position.y = 0;
                Prop(assets.cone, props.transform, position);
            }
            var barrier = crew.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.max.z + 1)); barrier.y = 0;
            Prop(assets.barrier, props.transform, barrier); return props;
        }
    }
}
