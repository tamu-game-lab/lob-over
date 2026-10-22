using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LOBOver.Editor
{
    public static class SiteGeometryBuilder
    {
        // WO-L4 items 4-5, metres. X covers the existing 0..914.4 m site plus its approaches.
        public const float MinX = -100, MaxX = 1100, HalfWidth = 100, FineStep = 2, CoarseStep = 10;
        public const float FineRadius = 30, FlatRadius = 8, Relief = 0.6f, Horizon = 3000;
        public const float RoadWidth = 4, RutDepth = 0.06f, SpoilHeight = 1, SpoilBreak = 30;
        public const int PipeSides = 24; // WO-L4 rejection 1: at most 400 triangles per laid cell.
        public const float PipeDiameter = 0.3f; // WO-L4 item 10, outside diameter.
        private const float HalfTrench = World.WorldUnits.TrenchWidth / 2;
        private static float Noise(float x, float z) => (Mathf.PerlinNoise(x * 0.017f + 23, z * 0.017f + 73) * 0.57f
            + Mathf.PerlinNoise(x * 0.037f + 9, z * 0.037f + 31) * 0.28f + Mathf.PerlinNoise(x * 0.081f + 59, z * 0.081f + 11) * 0.15f) * 2 - 1;
        public static float Height(float x, float z)
        {
            var distance = Mathf.Min(Mathf.Abs(z), Mathf.Abs(z - World.WorldUnits.RoadZ));
            var height = Relief * Noise(x, z) * Mathf.SmoothStep(0, 1, (distance - FlatRadius) / 14);
            var road = z - World.WorldUnits.RoadZ;
            var ruts = Mathf.Exp(-Mathf.Pow((road - 0.85f) / 0.3f, 2)) + Mathf.Exp(-Mathf.Pow((road + 0.85f) / 0.3f, 2));
            return height - RutDepth * ruts;
        }
        public static Color Paint(float x, float z)
        {
            var edge = Noise(x * 7, z * 7) * 0.8f;
            var gravel = 1 - Mathf.SmoothStep(0, 1, (Mathf.Abs(z - World.WorldUnits.RoadZ) - RoadWidth / 2 + edge) / 1.4f);
            var dirt = 1 - Mathf.SmoothStep(0, 1, (Mathf.Abs(z) - 12 + edge) / 3);
            return new Color(dirt * (1 - gravel), gravel, (1 - dirt) * (1 - gravel), 1);
        }
        public static Mesh Ground()
        {
            var builder = new MeshData();
            foreach (var side in new[] { -1, 1 })
            {
                // Fine bank rows terminate at a shared coarse edge. The zipper stitches 2 m to
                // 10 m without T-junctions; relief and splat weights are continuous at the join.
                var rows = new List<(float z, float step)>();
                for (var z = HalfTrench; z < FineRadius; z += FineStep) rows.Add((z, FineStep));
                // Additional road rows keep both shallow wheel ruts in the geometry.
                if (side > 0) foreach (var z in new[] { 4f, 5.15f, 6f, 6.85f, 8f }) rows.Add((z, FineStep));
                rows.Add((FineRadius, FineStep));
                for (var z = FineRadius + CoarseStep; z <= HalfWidth; z += CoarseStep) rows.Add((z, CoarseStep));
                var sorted = rows.OrderBy(r => r.z).ToArray();
                int[] previous = null; float[] previousX = null;
                foreach (var row in sorted)
                {
                    var count = Mathf.RoundToInt((MaxX - MinX) / row.step) + 1;
                    var ids = new int[count]; var xs = new float[count];
                    for (var i = 0; i < count; i++)
                    {
                        var x = xs[i] = MinX + i * row.step; var z = side * row.z;
                        ids[i] = builder.Vertex(new Vector3(x, Height(x, z), z), Paint(x, z));
                    }
                    if (previous != null)
                    {
                        var a = 0; var b = 0;
                        while (a < previous.Length - 1 || b < ids.Length - 1)
                        {
                            if (a < previous.Length - 1 && (b == ids.Length - 1 || previousX[a + 1] <= xs[b + 1]))
                            { builder.Triangle(previous[a], previous[a + 1], ids[b], side > 0); a++; }
                            else { builder.Triangle(previous[a], ids[b + 1], ids[b], side > 0); b++; }
                        }
                    }
                    previous = ids; previousX = xs;
                }
            }
            builder.Quad(new Vector3(MinX, 0, -HalfTrench), new Vector3(0, 0, -HalfTrench), new Vector3(0, 0, HalfTrench), new Vector3(MinX, 0, HalfTrench));
            // The host-driven Surface is the only lid over a station. Never let a bank
            // triangle bridge the cut, including after changes to the stitching rows.
            builder.CutTrench();
            return Save("GroundRelief", builder.Mesh());
        }
        public static Mesh HorizonMesh()
        {
            var b = new MeshData();
            // Four strips start at the exact terrain boundary. A gentle skirt avoids a crack
            // where the coarse edge has relief; fog hides the remote outer edge.
            for (var x = MinX; x < MaxX; x += CoarseStep) foreach (var side in new[] { -1, 1 })
            {
                var a = new Vector3(x, Height(x, side * HalfWidth), side * HalfWidth);
                var c = new Vector3(x + CoarseStep, Height(x + CoarseStep, side * HalfWidth), side * HalfWidth);
                b.Quad(a, c, new Vector3(c.x, -1, side * Horizon), new Vector3(a.x, -1, side * Horizon), side > 0);
            }
            var edge = new List<float> { -Horizon, Horizon, -HalfTrench, HalfTrench, 4, 5.15f, 6, 6.85f, 8 };
            foreach (var side in new[] { -1, 1 })
            {
                for (var z = HalfTrench; z < FineRadius; z += FineStep) edge.Add(side * z);
                for (var z = FineRadius; z <= HalfWidth; z += CoarseStep) edge.Add(side * z);
            }
            var zs = edge.Distinct().OrderBy(z => z).ToArray();
            foreach (var x in new[] { MinX, MaxX }) for (var i = 0; i < zs.Length - 1; i++)
            {
                var z0 = zs[i]; var z1 = zs[i + 1]; var end = x == MinX ? -Horizon : Horizon;
                b.Quad(new Vector3(x, Mathf.Abs(z0) > HalfWidth ? -1 : Height(x, z0), z0), new Vector3(end, -1, z0),
                    new Vector3(end, -1, z1), new Vector3(x, Mathf.Abs(z1) > HalfWidth ? -1 : Height(x, z1), z1), x == MaxX);
            }
            return Save("Horizon", b.Mesh());
        }
        public static Mesh Strip()
        {
            var b = new MeshData(); b.Quad(new Vector3(0, 0, -HalfTrench), new Vector3(World.WorldUnits.CellLength, 0, -HalfTrench),
                new Vector3(World.WorldUnits.CellLength, 0, HalfTrench), new Vector3(0, 0, HalfTrench));
            return Save("TrenchStrip", b.Mesh());
        }
        public static Mesh Wall(int side)
        {
            var b = new MeshData(); const int slices = 24, levels = 6; // WO-L4 irregular wall presentation tessellation.
            for (var i = 0; i <= slices; i++) for (var j = 0; j <= levels; j++)
            {
                var x = World.WorldUnits.CellLength * i / slices; var y = -World.WorldUnits.TrenchDepth * j / levels;
                var offset = Mathf.Sin(j * Mathf.PI / levels) * Noise(x * 15, y * 17) * 0.07f;
                b.Vertex(new Vector3(x, y, side * (HalfTrench + offset)), Color.white);
            }
            for (var i = 0; i < slices; i++) for (var j = 0; j < levels; j++)
            {
                var a = i * (levels + 1) + j; var c = a + levels + 1;
                b.Triangle(a, c, a + 1, side < 0); b.Triangle(c, c + 1, a + 1, side < 0);
            }
            return Save(side < 0 ? "TrenchFarWall" : "TrenchNearWall", b.Mesh());
        }
        public static Mesh Windrow()
        {
            var b = new MeshData(); const int steps = 30, cross = 6;
            for (var i = 0; i <= steps; i++) for (var j = 0; j <= cross; j++)
            {
                var x = (SpoilBreak - 1) * i / steps + 0.5f; var v = j / (float)cross;
                var envelope = Mathf.Sin(v * Mathf.PI) * Mathf.SmoothStep(0, 1, Mathf.Min(x - 0.5f, SpoilBreak - 0.5f - x));
                b.Vertex(new Vector3(x, SpoilHeight * envelope * (0.85f + 0.15f * Noise(x * 10, v * 15)), -3 + (v - 0.5f) * 2.4f), Color.white);
            }
            for (var i = 0; i < steps; i++) for (var j = 0; j < cross; j++)
            { var a = i * (cross + 1) + j; var c = a + cross + 1; b.Triangle(a, a + 1, c); b.Triangle(c, a + 1, c + 1); }
            return Save("SpoilWindrow", b.Mesh());
        }
        public static Mesh Pipe()
        {
            // Three 96-triangle cylinders per cell = 288, including their end caps.
            var b = new MeshData(); var radius = PipeDiameter / 2;
            var halfLength = World.WorldUnits.CellLength / 6;
            for (var end = 0; end < 2; end++) for (var i = 0; i <= PipeSides; i++)
            {
                var angle = i * Mathf.PI * 2 / PipeSides;
                b.Vertex(new Vector3(end == 0 ? -halfLength : halfLength, radius + radius * Mathf.Cos(angle), radius * Mathf.Sin(angle)),
                    Color.white, new Vector2(end, i / (float)PipeSides));
            }
            for (var i = 0; i < PipeSides; i++)
            {
                var c = PipeSides + 1 + i;
                b.Triangle(i, i + 1, c); b.Triangle(c, i + 1, c + 1);
            }
            for (var end = 0; end < 2; end++)
            {
                var center = b.Vertex(new Vector3(end == 0 ? -halfLength : halfLength, radius, 0), Color.white, new Vector2(0.5f, 0.5f));
                for (var i = 0; i <= PipeSides; i++)
                {
                    var angle = i * Mathf.PI * 2 / PipeSides;
                    b.Vertex(new Vector3(end == 0 ? -halfLength : halfLength, radius + radius * Mathf.Cos(angle), radius * Mathf.Sin(angle)),
                        Color.white, new Vector2(0.5f + 0.5f * Mathf.Cos(angle), 0.5f + 0.5f * Mathf.Sin(angle)));
                }
                for (var i = 0; i < PipeSides; i++) b.Triangle(center, center + 1 + i, center + 2 + i, end == 0);
            }
            return Save("LaidPipe24", b.Mesh());
        }
        public static Mesh HeroPile()
        {
            // WO-L4 rejection 1 permits a builder mound with the scan's PBR maps.
            // 32 by 16 cells = 1,024 triangles, one metre tall like the accepted scan.
            const int along = 32, across = 16;
            var b = new MeshData();
            for (var i = 0; i <= along; i++) for (var j = 0; j <= across; j++)
            {
                var u = i / (float)along; var v = j / (float)across;
                var envelope = Mathf.Sin(u * Mathf.PI) * Mathf.Sin(v * Mathf.PI);
                var height = envelope * (1 + 0.12f * Noise(u * 80, v * 80) * (1 - envelope));
                b.Vertex(new Vector3((u - 0.5f) * 7, height, (v - 0.5f) * 3), Color.white);
            }
            for (var i = 0; i < along; i++) for (var j = 0; j < across; j++)
            {
                var a = i * (across + 1) + j; var c = a + across + 1;
                b.Triangle(a, a + 1, c); b.Triangle(c, a + 1, c + 1);
            }
            return Save("HeroSpoilMound", b.Mesh());
        }
        public static Mesh Collapse()
        {
            var b = new MeshData(); const int steps = 20;
            for (var i = 0; i <= steps; i++)
            {
                var x = World.WorldUnits.CellLength * i / steps;
                var h = 0.8f + 0.4f * Noise(x * 11, 2);
                b.Vertex(new Vector3(x, -World.WorldUnits.TrenchDepth, -HalfTrench), Color.white);
                b.Vertex(new Vector3(x, -World.WorldUnits.TrenchDepth + h, -HalfTrench), Color.white);
                b.Vertex(new Vector3(x, -World.WorldUnits.TrenchDepth + 0.08f, 0.25f + Noise(x * 7, 5) * 0.15f), Color.white);
            }
            for (var i = 0; i < steps; i++)
            { var a = i * 3; b.Triangle(a + 1, a + 2, a + 4); b.Triangle(a + 4, a + 2, a + 5); b.Triangle(a, a + 1, a + 3); b.Triangle(a + 3, a + 1, a + 4); }
            b.Triangle(0, 2, 1); b.Triangle(steps * 3, steps * 3 + 1, steps * 3 + 2);
            return Save("CollapsedWedge", b.Mesh());
        }
        public static Mesh Block()
        {
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var mesh = UnityEngine.Object.Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);
                mesh.colors = Enumerable.Repeat(Color.white, mesh.vertexCount).ToArray();
                BakeBarycentrics(mesh); return Save("SiteBlock", mesh);
            }
            finally { UnityEngine.Object.DestroyImmediate(primitive); }
        }
        public static Mesh Save(string name, Mesh mesh)
        {
            var path = SiteAssetBuilder.Art + "Generated/" + name + ".asset"; mesh.name = name;
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            var oldBarycentrics = new List<Vector4>(); var barycentrics = new List<Vector4>();
            old.GetUVs(2, oldBarycentrics); mesh.GetUVs(2, barycentrics);
            if (!old.vertices.SequenceEqual(mesh.vertices) || !old.triangles.SequenceEqual(mesh.triangles) || !old.colors.SequenceEqual(mesh.colors)
                || !old.uv.SequenceEqual(mesh.uv) || !old.normals.SequenceEqual(mesh.normals) || !old.tangents.SequenceEqual(mesh.tangents)
                || !oldBarycentrics.SequenceEqual(barycentrics))
            { EditorUtility.CopySerialized(mesh, old); EditorUtility.SetDirty(old); }
            UnityEngine.Object.DestroyImmediate(mesh); return old;
        }
        private sealed class MeshData
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<Vector2> uvs = new List<Vector2>();
            private readonly List<int> indices = new List<int>();
            public int Vertex(Vector3 v, Color c, Vector2? uv = null)
            { vertices.Add(v); colors.Add(c); uvs.Add(uv ?? new Vector2(v.x, v.z)); return vertices.Count - 1; }
            public void CutTrench()
            {
                for (var i = indices.Count - 3; i >= 0; i -= 3)
                {
                    var a = vertices[indices[i]]; var b = vertices[indices[i + 1]]; var c = vertices[indices[i + 2]];
                    if (Mathf.Max(a.x, Mathf.Max(b.x, c.x)) > 0
                        && Mathf.Min(a.z, Mathf.Min(b.z, c.z)) < HalfTrench
                        && Mathf.Max(a.z, Mathf.Max(b.z, c.z)) > -HalfTrench) indices.RemoveRange(i, 3);
                }
            }
            public void Triangle(int a, int b, int c, bool reverse = false) { indices.Add(a); indices.Add(reverse ? c : b); indices.Add(reverse ? b : c); }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool reverse = true)
            {
                var start = vertices.Count; foreach (var v in new[] { a, b, c, d }) Vertex(v, Paint(v.x, v.z));
                Triangle(start, start + 1, start + 2, reverse); Triangle(start, start + 2, start + 3, reverse);
            }
            public Mesh Mesh()
            {
                var mesh = new Mesh { indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
                mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(indices, 0);
                mesh.SetUVs(0, uvs);
                mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
                BakeBarycentrics(mesh); return mesh;
            }
        }
        public static void BakeBarycentrics(Mesh mesh)
        {
            // WO-L5: split corners only after smoothing so the normal-lit surface stays unchanged.
            var indices = mesh.triangles; var positions = mesh.vertices; var normals = mesh.normals;
            var tangents = mesh.tangents; var colors = mesh.colors; var uvs = mesh.uv;
            var corners = new Vector3[indices.Length]; var cornerNormals = new Vector3[indices.Length];
            var cornerTangents = new Vector4[indices.Length]; var cornerColors = new Color[indices.Length];
            var cornerUVs = new Vector2[indices.Length]; var barycentrics = new List<Vector4>(indices.Length);
            var axes = new[] { new Vector4(1, 0, 0, 1), new Vector4(0, 1, 0, 1), new Vector4(0, 0, 1, 1) };
            for (var i = 0; i < indices.Length; i++)
            {
                var source = indices[i]; corners[i] = positions[source]; cornerNormals[i] = normals[source];
                cornerTangents[i] = tangents[source]; cornerColors[i] = colors[source]; cornerUVs[i] = uvs[source];
                barycentrics.Add(axes[i % 3]); indices[i] = i;
            }
            mesh.Clear();
            mesh.indexFormat = indices.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = corners; mesh.normals = cornerNormals; mesh.tangents = cornerTangents;
            mesh.colors = cornerColors; mesh.uv = cornerUVs; mesh.SetUVs(2, barycentrics); mesh.triangles = indices;
            mesh.RecalculateBounds();
        }
    }
}
